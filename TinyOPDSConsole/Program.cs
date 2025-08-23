/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * TinyOPDS CLI main thread 
 *
 */

using System;
using System.ServiceProcess;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Collections.Concurrent;
using System.Collections.Generic;

using TinyOPDS;
using TinyOPDS.Data;
using TinyOPDS.Scanner;
using TinyOPDS.Server;
using TinyOPDS.Properties;
using UPnP;
using Bluegrams.Application;

namespace TinyOPDSConsole
{
    class Program : ServiceBase
    {
        private static readonly string _exePath = Assembly.GetExecutingAssembly().Location;
        private const string SERVICE_NAME = "TinyOPDSSvc";
        private const string SERVICE_DISPLAY_NAME = "TinyOPDS service";
        private const string SERVICE_DESCRIPTION = "Simple, fast and portable OPDS service and HTTP server";
        private const string _urlTemplate = "http://{0}:{1}/{2}";

        private static OPDSServer _server;
        private static Thread _serverThread;
        private static FileScanner _scanner;
        private static Watcher _watcher;
        private static DateTime _scanStartTime;
        private static readonly UPnPController _upnpController = new UPnPController();
        private static Timer _upnpRefreshTimer = null;

        #region Statistical information
        private static int _fb2Count, _epubCount, _skippedFiles, _invalidFiles, _duplicates;
        #endregion

        #region Batch processing for CLI performance
        private static readonly List<Book> _pendingBooks = new List<Book>();
        private static readonly object _batchLock = new object();
        private static int _batchSize = 500;
        #endregion

        #region Startup, command line processing and service overrides

        /// <summary>
        /// Process unhandled exceptions
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs args)
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

        static int Main(string[] args)
        {
            // Initialize embedded DLL loader FIRST
            EmbeddedDllLoader.Initialize();
            EmbeddedDllLoader.PreloadNativeDlls();

            // Use portable settings provider
            PortableSettingsProvider.SettingsFileName = "TinyOPDS.config";
            PortableSettingsProvider.ApplyProvider(Settings.Default);

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Log.SaveToFile = Settings.Default.SaveLogToDisk;

            // Initialize batch size from settings
            _batchSize = Settings.Default.BatchSize > 0 ? Settings.Default.BatchSize : 500;

            if (Utils.IsLinux || Environment.UserInteractive)
            {
                // Add Ctrl+c handler with proper cleanup
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    StopServer();

                    if (_scanner != null)
                    {
                        _scanner.Stop();
                        FlushRemainingBooks();
                        SaveLibrary();
                        Console.WriteLine("\nScanner interruped by user.");
                        Log.WriteLine("Directory scanner stopped");
                    }

                    CleanupAndExit(0);
                };

                // On Linux, we need clear console (terminal) window first
                if (Utils.IsLinux) Console.Write("\u001b[1J\u001b[0;0H");
                Console.WriteLine("TinyOPDS console, {0}, copyright (c) 2013 SeNSSoFT", string.Format(Localizer.Text("version {0}.{1} {2}"), Utils.Version.Major, Utils.Version.Minor, Utils.Version.Major == 0 ? " (beta)" : ""));

                if (args.Length > 0)
                {
                    switch (args[0].ToLower())
                    {
                        // Install & run service command
                        case "install":
                            {
                                if (Utils.IsElevated)
                                {
                                    try
                                    {
                                        TinyOPDS.ServiceInstaller.InstallAndStart(SERVICE_NAME, SERVICE_DISPLAY_NAME, _exePath, SERVICE_DESCRIPTION);
                                        Console.WriteLine(SERVICE_DISPLAY_NAME + " installed");
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(SERVICE_DISPLAY_NAME + " failed to install with exception: \"{0}\"", e.Message);
                                        CleanupAndExit(-1);
                                    }
                                }
                                else
                                {
                                    // Re-run app with elevated privileges 
                                    if (RunElevated("install")) Console.WriteLine(SERVICE_DISPLAY_NAME + " installed");
                                    else Console.WriteLine(SERVICE_DISPLAY_NAME + " failed to install");
                                }
                                CleanupAndExit(0);
                            }
                            break;

                        // Uninstall service command
                        case "uninstall":
                            {
                                if (!TinyOPDS.ServiceInstaller.ServiceIsInstalled(SERVICE_NAME))
                                {
                                    Console.WriteLine(SERVICE_DISPLAY_NAME + " is not installed");
                                    CleanupAndExit(-1);
                                }

                                if (Utils.IsElevated)
                                {
                                    try
                                    {
                                        TinyOPDS.ServiceInstaller.Uninstall(SERVICE_NAME);
                                        Console.WriteLine(SERVICE_DISPLAY_NAME + " uninstalled");

                                        // Let's close service process (except ourselves)
                                        Process[] localByName = Process.GetProcessesByName("TinyOPDSConsole");
                                        foreach (Process p in localByName)
                                        {
                                            // Don't kill ourselves!
                                            if (!p.StartInfo.Arguments.Contains("uninstall")) p.Kill();
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(SERVICE_DISPLAY_NAME + " failed to uninstall with exception: \"{0}\"", e.Message);
                                        CleanupAndExit(-1);
                                    }
                                }
                                else
                                {
                                    // Re-run app with elevated privileges 
                                    if (RunElevated("uninstall")) Console.WriteLine(SERVICE_DISPLAY_NAME + " uninstalled");
                                    else Console.WriteLine(SERVICE_DISPLAY_NAME + " failed to uninstall");
                                }
                                CleanupAndExit(0);
                            }
                            break;

                        // Start service command
                        case "start":
                            {
                                if (!Utils.IsLinux && TinyOPDS.ServiceInstaller.ServiceIsInstalled(SERVICE_NAME))
                                {
                                    if (Utils.IsElevated)
                                    {
                                        try
                                        {
                                            TinyOPDS.ServiceInstaller.StartService(SERVICE_NAME);
                                            Console.WriteLine(SERVICE_DISPLAY_NAME + " started");
                                        }
                                        catch (Exception e)
                                        {
                                            Console.WriteLine(SERVICE_DISPLAY_NAME + " failed to start with exception: \"{0}\"", e.Message);
                                            CleanupAndExit(-1);
                                        }
                                    }
                                    else
                                    {
                                        // Re-run app with elevated privileges 
                                        if (RunElevated("start")) Console.WriteLine(SERVICE_DISPLAY_NAME + " started");
                                        else Console.WriteLine(SERVICE_DISPLAY_NAME + " failed to start");
                                    }
                                }
                                else StartServer();
                                CleanupAndExit(0);
                            }
                            break;

                        // Stop service command
                        case "stop":
                            {
                                if (!TinyOPDS.ServiceInstaller.ServiceIsInstalled(SERVICE_NAME))
                                {
                                    Console.WriteLine(SERVICE_DISPLAY_NAME + " is not installed");
                                    CleanupAndExit(-1);
                                }

                                if (Utils.IsElevated)
                                {
                                    try
                                    {
                                        TinyOPDS.ServiceInstaller.StopService(SERVICE_NAME);
                                        Console.WriteLine(SERVICE_DISPLAY_NAME + " stopped");
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(SERVICE_DISPLAY_NAME + " failed to stop with exception: \"{0}\"", e.Message);
                                        CleanupAndExit(-1);
                                    }
                                }
                                else
                                {
                                    // Re-run app with elevated privileges 
                                    if (RunElevated("stop")) Console.WriteLine(SERVICE_DISPLAY_NAME + " stopped");
                                    else Console.WriteLine(SERVICE_DISPLAY_NAME + " failed to stop");
                                }
                                CleanupAndExit(0);
                            }
                            break;

                        case "scan":
                            {
                                ScanFolder();
                                CleanupAndExit(0);
                            }
                            break;

                        case "encred":
                            {
                                if ((args.Length - 1) % 2 == 0)
                                {
                                    string s = string.Empty;
                                    for (int i = 0; i < (args.Length - 1) / 2; i++) s += args[(i * 2) + 1] + ":" + args[(i * 2) + 2] + ";";
                                    Console.WriteLine(Crypt.EncryptStringAES(s, _urlTemplate));
                                }
                                else
                                {
                                    Console.WriteLine("To encode credentials, please provide additional parameters: user1 password1 user2 password2 ...");
                                }
                                CleanupAndExit(0);
                            }
                            break;

                        case "encpath":
                            {
                                if (args.Length > 1)
                                {
                                    string libName = Utils.CreateGuid(Utils.IsoOidNamespace, args[1].SanitizePathName()).ToString() + ".db";
                                    Console.WriteLine("Library name for the path \"{0}\" is: {1}", args[1], libName);
                                }
                                else
                                {
                                    Console.WriteLine("Please provide a path to the library files (without closing slash), for example: TinyOPDSConsole.exe \"C:\\My Documents\\My ebooks\"");
                                }
                                CleanupAndExit(0);
                            }
                            break;
                    }
                }

                bool l = Utils.IsLinux;
                Console.WriteLine("Use: TinyOPDSConsole.exe [command], where [command] is \n\n" +
                              (l ? "" : "\t install \t - install and run TinyOPDS service\n") +
                              (l ? "" : "\t uninstall \t - uninstall TinyOPDS service\n") +
                                        "\t start \t\t - start service\n" +
                              (l ? "" : "\t stop \t\t - stop service\n") +
                                        "\t scan \t\t - scan book directory\n" +
                                        "\t encred usr pwd\t - encode credentials\n" +
                                        "\t encpath path\t - get library file name from path\n\n" +
                                        "For more info please visit https://tinyopds.codeplex.com");
            }
            else
            {
                if (!Utils.IsLinux)
                {
                    ServiceBase[] ServicesToRun;
                    ServicesToRun = new ServiceBase[] { new Program() };
                    ServiceBase.Run(ServicesToRun);
                }
            }

            CleanupAndExit(0);
            return 0; // This line will never be reached, but satisfies compiler
        }

        /// <summary>
        /// Cleanup extracted DLLs and exit gracefully
        /// </summary>
        /// <param name="exitCode"></param>
        private static void CleanupAndExit(int exitCode = 0)
        {
            try
            {
                // Flush any remaining books before exit
                FlushRemainingBooks();
                SaveLibrary();
                Log.WriteLine("Application cleanup completed");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error during cleanup: {0}", ex.Message);
            }
        }

        private static bool RunElevated(string param)
        {
            var info = new ProcessStartInfo(_exePath, param)
            {
                Verb = "runas", // indicates to elevate privileges
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            var process = new Process
            {
                EnableRaisingEvents = true, // enable WaitForExit()
                StartInfo = info
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }

        protected override void OnStart(string[] args)
        {
            StartServer();
        }

        protected override void OnStop()
        {
            StopServer();
        }

        #endregion

        #region Batch processing methods (same as MainForm.cs)

        /// <summary>
        /// Add book to pending batch - console shows progress, actual writing is deferred
        /// </summary>
        /// <param name="book"></param>
        /// <returns></returns>
        private static bool AddBookToBatch(Book book)
        {
            lock (_batchLock)
            {
                _pendingBooks.Add(book);

                // Check if we need to flush the batch
                if (_pendingBooks.Count >= _batchSize)
                {
                    FlushPendingBooks();
                }

                return true; // For console counting purposes
            }
        }

        /// <summary>
        /// Flush pending books to database using batch insert
        /// </summary>
        private static void FlushPendingBooks()
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
        private static void FlushRemainingBooks()
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
        /// Wrapper for Library.Save with SQLite support
        /// </summary>
        private static void SaveLibrary()
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
        private static (int Total, int FB2, int EPUB) GetLibraryStats()
        {
            return (Library.Count, Library.FB2Count, Library.EPUBCount);
        }

        #endregion

        #region OPDS server routines

        /// <summary>
        /// Initialize SQLite database (same as in MainForm)
        /// </summary>
        private static void InitializeSQLiteDatabase()
        {
            try
            {
                string libraryPath = Settings.Default.LibraryPath.SanitizePathName();
                string sqliteDbPath = GetSQLiteDatabasePath();

                Log.WriteLine("Initializing SQLite database...");
                Log.WriteLine("Library path: {0}", libraryPath);
                Log.WriteLine("SQLite database: {0}", sqliteDbPath);

                // Initialize SQLite
                Library.LibraryPath = libraryPath;
                Library.Initialize(sqliteDbPath);

                Log.WriteLine("✓ SQLite database successfully initialized");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "✗ Error initializing SQLite: {0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Get SQLite database path
        /// </summary>
        /// <returns></returns>
        private static string GetSQLiteDatabasePath()
        {
            return Path.Combine(Utils.ServiceFilesLocation, "books.sqlite");
        }

        /// <summary>
        /// Start OPDS server
        /// </summary>
        private static void StartServer()
        {
            // Init log file settings
            Log.SaveToFile = Settings.Default.SaveLogToDisk;

            // Init localization service
            Localizer.Init();
            Localizer.Language = Settings.Default.Language;

            // Initialize SQLite database before starting server
            InitializeSQLiteDatabase();

            // Load library
            Library.Load();

            // Create timer for periodical refresh UPnP forwarding
            _upnpRefreshTimer = new Timer(UpdateUPnPForwarding, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            // Create file watcher
            _watcher = new Watcher(Library.LibraryPath);
            _watcher.OnBookAdded += (object sender, BookAddedEventArgs e) =>
            {
                if (e.BookType == BookType.FB2) _fb2Count++; else _epubCount++;
                Log.WriteLine(LogLevel.Info, "Added: \"{0}\"", e.BookPath);
            };
            _watcher.OnInvalidBook += (_, __) =>
            {
                _invalidFiles++;
            };
            _watcher.OnFileSkipped += (object _sender, FileSkippedEventArgs _e) =>
            {
                _skippedFiles = _e.Count;
            };

            _watcher.OnBookDeleted += (object sender, BookDeletedEventArgs e) =>
            {
                Log.WriteLine(LogLevel.Info, "Deleted: \"{0}\"", e.BookPath);
            };
            _watcher.IsEnabled = Settings.Default.WatchLibrary;

            _upnpController.DiscoverCompleted += UpnpController_DiscoverCompleted;
            _upnpController.DiscoverAsync(Settings.Default.UseUPnP);

            // Load saved credentials
            try
            {
                HttpProcessor.Credentials.Clear();
                if (!string.IsNullOrEmpty(Settings.Default.Credentials))
                {
                    string decryptedCredentials = Crypt.DecryptStringAES(Settings.Default.Credentials, _urlTemplate);
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
            }

            // Create and start HTTP server
            HttpProcessor.AuthorizedClients = new ConcurrentBag<string>();
            HttpProcessor.BannedClients.Clear();
            _server = new OPDSServer(IPAddress.Any, int.Parse(Settings.Default.ServerPort));

            _serverThread = new Thread(new ThreadStart(_server.Listen))
            {
                Priority = ThreadPriority.BelowNormal
            };
            _serverThread.Start();
            _server.ServerReady.WaitOne(TimeSpan.FromMilliseconds(500));
            if (!_server.IsActive)
            {
                if (_server.ServerException != null)
                {
                    if (_server.ServerException is System.Net.Sockets.SocketException)
                    {
                        string msg = string.Format("Probably, port {0} is already in use. Please try the different port.", Settings.Default.ServerPort);
                        Console.WriteLine(msg);
                        Log.WriteLine(msg);
                    }
                    else
                    {
                        Console.WriteLine(_server.ServerException.Message);
                        Log.WriteLine(LogLevel.Error, _server.ServerException.Message);
                    }

                    StopServer();
                }
            }
            else
            {
                Log.WriteLine("HTTP server started");
                if (Utils.IsLinux || Environment.UserInteractive)
                {
                    Console.WriteLine("Server is running... Press <Ctrl+c> to shutdown server.");
                    while (_server != null && _server.IsActive) Thread.Sleep(500);
                }
            }
        }

        /// <summary>
        /// Stop OPDS server
        /// </summary>
        private static void StopServer()
        {
            _upnpRefreshTimer?.Dispose();

            if (_server != null)
            {
                _server.StopServer();
                _serverThread = null;
                _server = null;
                Log.WriteLine("HTTP server stopped");
            }

            if (_watcher != null)
            {
                _watcher.IsEnabled = false;
                _watcher = null;
            }

            if (_upnpController != null)
            {
                if (_upnpController.UPnPReady)
                {
                    int port = int.Parse(Settings.Default.ServerPort);
                    _upnpController.DeleteForwardingRule(port, System.Net.Sockets.ProtocolType.Tcp);
                    Log.WriteLine("Port {0} closed", port);
                }
                _upnpController.DiscoverCompleted -= UpnpController_DiscoverCompleted;
                _upnpController.Dispose();
            }
        }

        private static void UpnpController_DiscoverCompleted(object sender, EventArgs e)
        {
            if (_upnpController != null && _upnpController.UPnPReady)
            {
                if (Settings.Default.OpenNATPort)
                {
                    int port = int.Parse(Settings.Default.ServerPort);
                    _upnpController.ForwardPort(port, System.Net.Sockets.ProtocolType.Tcp, "TinyOPDS server");
                    Log.WriteLine("Port {0} forwarded by UPnP", port);
                }
            }
        }

        private static void UpdateUPnPForwarding(Object state)
        {
            if (Settings.Default.UseUPnP)
            {
                if (_server != null && _server.IsActive && _server.IsIdle && _upnpController != null && _upnpController.UPnPReady)
                {
                    if (!_upnpController.Discovered)
                    {
                        _upnpController.DiscoverAsync(true);
                    }
                    else if (Settings.Default.OpenNATPort && _upnpController.UPnPReady)
                    {
                        int port = int.Parse(Settings.Default.ServerPort);
                        _upnpController.ForwardPort(port, System.Net.Sockets.ProtocolType.Tcp, "TinyOPDS server");
                    }
                }
            }
        }

        private static void ScanFolder()
        {
            // Init log file settings
            Log.SaveToFile = Settings.Default.SaveLogToDisk;

            // Init localization service
            Localizer.Init();
            Localizer.Language = Settings.Default.Language;

            // Initialize SQLite database before scanning
            InitializeSQLiteDatabase();

            // Load existing library
            Library.Load();

            _scanner = new FileScanner();
            _scanner.OnBookFound += Scanner_OnBookFound;
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
                Console.WriteLine("\nScan completed. Press any key to exit...");
                Console.ReadKey();
            };
            _fb2Count = _epubCount = _skippedFiles = _invalidFiles = _duplicates = 0;
            _scanStartTime = DateTime.Now;
            _scanner.Start(Library.LibraryPath);
            Console.WriteLine("Scanning directory {0}", Library.LibraryPath);
            Log.WriteLine("Directory scanner started with batch size: {0}", _batchSize);
            UpdateInfo();
            while (_scanner != null && _scanner.Status == FileScannerStatus.SCANNING) Thread.Sleep(500);

            // Final flush and save at the end
            FlushRemainingBooks();
            SaveLibrary();
        }

        /// <summary>
        /// Optimized book found handler with batching and regular flushes
        /// </summary>
        static void Scanner_OnBookFound(object sender, BookFoundEventArgs e)
        {
            // Add book to batch instead of directly to library
            if (AddBookToBatch(e.Book))
            {
                if (e.Book.BookType == BookType.FB2) _fb2Count++; else _epubCount++;
            }
            else _duplicates++;

            var totalProcessed = _fb2Count + _epubCount + _duplicates;

            // Force flush every 1000 books to prevent "freezing"
            if (totalProcessed % 1000 == 0)
            {
                FlushPendingBooks(); // Forced flush every 1000 books
                Log.WriteLine("Forced flush at {0} books processed", totalProcessed);
            }

            // Update console every 10 books for better responsiveness
            if (totalProcessed % 10 == 0)
            {
                UpdateInfo();
            }

            // Force GC every 20,000 books to manage memory
            if (totalProcessed % 20000 == 0)
            {
                GC.Collect();
            }
        }

        private static void UpdateInfo(bool IsScanFinished = false)
        {
            try
            {
                int totalBooksProcessed = _fb2Count + _epubCount + _skippedFiles + _invalidFiles + _duplicates;

                // Always show info - no additional checks
                var stats = GetLibraryStats();
                TimeSpan dt = DateTime.Now.Subtract(_scanStartTime);
                string rate = (dt.TotalSeconds) > 0 ? string.Format("{0:0.} books/min", totalBooksProcessed / dt.TotalSeconds * 60) : "---";

                string info = string.Format("Elapsed: {0}, rate: {1}, found fb2: {2}, epub: {3}, skipped: {4}, dups: {5}, invalid: {6}, total: {7}, in DB: {8}     ",
                    dt.ToString(@"hh\:mm\:ss"),
                    rate,
                    _fb2Count,
                    _epubCount,
                    _skippedFiles,
                    _duplicates,
                    _invalidFiles,
                    totalBooksProcessed,
                    stats.Total);

                if (!Utils.IsLinux)
                {
                    Console.Write(info + "\r");
                    Console.SetCursorPosition(0, Console.CursorTop - info.Length / Console.WindowWidth);
                }
                else
                {
                    // For Linux we use ANSI escape sequences to control cursor
                    Console.Write("\u001b[s" + info + "\u001b[u");
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in UpdateInfo: {0}", ex.Message);
                Console.WriteLine("Books processed: {0}", _fb2Count + _epubCount + _duplicates);
            }
        }

        #endregion
    }
}