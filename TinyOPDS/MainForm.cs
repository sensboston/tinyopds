﻿/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * TinyOPDS main UI thread
 * 
 ************************************************************/

using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Threading;
using System.Net;

using TinyOPDS.Data;
using TinyOPDS.Scanner;
using TinyOPDS.OPDS;
using TinyOPDS.Server;
using UPnP;

namespace TinyOPDS
{
    public partial class MainForm : Form
    {
        OPDSServer _server;
        Thread _serverThread;
        FileScanner _scanner = new FileScanner();
        Watcher _watcher;
        DateTime _scanStartTime;
        UPnPController _upnpController = new UPnPController();
        NotifyIcon _notifyIcon = new NotifyIcon();
        BindingSource bs = new BindingSource();
        System.Windows.Forms.Timer _updateChecker = new System.Windows.Forms.Timer();
        string _updateUrl = string.Empty;

        #region Statistical information
        int _fb2Count, _epubCount, _skippedFiles, _invalidFiles, _duplicates;
        #endregion

        private const string urlTemplate = "http://{0}:{1}/{2}";

        #region Initialization and startup

        public MainForm()
        {
            Log.SaveToFile = TinyOPDS.Properties.Settings.Default.SaveLogToDisk;

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += currentDomain_UnhandledException;

            Log.WriteLine("TinyOPDS version {0}.{1} started", Utils.Version.Major, Utils.Version.Minor);

            InitializeComponent();

            // Assign combo data source to the list of all available interfaces
            interfaceCombo.DataSource = UPnPController.LocalInterfaces;
            interfaceCombo.DataBindings.Add(new Binding("SelectedIndex", Properties.Settings.Default, "LocalInterfaceIndex", false, DataSourceUpdateMode.OnPropertyChanged));

            logVerbosity.DataBindings.Add(new Binding("SelectedIndex", Properties.Settings.Default, "LogLevel", false, DataSourceUpdateMode.OnPropertyChanged));
            updateCombo.DataBindings.Add(new Binding("SelectedIndex", Properties.Settings.Default, "UpdatesCheck", false, DataSourceUpdateMode.OnPropertyChanged));

            this.PerformLayout();

            // Manually assign icons from resources (fix for Mono)
            this.Icon = Properties.Resources.trayIcon;
            _notifyIcon.ContextMenuStrip = this.contextMenuStrip;
            _notifyIcon.Icon = Properties.Resources.trayIcon;
            _notifyIcon.MouseClick += notifyIcon1_MouseClick;
            _notifyIcon.BalloonTipClicked += _notifyIcon_BalloonTipClicked;
            _notifyIcon.BalloonTipClosed += _notifyIcon_BalloonTipClosed;

            // Init localization service
            Localizer.Init();
            Localizer.AddMenu(contextMenuStrip);
            langCombo.DataSource = Localizer.Languages.ToArray();

            // Load application settings
            LoadSettings();

            // Initialize update checker timer to tick every minute
            _updateChecker.Interval = 1000 * 60;
            _updateChecker.Tick += _updateChecker_Tick;

            // Setup credentials grid
            bs.AddingNew += bs_AddingNew;
            bs.AllowNew = true;
            bs.DataSource = HttpProcessor.Credentials;
            dataGridView1.DataSource = bs;
            bs.CurrentItemChanged += bs_CurrentItemChanged;
            foreach (DataGridViewColumn col in dataGridView1.Columns) col.Width = 180;

            Library.LibraryPath = TinyOPDS.Properties.Settings.Default.LibraryPath.SanitizePathName();
            Library.LibraryLoaded += (_, __) => 
            {
                UpdateInfo();
                _watcher.DirectoryToWatch = Library.LibraryPath;
                _watcher.IsEnabled = TinyOPDS.Properties.Settings.Default.WatchLibrary;
            };

            // Create file watcher
            _watcher = new Watcher(Library.LibraryPath);
            _watcher.OnBookAdded += (object sender, BookAddedEventArgs e) => 
                {
                    if (e.BookType == BookType.FB2) _fb2Count++; else _epubCount++;
                    UpdateInfo();
                    Log.WriteLine(LogLevel.Info, "Added: \"{0}\"", e.BookPath);
                };
            _watcher.OnInvalidBook += (_, __) => 
                {
                    _invalidFiles++;
                    UpdateInfo();
                };
            _watcher.OnFileSkipped += (object _sender, FileSkippedEventArgs _e) =>
                {
                    _skippedFiles = _e.Count;
                    UpdateInfo();
                };

            _watcher.OnBookDeleted += (object sender, BookDeletedEventArgs e) => 
                {
                    UpdateInfo();
                    Log.WriteLine(LogLevel.Info, "Deleted: \"{0}\"", e.BookPath);
                };
            _watcher.IsEnabled = false;

            intLink.Text = string.Format(urlTemplate, _upnpController.LocalIP.ToString(), TinyOPDS.Properties.Settings.Default.ServerPort, rootPrefix.Text);
            intWebLink.Text = string.Format(urlTemplate, _upnpController.LocalIP.ToString(), TinyOPDS.Properties.Settings.Default.ServerPort, webPrefix.Text);

            // Start OPDS server
            StartHttpServer();

            // Set server statistics handler
            HttpServer.ServerStatistics.StatisticsUpdated += (_, __) =>
                {
                    this.BeginInvoke((MethodInvoker)delegate
                    {
                        statRequests.Text = HttpServer.ServerStatistics.GetRequests.ToString();
                        statBooks.Text = HttpServer.ServerStatistics.BooksSent.ToString();
                        statImages.Text = HttpServer.ServerStatistics.ImagesSent.ToString();
                        statUniqueClients.Text = HttpServer.ServerStatistics.UniqueClientsCount.ToString();
                        statGoodLogins.Text = HttpServer.ServerStatistics.SuccessfulLoginAttempts.ToString();
                        statWrongLogins.Text = HttpServer.ServerStatistics.WrongLoginAttempts.ToString();
                        statBannedClients.Text = HttpServer.ServerStatistics.BannedClientsCount.ToString();
                    });
                };

            _scanStartTime = DateTime.Now;
            _notifyIcon.Visible = TinyOPDS.Properties.Settings.Default.CloseToTray;
        }

        /// <summary>
        /// We should call DiscoverAsync when windows handle already created (invoker used)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            _upnpController.DiscoverCompleted += _upnpController_DiscoverCompleted;
            _upnpController.DiscoverAsync(TinyOPDS.Properties.Settings.Default.UseUPnP);
        }

        /// <summary>
        /// Process unhandled exceptions
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void currentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (args != null)
            {
                Exception e = (Exception)args.ExceptionObject;
                if (e != null)
                {
                    Log.WriteLine(LogLevel.Error, "{2}: {0}\nStack trace: {1}", e.Message, e.StackTrace, args.IsTerminating ? "Fatal exception" : "Unhandled exception");
                }
                else
                {
                    Log.WriteLine(LogLevel.Error, "Unhandled exception, args.ExceptionObject is null");
                }
            }
            else
            {
                Log.WriteLine(LogLevel.Error, "Unhandled exception, args is null");
            }
        }

        void _upnpController_DiscoverCompleted(object sender, EventArgs e)
        {
            if (!IsDisposed && _upnpController != null)
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    extLink.Text = string.Format(urlTemplate, _upnpController.ExternalIP.ToString(), TinyOPDS.Properties.Settings.Default.ServerPort, TinyOPDS.Properties.Settings.Default.RootPrefix);
                    extWebLink.Text = string.Format(urlTemplate, _upnpController.ExternalIP.ToString(), TinyOPDS.Properties.Settings.Default.ServerPort, TinyOPDS.Properties.Settings.Default.HttpPrefix);
                    if (_upnpController.UPnPReady)
                    {
                        openPort.Enabled = true;
                        if (TinyOPDS.Properties.Settings.Default.OpenNATPort) openPort_CheckedChanged(this, null);
                    }
                });
            }
        }

        #endregion

        #region Application settings

        private void LoadSettings()
        {
            // Setup link labels
            converterLinkLabel.Links.Add(0, converterLinkLabel.Text.Length, "http://fb2epub.net/files/Fb2ePubSetup_1_1_3.zip");
            linkLabel3.Links.Add(0, linkLabel3.Text.Length, "https://code.google.com/p/fb2librarynet/");
            linkLabel5.Links.Add(0, linkLabel5.Text.Length, "http://epubreader.codeplex.com/");
            linkLabel4.Links.Add(0, linkLabel4.Text.Length, "http://dotnetzip.codeplex.com/");
            linkLabel6.Links.Add(0, linkLabel6.Text.Length, "http://www.fb2library.net/projects/fb2fix");

            // Setup settings controls
            libraryPath.Text = TinyOPDS.Properties.Settings.Default.LibraryPath;
            if (!string.IsNullOrEmpty(TinyOPDS.Properties.Settings.Default.LibraryPath))
            {
                databaseFileName.Text = Utils.CreateGuid(Utils.IsoOidNamespace, TinyOPDS.Properties.Settings.Default.LibraryPath.SanitizePathName()).ToString() + ".db";
            }
            if (Utils.IsLinux) startWithWindows.Enabled = false;
            if (string.IsNullOrEmpty(TinyOPDS.Properties.Settings.Default.ConvertorPath))
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramFiles")))
                {
                    if (File.Exists(Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), "FB2ePub\\Fb2ePub.exe")))
                    {
                        convertorPath.Text = TinyOPDS.Properties.Settings.Default.ConvertorPath = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), "FB2ePub");
                    }
                }
            }
            else convertorPath.Text = TinyOPDS.Properties.Settings.Default.ConvertorPath;
            converterLinkLabel.Visible = string.IsNullOrEmpty(convertorPath.Text);

            // We should update all invisible controls
            interfaceCombo.SelectedIndex = Math.Min(UPnPController.LocalInterfaces.Count - 1, TinyOPDS.Properties.Settings.Default.LocalInterfaceIndex);
            logVerbosity.SelectedIndex = Math.Min(2, TinyOPDS.Properties.Settings.Default.LogLevel);
            updateCombo.SelectedIndex = Math.Min(2, TinyOPDS.Properties.Settings.Default.UpdatesCheck);
            langCombo.SelectedValue = TinyOPDS.Properties.Settings.Default.Language;
            sortOrderCombo.SelectedIndex = TinyOPDS.Properties.Settings.Default.SortOrder;
            newBooksPeriodCombo.SelectedIndex = TinyOPDS.Properties.Settings.Default.NewBooksPeriod;

            openPort.Checked = TinyOPDS.Properties.Settings.Default.UseUPnP ? TinyOPDS.Properties.Settings.Default.OpenNATPort : false;
            banClients.Enabled = rememberClients.Enabled = dataGridView1.Enabled = TinyOPDS.Properties.Settings.Default.UseHTTPAuth;
            wrongAttemptsCount.Enabled = banClients.Checked && useHTTPAuth.Checked;

            _notifyIcon.Visible = TinyOPDS.Properties.Settings.Default.CloseToTray;
            _updateChecker.Start();

            // Load saved credentials
            try
            {
                HttpProcessor.Credentials.Clear();
                string[] pairs = Crypt.DecryptStringAES(TinyOPDS.Properties.Settings.Default.Credentials, urlTemplate).Split(';');
                foreach (string pair in pairs)
                {
                    string[] cred = pair.Split(':');
                    if (cred.Length == 2) HttpProcessor.Credentials.Add( new Credential(cred[0], cred[1]));
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            TinyOPDS.Properties.Settings.Default.LibraryPath = libraryPath.Text.SanitizePathName();
            TinyOPDS.Properties.Settings.Default.Language = langCombo.SelectedValue as string;
            TinyOPDS.Properties.Settings.Default.Save();
        }

        #endregion

        #region Credentials handling

        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dataGridView1.Columns[e.ColumnIndex].Name == "Password" && e.Value != null)
            {
                dataGridView1.Rows[e.RowIndex].Tag = e.Value;
                e.Value = new String('*', e.Value.ToString().Length);
            }
        }

        void bs_AddingNew(object sender, AddingNewEventArgs e)
        {
            e.NewObject = new Credential("", "");
        }

        void bs_CurrentItemChanged(object sender, EventArgs e)
        {
            string s = string.Empty;
            foreach (Credential cred in HttpProcessor.Credentials) s += cred.User + ":" + cred.Password + ";";
            try 
            {
                TinyOPDS.Properties.Settings.Default.Credentials = string.IsNullOrEmpty(s) ? string.Empty : Crypt.EncryptStringAES(s, urlTemplate); 
            }
            finally 
            {
                TinyOPDS.Properties.Settings.Default.Save(); 
            }
        }

        #endregion

        #region Library scanning support

        private void libraryPath_TextChanged(object sender, EventArgs e)
        {
            databaseFileName.Text = Utils.CreateGuid(Utils.IsoOidNamespace, libraryPath.Text.SanitizePathName()).ToString() + ".db";
        }

        private void libraryPath_Validated(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(databaseFileName.Text) && !Library.LibraryPath.Equals(databaseFileName.Text.SanitizePathName()) &&
                Directory.Exists(libraryPath.Text.SanitizePathName()))
            {
                if (Library.IsChanged) Library.Save();
                Library.LibraryPath = TinyOPDS.Properties.Settings.Default.LibraryPath = libraryPath.Text.SanitizePathName();
                booksInDB.Text = string.Format("{0}       fb2: {1}      epub: {2}", 0, 0, 0);
                databaseFileName.Text = Utils.CreateGuid(Utils.IsoOidNamespace, TinyOPDS.Properties.Settings.Default.LibraryPath).ToString() + ".db";
                _watcher.IsEnabled = false;
                // Reload library
                Library.LoadAsync();
            }
            else
            {
                if (!string.IsNullOrEmpty(TinyOPDS.Properties.Settings.Default.LibraryPath)) libraryPath.Text = TinyOPDS.Properties.Settings.Default.LibraryPath.SanitizePathName();
                else libraryPath.Undo();
            }
        }

        private void folderButton_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = (sender as Button == folderButton) ? libraryPath.Text : convertorPath.Text;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (sender as Button == folderButton)
                    {
                        libraryPath.Text = dialog.SelectedPath.SanitizePathName();
                        libraryPath_Validated(sender, e);
                    }
                    else
                    {
                        convertorPath.Text = dialog.SelectedPath;
                        convertorPath_Validated(sender, e);
                    }
                }
            }
        }

        private void scannerButton_Click(object sender, EventArgs e)
        {
            if (_scanner.Status != FileScannerStatus.SCANNING)
            {
                _scanner.OnBookFound += scanner_OnBookFound;
                _scanner.OnInvalidBook += (_, __) => { _invalidFiles++; };
                _scanner.OnFileSkipped += (object _sender, FileSkippedEventArgs _e) => 
                { 
                    _skippedFiles = _e.Count; 
                    UpdateInfo(); 
                };
                _scanner.OnScanCompleted += (_, __) => 
                {
                    Library.Save();
                    UpdateInfo(true);

                    Log.WriteLine("Directory scanner completed");
                };
                _fb2Count = _epubCount = _skippedFiles = _invalidFiles = _duplicates = 0;
                _scanStartTime = DateTime.Now;
                startTime.Text = _scanStartTime.ToString(@"hh\:mm\:ss");
                _scanner.Start(libraryPath.Text.SanitizePathName());
                scannerButton.Text = Localizer.Text("Stop scanning");

                Log.WriteLine("Directory scanner started");
            }
            else
            {
                _scanner.Stop();
                Library.Save();
                UpdateInfo(true);
                scannerButton.Text = Localizer.Text("Start scanning");

                Log.WriteLine("Directory scanner stopped");
            }
        }

        void scanner_OnBookFound(object sender, BookFoundEventArgs e)
        {
            if (Library.Add(e.Book))
            {
                if (e.Book.BookType == BookType.FB2) _fb2Count++; else _epubCount++;
            }
            else _duplicates++;
            if (Library.Count % 500 == 0) Library.Save();
            if (Library.Count % 20000 == 0) GC.Collect();
            UpdateInfo();
        }

        private void UpdateInfo(bool IsScanFinished = false)
        {
            if (this.InvokeRequired) { this.BeginInvoke((MethodInvoker)delegate { internalUpdateInfo(IsScanFinished); }); }
            else { internalUpdateInfo(IsScanFinished); }
        }

        private void internalUpdateInfo(bool IsScanFinished)
        {
            booksInDB.Text = string.Format("{0}       fb2: {1}      epub: {2}", Library.Count, Library.FB2Count,  Library.EPUBCount);
            booksFound.Text = string.Format("fb2: {0}   epub: {1}", _fb2Count, _epubCount);
            skippedBooks.Text = _skippedFiles.ToString();
            invalidBooks.Text = _invalidFiles.ToString();
            duplicates.Text = _duplicates.ToString();
            int totalBooksProcessed = _fb2Count + _epubCount + _skippedFiles + _invalidFiles + _duplicates;
            booksProcessed.Text = totalBooksProcessed.ToString();

            TimeSpan dt = DateTime.Now.Subtract(_scanStartTime);
            elapsedTime.Text = dt.ToString(@"hh\:mm\:ss");
            rate.Text = (dt.TotalSeconds) > 0 ? string.Format("{0:0.} books/min", totalBooksProcessed / dt.TotalSeconds * 60) : "---";
            if (scannerButton.Enabled)
            {
                status.Text = IsScanFinished ? Localizer.Text("FINISHED") : (_scanner.Status == FileScannerStatus.SCANNING ? Localizer.Text("SCANNING") : Localizer.Text("STOPPED"));
                scannerButton.Text = (_scanner.Status == FileScannerStatus.SCANNING) ? Localizer.Text("Stop scanning") : Localizer.Text("Start scanning");
            }
        }

        #endregion

        #region HTTP (OPDS) server & network support

        private void serverButton_Click(object sender, EventArgs e)
        {
            if (_server == null) StartHttpServer(); else StopHttpServer();
        }

        private void StartHttpServer()
        {
            // Create and start HTTP server
            HttpProcessor.AuthorizedClients.Clear();
            HttpProcessor.BannedClients.Clear();
            _server = new OPDSServer(_upnpController.LocalIP, int.Parse(TinyOPDS.Properties.Settings.Default.ServerPort));

            _serverThread = new Thread(new ThreadStart(_server.Listen));
            _serverThread.Priority = ThreadPriority.BelowNormal;
            _serverThread.Start();
            _server.ServerReady.WaitOne(TimeSpan.FromMilliseconds(500));
            if (!_server.IsActive)
            {
                if (_server.ServerException != null)
                {
                    if (_server.ServerException is System.Net.Sockets.SocketException)
                    {
                        MessageBox.Show(string.Format(Localizer.Text("Probably, port {0} is already in use. Please try different port value."), TinyOPDS.Properties.Settings.Default.ServerPort), Localizer.Text("Warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show(_server.ServerException.Message, Localizer.Text("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    _server.StopServer();
                    _serverThread = null;
                    _server = null;
                }
            }
            else
            {
                serverButton.Text = serverMenuItem.Text = Localizer.Text("Stop server");
                Log.WriteLine("HTTP server started");
            }
        }

        private void StopHttpServer()
        {
            if (_server != null)
            {
                _server.StopServer();
                _serverThread = null;
                _server = null;
                Log.WriteLine("HTTP server stopped");
            }
            serverButton.Text = serverMenuItem.Text = Localizer.Text("Start server");
        }

        private void RestartHttpServer()
        {
            StopHttpServer();
            StartHttpServer();
        }

        private void useUPnP_CheckStateChanged(object sender, EventArgs e)
        {
            if (useUPnP.Checked)
            {
                // Re-detect IP addresses using UPnP
                _upnpController.DiscoverAsync(true);
            }
            else
            {
                openPort.Checked = openPort.Enabled = false;
            }
        }

        private void openPort_CheckedChanged(object sender, EventArgs e)
        {
            if (_upnpController != null && _upnpController.UPnPReady)
            {
                int port = int.Parse(TinyOPDS.Properties.Settings.Default.ServerPort);
                if (openPort.Checked)
                {
                    _upnpController.ForwardPort(port, System.Net.Sockets.ProtocolType.Tcp, "TinyOPDS server");

                    Log.WriteLine("Port {0} forwarded by UPnP", port);
                }
                else
                {
                    _upnpController.DeleteForwardingRule(port, System.Net.Sockets.ProtocolType.Tcp);

                    Log.WriteLine("Port {0} closed", port);
                }
            }
        }

        private void interfaceCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_upnpController != null && _upnpController.InterfaceIndex != interfaceCombo.SelectedIndex)
            {
                _upnpController.InterfaceIndex = interfaceCombo.SelectedIndex;
                intLink.Text = string.Format(urlTemplate, _upnpController.LocalIP.ToString(), TinyOPDS.Properties.Settings.Default.ServerPort, rootPrefix.Text);
                intWebLink.Text = string.Format(urlTemplate, _upnpController.LocalIP.ToString(), TinyOPDS.Properties.Settings.Default.ServerPort, webPrefix.Text);

                if (TinyOPDS.Properties.Settings.Default.UseUPnP && openPort.Checked)
                {
                    int port = int.Parse(TinyOPDS.Properties.Settings.Default.ServerPort);
                    _upnpController.DeleteForwardingRule(port, System.Net.Sockets.ProtocolType.Tcp);
                    _upnpController.ForwardPort(port, System.Net.Sockets.ProtocolType.Tcp, "TinyOPDS server");
                }
                RestartHttpServer();
            }
        }

        #endregion

        #region Form minimizing and closing

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (TinyOPDS.Properties.Settings.Default.CloseToTray)
            {
                Visible = (WindowState == FormWindowState.Normal);
                windowMenuItem.Text = Localizer.Text("Show window");
            }
        }

        private void windowMenuItem_Click(object sender, EventArgs e)
        {
            if (!ShowInTaskbar) ShowInTaskbar = true; else Visible = !Visible;
            if (Visible) WindowState = FormWindowState.Normal;
            windowMenuItem.Text = Localizer.Text(Visible ? "Hide window" : "Show window");
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left) windowMenuItem_Click(this, null);
        }

        private bool realExit = false;
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (TinyOPDS.Properties.Settings.Default.CloseToTray && !realExit)
            {
                e.Cancel = true;
                Visible = false;
                WindowState = FormWindowState.Minimized;
                windowMenuItem.Text = Localizer.Text("Show window");
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            SaveSettings();
            if (_server != null && _server._isActive)
            {
                _server.StopServer();
                _serverThread = null;
                _server = null;

                if (_upnpController != null)
                {
                    if (TinyOPDS.Properties.Settings.Default.UseUPnP)
                    {
                        int port = int.Parse(TinyOPDS.Properties.Settings.Default.ServerPort);
                        _upnpController.DeleteForwardingRule(port, System.Net.Sockets.ProtocolType.Tcp);
                    }
                    _upnpController.DiscoverCompleted -= _upnpController_DiscoverCompleted;
                    _upnpController.Dispose();
                }
            }

            if (_scanner.Status == FileScannerStatus.SCANNING) _scanner.Stop();
            if (Library.IsChanged) Library.Save();

            _notifyIcon.Visible = false;

            Log.WriteLine("TinyOPDS closed\n");
        }

        private void exitMenuItem_Click(object sender, EventArgs e)
        {
            realExit = true;
            Close();
        }

        #endregion

        #region Form controls handling

        private void convertorPath_Validated(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(convertorPath.Text) && Directory.Exists(convertorPath.Text) && File.Exists(Path.Combine(convertorPath.Text, Utils.IsLinux ? "fb2toepub" : "Fb2ePub.exe")))
            {
                TinyOPDS.Properties.Settings.Default.ConvertorPath = convertorPath.Text;
            }
            else
            {
                convertorPath.Text = TinyOPDS.Properties.Settings.Default.ConvertorPath;
            }
        }

        private void useWatcher_CheckedChanged(object sender, EventArgs e)
        {
            if (_watcher != null && _watcher.IsEnabled != useWatcher.Checked)
            {
                _watcher.IsEnabled = useWatcher.Checked;
            }
        }

        private void closeToTray_CheckedChanged(object sender, EventArgs e)
        {
            _notifyIcon.Visible = closeToTray.Checked;
        }

        private void startWithWindows_CheckedChanged(object sender, EventArgs e)
        {
            Microsoft.Win32.RegistryKey registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            bool exists = (registryKey.GetValue("TinyOPDS") != null);
            if (startWithWindows.Checked && !exists) registryKey.SetValue("TinyOPDS", Application.ExecutablePath);
            else if (exists && !startWithWindows.Checked) registryKey.DeleteValue("TinyOPDS");
        }

        private void saveLog_CheckedChanged(object sender, EventArgs e)
        {
            Log.SaveToFile = label22.Enabled = logVerbosity.Enabled = saveLog.Checked;
        }

        private void UpdateServerLinks()
        {
            if (_upnpController != null)
            {
                if (_upnpController.LocalIP != null)
                {
                    intLink.Text = string.Format(urlTemplate, _upnpController.LocalIP.ToString(), TinyOPDS.Properties.Settings.Default.ServerPort, rootPrefix.Text);
                    intWebLink.Text = string.Format(urlTemplate, _upnpController.LocalIP.ToString(), TinyOPDS.Properties.Settings.Default.ServerPort, webPrefix.Text);
                }
                if (_upnpController.ExternalIP != null)
                {
                    extLink.Text = string.Format(urlTemplate, _upnpController.ExternalIP.ToString(), TinyOPDS.Properties.Settings.Default.ServerPort, rootPrefix.Text);
                    extWebLink.Text = string.Format(urlTemplate, _upnpController.ExternalIP.ToString(), TinyOPDS.Properties.Settings.Default.ServerPort, webPrefix.Text);
                }
            }
        }

        /// <summary>
        /// Handle server's root prefix change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rootPrefix_TextChanged(object sender, EventArgs e)
        {
            if (sender is TextBox && (sender as TextBox).CanUndo)
            {
                if (rootPrefix.Text.EndsWith("/")) rootPrefix.Text = rootPrefix.Text.Remove(rootPrefix.Text.Length - 1);
                if (webPrefix.Text.EndsWith("/")) webPrefix.Text = webPrefix.Text.Remove(webPrefix.Text.Length - 1);
                if (rootPrefix.Text.ToLower().Equals(webPrefix.Text.ToLower()))
                {
                    MessageBox.Show(Localizer.Text("OPDS and web root prefixes can not be the same."), Localizer.Text("Warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    (sender as TextBox).Undo();
                }
                UpdateServerLinks();
            }
        }

        /// <summary>
        /// Validate server port
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void serverPort_Validated(object sender, EventArgs e)
        {
            int port = 8080;
            bool valid = int.TryParse(serverPort.Text, out port);
            if (valid && port >= 1 && port <= 65535)
            {
                if (_upnpController != null && _upnpController.UPnPReady && openPort.Checked)
                {
                    openPort.Checked = false;
                    TinyOPDS.Properties.Settings.Default.ServerPort = port.ToString();
                    openPort.Checked = true;
                }
                else TinyOPDS.Properties.Settings.Default.ServerPort = port.ToString();
                if (_server != null && _server.IsActive)
                {
                    RestartHttpServer();
                }
            }
            else
            {
                MessageBox.Show(Localizer.Text("Invalid port value: value must be numeric and in range from 1 to 65535"), Localizer.Text("Warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                serverPort.Text = TinyOPDS.Properties.Settings.Default.ServerPort.ToString();
            }
            // Update link labels
            UpdateServerLinks();
        }

        /// <summary>
        /// Set UI language
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void langCombo_SelectedValueChanged(object sender, EventArgs e)
        {
            Localizer.SetLanguage(this, langCombo.SelectedValue as string);
            appVersion.Text = string.Format(Localizer.Text("version {0}.{1} {2}"), Utils.Version.Major, Utils.Version.Minor, Utils.Version.Major == 0?" (beta)":"");
            scannerButton.Text = Localizer.Text( (_scanner.Status == FileScannerStatus.STOPPED) ? "Start scanning" : "Stop scanning");
            serverButton.Text = Localizer.Text((_server == null) ? "Start server" : "Stop server");
            serverMenuItem.Text = Localizer.Text((_server == null) ? "Start server" : "Stop server");
            windowMenuItem.Text = Localizer.Text(Visible || ShowInTaskbar ? "Hide window" : "Show window");
            logVerbosity.Items[0] = Localizer.Text("Info, warnings and errors");
            logVerbosity.Items[1] = Localizer.Text("Warnings and errors");
            logVerbosity.Items[2] = Localizer.Text("Errors only");
            updateCombo.Items[0] = Localizer.Text("Never");
            updateCombo.Items[1] = Localizer.Text("Once a week");
            updateCombo.Items[2] = Localizer.Text("Once a month");
            sortOrderCombo.Items[0] = Localizer.Text("Latin first");
            sortOrderCombo.Items[1] = Localizer.Text("Cyrillic first");
            newBooksPeriodCombo.Items[0] = Localizer.Text("one week");
            newBooksPeriodCombo.Items[1] = Localizer.Text("two weeks");
            newBooksPeriodCombo.Items[2] = Localizer.Text("three weeks");
            newBooksPeriodCombo.Items[3] = Localizer.Text("month");
            newBooksPeriodCombo.Items[4] = Localizer.Text("month and half");
            newBooksPeriodCombo.Items[5] = Localizer.Text("two month");
            newBooksPeriodCombo.Items[6] = Localizer.Text("three month");
        }

        /// <summary>
        /// Handle PayPal donation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void donateButton_Click(object sender, EventArgs e)
        {
            const string business = "sens.boston@gmail.com", description = "Donation%20for%20the%20TinyOPDS", country = "US", currency = "USD";
            string url = string.Format("https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business={0}&lc={1}&item_name={2}&currency_code={3}&bn=PP%2dDonationsBF",
                business, country, description, currency);
            System.Diagnostics.Process.Start(url);
        }

        private bool checkUrl(string uriName)
        {
            Uri uriResult;
            bool result = Uri.TryCreate(uriName, UriKind.Absolute, out uriResult);
            return result && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        private void linkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (sender is LinkLabel && checkUrl((sender as LinkLabel).Text))
            {
                System.Diagnostics.Process.Start((sender as LinkLabel).Text);
            }
        }

        private void linkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (sender is LinkLabel && checkUrl((sender as LinkLabel).Links[0].LinkData as string))
            {
                System.Diagnostics.Process.Start((sender as LinkLabel).Links[0].LinkData as string);
            }
        }

        private void useHTTPAuth_CheckedChanged(object sender, EventArgs e)
        {
            dataGridView1.Enabled = banClients.Enabled = rememberClients.Enabled = useHTTPAuth.Checked;
            wrongAttemptsCount.Enabled = banClients.Enabled && banClients.Checked;
        }

        private void banClients_CheckedChanged(object sender, EventArgs e)
        {
            wrongAttemptsCount.Enabled = banClients.Checked;
        }

        private void logVerbosity_SelectedIndexChanged(object sender, EventArgs e)
        {
            Log.VerbosityLevel = (LogLevel)logVerbosity.SelectedIndex;
        }

        private void updateCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            TinyOPDS.Properties.Settings.Default.UpdatesCheck = updateCombo.SelectedIndex;
        }

        private void viewLogFile_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Log.LogFileName);
        }

        private void sortOrderCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            TinyOPDS.Properties.Settings.Default.SortOrder = sortOrderCombo.SelectedIndex;
        }

        private void newBooksPeriodCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            TinyOPDS.Properties.Settings.Default.NewBooksPeriod = newBooksPeriodCombo.SelectedIndex;
        }

        #endregion

        #region TinyOPDS updates checker

        /// <summary>
        /// This timer event should be raised every hour
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static int[] checkIntervals = new int[] { 0, 60 * 24 * 7, 60 * 24 * 30, 1};
        static int _timerCallsCount = 0;
        void _updateChecker_Tick(object sender, EventArgs e)
        {
            if (TinyOPDS.Properties.Settings.Default.UpdatesCheck > 0)
            {
                _updateUrl = string.Empty;
                int minutesFromLastCheck = (int)Math.Round(DateTime.Now.Subtract(TinyOPDS.Properties.Settings.Default.LastCheck).TotalMinutes);
                if (minutesFromLastCheck >= checkIntervals[TinyOPDS.Properties.Settings.Default.UpdatesCheck])
                {
                    Log.WriteLine(LogLevel.Info, "Checking software update. Minutes from the last check: {0}", minutesFromLastCheck);
                    WebClient wc = new WebClient();
                    wc.DownloadStringCompleted += wc_DownloadStringCompleted;
                    wc.DownloadStringAsync(new Uri("http://senssoft.com/tinyopds.txt"));
                }
            }

            if (TinyOPDS.Properties.Settings.Default.UseUPnP && _timerCallsCount++ > 5)
            {
                _timerCallsCount = 0;
                if (_server != null && _server.IsActive && _server.IsIdle && _upnpController != null && _upnpController.UPnPReady)
                {
                    if (!_upnpController.Discovered)
                    {
                        _upnpController.DiscoverAsync(true);
                    }
                    else if (openPort.Checked)
                    {
                        int port = int.Parse(TinyOPDS.Properties.Settings.Default.ServerPort);
                        _upnpController.ForwardPort(port, System.Net.Sockets.ProtocolType.Tcp, "TinyOPDS server");
                    }
                }
            }
        }

        void wc_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                TinyOPDS.Properties.Settings.Default.LastCheck = DateTime.Now;
                TinyOPDS.Properties.Settings.Default.Save();

                string[] s = e.Result.Split('\n');
                if (s.Length == 2)
                {
                    s[0] = s[0].Replace("\r", "");
                    double currentVersion = 0, newVersion = 0;
                    if (double.TryParse(string.Format("{0}.{1}", Utils.Version.Major, Utils.Version.Minor), out currentVersion))
                    {
                        if (double.TryParse(s[0], out newVersion))
                        {
                            if (newVersion > currentVersion)
                            {
                                _updateUrl = s[1];
                                _notifyIcon.Visible = true;
                                _notifyIcon.ShowBalloonTip(30000, Localizer.Text("TinyOPDS: update found"), string.Format(Localizer.Text("Click here to download update v {0}"), s[0]), ToolTipIcon.Info);
                            }
                        }
                    }
                }
            }
        }

        void _notifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(_updateUrl);
        }

        void _notifyIcon_BalloonTipClosed(object sender, EventArgs e)
        {
            _notifyIcon.Visible = Properties.Settings.Default.CloseToTray;
        }

        #endregion

        /// <summary>
        /// This event raised on the checkbox change and should reload library
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBox_CheckedChanged(object sender, EventArgs e)
        {
            if (((sender as CheckBox).Name.Equals("checkBox1") && (sender as CheckBox).Checked != TinyOPDS.Properties.Settings.Default.LowMemoryProfile) ||
                 ((sender as CheckBox).Name.Equals("checkBox2") && (sender as CheckBox).Checked != TinyOPDS.Properties.Settings.Default.UseAuthorsAliases))
            {
                // Reload library
                _watcher.IsEnabled = false;
                Library.LoadAsync();
            }
        }

    }
}
