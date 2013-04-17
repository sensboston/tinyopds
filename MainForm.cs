/***********************************************************
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

        #region Statistical information
        int _fb2Count, _epubCount, _skippedFiles, _invalidFiles, _duplicates;
        #endregion

        private const string urlTemplate = "http://{0}:{1}/{2}";

        #region Initialization and startup

        public MainForm()
        {
            Log.SaveToFile = Properties.Settings.Default.SaveLogToDisk;

            InitializeComponent();

            Localizer.Init();
            Localizer.AddMenu(contextMenuStrip1);
            langCombo.DataSource = Localizer.Languages.ToArray();

            LoadSettings();

            Library.LibraryPath = Properties.Settings.Default.LibraryPath;
            Library.LibraryLoaded += (_, __) => 
            { 
                UpdateInfo();
                _watcher.DirectoryToWatch = Library.LibraryPath;
                _watcher.IsEnabled = Properties.Settings.Default.WatchLibrary;
            };

            // Create file watcher
            _watcher = new Watcher(Library.LibraryPath);
            _watcher.OnBookAdded += (object sender, BookAddedEventArgs e) => 
                {
                    UpdateInfo();
                    Log.WriteLine(LogLevel.Info, "Book {0} added to the library", e.BookPath);
                };
            _watcher.OnBookDeleted += (object sender, BookDeletedEventArgs e) => 
                {
                    UpdateInfo();
                    Log.WriteLine(LogLevel.Info, "Book {0} deleted from the library", e.BookPath);
                };
            _watcher.IsEnabled = false;

            intLink.Text = string.Format(urlTemplate, _upnpController.LocalIP.ToString(), Properties.Settings.Default.ServerPort, Properties.Settings.Default.RootPrefix);
            _upnpController.DiscoverCompleted += new EventHandler(_upnpController_DiscoverCompleted);
            _upnpController.DiscoverAsync(Properties.Settings.Default.UseUPnP);

            // Start OPDS server
            StartHttpServer();

            _scanStartTime = DateTime.Now;
            notifyIcon1.Visible = Properties.Settings.Default.CloseToTray;

            Log.WriteLine("TinyOPDS application started");
        }

        void _upnpController_DiscoverCompleted(object sender, EventArgs e)
        {
            if (!IsDisposed && _upnpController != null)
            {
                this.BeginInvoke((MethodInvoker)delegate
                {
                    extLink.Text = string.Format(urlTemplate, _upnpController.ExternalIP.ToString(), Properties.Settings.Default.ServerPort, Properties.Settings.Default.RootPrefix);
                    if (_upnpController.UPnPReady)
                    {
                        openPort.Enabled = true;
                        if (Properties.Settings.Default.OpenNATPort) openPort_CheckedChanged(this, null);
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
            // Setup settings controls
            libraryPath.Text = Properties.Settings.Default.LibraryPath;
            databaseFileName.Text = Utils.Create(Utils.IsoOidNamespace, Properties.Settings.Default.LibraryPath).ToString() + ".db";
            serverName.Text = Properties.Settings.Default.ServerName;
            serverPort.Text = Properties.Settings.Default.ServerPort.ToString();
            rootPrefix.Text = Properties.Settings.Default.RootPrefix;
            startMinimized.Checked = Properties.Settings.Default.StartMinimized;
            startWithWindows.Checked = Properties.Settings.Default.StartWithWindows;
            closeToTray.Checked = Properties.Settings.Default.CloseToTray;
            if (string.IsNullOrEmpty(Properties.Settings.Default.ConvertorPath))
            {
                if (File.Exists(Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), "FB2ePub\\Fb2ePub.exe")))
                {
                    convertorPath.Text = Properties.Settings.Default.ConvertorPath = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), "FB2ePub");
                }
            }
            else convertorPath.Text = Properties.Settings.Default.ConvertorPath;
            converterLinkLabel.Visible = string.IsNullOrEmpty(convertorPath.Text);
            useUPnP.Checked = Properties.Settings.Default.UseUPnP;
            openPort.Checked = Properties.Settings.Default.UseUPnP ? Properties.Settings.Default.OpenNATPort : false;
            langCombo.SelectedValue = Properties.Settings.Default.Language;
            saveLog.Checked = Properties.Settings.Default.SaveLogToDisk;
            useWatcher.Checked = Properties.Settings.Default.WatchLibrary;
            notifyIcon1.Visible = Properties.Settings.Default.CloseToTray;
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.LibraryPath = libraryPath.Text;
            Properties.Settings.Default.ServerName = serverName.Text;
            Properties.Settings.Default.ServerPort = int.Parse(serverPort.Text);
            Properties.Settings.Default.RootPrefix = rootPrefix.Text;
            Properties.Settings.Default.StartMinimized = startMinimized.Checked;
            Properties.Settings.Default.StartWithWindows = startWithWindows.Checked;
            Properties.Settings.Default.CloseToTray = closeToTray.Checked;
            Properties.Settings.Default.ConvertorPath = convertorPath.Text;
            Properties.Settings.Default.Language = langCombo.SelectedValue as string;
            Properties.Settings.Default.OpenNATPort = openPort.Checked;
            Properties.Settings.Default.SaveLogToDisk = saveLog.Checked;
            Properties.Settings.Default.WatchLibrary = useWatcher.Checked;
            Properties.Settings.Default.UseUPnP = useUPnP.Checked;
            Properties.Settings.Default.Save();
        }

        #endregion

        #region Library scanning support

        private void libraryPath_TextChanged(object sender, EventArgs e)
        {
            if (!libraryPath.Text.Equals(Properties.Settings.Default.LibraryPath) && Directory.Exists(libraryPath.Text))
            {
                Properties.Settings.Default.LibraryPath = Library.LibraryPath = libraryPath.Text;
                Properties.Settings.Default.Save();
                booksInDB.Text = string.Format("{0}       fb2: {1}      epub: {2}", 0, 0, 0);
                databaseFileName.Text = Utils.Create(Utils.IsoOidNamespace, Library.LibraryPath).ToString() + ".db";
                // Reload library
                Library.LoadAsync();
                _watcher.DirectoryToWatch = Properties.Settings.Default.LibraryPath;
            }
            else libraryPath.Text = Properties.Settings.Default.LibraryPath;
        }

        private void folderButton_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.SelectedPath = libraryPath.Text;
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                libraryPath.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void scannerButton_Click(object sender, EventArgs e)
        {
            if (_scanner.Status == FileScannerStatus.STOPPED)
            {
                _scanner.OnBookFound += new BookFoundEventHandler(scanner_OnBookFound);
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
                _scanner.Start(libraryPath.Text);
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

            bool isScanning = _scanner.Status == FileScannerStatus.SCANNING;
            TimeSpan dt = DateTime.Now.Subtract(_scanStartTime);
            elapsedTime.Text = dt.ToString(@"hh\:mm\:ss");
            rate.Text = (dt.TotalSeconds) > 0 ? string.Format("{0:0.} books/min", totalBooksProcessed / dt.TotalSeconds * 60) : "---";
            status.Text = IsScanFinished ? Localizer.Text("FINISHED") : (_scanner.Status == FileScannerStatus.SCANNING ? Localizer.Text("SCANNING") : Localizer.Text("STOPPED"));
            scannerButton.Text = (_scanner.Status == FileScannerStatus.SCANNING) ? Localizer.Text("Stop scanning") : Localizer.Text("Start scanning");
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
            _server = new OPDSServer(Properties.Settings.Default.ServerPort);

            serverButton.Text = serverMenuItem.Text = Localizer.Text("Stop server");
            _serverThread = new Thread(new ThreadStart(_server.Listen));
            _serverThread.Priority = ThreadPriority.BelowNormal;
            _serverThread.Start();

            Log.WriteLine("HTTP server started");
        }

        private void StopHttpServer()
        {
            _server.StopServer();
            _serverThread = null;
            _server = null;
            serverButton.Text = serverMenuItem.Text = Localizer.Text("Start server");

            Log.WriteLine("HTTP server stopped");
        }

        private void RestartHttpServer()
        {
            StopHttpServer();
            StartHttpServer();
        }

        private void useUPnP_CheckedChanged(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.UseUPnP != useUPnP.Checked)
            {
                if (useUPnP.Checked)
                {
                    // Re-detect IP addresses using UPnP
                    if (!_upnpController.Discovered) _upnpController.DiscoverAsync(true);
                }
                else
                {
                    openPort.Checked = openPort.Enabled = false;
                }
                Properties.Settings.Default.UseUPnP = useUPnP.Checked;
            }
        }

        private void openPort_CheckedChanged(object sender, EventArgs e)
        {
            if (_upnpController != null && _upnpController.UPnPReady)
            {
                int port = Properties.Settings.Default.ServerPort;
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

        #endregion

        #region Form minimizing and closing

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.CloseToTray)
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
            if (Properties.Settings.Default.CloseToTray && !realExit)
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
            }
            if (_scanner.Status == FileScannerStatus.SCANNING)
            {
                _scanner.Stop();
                Library.Save();
            }
            if (_upnpController != null)
            {
                _upnpController.DiscoverCompleted -= _upnpController_DiscoverCompleted;
                _upnpController.Dispose();
            }

            // Remove port forwarding
            openPort.Checked = false;

            Log.WriteLine("TinyOPDS application closed\n");
        }

        private void exitMenuItem_Click(object sender, EventArgs e)
        {
            realExit = true;
            Close();
        }

        #endregion

        #region Form controls handling

        private void convertorFolder_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.SelectedPath = convertorPath.Text;
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                convertorPath.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void useWatcher_CheckedChanged(object sender, EventArgs e)
        {
            if (_watcher != null && _watcher.IsEnabled != useWatcher.Checked)
            {
                _watcher.IsEnabled = Properties.Settings.Default.WatchLibrary = useWatcher.Checked;
            }
        }

        private void closeToTray_CheckedChanged(object sender, EventArgs e)
        {
            notifyIcon1.Visible = Properties.Settings.Default.CloseToTray = closeToTray.Checked;
            windowMenuItem.Text = Localizer.Text(Visible ? "Hide window" : "Show window");
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
            if (Properties.Settings.Default.SaveLogToDisk != saveLog.Checked)
            {
                Log.SaveToFile = Properties.Settings.Default.SaveLogToDisk = saveLog.Checked;
            }
        }

        /// <summary>
        /// Handle server's root prefix change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rootPrefix_TextChanged(object sender, EventArgs e)
        {
            if (_upnpController != null && _upnpController.UPnPReady)
            {
                if (rootPrefix.Text.EndsWith("/")) rootPrefix.Text = rootPrefix.Text.Remove(rootPrefix.Text.Length - 1);
                Properties.Settings.Default.RootPrefix = rootPrefix.Text;
                intLink.Text = string.Format(urlTemplate, _upnpController.LocalIP.ToString(), Properties.Settings.Default.ServerPort, Properties.Settings.Default.RootPrefix);
                extLink.Text = string.Format(urlTemplate, _upnpController.ExternalIP.ToString(), Properties.Settings.Default.ServerPort, Properties.Settings.Default.RootPrefix);
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
                if (port != Properties.Settings.Default.ServerPort)
                {
                    if (_upnpController != null && _upnpController.UPnPReady && openPort.Checked)
                    {
                        openPort.Checked = false;
                        Properties.Settings.Default.ServerPort = port;
                        openPort.Checked = true;
                    }
                    else Properties.Settings.Default.ServerPort = port;
                    RestartHttpServer();
                }
            }
            else
            {
                MessageBox.Show(Localizer.Text("Invalid port value: value must be numeric and in range from 1 to 65535"));
                serverPort.Text = Properties.Settings.Default.ServerPort.ToString();
            }
        }

        /// <summary>
        /// Set UI language
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void langCombo_SelectedValueChanged(object sender, EventArgs e)
        {
            Localizer.SetLanguage(this, langCombo.SelectedValue as string);
            if (!Properties.Settings.Default.ServerName.Equals(serverName.Text))
            {
                serverName.Text = Properties.Settings.Default.ServerName;
            }
            appVersion.Text = string.Format(Localizer.Text("version {0}.{1} {2}"), Utils.Version.Major, Utils.Version.Minor, Utils.Version.Major == 0?" (beta)":"");
            scannerButton.Text = (_scanner.Status == FileScannerStatus.STOPPED) ? Localizer.Text("Start scanning") : Localizer.Text("Stop scanning");
            serverButton.Text = (_server == null) ? Localizer.Text("Start server") : Localizer.Text("Stop server");
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
            if (sender is LinkLabel && checkUrl((sender as LinkLabel).Text))
            {
                System.Diagnostics.Process.Start((sender as LinkLabel).Links[0].LinkData as string);
            }
        }

        #endregion

    }
}
