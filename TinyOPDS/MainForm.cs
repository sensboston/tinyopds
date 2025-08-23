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
using System.Collections.Concurrent;

using TinyOPDS.Data;
using TinyOPDS.Scanner;
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

        #region Batch processing for performance
        private List<Book> _pendingBooks = new List<Book>();
        private readonly object _batchLock = new object();
        #endregion

        private Dictionary<string, bool> _opdsStructure;
        private bool _isLoading = false;

        private const string urlTemplate = "http://{0}:{1}/{2}";

        #region Initialization and startup

        public MainForm()
        {
            Log.SaveToFile = Properties.Settings.Default.SaveLogToDisk;

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += currentDomain_UnhandledException;

            Log.WriteLine("TinyOPDS version {0}.{1} started", Utils.Version.Major, Utils.Version.Minor);

            InitializeComponent();

            // Fix TabControl height for high DPI displays
            using (Graphics g = CreateGraphics())
            {
                float dpiScale = g.DpiX / 96f;
                if (dpiScale > 1.0f)
                {
                    tabControl1.SizeMode = TabSizeMode.Normal;
                    tabControl1.ItemSize = new Size(Math.Max(120, (int)(120 * dpiScale)), Math.Max(28, (int)(28 * dpiScale)));
                }
            }

            // Assign combo data source to the list of all available interfaces
            interfaceCombo.DataSource = UPnPController.LocalInterfaces;
            interfaceCombo.DataBindings.Add(new Binding("SelectedIndex", Properties.Settings.Default, "LocalInterfaceIndex", false, DataSourceUpdateMode.OnPropertyChanged));

            logVerbosity.DataBindings.Add(new Binding("SelectedIndex", Properties.Settings.Default, "LogLevel", false, DataSourceUpdateMode.OnPropertyChanged));
            updateCombo.DataBindings.Add(new Binding("SelectedIndex", Properties.Settings.Default, "UpdatesCheck", false, DataSourceUpdateMode.OnPropertyChanged));

            this.PerformLayout();

            // Manually assign icons from resources (fix for Mono)
            this.Icon = Properties.Resources.trayIcon;
            _notifyIcon.ContextMenuStrip = contextMenuStrip;
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

            Library.LibraryPath = Properties.Settings.Default.LibraryPath.SanitizePathName();

            // Initialize SQLite database with automatic migration
            InitializeSQLiteDatabase();

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

            intLink.Text = string.Format(urlTemplate, _upnpController.LocalIP.ToString(), Properties.Settings.Default.ServerPort, rootPrefix.Text);
            intWebLink.Text = string.Format(urlTemplate, _upnpController.LocalIP.ToString(), Properties.Settings.Default.ServerPort, webPrefix.Text);

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
            _notifyIcon.Visible = Properties.Settings.Default.CloseToTray;

            InitializeOPDSStructure();
            LoadOPDSSettings();
            BuildOPDSTree();
        }

        /// <summary>
        /// We should call DiscoverAsync when windows handle already created (invoker used)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            _upnpController.DiscoverCompleted += _upnpController_DiscoverCompleted;
            _upnpController.DiscoverAsync(Properties.Settings.Default.UseUPnP);

            // Update UI after form is fully loaded
            UpdateInfo(); // Show correct book counts
            databaseFileName.Text = "books.sqlite";
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
                BeginInvoke((MethodInvoker)delegate
                {
                    extLink.Text = string.Format(urlTemplate, _upnpController.ExternalIP.ToString(), Properties.Settings.Default.ServerPort, Properties.Settings.Default.RootPrefix);
                    extWebLink.Text = string.Format(urlTemplate, _upnpController.ExternalIP.ToString(), Properties.Settings.Default.ServerPort, Properties.Settings.Default.HttpPrefix);
                    if (_upnpController.UPnPReady)
                    {
                        openPort.Enabled = true;
                        if (Properties.Settings.Default.OpenNATPort) openPort_CheckedChanged(this, null);
                    }
                });
            }
        }

        #endregion

        #region SQLite initialization

        /// <summary>
        /// Initialize SQLite database
        /// </summary>
        private void InitializeSQLiteDatabase()
        {
            try
            {
                // Determine database paths
                string libraryPath = Properties.Settings.Default.LibraryPath.SanitizePathName();
                string binaryDbPath = GetBinaryDatabasePath(libraryPath);
                string sqliteDbPath = GetSQLiteDatabasePath();

                Log.WriteLine("Initializing SQLite database...");
                Log.WriteLine("Library path: {0}", libraryPath);
                Log.WriteLine("Binary database: {0}", binaryDbPath);
                Log.WriteLine("SQLite database: {0}", sqliteDbPath);

                // Initialize SQLite
                Library.LibraryPath = libraryPath;
                Library.Initialize(sqliteDbPath);

                Log.WriteLine("✓ SQLite database successfully initialized");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "✗ Error initializing SQLite: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Get binary database path (same logic as original)
        /// </summary>
        /// <param name="libraryPath"></param>
        /// <returns></returns>
        private string GetBinaryDatabasePath(string libraryPath)
        {
            string dbFileName = Utils.CreateGuid(Utils.IsoOidNamespace, libraryPath).ToString() + ".db";
            return Path.Combine(Utils.ServiceFilesLocation, dbFileName);
        }

        /// <summary>
        /// Get SQLite database path
        /// </summary>
        /// <returns></returns>
        private string GetSQLiteDatabasePath()
        {
            return Path.Combine(Utils.ServiceFilesLocation, "books.sqlite");
        }

        #endregion

        #region Batch processing methods

        /// <summary>
        /// Add book to pending batch - UI shows progress, actual writing is deferred
        /// </summary>
        /// <param name="book"></param>
        /// <returns></returns>
        private bool AddBookToBatch(Book book)
        {
            lock (_batchLock)
            {
                _pendingBooks.Add(book);

                // Check if we need to flush the batch
                int batchSize = Math.Max(1, Properties.Settings.Default.BatchSize);
                if (_pendingBooks.Count >= batchSize)
                {
                    FlushPendingBooks();
                }

                return true; // For UI counting purposes
            }
        }

        /// <summary>
        /// Flush pending books to database using batch insert
        /// </summary>
        private void FlushPendingBooks()
        {
            List<Book> booksToProcess;

            lock (_batchLock)
            {
                if (_pendingBooks.Count == 0) return;

                booksToProcess = new List<Book>(_pendingBooks);
                _pendingBooks.Clear();
            }

            try
            {
                var addedCount = Library.AddBatch(booksToProcess);
                Log.WriteLine("Flushed {0} books to database ({1} actually added)", booksToProcess.Count, addedCount);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error flushing books batch: {0}", ex.Message);

                // Fallback: try to add books individually
                foreach (var book in booksToProcess)
                {
                    try
                    {
                        Library.Add(book);
                    }
                    catch (Exception ex2)
                    {
                        Log.WriteLine(LogLevel.Error, "Error adding book {0}: {1}", book.FileName, ex2.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Flush any remaining pending books (call at scan completion)
        /// </summary>
        private void FlushRemainingBooks()
        {
            lock (_batchLock)
            {
                if (_pendingBooks.Count > 0)
                {
                    Log.WriteLine("Flushing {0} remaining books at scan completion", _pendingBooks.Count);
                    FlushPendingBooks();
                }
            }
        }

        /// <summary>
        /// Wrapper for Library.Add with batch support - used by individual book operations
        /// </summary>
        /// <param name="book"></param>
        /// <returns></returns>
        private bool AddBook(Book book)
        {
            // For individual book additions (not during scan), add directly
            return Library.Add(book);
        }

        /// <summary>
        /// Wrapper for Library.Save with SQLite support
        /// </summary>
        private void SaveLibrary()
        {
            // First flush any pending books
            FlushRemainingBooks();

            // Then call the library save (no-op for SQLite, but maintains API compatibility)
            Library.Save();
        }

        /// <summary>
        /// Get library statistics with SQLite support
        /// </summary>
        /// <returns></returns>
        private (int Total, int FB2, int EPUB) GetLibraryStats()
        {
            return (Library.Count, Library.FB2Count, Library.EPUBCount);
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
            libraryPath.Text = Properties.Settings.Default.LibraryPath;
            if (!string.IsNullOrEmpty(Properties.Settings.Default.LibraryPath))
            {
                databaseFileName.Text = "books.sqlite";
            }

            if (Utils.IsLinux) startWithWindows.Enabled = false;
            if (string.IsNullOrEmpty(Properties.Settings.Default.ConvertorPath))
            {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramFiles")))
                {
                    if (File.Exists(Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), "FB2ePub\\Fb2ePub.exe")))
                    {
                        convertorPath.Text = Properties.Settings.Default.ConvertorPath = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), "FB2ePub");
                    }
                }
            }
            else convertorPath.Text = Properties.Settings.Default.ConvertorPath;
            converterLinkLabel.Visible = string.IsNullOrEmpty(convertorPath.Text);

            // We should update all invisible controls
            interfaceCombo.SelectedIndex = Math.Min(UPnPController.LocalInterfaces.Count - 1, Properties.Settings.Default.LocalInterfaceIndex);
            logVerbosity.SelectedIndex = Math.Min(2, Properties.Settings.Default.LogLevel);
            updateCombo.SelectedIndex = Math.Min(2, Properties.Settings.Default.UpdatesCheck);
            langCombo.SelectedValue = Properties.Settings.Default.Language;
            sortOrderCombo.SelectedIndex = Properties.Settings.Default.SortOrder;
            newBooksPeriodCombo.SelectedIndex = Properties.Settings.Default.NewBooksPeriod;

            openPort.Checked = Properties.Settings.Default.UseUPnP ? Properties.Settings.Default.OpenNATPort : false;
            banClients.Enabled = rememberClients.Enabled = dataGridView1.Enabled = Properties.Settings.Default.UseHTTPAuth;
            wrongAttemptsCount.Enabled = banClients.Checked && useHTTPAuth.Checked;

            _notifyIcon.Visible = Properties.Settings.Default.CloseToTray;
            _updateChecker.Start();

            // Ensure BatchSize has a reasonable default if not set
            if (Properties.Settings.Default.BatchSize <= 0)
            {
                Properties.Settings.Default.BatchSize = 500;
                Properties.Settings.Default.Save();
            }

            // Load saved credentials
            try
            {
                HttpProcessor.Credentials.Clear();
                if (!string.IsNullOrEmpty(Properties.Settings.Default.Credentials))
                {
                    string decryptedCredentials = Crypt.DecryptStringAES(Properties.Settings.Default.Credentials, urlTemplate);
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
                Properties.Settings.Default.Credentials = string.Empty;
                Properties.Settings.Default.Save();
            }
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.LibraryPath = libraryPath.Text.SanitizePathName();
            Properties.Settings.Default.Language = langCombo.SelectedValue as string;
            Properties.Settings.Default.Save();
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
                Properties.Settings.Default.Credentials = string.IsNullOrEmpty(s) ? string.Empty : Crypt.EncryptStringAES(s, urlTemplate);
            }
            finally
            {
                Properties.Settings.Default.Save();
            }
        }

        #endregion

        #region Library scanning support

        private void libraryPath_TextChanged(object sender, EventArgs e)
        {
            databaseFileName.Text = "books.sqlite";
        }

        private void libraryPath_Validated(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(databaseFileName.Text) && !Library.LibraryPath.Equals(databaseFileName.Text.SanitizePathName()) &&
                Directory.Exists(libraryPath.Text.SanitizePathName()))
            {
                if (Library.IsChanged) Library.Save();
                Library.LibraryPath = Properties.Settings.Default.LibraryPath = libraryPath.Text.SanitizePathName();

                var stats = GetLibraryStats();
                booksInDB.Text = string.Format("{0}       fb2: {1}      epub: {2}", 0, 0, 0);

                databaseFileName.Text = "books.sqlite";

                _watcher.IsEnabled = false;

                // Reload library
                Library.Load();
            }
            else
            {
                if (!string.IsNullOrEmpty(Properties.Settings.Default.LibraryPath)) libraryPath.Text = Properties.Settings.Default.LibraryPath.SanitizePathName();
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
                    // Flush any remaining books at scan completion
                    FlushRemainingBooks();
                    SaveLibrary();
                    UpdateInfo(true);

                    Log.WriteLine("Directory scanner completed");
                };
                _fb2Count = _epubCount = _skippedFiles = _invalidFiles = _duplicates = 0;
                _scanStartTime = DateTime.Now;
                startTime.Text = _scanStartTime.ToString(@"hh\:mm\:ss");
                _scanner.Start(libraryPath.Text.SanitizePathName());
                scannerButton.Text = Localizer.Text("Stop scanning");

                Log.WriteLine("Directory scanner started with batch size: {0}", Properties.Settings.Default.BatchSize);
            }
            else
            {
                _scanner.Stop();
                // Flush any remaining books when stopping
                FlushRemainingBooks();
                SaveLibrary();
                UpdateInfo(true);
                scannerButton.Text = Localizer.Text("Start scanning");

                Log.WriteLine("Directory scanner stopped");
            }
        }

        void scanner_OnBookFound(object sender, BookFoundEventArgs e)
        {
            // Add book to batch instead of directly to library
            if (AddBookToBatch(e.Book))
            {
                if (e.Book.BookType == BookType.FB2) _fb2Count++; else _epubCount++;
            }
            else _duplicates++;

            // Update UI every 20 books for responsiveness
            var totalProcessed = _fb2Count + _epubCount + _duplicates;
            if (totalProcessed % 20 == 0)
            {
                UpdateInfo();
            }

            // Force GC every 20,000 books to manage memory
            if (totalProcessed % 20000 == 0)
            {
                GC.Collect();
            }
        }

        private void UpdateInfo(bool IsScanFinished = false)
        {
            if (this.InvokeRequired) { this.BeginInvoke((MethodInvoker)delegate { internalUpdateInfo(IsScanFinished); }); }
            else { internalUpdateInfo(IsScanFinished); }
        }

        private void internalUpdateInfo(bool IsScanFinished)
        {
            var stats = GetLibraryStats();
            booksInDB.Text = string.Format("{0}       fb2: {1}      epub: {2}", stats.Total, stats.FB2, stats.EPUB);
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
            HttpProcessor.AuthorizedClients = new ConcurrentBag<string>();
            HttpProcessor.BannedClients.Clear();
            _server = new OPDSServer(IPAddress.Any, int.Parse(Properties.Settings.Default.ServerPort));


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
                        MessageBox.Show(string.Format(Localizer.Text("Probably, port {0} is already in use. Please try different port value."), Properties.Settings.Default.ServerPort), Localizer.Text("Warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                int port = int.Parse(Properties.Settings.Default.ServerPort);
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
                intLink.Text = string.Format(urlTemplate, _upnpController.LocalIP.ToString(), Properties.Settings.Default.ServerPort, rootPrefix.Text);
                intWebLink.Text = string.Format(urlTemplate, _upnpController.LocalIP.ToString(), Properties.Settings.Default.ServerPort, webPrefix.Text);

                if (Properties.Settings.Default.UseUPnP && openPort.Checked)
                {
                    int port = int.Parse(Properties.Settings.Default.ServerPort);
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
            if (_server != null && _server.IsActive)
            {
                _server.StopServer();
                _serverThread = null;
                _server = null;

                if (_upnpController != null)
                {
                    if (Properties.Settings.Default.UseUPnP)
                    {
                        int port = int.Parse(Properties.Settings.Default.ServerPort);
                        _upnpController.DeleteForwardingRule(port, System.Net.Sockets.ProtocolType.Tcp);
                    }
                    _upnpController.DiscoverCompleted -= _upnpController_DiscoverCompleted;
                    _upnpController.Dispose();
                }
            }

            if (_scanner.Status == FileScannerStatus.SCANNING) _scanner.Stop();

            // Save library using appropriate method
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
                Properties.Settings.Default.ConvertorPath = convertorPath.Text;
            }
            else
            {
                convertorPath.Text = Properties.Settings.Default.ConvertorPath;
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
                    intLink.Text = string.Format(urlTemplate, _upnpController.LocalIP.ToString(), Properties.Settings.Default.ServerPort, rootPrefix.Text);
                    intWebLink.Text = string.Format(urlTemplate, _upnpController.LocalIP.ToString(), Properties.Settings.Default.ServerPort, webPrefix.Text);
                }
                if (_upnpController.ExternalIP != null)
                {
                    extLink.Text = string.Format(urlTemplate, _upnpController.ExternalIP.ToString(), Properties.Settings.Default.ServerPort, rootPrefix.Text);
                    extWebLink.Text = string.Format(urlTemplate, _upnpController.ExternalIP.ToString(), Properties.Settings.Default.ServerPort, webPrefix.Text);
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
                    Properties.Settings.Default.ServerPort = port.ToString();
                    openPort.Checked = true;
                }
                else Properties.Settings.Default.ServerPort = port.ToString();
                if (_server != null && _server.IsActive)
                {
                    RestartHttpServer();
                }
            }
            else
            {
                MessageBox.Show(Localizer.Text("Invalid port value: value must be numeric and in range from 1 to 65535"), Localizer.Text("Warning"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                serverPort.Text = Properties.Settings.Default.ServerPort.ToString();
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
            appVersion.Text = string.Format(Localizer.Text("version {0}.{1} {2}"), Utils.Version.Major, Utils.Version.Minor, Utils.Version.Major == 0 ? " (beta)" : "");
            scannerButton.Text = Localizer.Text((_scanner.Status == FileScannerStatus.STOPPED) ? "Start scanning" : "Stop scanning");
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
            Properties.Settings.Default.UpdatesCheck = updateCombo.SelectedIndex;
        }

        private void viewLogFile_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Log.LogFileName);
        }

        private void sortOrderCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.SortOrder = sortOrderCombo.SelectedIndex;
        }

        private void newBooksPeriodCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.NewBooksPeriod = newBooksPeriodCombo.SelectedIndex;
        }

        #endregion

        #region TinyOPDS updates checker

        /// <summary>
        /// This timer event should be raised every hour
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static int[] checkIntervals = new int[] { 0, 60 * 24 * 7, 60 * 24 * 30, 1 };
        static int _timerCallsCount = 0;
        void _updateChecker_Tick(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.UpdatesCheck > 0)
            {
                _updateUrl = string.Empty;
                int minutesFromLastCheck = (int)Math.Round(DateTime.Now.Subtract(Properties.Settings.Default.LastCheck).TotalMinutes);
                if (minutesFromLastCheck >= checkIntervals[Properties.Settings.Default.UpdatesCheck])
                {
                    Log.WriteLine(LogLevel.Info, "Checking software update. Minutes from the last check: {0}", minutesFromLastCheck);
                    WebClient wc = new WebClient();
                    wc.DownloadStringCompleted += wc_DownloadStringCompleted;
                    wc.DownloadStringAsync(new Uri("http://senssoft.com/tinyopds.txt"));
                }
            }

            if (Properties.Settings.Default.UseUPnP && _timerCallsCount++ > 5)
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
                        int port = int.Parse(Properties.Settings.Default.ServerPort);
                        _upnpController.ForwardPort(port, System.Net.Sockets.ProtocolType.Tcp, "TinyOPDS server");
                    }
                }
            }
        }

        void wc_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                Properties.Settings.Default.LastCheck = DateTime.Now;
                Properties.Settings.Default.Save();

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
            if ((sender as CheckBox).Name.Equals("checkBox2") && (sender as CheckBox).Checked != Properties.Settings.Default.UseAuthorsAliases)
            {
                // Reload library
                _watcher.IsEnabled = false;
                Library.Load();
            }
        }

        #region OPDS routes structure 

        private void InitializeOPDSStructure()
        {
            // Default OPDS routes - all enabled
            _opdsStructure = new Dictionary<string, bool>
            {
                {"newdate", true},
                {"newtitle", true},
                {"authorsindex", true},
                {"author-details", true},
                {"author-series", true},
                {"author-no-series", true},
                {"author-alphabetic", true},
                {"author-by-date", true},
                {"sequencesindex", true},
                {"genres", true}
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
            return Properties.Settings.Default.OPDSStructure ??
                   "newdate:1;newtitle:1;authorsindex:1;author-details:1;author-series:1;author-no-series:1;author-alphabetic:1;author-by-date:1;sequencesindex:1;genres:1";
        }

        private void SaveOPDSStructureToSettings(string structure)
        {
            try
            {
                Properties.Settings.Default.OPDSStructure = structure;
                Properties.Settings.Default.Save();
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
                if (keyValue.Length == 2 && _opdsStructure.ContainsKey(keyValue[0]))
                {
                    _opdsStructure[keyValue[0]] = keyValue[1] == "1";
                }
            }
        }

        private string SerializeOPDSStructure()
        {
            return string.Join(";", _opdsStructure.Select(kvp => $"{kvp.Key}:{(kvp.Value ? "1" : "0")}"));
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
            _isLoading = true;
            treeViewOPDS.Nodes.Clear();

            // Root node (not bold)
            TreeNode rootNode = new TreeNode("Root")
            {
                Tag = "root",
                Checked = true
            };

            // New Books section (not bold, but acts as section for children)
            TreeNode newBooksNode = CreateTreeNode("New Books", "newbooks-section",
                _opdsStructure["newdate"] || _opdsStructure["newtitle"]);
            newBooksNode.Nodes.Add(CreateTreeNode("New Books (by date)", "newdate", _opdsStructure["newdate"]));
            newBooksNode.Nodes.Add(CreateTreeNode("New Books (alphabetically)", "newtitle", _opdsStructure["newtitle"]));
            rootNode.Nodes.Add(newBooksNode);

            // Authors section
            TreeNode authorsNode = CreateTreeNode("By Authors", "authorsindex", _opdsStructure["authorsindex"]);

            TreeNode authorDetailsNode = CreateTreeNode("Author's books", "author-details", _opdsStructure["author-details"]);
            authorDetailsNode.Nodes.Add(CreateTreeNode("Books by Series", "author-series", _opdsStructure["author-series"]));
            authorDetailsNode.Nodes.Add(CreateTreeNode("Books without Series", "author-no-series", _opdsStructure["author-no-series"]));
            authorDetailsNode.Nodes.Add(CreateTreeNode("Books Alphabetically", "author-alphabetic", _opdsStructure["author-alphabetic"]));
            authorDetailsNode.Nodes.Add(CreateTreeNode("Books by Date", "author-by-date", _opdsStructure["author-by-date"]));

            authorsNode.Nodes.Add(authorDetailsNode);
            rootNode.Nodes.Add(authorsNode);

            // Series section
            rootNode.Nodes.Add(CreateTreeNode("By Series", "sequencesindex", _opdsStructure["sequencesindex"]));

            // Genres section
            rootNode.Nodes.Add(CreateTreeNode("By Genres", "genres", _opdsStructure["genres"]));

            treeViewOPDS.Nodes.Add(rootNode);
            treeViewOPDS.ExpandAll();

            UpdateTreeNodeStyles();
            _isLoading = false;
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
            // No bold fonts - all nodes use regular font
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

            // Recursively update child nodes
            foreach (TreeNode childNode in node.Nodes)
            {
                UpdateNodeStyle(childNode);
            }
        }

        private void treeViewOPDS_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_isLoading) return;

            TreeNode node = e.Node;
            string tag = node.Tag?.ToString();

            Log.WriteLine(LogLevel.Info, "OPDS route changed: {0} = {1}", tag, node.Checked);

            // Handle section nodes
            if (tag == "newbooks-section")
            {
                HandleNewBooksSectionChange(node.Checked);
                SaveAndReload();
                return;
            }

            // Skip root node
            if (tag == "root")
            {
                return;
            }

            // Update the structure for regular nodes
            if (_opdsStructure.ContainsKey(tag))
            {
                _opdsStructure[tag] = node.Checked;

                // Handle special dependencies
                HandleNodeDependencies(tag, node.Checked);

                // Save and reload immediately
                SaveAndReload();
            }
        }

        private void HandleNewBooksSectionChange(bool isChecked)
        {
            _isLoading = true;

            // Update both child routes
            _opdsStructure["newdate"] = isChecked;
            _opdsStructure["newtitle"] = isChecked;

            // Update tree nodes visually
            UpdateTreeNodeCheckedState("newdate", isChecked);
            UpdateTreeNodeCheckedState("newtitle", isChecked);

            _isLoading = false;

            // Update visual styles
            UpdateTreeNodeStyles();
        }

        private void HandleNodeDependencies(string tag, bool isChecked)
        {
            _isLoading = true;

            try
            {
                // If New Books routes are changed, update section
                if (tag == "newdate" || tag == "newtitle")
                {
                    bool anyNewBooksEnabled = _opdsStructure["newdate"] || _opdsStructure["newtitle"];
                    UpdateTreeNodeCheckedState("newbooks-section", anyNewBooksEnabled);
                }

                // If authorsindex is disabled, disable all author sub-options
                if (tag == "authorsindex" && !isChecked)
                {
                    _opdsStructure["author-details"] = false;
                    _opdsStructure["author-series"] = false;
                    _opdsStructure["author-no-series"] = false;
                    _opdsStructure["author-alphabetic"] = false;
                    _opdsStructure["author-by-date"] = false;

                    UpdateTreeNodeCheckedState("author-details", false);
                    UpdateTreeNodeCheckedState("author-series", false);
                    UpdateTreeNodeCheckedState("author-no-series", false);
                    UpdateTreeNodeCheckedState("author-alphabetic", false);
                    UpdateTreeNodeCheckedState("author-by-date", false);
                }

                // If author-details is disabled, disable all its sub-options except alphabetic
                if (tag == "author-details" && !isChecked)
                {
                    _opdsStructure["author-series"] = false;
                    _opdsStructure["author-no-series"] = false;
                    _opdsStructure["author-by-date"] = false;
                    // Keep author-alphabetic enabled as fallback
                    _opdsStructure["author-alphabetic"] = true;

                    UpdateTreeNodeCheckedState("author-series", false);
                    UpdateTreeNodeCheckedState("author-no-series", false);
                    UpdateTreeNodeCheckedState("author-by-date", false);
                    UpdateTreeNodeCheckedState("author-alphabetic", true);
                }

                // Update visual styles after all changes
                UpdateTreeNodeStyles();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void UpdateTreeNodeCheckedState(string tag, bool isChecked)
        {
            TreeNode foundNode = FindNodeByTag(treeViewOPDS.Nodes[0], tag);
            if (foundNode != null)
            {
                bool wasLoading = _isLoading;
                _isLoading = true; // Prevent recursive calls
                foundNode.Checked = isChecked;
                _isLoading = wasLoading;

                // Force TreeView to refresh the node
                treeViewOPDS.Invalidate();
            }
        }

        private TreeNode FindNodeByTag(TreeNode parentNode, string tag)
        {
            if (parentNode.Tag?.ToString() == tag)
                return parentNode;

            foreach (TreeNode childNode in parentNode.Nodes)
            {
                TreeNode result = FindNodeByTag(childNode, tag);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void SaveAndReload()
        {
            try
            {
                // Save settings
                SaveOPDSSettings();

                // Force OPDS server to reload routes immediately
                Log.WriteLine(LogLevel.Info, "OPDS routes configuration changed, reloading server structure");

                // The server will reload structure on next request automatically
                // due to LoadOPDSStructure() call in HandleOPDSRequest()
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in SaveAndReload: {0}", ex.Message);
            }
        }

        #endregion
    }
}