﻿/*
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

            // Windows - use DPI awareness
            AutoScaleMode = Utils.IsLinux ? AutoScaleMode.Font : AutoScaleMode.Dpi;
            float dpiScale = 1.0f;

            // Fix TabControl height for high DPI displays
            using (Graphics g = CreateGraphics())
            {
                dpiScale = g.DpiX / 96f;
                if (dpiScale > 1.0f)
                {
                    tabControl1.SizeMode = TabSizeMode.Normal;
                    tabControl1.ItemSize = new Size(Math.Max(120, (int)(120 * dpiScale)), Math.Max(28, (int)(28 * dpiScale)));
                }
            }

            // Fix TreeeView
            treeViewOPDS.ShowLines = true;
            treeViewOPDS.Indent = (int)(treeViewOPDS.Indent * dpiScale * 2);
            treeViewOPDS.ItemHeight = (int)(21 * dpiScale);
            if (Utils.IsLinux) treeViewOPDS.Font = new Font("DejaVu Sans", 22, FontStyle.Regular);

            // Assign combo data source to the list of all available interfaces
            interfaceCombo.DataSource = UPnPController.LocalInterfaces;
            interfaceCombo.DataBindings.Add(new Binding("SelectedIndex", Settings.Default, "LocalInterfaceIndex", false, DataSourceUpdateMode.OnPropertyChanged));

            logVerbosity.DataBindings.Add(new Binding("SelectedIndex", Settings.Default, "LogLevel", false, DataSourceUpdateMode.OnPropertyChanged));
            updateCombo.DataBindings.Add(new Binding("SelectedIndex", Settings.Default, "UpdatesCheck", false, DataSourceUpdateMode.OnPropertyChanged));

            PerformLayout();

            // Manually assign icons from resources (fix for Mono)
            Icon = Resources.trayIcon;
            notifyIcon.ContextMenuStrip = contextMenuStrip;
            notifyIcon.Icon = Resources.trayIcon;
            notifyIcon.MouseClick += NotifyIcon1_MouseClick;
            notifyIcon.BalloonTipClicked += NotifyIcon_BalloonTipClicked;
            notifyIcon.BalloonTipClosed += NotifyIcon_BalloonTipClosed;

            // Init localization service
            Localizer.Init();
            Localizer.AddMenu(contextMenuStrip);
            langCombo.SelectedValueChanged -= new EventHandler(LangCombo_SelectedValueChanged);
            langCombo.DataSource = Localizer.Languages.ToArray();
            langCombo.SelectedValueChanged += new EventHandler(LangCombo_SelectedValueChanged);

            // Load application settings
            LoadSettings();

            // Initialize update checker
            updateCheckerTimer.Interval = 1000 * 60; // Check every minute
            updateCheckerTimer.Tick += UpdateChecker_Tick;
            updateChecker.CheckCompleted += UpdateChecker_CheckCompleted;
            // Check for updates on startup if enabled
            if (Settings.Default.UpdatesCheck > 0) updateChecker.CheckAsync();

            // Setup credentials grid
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

            // Initialize SQLite database with automatic migration
            InitializeSQLiteDatabase();

            Library.LibraryLoaded += (_, __) =>
            {
                UpdateInfo();
                watcher.DirectoryToWatch = Library.LibraryPath;
                watcher.IsEnabled = Settings.Default.WatchLibrary;
            };

            // Create file watcher
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

            // Start OPDS server
            StartHttpServer();

            // Set server statistics handler
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

            // Build OPDS structure tree
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
            upnpController.DiscoverCompleted += UpnpController_DiscoverCompleted;
            upnpController.DiscoverAsync(Settings.Default.UseUPnP);

            // Update UI after form is fully loaded
            UpdateInfo(); // Show correct book counts
            databaseFileName.Text = "books.sqlite";

            // Ensure icon is visible if starting minimized
            if (Settings.Default.StartMinimized && Settings.Default.CloseToTray)
            {
                WindowState = FormWindowState.Minimized;
                Visible = false;
                notifyIcon.Visible = true; // Force icon to be visible
            }
        }

        /// <summary>
        /// Process unhandled exceptions
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// Initialize SQLite database
        /// </summary>
        private void InitializeSQLiteDatabase()
        {
            try
            {
                // Determine database paths
                string libraryPath = Settings.Default.LibraryPath.SanitizePathName();
                string sqliteDbPath = GetSQLiteDatabasePath();

                Log.WriteLine("Initializing SQLite database...");
                Log.WriteLine("Library path: {0}", libraryPath);
                Log.WriteLine("SQLite database: {0}", sqliteDbPath);

                // Initialize SQLite
                Library.LibraryPath = libraryPath;
                Library.Initialize(sqliteDbPath);

                Log.WriteLine("SQLite database successfully initialized");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error initializing SQLite: {0}", ex.Message);
            }
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
            lock (batchLock)
            {
                pendingBooks.Add(book);

                // Check if we need to flush the batch
                int batchSize = Math.Max(1, Settings.Default.BatchSize);
                if (pendingBooks.Count >= batchSize)
                {
                    _ = FlushPendingBooksAsync();
                }

                return true; // For UI counting purposes, always return true here
            }
        }

        /// <summary>
        /// Flush pending books to database using batch insert asynchronously
        /// </summary>
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
                // Process batch asynchronously to avoid blocking UI thread
                var batchResult = await Library.AddBatchAsync(booksToProcess);

                // Update counters based on actual results
                // Note: We subtract what we already counted and add the real results
                if (batchResult.Duplicates > 0 || batchResult.Errors > 0)
                {
                    // Adjust counters based on actual batch results
                    // We need to subtract books that were counted as found but are actually duplicates/errors
                    int booksToSubtract = batchResult.Duplicates + batchResult.Errors;

                    // Add duplicates to duplicate counter
                    dups += batchResult.Duplicates;

                    // Subtract duplicates and errors from type-specific counters
                    // This is approximate since we don't know which specific books were duplicates
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

                // Fallback: try to add books individually
                foreach (var book in booksToProcess)
                {
                    try
                    {
                        if (!Library.Add(book))
                        {
                            dups++;
                            // Adjust counters for the duplicate
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

        /// <summary>
        /// Flush any remaining pending books (call at scan completion)
        /// </summary>
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
                    return; // Nothing to flush
                }
            }

            // Wait for the last batch to complete
            await FlushPendingBooksAsync();

            TimeSpan dt = DateTime.Now.Subtract(scanStartTime);
            Log.WriteLine($"Scan completed, elapsed time {dt:hh\\:mm\\:ss}");
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
            linkLabel6.Links.Add(0, linkLabel6.Text.Length, "https://github.com/rsarov/SQLiteFTS5NET");

            // Setup settings controls
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

            if (Utils.IsLinux) startWithWindows.Enabled = false;

            // We should update all invisible controls
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

            // Ensure BatchSize has a reasonable default if not set
            if (Settings.Default.BatchSize <= 0)
            {
                Settings.Default.BatchSize = 500;
                Settings.Default.Save();
            }

            // Setup image cache controls
            radioButton1.Checked = Settings.Default.CacheImagesInMemory;
            radioButton2.Checked = !radioButton1.Checked;
            comboBox1.Text = $"{Settings.Default.MaxRAMImageCacheSizeMB} MB";
            comboBox1.Enabled = radioButton1.Checked;

            // Load saved credentials
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

                // Reload library
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


                // Optional pre-scan database cleaning
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
                // Flush any remaining books when stopping
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
            // Add book to batch and count it for UI display
            // The actual duplicate detection happens in FlushPendingBooks()
            AddBookToBatch(e.Book);

            // Count for UI display - duplicates will be corrected during flush
            if (e.Book.BookType == BookType.FB2) fb2Count++;
            else epubCount++;

            // Update UI every 20 books for responsiveness
            var totalProcessed = fb2Count + epubCount + dups;
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
            // Create and start HTTP server
            HttpProcessor.AuthorizedClients = new List<string>();
            HttpProcessor.BannedClients.Clear();
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
            // Re-detect IP addresses using UPnP
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
                    notifyIcon.Visible = true; // Explicitly ensure icon is visible
                    windowMenuItem.Text = Localizer.Text("Show window");
                }
                else if (WindowState == FormWindowState.Normal)
                {
                    Visible = true;
                    notifyIcon.Visible = true; // Keep icon visible when form is normal
                    windowMenuItem.Text = Localizer.Text("Hide window");
                }
            }
        }

        private void WindowMenuItem_Click(object sender, EventArgs e)
        {
            if (!ShowInTaskbar)
            {
                ShowInTaskbar = true;
                notifyIcon.Visible = Settings.Default.CloseToTray; // Ensure icon stays if needed
            }
            else
            {
                Visible = !Visible;
                if (!Visible && Settings.Default.CloseToTray)
                {
                    notifyIcon.Visible = true; // Force icon visible when hiding form
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

            // Cancel update check if in progress
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
            }
        }

        private void CloseToTray_CheckedChanged(object sender, EventArgs e)
        {
            notifyIcon.Visible = closeToTray.Checked;

            // If unchecking and form is hidden, show it
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

        /// <summary>
        /// Handle server's root prefix change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RootPrefix_TextChanged(object sender, EventArgs e)
        {
            if (sender is TextBox && (sender as TextBox).CanUndo)
            {
                if (rootPrefix.Text.EndsWith("/")) rootPrefix.Text = rootPrefix.Text.Remove(rootPrefix.Text.Length - 1);
                UpdateServerLinks();
            }
        }

        /// <summary>
        /// Validate server port
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
            // Update link labels
            UpdateServerLinks();
        }

        /// <summary>
        /// Set UI language
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

            // Update dataGridView1 columns
            if (dataGridView1.Columns.Count >= 2)
            {
                dataGridView1.Columns[0].HeaderText = Localizer.Text("User");
                dataGridView1.Columns[1].HeaderText = Localizer.Text("Password");
            }
            // Rebuild OPDS tree with new translations
            BuildOPDSTree();
        }

        /// <summary>
        /// Handle PayPal donation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
                    // Copy to temp with same name for consistency
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

        /// <summary>
        /// Timer event raised every minute
        /// </summary>
        void UpdateChecker_Tick(object sender, EventArgs e)
        {
            // Check for updates if enabled and not already checking
            if (Settings.Default.UpdatesCheck > 0 && !updateChecker.IsChecking)
            {
                if (UpdateChecker.ShouldCheckForUpdates(Settings.Default.UpdatesCheck,
                                                        Settings.Default.LastCheck))
                {
                    updateChecker.CheckAsync();
                }
            }
        }

        /// <summary>
        /// Handle update check completion
        /// </summary>
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

        /// <summary>
        /// Handle balloon tip click - open GitHub releases page
        /// </summary>
        void NotifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(updateUrl))
            {
                System.Diagnostics.Process.Start(updateUrl);
            }
        }

        /// <summary>
        /// Handle balloon tip close
        /// </summary>
        void NotifyIcon_BalloonTipClosed(object sender, EventArgs e)
        {
            // Keep icon visible if CloseToTray is enabled OR if form is minimized/hidden
            notifyIcon.Visible = Settings.Default.CloseToTray ||
                                WindowState == FormWindowState.Minimized ||
                                !Visible;
        }

        #endregion

        #region OPDS routes structure 

        private void InitializeOPDSStructure()
        {
            // Default OPDS routes - all enabled
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
                {"downloads", true}
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
                   "newdate:0;newtitle:0;authorsindex:1;author-details:1;author-series:1;author-no-series:1;author-alphabetic:1;author-by-date:1;sequencesindex:1;genres:1;downloads:1";
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

                // Root node
                TreeNode rootNode = new TreeNode(Localizer.Text("Root"))
                {
                    Tag = "root",
                    Checked = true
                };

                // New Books section
                TreeNode newBooksNode = CreateTreeNode(Localizer.Text("New Books"), "newbooks-section", opdsStructure["newdate"] || opdsStructure["newtitle"]);
                newBooksNode.Nodes.Add(CreateTreeNode(Localizer.Text("New Books (by date)"), "newdate", opdsStructure["newdate"]));
                newBooksNode.Nodes.Add(CreateTreeNode(Localizer.Text("New Books (alphabetically)"), "newtitle", opdsStructure["newtitle"]));
                rootNode.Nodes.Add(newBooksNode);

                // Authors section
                TreeNode authorsNode = CreateTreeNode(Localizer.Text("By Authors"), "authorsindex", opdsStructure["authorsindex"]);

                TreeNode authorDetailsNode = CreateTreeNode(Localizer.Text("Author's books"), "author-details", opdsStructure["author-details"]);
                authorDetailsNode.Nodes.Add(CreateTreeNode(Localizer.Text("Books by Series"), "author-series", opdsStructure["author-series"]));
                authorDetailsNode.Nodes.Add(CreateTreeNode(Localizer.Text("Books without Series"), "author-no-series", opdsStructure["author-no-series"]));
                authorDetailsNode.Nodes.Add(CreateTreeNode(Localizer.Text("Books Alphabetically"), "author-alphabetic", opdsStructure["author-alphabetic"]));
                authorDetailsNode.Nodes.Add(CreateTreeNode(Localizer.Text("Books by Date"), "author-by-date", opdsStructure["author-by-date"]));

                authorsNode.Nodes.Add(authorDetailsNode);
                rootNode.Nodes.Add(authorsNode);

                // Series section
                rootNode.Nodes.Add(CreateTreeNode(Localizer.Text("By Series"), "sequencesindex", opdsStructure["sequencesindex"]));

                // Genres section
                rootNode.Nodes.Add(CreateTreeNode(Localizer.Text("By Genres"), "genres", opdsStructure["genres"]));

                // Downloads section
                rootNode.Nodes.Add(CreateTreeNode(Localizer.Text("Downloaded books"), "downloads", opdsStructure["downloads"]));

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
            if (opdsStructure.ContainsKey(tag))
            {
                opdsStructure[tag] = node.Checked;

                // Handle special dependencies
                HandleNodeDependencies(tag, node.Checked);

                // Save and reload immediately
                SaveAndReload();
            }
        }

        private void HandleNewBooksSectionChange(bool isChecked)
        {
            isLoading = true;

            // Update both child routes
            opdsStructure["newdate"] = isChecked;
            opdsStructure["newtitle"] = isChecked;

            // Update tree nodes visually
            UpdateTreeNodeCheckedState("newdate", isChecked);
            UpdateTreeNodeCheckedState("newtitle", isChecked);

            isLoading = false;

            // Update visual styles
            UpdateTreeNodeStyles();
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

        private void HandleNodeDependencies(string tag, bool isChecked)
        {
            isLoading = true;

            try
            {
                // If New Books routes are changed, update section
                if (tag == "newdate" || tag == "newtitle")
                {
                    bool anyNewBooksEnabled = opdsStructure["newdate"] || opdsStructure["newtitle"];
                    UpdateTreeNodeCheckedState("newbooks-section", anyNewBooksEnabled);
                }

                // If authorsindex is disabled, disable all author sub-options
                if (tag == "authorsindex" && !isChecked)
                {
                    opdsStructure["author-details"] = false;
                    opdsStructure["author-series"] = false;
                    opdsStructure["author-no-series"] = false;
                    opdsStructure["author-alphabetic"] = false;
                    opdsStructure["author-by-date"] = false;

                    UpdateTreeNodeCheckedState("author-details", false);
                    UpdateTreeNodeCheckedState("author-series", false);
                    UpdateTreeNodeCheckedState("author-no-series", false);
                    UpdateTreeNodeCheckedState("author-alphabetic", false);
                    UpdateTreeNodeCheckedState("author-by-date", false);
                }

                // If author-details is disabled, disable all its sub-options except alphabetic
                if (tag == "author-details" && !isChecked)
                {
                    opdsStructure["author-series"] = false;
                    opdsStructure["author-no-series"] = false;
                    opdsStructure["author-by-date"] = false;
                    // Keep author-alphabetic enabled as fallback
                    opdsStructure["author-alphabetic"] = true;

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
                isLoading = false;
            }
        }

        private void UpdateTreeNodeCheckedState(string tag, bool isChecked)
        {
            TreeNode foundNode = FindNodeByTag(treeViewOPDS.Nodes[0], tag);
            if (foundNode != null)
            {
                bool wasLoading = isLoading;
                isLoading = true; // Prevent recursive calls
                foundNode.Checked = isChecked;
                isLoading = wasLoading;

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