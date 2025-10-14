/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * TinyOPDS main UI thread 
 *
 */

using System;
using System.IO;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Diagnostics;

using UPnP;

using TinyOPDS.Data;
using TinyOPDS.Scanner;
using TinyOPDS.Server;
using TinyOPDS.Properties;

namespace TinyOPDS
{
    public partial class MainForm : Form
    {
        OPDSServer server;
        Thread serverThread;
        readonly FileScanner scanner = new FileScanner();
        readonly Watcher watcher;
        DateTime scanStartTime;
        readonly UPnPController upnpController = new UPnPController();
        readonly NotifyIcon notifyIcon = new NotifyIcon();
        readonly BindingSource bs = new BindingSource();
        readonly System.Windows.Forms.Timer updateCheckerTimer = new System.Windows.Forms.Timer();
        readonly UpdateChecker updateChecker = new UpdateChecker();
        string updateUrl = string.Empty;

        int fb2Count, epubCount, skippedFiles, invalidFiles, dups;

        private readonly List<Book> pendingBooks = new List<Book>();
        private readonly object batchLock = new object();

        private Dictionary<string, bool> opdsStructure;
        private bool isLoading = false;

        private const string urlTemplate = "http://{0}:{1}/{2}";

        #region Initialization and startup

        public MainForm()
        {
            Log.SaveToFile = Settings.Default.SaveLogToDisk;

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += CurrentDomain_UnhandledException;

            Log.WriteLine("TinyOPDS version {0}.{1} started", Utils.Version.Major, Utils.Version.Minor);

            InitializeComponent();

            AutoScaleMode = Utils.IsLinux ? AutoScaleMode.Font : AutoScaleMode.Dpi;
            float dpiScale = 1.0f;

            using (Graphics g = CreateGraphics())
            {
                dpiScale = g.DpiX / 96f;
                if (dpiScale > 1.0f)
                {
                    tabControl1.SizeMode = TabSizeMode.Normal;
                    tabControl1.ItemSize = new Size(Math.Max(120, (int)(120 * dpiScale)), Math.Max(28, (int)(28 * dpiScale)));
                }
            }

            treeViewOPDS.ShowLines = true;
            treeViewOPDS.Indent = (int)(treeViewOPDS.Indent * dpiScale * 2);
            treeViewOPDS.ItemHeight = (int)(19 * dpiScale);
            if (Utils.IsLinux) treeViewOPDS.Font = new Font("DejaVu Sans", 20, FontStyle.Regular);

            interfaceCombo.DataSource = UPnPController.LocalInterfaces;
            interfaceCombo.DataBindings.Add(new Binding("SelectedIndex", Settings.Default, "LocalInterfaceIndex", false, DataSourceUpdateMode.OnPropertyChanged));

            logVerbosity.DataBindings.Add(new Binding("SelectedIndex", Settings.Default, "LogLevel", false, DataSourceUpdateMode.OnPropertyChanged));
            updateCombo.DataBindings.Add(new Binding("SelectedIndex", Settings.Default, "UpdatesCheck", false, DataSourceUpdateMode.OnPropertyChanged));

            PerformLayout();

            Icon = Resources.trayIcon;
            notifyIcon.ContextMenuStrip = contextMenuStrip;
            notifyIcon.Icon = Resources.trayIcon;
            notifyIcon.MouseClick += NotifyIcon1_MouseClick;
            notifyIcon.BalloonTipClicked += NotifyIcon_BalloonTipClicked;
            notifyIcon.BalloonTipClosed += NotifyIcon_BalloonTipClosed;

            Localizer.Init();
            Localizer.AddMenu(contextMenuStrip);
            langCombo.SelectedValueChanged -= new EventHandler(LangCombo_SelectedValueChanged);
            langCombo.DataSource = Localizer.Languages.ToArray();
            langCombo.SelectedValueChanged += new EventHandler(LangCombo_SelectedValueChanged);

            LoadSettings();

            updateCheckerTimer.Interval = 1000 * 60;
            updateCheckerTimer.Tick += UpdateChecker_Tick;
            updateChecker.CheckCompleted += UpdateChecker_CheckCompleted;
            if (Settings.Default.UpdatesCheck > 0) updateChecker.CheckAsync();

            bs.AddingNew += Bs_AddingNew;
            bs.AllowNew = true;
            bs.DataSource = HttpProcessor.Credentials;
            dataGridView1.DataSource = bs;
            dataGridView1.Columns[0].HeaderText = Localizer.Text("User");
            dataGridView1.Columns[1].HeaderText = Localizer.Text("Password");
            bs.CurrentItemChanged += Bs_CurrentItemChanged;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            foreach (DataGridViewColumn col in dataGridView1.Columns)
            {
                col.MinimumWidth = 100;
                col.FillWeight = 100;
            }

            Library.LibraryPath = Settings.Default.LibraryPath.SanitizePathName();

            InitializeSQLiteDatabase();

            Library.LibraryLoaded += (_, __) =>
            {
                UpdateInfo();
                watcher.DirectoryToWatch = Library.LibraryPath;
                watcher.IsEnabled = Settings.Default.WatchLibrary;
            };

            watcher = new Watcher(Library.LibraryPath);
            watcher.OnBookAdded += (object sender, BookAddedEventArgs e) =>
            {
                if (e.BookType == BookType.FB2) fb2Count++; else epubCount++;
                UpdateInfo();
                Log.WriteLine(LogLevel.Info, "Added: \"{0}\"", e.BookPath);
            };
            watcher.OnInvalidBook += (_, __) =>
            {
                invalidFiles++;
                UpdateInfo();
            };
            watcher.OnFileSkipped += (object _sender, FileSkippedEventArgs _e) =>
            {
                skippedFiles = _e.Count;
                UpdateInfo();
            };

            watcher.OnBookDeleted += (object sender, BookDeletedEventArgs e) =>
            {
                UpdateInfo();
                Log.WriteLine(LogLevel.Info, "Deleted: \"{0}\"", e.BookPath);
            };
            watcher.IsEnabled = false;

            intLink.Text = string.Format(urlTemplate, upnpController.LocalIP.ToString(), Settings.Default.ServerPort, rootPrefix.Text);
            intWebLink.Text = string.Format(urlTemplate, upnpController.LocalIP.ToString(), Settings.Default.ServerPort, string.Empty);

            StartHttpServer();

            HttpServer.ServerStatistics.StatisticsUpdated += (_, __) =>
            {
                BeginInvoke((MethodInvoker)delegate
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

            scanStartTime = DateTime.Now;
            notifyIcon.Visible = Settings.Default.CloseToTray;

            InitializeOPDSStructure();
            LoadOPDSSettings();
            BuildOPDSTree();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            upnpController.DiscoverCompleted += UpnpController_DiscoverCompleted;
            upnpController.DiscoverAsync(Settings.Default.UseUPnP);

            UpdateInfo();
            databaseFileName.Text = "books.sqlite";

            if (Settings.Default.StartMinimized && Settings.Default.CloseToTray)
            {
                WindowState = FormWindowState.Minimized;
                Visible = false;
                notifyIcon.Visible = true;
            }
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs args)
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

        void UpnpController_DiscoverCompleted(object sender, EventArgs e)
        {
            if (!IsDisposed && upnpController != null)
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    extLink.Text = string.Format(urlTemplate, upnpController.ExternalIP.ToString(), Settings.Default.ServerPort, Settings.Default.RootPrefix);
                    extWebLink.Text = string.Format(urlTemplate, upnpController.ExternalIP.ToString(), Settings.Default.ServerPort, string.Empty);
                    if (upnpController.UPnPReady)
                    {
                        openPort.Enabled = true;
                        if (Settings.Default.OpenNATPort) OpenPort_CheckedChanged(this, null);
                    }
                });
            }
        }

        #endregion

        #region SQLite initialization

        private void InitializeSQLiteDatabase()
        {
            try
            {
                string libraryPath = Settings.Default.LibraryPath.SanitizePathName();
                string sqliteDbPath = GetSQLiteDatabasePath();

                Log.WriteLine("Initializing SQLite database...");
                Log.WriteLine("Library path: {0}", libraryPath);
                Log.WriteLine("SQLite database: {0}", sqliteDbPath);

                Library.LibraryPath = libraryPath;
                Library.Initialize(sqliteDbPath);

                Log.WriteLine("SQLite database successfully initialized");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error initializing SQLite: {0}", ex.Message);
            }
        }

        private string GetSQLiteDatabasePath()
        {
            return Path.Combine(Utils.ServiceFilesLocation, "books.sqlite");
        }

        #endregion

        #region Batch processing methods

        private bool AddBookToBatch(Book book)
        {
            lock (batchLock)
            {
                pendingBooks.Add(book);

                int batchSize = Math.Max(1, Settings.Default.BatchSize);
                if (pendingBooks.Count >= batchSize)
                {
                    _ = FlushPendingBooksAsync();
                }

                return true;
            }
        }

        private async Task FlushPendingBooksAsync()
        {
            List<Book> booksToProcess;

            lock (batchLock)
            {
                if (pendingBooks.Count == 0) return;

                booksToProcess = new List<Book>(pendingBooks);
                pendingBooks.Clear();
            }

            try
            {
                var batchResult = await Library.AddBatchAsync(booksToProcess);

                if (batchResult.Duplicates > 0 || batchResult.Errors > 0)
                {
                    int booksToSubtract = batchResult.Duplicates + batchResult.Errors;

                    dups += batchResult.Duplicates;

                    double fb2Ratio = batchResult.FB2Count > 0 ? (double)batchResult.FB2Count / (batchResult.FB2Count + batchResult.EPUBCount) : 0.5;
                    int fb2ToSubtract = (int)(booksToSubtract * fb2Ratio);
                    int epubToSubtract = booksToSubtract - fb2ToSubtract;

                    fb2Count = Math.Max(0, fb2Count - fb2ToSubtract);
                    epubCount = Math.Max(0, epubCount - epubToSubtract);

                    Log.WriteLine("Batch flush completed: {0} processed, {1} added, {2} duplicates, {3} errors",
                        batchResult.TotalProcessed, batchResult.Added, batchResult.Duplicates, batchResult.Errors);
                }
                else
                {
                    Log.WriteLine("Batch flush completed: {0} books successfully added", batchResult.Added);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error flushing books batch: {0}", ex.Message);

                foreach (var book in booksToProcess)
                {
                    try
                    {
                        if (!Library.Add(book))
                        {
                            dups++;
                            if (book.BookType == BookType.FB2) fb2Count = Math.Max(0, fb2Count - 1);
                            else epubCount = Math.Max(0, epubCount - 1);
                        }
                    }
                    catch (Exception ex2)
                    {
                        Log.WriteLine(LogLevel.Error, "Error adding book {0}: {1}", book.FileName, ex2.Message);
                    }
                }
            }
        }

        private async Task FlushRemainingBooksAsync()
        {
            lock (batchLock)
            {
                if (pendingBooks.Count > 0)
                {
                    Log.WriteLine("Flushing {0} remaining books at scan completion", pendingBooks.Count);
                }
                else
                {
                    return;
                }
            }

            await FlushPendingBooksAsync();

            TimeSpan dt = DateTime.Now.Subtract(scanStartTime);
            Log.WriteLine($"Scan completed, elapsed time {dt:hh\\:mm\\:ss}");
        }

        private (int Total, int FB2, int EPUB) GetLibraryStats()
        {
            return (Library.Count, Library.FB2Count, Library.EPUBCount);
        }

        #endregion

        #region Application settings

        private void LoadSettings()
        {
            linkLabel6.Links.Add(0, linkLabel6.Text.Length, "https://github.com/rsarov/SQLiteFTS5NET");

            libraryPath.Text = Settings.Default.LibraryPath;
            if (!string.IsNullOrEmpty(Settings.Default.LibraryPath))
            {
                databaseFileName.Text = "books.sqlite";
            }

            serverName.Validated -= ServerName_Validated;
            serverName.Text = Settings.Default.ServerName ?? "Home library";
            serverName.Validated += ServerName_Validated;

            appVersion.Text = string.Format(Localizer.Text("version {0}.{1} {2}"), Utils.Version.Major, Utils.Version.Minor, Utils.Version.Major == 0 ? " (beta)" : "");
            filterByLanguage.Checked = Settings.Default.FilterBooksByInterfaceLanguage;
            oneInstance.Checked = Settings.Default.OnlyOneInstance;
            darkTheme.Checked = Settings.Default.DarkThemeOnWeb;

            if (Utils.IsLinux) startWithWindows.Enabled = false;

            interfaceCombo.SelectedIndex = Math.Min(UPnPController.LocalInterfaces.Count - 1, Settings.Default.LocalInterfaceIndex);
            logVerbosity.SelectedIndex = Math.Min(2, Settings.Default.LogLevel);
            updateCombo.SelectedIndex = Math.Min(2, Settings.Default.UpdatesCheck);
            langCombo.SelectedValue = Settings.Default.Language;
            sortOrderCombo.SelectedIndex = Settings.Default.SortOrder;
            newBooksPeriodCombo.SelectedIndex = Settings.Default.NewBooksPeriod;

            label22.Enabled = logVerbosity.Enabled = saveLog.Checked = Settings.Default.SaveLogToDisk;

            openPort.Checked = Settings.Default.UseUPnP && Settings.Default.OpenNATPort;
            banClients.Enabled = rememberClients.Enabled = dataGridView1.Enabled = Settings.Default.UseHTTPAuth;
            wrongAttemptsCount.Enabled = banClients.Checked && useHTTPAuth.Checked;

            notifyIcon.Visible = Settings.Default.CloseToTray;
            updateCheckerTimer.Start();

            if (Settings.Default.BatchSize <= 0)
            {
                Settings.Default.BatchSize = 500;
                Settings.Default.Save();
            }

            radioButton1.Checked = Settings.Default.CacheImagesInMemory;
            radioButton2.Checked = !radioButton1.Checked;
            comboBox1.Text = $"{Settings.Default.MaxRAMImageCacheSizeMB} MB";
            comboBox1.Enabled = radioButton1.Checked;

            try
            {
                HttpProcessor.Credentials.Clear();
                if (!string.IsNullOrEmpty(Settings.Default.Credentials))
                {
                    string decryptedCredentials = Crypt.DecryptStringAES(Settings.Default.Credentials, urlTemplate);
                    if (!string.IsNullOrEmpty(decryptedCredentials))
                    {
                        string[] pairs = decryptedCredentials.Split(';');
                        foreach (string pair in pairs)
                        {
                            if (!string.IsNullOrEmpty(pair))
                            {
                                string[] cred = pair.Split(':');
                                if (cred.Length == 2 && !string.IsNullOrEmpty(cred[0]))
                                {
                                    HttpProcessor.Credentials.Add(new Credential(cred[0], cred[1]));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error loading credentials: {0}", ex.Message);
                Settings.Default.Credentials = string.Empty;
                Settings.Default.Save();
            }
        }

        private void SaveSettings()
        {
            Settings.Default.LibraryPath = libraryPath.Text.SanitizePathName();
            Settings.Default.Language = langCombo.SelectedValue as string;
            Settings.Default.Save();
        }

        #endregion

        #region Credentials handling

        private void DataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dataGridView1.Columns[e.ColumnIndex].Name == "Password" && e.Value != null)
            {
                dataGridView1.Rows[e.RowIndex].Tag = e.Value;
                e.Value = new string('*', e.Value.ToString().Length);
            }
        }

        void Bs_AddingNew(object sender, AddingNewEventArgs e)
        {
            e.NewObject = new Credential("", "");
        }

        void Bs_CurrentItemChanged(object sender, EventArgs e)
        {
            string s = string.Empty;
            foreach (Credential cred in HttpProcessor.Credentials) s += cred.User + ":" + cred.Password + ";";
            try
            {
                Settings.Default.Credentials = string.IsNullOrEmpty(s) ? string.Empty : Crypt.EncryptStringAES(s, urlTemplate);
            }
            finally
            {
                Settings.Default.Save();
            }
        }

        #endregion

        #region Library scanning support

        private void LibraryPath_TextChanged(object sender, EventArgs e)
        {
            databaseFileName.Text = "books.sqlite";
        }

        private void LibraryPath_Validated(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(databaseFileName.Text) && !Library.LibraryPath.Equals(databaseFileName.Text.SanitizePathName()) &&
                Directory.Exists(libraryPath.Text.SanitizePathName()))
            {
                Library.LibraryPath = Settings.Default.LibraryPath = libraryPath.Text.SanitizePathName();

                var (Total, FB2, EPUB) = GetLibraryStats();
                booksInDB.Text = $"{Total}           fb2: {FB2}       epub: {EPUB}";

                databaseFileName.Text = "books.sqlite";

                watcher.IsEnabled = false;

                Library.Load();
            }
            else
            {
                if (!string.IsNullOrEmpty(Settings.Default.LibraryPath)) libraryPath.Text = Settings.Default.LibraryPath.SanitizePathName();
                else libraryPath.Undo();
            }
        }

        private void FolderButton_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.SelectedPath = libraryPath.Text;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    libraryPath.Text = dialog.SelectedPath.SanitizePathName();
                    LibraryPath_Validated(sender, e);
                }
            }
        }

        private void ScannerButton_Click(object sender, EventArgs e)
        {
            if (scanner.Status != FileScannerStatus.SCANNING)
            {
                scanner.OnBookFound += Scanner_OnBookFound;
                scanner.OnInvalidBook += (_, __) => { invalidFiles++; };
                scanner.OnFileSkipped += (object _sender, FileSkippedEventArgs _e) =>
                {
                    skippedFiles = _e.Count;
                    UpdateInfo();
                };
                scanner.OnScanCompleted += async (_, __) =>
                {
                    await FlushRemainingBooksAsync();
                    UpdateInfo(true);
                    Log.WriteLine("Directory scanner completed");
                };

                if (Settings.Default.ClearDBOnScan)
                {
                    try
                    {
                        scannerButton.Text = Localizer.Text("Cleaning database...");
                        scannerButton.Enabled = false;

                        Library.ClearDatabase(preserveGenres: true);
                        Log.WriteLine("Database cleared before scanning (CleanDBOnScan = true)");
                        var stats = GetLibraryStats();
                        stats.Total = stats.FB2 = stats.EPUB = 0;
                        booksInDB.Text = $"{stats.Total}           fb2: {stats.FB2}       epub: {stats.EPUB}";
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine(LogLevel.Error, "Failed to clear database before scan: {0}", ex.Message);
                    }
                    finally
                    {
                        scannerButton.Enabled = true;
                    }
                }

                fb2Count = epubCount = skippedFiles = invalidFiles = dups = 0;
                scanStartTime = DateTime.Now;
                startTime.Text = scanStartTime.ToString(@"HH\:mm\:ss");
                scanner.Start(libraryPath.Text.SanitizePathName());
                scannerButton.Text = Localizer.Text("Stop scanning");

                Log.WriteLine("Directory scanner started with batch size: {0}", Settings.Default.BatchSize);
            }
            else
            {
                scanner.Stop();
                Task.Run(async () =>
                {
                    await FlushRemainingBooksAsync();
                    BeginInvoke((MethodInvoker)delegate
                    {
                        UpdateInfo(true);
                        scannerButton.Text = Localizer.Text("Start scanning");
                        Log.WriteLine("Directory scanner stopped");
                    });
                });
            }
        }

        void Scanner_OnBookFound(object sender, BookFoundEventArgs e)
        {
            AddBookToBatch(e.Book);

            if (e.Book.BookType == BookType.FB2) fb2Count++;
            else epubCount++;

            var totalProcessed = fb2Count + epubCount + dups;
            if (totalProcessed % 20 == 0)
            {
                UpdateInfo();
            }

            if (totalProcessed % 20000 == 0)
            {
                GC.Collect();
            }
        }

        private void UpdateInfo(bool IsScanFinished = false)
        {
            if (InvokeRequired) { BeginInvoke((MethodInvoker)delegate { InternalUpdateInfo(IsScanFinished); }); }
            else { InternalUpdateInfo(IsScanFinished); }
        }

        private void InternalUpdateInfo(bool IsScanFinished)
        {
            var (Total, FB2, EPUB) = GetLibraryStats();
            if (Total > 0) booksInDB.Text = $"{Total}           fb2: {FB2}       epub: {EPUB}";
            booksFound.Text = $"fb2: {fb2Count}   epub: {epubCount}";
            skippedBooks.Text = skippedFiles.ToString();
            invalidBooks.Text = invalidFiles.ToString();
            duplicates.Text = dups.ToString();
            int totalProcessed = fb2Count + epubCount + skippedFiles + invalidFiles + dups;
            booksProcessed.Text = totalProcessed.ToString();

            TimeSpan dt = DateTime.Now.Subtract(scanStartTime);
            elapsedTime.Text = dt.ToString(@"hh\:mm\:ss");
            rate.Text = (dt.TotalSeconds) > 0 ? string.Format(Localizer.Text("{0} books/min"), (int)(totalProcessed / dt.TotalSeconds * 60)) : "---";
            if (scannerButton.Enabled)
            {
                status.Text = IsScanFinished ? Localizer.Text("FINISHED") : (scanner.Status == FileScannerStatus.SCANNING ? Localizer.Text("SCANNING") : Localizer.Text("STOPPED"));
                scannerButton.Text = (scanner.Status == FileScannerStatus.SCANNING) ? Localizer.Text("Stop scanning") : Localizer.Text("Start scanning");
            }
        }

        #endregion

        #region HTTP (OPDS) server & network support

        private void ServerButton_Click(object sender, EventArgs e)
        {
            if (server == null) StartHttpServer(); else StopHttpServer();
        }

        private void StartHttpServer()
        {
            HttpProcessor.ClearAllAuthorizedClients();
            server = new OPDSServer(IPAddress.Any, int.Parse(Settings.Default.ServerPort));

            serverThread = new Thread(new ThreadStart(server.Listen)) { Priority = ThreadPriority.BelowNormal };
            serverThread.Start();
            server.ServerReady.WaitOne(TimeSpan.FromMilliseconds(500));
            if (!server.IsActive)
            {
                if (server.ServerException != null)
                {
                    if (server.ServerException is System.Net.Sockets.SocketException)
                    {
                        MessageBox.Show(string.Format(Localizer.Text("Probably, port {0} is already in use. Please try different port value."), Settings.Default.ServerPort), Localizer.Text("Warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show(server.ServerException.Message, Localizer.Text("Error"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    server.StopServer();
                    serverThread = null;
                    server = null;
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
            if (server != null)
            {
                server.StopServer();
                serverThread = null;
                server = null;
                Log.WriteLine("HTTP server stopped");
            }
            serverButton.Text = serverMenuItem.Text = Localizer.Text("Start server");
        }

        private void RestartHttpServer()
        {
            StopHttpServer();
            StartHttpServer();
        }

        private void UseUPnP_CheckStateChanged(object sender, EventArgs e)
        {
            upnpController.DiscoverAsync(true);
        }

        private void OpenPort_CheckedChanged(object sender, EventArgs e)
        {
            if (upnpController != null && upnpController.UPnPReady)
            {
                int port = int.Parse(Settings.Default.ServerPort);
                if (openPort.Checked)
                {
                    upnpController.ForwardPort(port, System.Net.Sockets.ProtocolType.Tcp, "TinyOPDS server");
                    Log.WriteLine("Port {0} forwarded by UPnP", port);
                }
                else
                {
                    upnpController.DeleteForwardingRule(port, System.Net.Sockets.ProtocolType.Tcp);
                    Log.WriteLine("Port {0} closed", port);
                }
            }
        }

        private void InterfaceCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (upnpController != null && upnpController.InterfaceIndex != interfaceCombo.SelectedIndex)
            {
                upnpController.InterfaceIndex = interfaceCombo.SelectedIndex;
                intLink.Text = string.Format(urlTemplate, upnpController.LocalIP.ToString(), Settings.Default.ServerPort, rootPrefix.Text);
                intWebLink.Text = string.Format(urlTemplate, upnpController.LocalIP.ToString(), Settings.Default.ServerPort, string.Empty);

                if (Settings.Default.UseUPnP && openPort.Checked)
                {
                    int port = int.Parse(Settings.Default.ServerPort);
                    upnpController.DeleteForwardingRule(port, System.Net.Sockets.ProtocolType.Tcp);
                    upnpController.ForwardPort(port, System.Net.Sockets.ProtocolType.Tcp, "TinyOPDS server");
                }
                RestartHttpServer();
            }
        }

        #endregion

        #region Form minimizing and closing

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (Settings.Default.CloseToTray)
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    Visible = false;
                    notifyIcon.Visible = true;
                    windowMenuItem.Text = Localizer.Text("Show window");
                }
                else if (WindowState == FormWindowState.Normal)
                {
                    Visible = true;
                    notifyIcon.Visible = true;
                    windowMenuItem.Text = Localizer.Text("Hide window");
                }
            }
        }

        private void WindowMenuItem_Click(object sender, EventArgs e)
        {
            if (!ShowInTaskbar)
            {
                ShowInTaskbar = true;
                notifyIcon.Visible = Settings.Default.CloseToTray;
            }
            else
            {
                Visible = !Visible;
                if (!Visible && Settings.Default.CloseToTray)
                {
                    notifyIcon.Visible = true;
                }
            }

            if (Visible) WindowState = FormWindowState.Normal;
            windowMenuItem.Text = Localizer.Text(Visible ? "Hide window" : "Show window");
        }

        private void NotifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) WindowMenuItem_Click(this, null);
        }

        private bool realExit = false;
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Settings.Default.CloseToTray && !realExit)
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

            if (updateChecker != null && updateChecker.IsChecking)
            {
                updateChecker.Cancel();
            }

            if (server != null && server.IsActive)
            {
                server.StopServer();
                serverThread = null;
                server = null;

                if (upnpController != null)
                {
                    if (Settings.Default.UseUPnP)
                    {
                        int port = int.Parse(Settings.Default.ServerPort);
                        upnpController.DeleteForwardingRule(port, System.Net.Sockets.ProtocolType.Tcp);
                    }
                    upnpController.DiscoverCompleted -= UpnpController_DiscoverCompleted;
                    upnpController.Dispose();
                }
            }

            if (scanner.Status == FileScannerStatus.SCANNING) scanner.Stop();

            notifyIcon.Visible = false;

            Log.WriteLine("TinyOPDS closed\n");
        }

        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            realExit = true;
            Close();
        }

        #endregion

        #region Form controls handling

        private void OneInstance_CheckedChanged(object sender, EventArgs e)
        {
            if (sender != null)
            {
                Settings.Default.OnlyOneInstance = oneInstance.Checked;
                Settings.Default.Save();
            }
        }

        private void CacheType_CheckedChanged(object sender, EventArgs e)
        {
            if (sender != null)
            {
                comboBox1.Enabled = (sender == radioButton1);
                Settings.Default.CacheImagesInMemory = (sender == radioButton1);
                Settings.Default.Save();
            }
        }

        private void ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender != null)
            {
                var data = ((sender as ComboBox).SelectedItem as string).Split();
                if (int.TryParse(data[0], out int memorySize))
                {
                    Settings.Default.MaxRAMImageCacheSizeMB = memorySize;
                    Settings.Default.Save();
                }
            }
        }

        private void UseWatcher_CheckedChanged(object sender, EventArgs e)
        {
            if (watcher != null && watcher.IsEnabled != useWatcher.Checked)
            {
                watcher.IsEnabled = useWatcher.Checked;
                Settings.Default.WatchLibrary = watcher.IsEnabled;
                Settings.Default.Save();
            }
        }

        private void CloseToTray_CheckedChanged(object sender, EventArgs e)
        {
            notifyIcon.Visible = closeToTray.Checked;

            if (!closeToTray.Checked && !Visible)
            {
                Visible = true;
                WindowState = FormWindowState.Normal;
            }
        }

        private void StartWithWindows_CheckedChanged(object sender, EventArgs e)
        {
            Microsoft.Win32.RegistryKey registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            bool exists = (registryKey.GetValue("TinyOPDS") != null);
            if (startWithWindows.Checked && !exists) registryKey.SetValue("TinyOPDS", Application.ExecutablePath);
            else if (exists && !startWithWindows.Checked) registryKey.DeleteValue("TinyOPDS");
        }

        private void SaveLog_CheckedChanged(object sender, EventArgs e)
        {
            Log.SaveToFile = label22.Enabled = logVerbosity.Enabled = saveLog.Checked;
        }

        private void UpdateServerLinks()
        {
            if (upnpController != null)
            {
                if (upnpController.LocalIP != null)
                {
                    intLink.Text = string.Format(urlTemplate, upnpController.LocalIP.ToString(), Settings.Default.ServerPort, rootPrefix.Text);
                    intWebLink.Text = string.Format(urlTemplate, upnpController.LocalIP.ToString(), Settings.Default.ServerPort, string.Empty);
                }
                if (upnpController.ExternalIP != null)
                {
                    extLink.Text = string.Format(urlTemplate, upnpController.ExternalIP.ToString(), Settings.Default.ServerPort, rootPrefix.Text);
                    extWebLink.Text = string.Format(urlTemplate, upnpController.ExternalIP.ToString(), Settings.Default.ServerPort, string.Empty);
                }
            }
        }

        private void RootPrefix_TextChanged(object sender, EventArgs e)
        {
            if (sender is TextBox && (sender as TextBox).CanUndo)
            {
                if (rootPrefix.Text.EndsWith("/")) rootPrefix.Text = rootPrefix.Text.Remove(rootPrefix.Text.Length - 1);
                UpdateServerLinks();
            }
        }

        private void ServerPort_Validated(object sender, EventArgs e)
        {
            bool valid = int.TryParse(serverPort.Text, out int port);
            if (valid && port >= 1 && port <= 65535)
            {
                if (upnpController != null && upnpController.UPnPReady && openPort.Checked)
                {
                    openPort.Checked = false;
                    Settings.Default.ServerPort = port.ToString();
                    openPort.Checked = true;
                }
                else Settings.Default.ServerPort = port.ToString();
                if (server != null && server.IsActive)
                {
                    RestartHttpServer();
                }
            }
            else
            {
                MessageBox.Show(Localizer.Text("Invalid port value: value must be numeric and in range from 1 to 65535"), Localizer.Text("Warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                serverPort.Text = Settings.Default.ServerPort.ToString();
            }
            UpdateServerLinks();
        }

        private void LangCombo_SelectedValueChanged(object sender, EventArgs e)
        {
            var lang = langCombo.SelectedValue as string;
            Settings.Default.Language = lang;
            Settings.Default.Save();
            Localizer.SetLanguage(this, lang);
            appVersion.Text = string.Format(Localizer.Text("version {0}.{1} {2}"), Utils.Version.Major, Utils.Version.Minor, Utils.Version.Major == 0 ? " (beta)" : "");
            scannerButton.Text = Localizer.Text((scanner.Status == FileScannerStatus.STOPPED) ? "Start scanning" : "Stop scanning");
            serverButton.Text = Localizer.Text((server == null) ? "Start server" : "Stop server");
            serverMenuItem.Text = Localizer.Text((server == null) ? "Start server" : "Stop server");
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

            if (dataGridView1.Columns.Count >= 2)
            {
                dataGridView1.Columns[0].HeaderText = Localizer.Text("User");
                dataGridView1.Columns[1].HeaderText = Localizer.Text("Password");
            }
            BuildOPDSTree();
        }

        private void DonateButton_Click(object sender, EventArgs e)
        {
            const string business = "sens.boston@gmail.com", description = "Donation%20for%20the%20TinyOPDS", country = "US", currency = "USD";
            string url = string.Format("https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business={0}&lc={1}&item_name={2}&currency_code={3}&bn=PP%2dDonationsBF",
                business, country, description, currency);
            Process.Start(url);
        }

        private bool CheckUrl(string uriName)
        {
            bool result = Uri.TryCreate(uriName, UriKind.Absolute, out Uri uriResult);
            return result && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        private void LinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (sender is LinkLabel && CheckUrl((sender as LinkLabel).Text))
            {
                System.Diagnostics.Process.Start((sender as LinkLabel).Text);
            }
        }

        private void LinkLabel3_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (sender is LinkLabel && CheckUrl((sender as LinkLabel).Links[0].LinkData as string))
            {
                System.Diagnostics.Process.Start((sender as LinkLabel).Links[0].LinkData as string);
            }
        }

        private void UseHTTPAuth_CheckedChanged(object sender, EventArgs e)
        {
            dataGridView1.Enabled = banClients.Enabled = rememberClients.Enabled = useHTTPAuth.Checked;
            wrongAttemptsCount.Enabled = banClients.Enabled && banClients.Checked;
        }

        private void BanClients_CheckedChanged(object sender, EventArgs e)
        {
            wrongAttemptsCount.Enabled = banClients.Checked;
        }

        private void LogVerbosity_SelectedIndexChanged(object sender, EventArgs e)
        {
            Log.VerbosityLevel = (LogLevel)logVerbosity.SelectedIndex;
        }

        private void UpdateCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Default.UpdatesCheck = updateCombo.SelectedIndex;
        }

        private void ViewLogFile_Click(object sender, EventArgs e)
        {
            try
            {
                string logPath = Log.LogFileName;

                if (File.Exists(logPath))
                {
                    string tempLogPath = Path.Combine(Path.GetTempPath(), "TinyOPDS.log");
                    File.Copy(logPath, tempLogPath, true);

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = tempLogPath,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                else
                {
                    Log.WriteLine(LogLevel.Warning, "Log file not found: {0}", logPath);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Failed to open log file: {0}", ex.Message);
            }
        }

        private void SortOrderCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Default.SortOrder = sortOrderCombo.SelectedIndex;
        }

        private void NewBooksPeriodCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            Settings.Default.NewBooksPeriod = newBooksPeriodCombo.SelectedIndex;
        }

        #endregion

        #region TinyOPDS updates checker

        void UpdateChecker_Tick(object sender, EventArgs e)
        {
            if (Settings.Default.UpdatesCheck > 0 && !updateChecker.IsChecking)
            {
                if (UpdateChecker.ShouldCheckForUpdates(Settings.Default.UpdatesCheck,
                                                        Settings.Default.LastCheck))
                {
                    updateChecker.CheckAsync();
                }
            }
        }

        void UpdateChecker_CheckCompleted(object sender, UpdateCheckEventArgs e)
        {
            if (e.UpdateAvailable)
            {
                updateUrl = e.DownloadUrl;

                BeginInvoke((MethodInvoker)delegate
                {
                    notifyIcon.Visible = true;
                    notifyIcon.ShowBalloonTip(30000,
                        Localizer.Text("TinyOPDS: update found"),
                        string.Format(Localizer.Text("Version {0} is available. Click here to view releases."),
                            e.NewVersion),
                        ToolTipIcon.Info);
                });
            }
        }

        void NotifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(updateUrl))
            {
                System.Diagnostics.Process.Start(updateUrl);
            }
        }

        void NotifyIcon_BalloonTipClosed(object sender, EventArgs e)
        {
            notifyIcon.Visible = Settings.Default.CloseToTray ||
                                WindowState == FormWindowState.Minimized ||
                                !Visible;
        }

        #endregion

        #region OPDS routes structure 

        private void InitializeOPDSStructure()
        {
            opdsStructure = new Dictionary<string, bool>
            {
                {"newdate", false},
                {"newtitle", false},
                {"authorsindex", true},
                {"author-details", true},
                {"author-series", true},
                {"author-no-series", true},
                {"author-alphabetic", true},
                {"author-by-date", true},
                {"sequencesindex", true},
                {"genres", true},
                {"downloads", true},
                {"downloads-by-date", true},
                {"downloads-alphabetic", true}
            };
        }

        private void LoadOPDSSettings()
        {
            try
            {
                string settingsString = GetOPDSStructureFromSettings();

                if (!string.IsNullOrEmpty(settingsString))
                {
                    ParseOPDSStructure(settingsString);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error loading OPDS routes: {0}", ex.Message);
            }
        }

        private string GetOPDSStructureFromSettings()
        {
            return Settings.Default.OPDSStructure ??
                   "newdate:0;newtitle:0;authorsindex:1;author-details:1;author-series:1;author-no-series:1;author-alphabetic:1;author-by-date:1;sequencesindex:1;genres:1;downloads:1;downloads-by-date:1;downloads-alphabetic:1";
        }

        private void SaveOPDSStructureToSettings(string structure)
        {
            try
            {
                Settings.Default.OPDSStructure = structure;
                Settings.Default.Save();
                Log.WriteLine(LogLevel.Info, "OPDS routes saved: {0}", structure);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error saving OPDS routes: {0}", ex.Message);
            }
        }

        private void ParseOPDSStructure(string structure)
        {
            if (string.IsNullOrEmpty(structure)) return;

            string[] parts = structure.Split(';');
            foreach (string part in parts)
            {
                string[] keyValue = part.Split(':');
                if (keyValue.Length == 2 && opdsStructure.ContainsKey(keyValue[0]))
                {
                    opdsStructure[keyValue[0]] = keyValue[1] == "1";
                }
            }
        }

        private string SerializeOPDSStructure()
        {
            return string.Join(";", opdsStructure.Select(kvp => $"{kvp.Key}:{(kvp.Value ? "1" : "0")}"));
        }

        private void SaveOPDSSettings()
        {
            try
            {
                string structure = SerializeOPDSStructure();
                SaveOPDSStructureToSettings(structure);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error saving OPDS routes: {0}", ex.Message);
            }
        }

        private void BuildOPDSTree()
        {
            if (opdsStructure != null)
            {
                isLoading = true;
                treeViewOPDS.Nodes.Clear();

                TreeNode rootNode = new TreeNode(Localizer.Text("Root"))
                {
                    Tag = "root",
                    Checked = true
                };

                TreeNode newBooksNode = CreateTreeNode(Localizer.Text("New Books"), "newbooks-section", opdsStructure["newdate"] || opdsStructure["newtitle"]);
                newBooksNode.Nodes.Add(CreateTreeNode(Localizer.Text("New Books (by date)"), "newdate", opdsStructure["newdate"]));
                newBooksNode.Nodes.Add(CreateTreeNode(Localizer.Text("New Books (alphabetically)"), "newtitle", opdsStructure["newtitle"]));
                rootNode.Nodes.Add(newBooksNode);

                TreeNode authorsNode = CreateTreeNode(Localizer.Text("By Authors"), "authorsindex", opdsStructure["authorsindex"]);

                TreeNode authorDetailsNode = CreateTreeNode(Localizer.Text("Author's books"), "author-details", opdsStructure["author-details"]);
                authorDetailsNode.Nodes.Add(CreateTreeNode(Localizer.Text("Books by Series"), "author-series", opdsStructure["author-series"]));
                authorDetailsNode.Nodes.Add(CreateTreeNode(Localizer.Text("Books without Series"), "author-no-series", opdsStructure["author-no-series"]));
                authorDetailsNode.Nodes.Add(CreateTreeNode(Localizer.Text("Books Alphabetically"), "author-alphabetic", opdsStructure["author-alphabetic"]));
                authorDetailsNode.Nodes.Add(CreateTreeNode(Localizer.Text("Books by Date"), "author-by-date", opdsStructure["author-by-date"]));

                authorsNode.Nodes.Add(authorDetailsNode);
                rootNode.Nodes.Add(authorsNode);

                rootNode.Nodes.Add(CreateTreeNode(Localizer.Text("By Series"), "sequencesindex", opdsStructure["sequencesindex"]));
                rootNode.Nodes.Add(CreateTreeNode(Localizer.Text("By Genres"), "genres", opdsStructure["genres"]));

                TreeNode downloadsNode = CreateTreeNode(Localizer.Text("Downloaded books"), "downloads", opdsStructure["downloads"]);
                downloadsNode.Nodes.Add(CreateTreeNode(Localizer.Text("By download date"), "downloads-by-date", opdsStructure["downloads-by-date"]));
                downloadsNode.Nodes.Add(CreateTreeNode(Localizer.Text("Alphabetically"), "downloads-alphabetic", opdsStructure["downloads-alphabetic"]));
                rootNode.Nodes.Add(downloadsNode);

                treeViewOPDS.Nodes.Add(rootNode);
                treeViewOPDS.ExpandAll();

                UpdateTreeNodeStyles();
                isLoading = false;
            }
        }

        private TreeNode CreateTreeNode(string text, string tag, bool isChecked)
        {
            TreeNode node = new TreeNode(text)
            {
                Tag = tag,
                Checked = isChecked
            };
            return node;
        }

        private void UpdateTreeNodeStyles()
        {
            foreach (TreeNode node in treeViewOPDS.Nodes)
            {
                UpdateNodeStyle(node);
            }
        }

        private void UpdateNodeStyle(TreeNode node)
        {
            bool isEnabled = node.Checked;

            if (isEnabled)
            {
                node.NodeFont = new Font(treeViewOPDS.Font, FontStyle.Regular);
                node.ForeColor = Color.Black;
            }
            else
            {
                node.NodeFont = new Font(treeViewOPDS.Font, FontStyle.Regular);
                node.ForeColor = Color.Gray;
            }

            foreach (TreeNode childNode in node.Nodes)
            {
                UpdateNodeStyle(childNode);
            }
        }

        private void TreeViewOPDS_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (isLoading) return;

            TreeNode node = e.Node;
            string tag = node.Tag?.ToString();

            // Root node protection
            if (tag == "root" && !node.Checked)
            {
                isLoading = true;
                node.Checked = true;
                isLoading = false;
                return;
            }

            Log.WriteLine(LogLevel.Info, "OPDS route changed: {0} = {1}", tag, node.Checked);

            isLoading = true;
            try
            {
                // Update all child nodes recursively
                UpdateChildNodesRecursive(node, node.Checked);

                // Update parent node state based on children
                UpdateParentNodeState(node);

                // Sync structure from tree
                SyncStructureFromTree();

                // Update visual styles
                UpdateTreeNodeStyles();

                // Save and reload
                SaveAndReload();
            }
            finally
            {
                isLoading = false;
            }
        }

        private void UpdateChildNodesRecursive(TreeNode parentNode, bool isChecked)
        {
            foreach (TreeNode childNode in parentNode.Nodes)
            {
                string childTag = childNode.Tag?.ToString();

                if (childTag == "root") continue;

                childNode.Checked = isChecked;

                if (!string.IsNullOrEmpty(childTag) && opdsStructure.ContainsKey(childTag))
                {
                    opdsStructure[childTag] = isChecked;
                }

                if (childNode.Nodes.Count > 0)
                {
                    UpdateChildNodesRecursive(childNode, isChecked);
                }
            }
        }

        private void UpdateParentNodeState(TreeNode childNode)
        {
            TreeNode parent = childNode.Parent;
            if (parent == null) return;

            string parentTag = parent.Tag?.ToString();

            if (parentTag == "root") return;

            bool anyChildChecked = false;
            foreach (TreeNode sibling in parent.Nodes)
            {
                if (sibling.Checked)
                {
                    anyChildChecked = true;
                    break;
                }
            }

            if (!anyChildChecked && parent.Checked)
            {
                parent.Checked = false;

                if (!string.IsNullOrEmpty(parentTag) && opdsStructure.ContainsKey(parentTag))
                {
                    opdsStructure[parentTag] = false;
                }

                UpdateParentNodeState(parent);
            }
            else if (anyChildChecked && !parent.Checked)
            {
                parent.Checked = true;

                if (!string.IsNullOrEmpty(parentTag) && opdsStructure.ContainsKey(parentTag))
                {
                    opdsStructure[parentTag] = true;
                }

                UpdateParentNodeState(parent);
            }
        }

        private void SyncStructureFromTree()
        {
            foreach (TreeNode rootNode in treeViewOPDS.Nodes)
            {
                SyncStructureFromNode(rootNode);
            }
        }

        private void SyncStructureFromNode(TreeNode node)
        {
            string tag = node.Tag?.ToString();

            if (!string.IsNullOrEmpty(tag) && tag != "root" && opdsStructure.ContainsKey(tag))
            {
                opdsStructure[tag] = node.Checked;
            }

            foreach (TreeNode childNode in node.Nodes)
            {
                SyncStructureFromNode(childNode);
            }
        }

        private void ClearDownloadsButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                    Localizer.Text("Are you sure you want to clear download history?"),
                    Localizer.Text("Confirmation"),
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    Library.ClearDownloadHistory();
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Error clearing download history: {0}", ex.Message);
                }
            }
        }

        private void ItemsPerOPDS_ValueChanged(object sender, EventArgs e)
        {
            Settings.Default.ItemsPerOPDSPage = (int)itemsPerOPDS.Value;
            Settings.Default.Save();
        }

        private void ItemsPerWeb_ValueChanged(object sender, EventArgs e)
        {
            Settings.Default.ItemsPerWebPage = (int)itemsPerWeb.Value;
            Settings.Default.Save();
        }

        private void RememberClients_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.RememberClients = rememberClients.Checked;
            Settings.Default.Save();
        }

        private void WrongAttemptsCount_ValueChanged(object sender, EventArgs e)
        {
            Settings.Default.WrongAttemptsCount = (int)wrongAttemptsCount.Value;
            Settings.Default.Save();
        }

        private void StartMinimized_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.StartMinimized = startMinimized.Checked;
            Settings.Default.Save();
        }

        private void FilterByLanguage_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.FilterBooksByInterfaceLanguage = filterByLanguage.Checked;
            Settings.Default.Save();
        }

        private void ServerName_Validated(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(serverName.Text))
            {
                Settings.Default.ServerName = serverName.Text;
                Settings.Default.Save();
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.DarkThemeOnWeb = darkTheme.Checked;
            Settings.Default.Save();
        }

        private void clearAuthorizedClients_Click(object sender, EventArgs e)
        {
            HttpProcessor.ClearAllAuthorizedClients();
        }

        private void SaveAndReload()
        {
            try
            {
                SaveOPDSSettings();
                Log.WriteLine(LogLevel.Info, "OPDS routes configuration changed, reloading server structure");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in SaveAndReload: {0}", ex.Message);
            }
        }

        #endregion
    }
}