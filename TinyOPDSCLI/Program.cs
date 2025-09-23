/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * TinyOPDS CLI main thread 
 * MODIFIED: Added async batch processing support
 *
 */

using System;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Collections.Generic;

using UPnP;
using Bluegrams.Application;

using TinyOPDS;
using TinyOPDS.Data;
using TinyOPDS.Scanner;
using TinyOPDS.Server;
using TinyOPDS.Properties;

namespace TinyOPDSCLI
{
    class Program : ServiceBase
    {
        private static readonly string exePath = Assembly.GetExecutingAssembly().Location;
        private const string SERVICE_NAME = "TinyOPDSSvc";
        private const string SERVICE_DISPLAY_NAME = "TinyOPDS service";
        private const string SERVICE_DESCRIPTION = "Simple, fast and portable OPDS service and HTTP server";
        private const string _urlTemplate = "http://{0}:{1}/{2}";

        private static OPDSServer server;
        private static Thread serverThread;
        private static FileScanner scanner;
        private static Watcher watcher;
        private static DateTime scanStartTime;
        private static readonly UPnPController upnpController = new UPnPController();
        private static Timer upnpRefreshTimer = null;

        private static readonly Mutex mutex = new Mutex(false, "tiny_opds_cli_mutex");
        private static int fb2Count, epubCount, skippedFiles, invalidFiles, dups;

        private static readonly List<Book> pendingBooks = new List<Book>();
        private static readonly object batchLock = new object();
        private static int batchSize = 1000;

        #region Startup, command line processing and service overrides

        /// <summary>
        /// Check if another instance is running on Mono/Linux
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        private static bool IsApplicationRunningOnMono(string processName)
        {
            var processFound = 0;

            Process[] monoProcesses;
            ProcessModuleCollection processModuleCollection;

            // Find all processes called 'mono', that's necessary because our app runs under the mono process
            monoProcesses = Process.GetProcessesByName("mono");

            for (var i = 0; i <= monoProcesses.GetUpperBound(0); ++i)
            {
                try
                {
                    processModuleCollection = monoProcesses[i].Modules;

                    for (var j = 0; j < processModuleCollection.Count; ++j)
                    {
                        if (processModuleCollection[j].FileName.EndsWith(processName))
                        {
                            processFound++;
                        }
                    }
                }
                catch
                {
                    // Skip processes we can't access
                }
            }

            // We don't find the current process, but if there is already another one running, return true
            return (processFound == 1);
        }

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
            // Transparent restart with Homebrew SQLite on macOS
            if (Utils.IsMacOS && Environment.GetEnvironmentVariable("TINYOPDS_SQLITE_FIXED") == null)
            {
                string homebrewSqlitePath = null;

                // Check for Homebrew SQLite
                if (Directory.Exists("/usr/local/opt/sqlite/lib")) homebrewSqlitePath = "/usr/local/opt/sqlite/lib";  // Intel Mac
                else if (Directory.Exists("/opt/homebrew/opt/sqlite/lib")) homebrewSqlitePath = "/opt/homebrew/opt/sqlite/lib";  // ARM Mac

                if (homebrewSqlitePath != null)
                {
                    // Restart silently with correct SQLite
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "mono",
                        Arguments = "\"" + Assembly.GetExecutingAssembly().Location + "\" " + string.Join(" ", args),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false
                    };

                    // Set environment variables
                    startInfo.EnvironmentVariables["DYLD_LIBRARY_PATH"] = homebrewSqlitePath;
                    startInfo.EnvironmentVariables["TINYOPDS_SQLITE_FIXED"] = "1";  // Prevent infinite loop

                    var process = Process.Start(startInfo);
                    process.WaitForExit();
                    return process.ExitCode;
                }
            }

            // Use portable settings provider
            PortableSettingsProvider.SettingsFileName = "TinyOPDS.config";
            PortableSettingsProvider.ApplyProvider(Settings.Default);

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Log.SaveToFile = Settings.Default.SaveLogToDisk;

            // Initialize embedded DLL loader FIRST
            EmbeddedDllLoader.Initialize();
            EmbeddedDllLoader.PreloadNativeDlls();
            EmbeddedDllLoader.PreloadManagedAssemblies();

            // Initialize batch size from settings
            batchSize = Settings.Default.BatchSize > 0 ? Settings.Default.BatchSize : 1000;

            // Check for single instance only if enabled in settings
            if (Settings.Default.OnlyOneInstance)
            {
                if (Utils.IsLinux || Utils.IsMacOS)
                {
                    if (IsApplicationRunningOnMono("TinyOPDSCLI.exe"))
                    {
                        Console.WriteLine("TinyOPDS CLI is already running.");
                        return -1;
                    }
                }
                else
                {
                    if (!mutex.WaitOne(TimeSpan.FromSeconds(1), false))
                    {
                        Console.WriteLine("TinyOPDS CLI is already running.");
                        return -1;
                    }
                }
            }

            try
            {
                if (Utils.IsLinux || Utils.IsMacOS || Environment.UserInteractive)
                {
                    // Add Ctrl+c handler with proper cleanup
                    Console.CancelKeyPress += async (sender, eventArgs) =>
                    {
                        eventArgs.Cancel = true;
                        StopServer();

                        if (scanner != null)
                        {
                            scanner.Stop();
                            await FlushRemainingBooksAsync();
                            Console.WriteLine("\nScanner interruped by user.");
                            Log.WriteLine("Directory scanner stopped");
                        }

                        CleanupAndExit(0);
                    };

                    // On Linux, we need clear console (terminal) window first
                    if (!Utils.IsWindows) Console.Write("\u001b[1J\u001b[0;0H");
                    Console.WriteLine("TinyOPDS Command Line Interface, {0}, copyright (c) 2013-2025 SeNSSoFT",
                        string.Format(Localizer.Text("version {0}.{1} {2}"), Utils.Version.Major, Utils.Version.Minor,
                        Utils.Version.Major == 0 ? " (beta)" : ""));

                    if (args.Length > 0)
                    {
                        switch (args[0].ToLower())
                        {
                            // Install & run service command
                            case "install":
                                {
                                    try
                                    {
                                        var installer = ServiceInstallerBase.CreateInstaller(
                                            SERVICE_NAME, SERVICE_DISPLAY_NAME, exePath, SERVICE_DESCRIPTION);

                                        installer.Install();
                                        installer.Start();
                                        Console.WriteLine("{0} installed and started", SERVICE_DISPLAY_NAME);
                                    }
                                    catch (UnauthorizedAccessException)
                                    {
                                        if (Utils.IsLinux || Utils.IsMacOS)
                                        {
                                            Console.WriteLine("Please run with sudo: sudo mono TinyOPDSCLI.exe install");
                                        }
                                        else
                                        {
                                            // Windows - try to elevate
                                            if (RunElevated("install"))
                                                Console.WriteLine("{0} installed", SERVICE_DISPLAY_NAME);
                                            else
                                                Console.WriteLine("{0} failed to install", SERVICE_DISPLAY_NAME);
                                        }
                                        CleanupAndExit(-1);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine("{0} failed to install: {1}", SERVICE_DISPLAY_NAME, e.Message);
                                        CleanupAndExit(-1);
                                    }
                                    CleanupAndExit(0);
                                }
                                break;

                            // Uninstall service command
                            case "uninstall":
                                {
                                    try
                                    {
                                        var installer = ServiceInstallerBase.CreateInstaller(
                                            SERVICE_NAME, SERVICE_DISPLAY_NAME, exePath, SERVICE_DESCRIPTION);

                                        if (!installer.IsInstalled())
                                        {
                                            Console.WriteLine("{0} is not installed", SERVICE_DISPLAY_NAME);
                                            CleanupAndExit(-1);
                                        }

                                        installer.Uninstall();
                                        Console.WriteLine("{0} uninstalled", SERVICE_DISPLAY_NAME);

                                        // On Windows, kill other service processes
                                        if (Utils.IsWindows)
                                        {
                                            Process[] localByName = Process.GetProcessesByName("TinyOPDSCLI");
                                            foreach (Process p in localByName)
                                            {
                                                // Don't kill ourselves!
                                                if (!p.StartInfo.Arguments.Contains("uninstall")) p.Kill();
                                            }
                                        }
                                    }
                                    catch (UnauthorizedAccessException)
                                    {
                                        if (!Utils.IsWindows)
                                        {
                                            Console.WriteLine("Please run with sudo: sudo mono TinyOPDSCLI.exe uninstall");
                                        }
                                        else
                                        {
                                            // Windows - try to elevate
                                            if (RunElevated("uninstall"))
                                                Console.WriteLine("{0} uninstalled", SERVICE_DISPLAY_NAME);
                                            else
                                                Console.WriteLine("{0} failed to uninstall", SERVICE_DISPLAY_NAME);
                                        }
                                        CleanupAndExit(-1);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine("{0} failed to uninstall: {1}", SERVICE_DISPLAY_NAME, e.Message);
                                        CleanupAndExit(-1);
                                    }
                                    CleanupAndExit(0);
                                }
                                break;

                            // Start service command
                            case "start":
                                {
                                    try
                                    {
                                        var installer = ServiceInstallerBase.CreateInstaller(SERVICE_NAME, SERVICE_DISPLAY_NAME, exePath, SERVICE_DESCRIPTION);

                                        if (installer.IsInstalled())
                                        {
                                            // Service is installed - start it through system service manager
                                            try
                                            {
                                                installer.Start();
                                                Console.WriteLine("{0} started", SERVICE_DISPLAY_NAME);
                                                CleanupAndExit(0);
                                            }
                                            catch (UnauthorizedAccessException)
                                            {
                                                if (!Utils.IsWindows)
                                                {
                                                    Console.WriteLine("Please run with sudo: sudo mono TinyOPDSCLI.exe start");
                                                }
                                                else
                                                {
                                                    // Windows - try to elevate
                                                    if (RunElevated("start"))
                                                        Console.WriteLine("{0} started", SERVICE_DISPLAY_NAME);
                                                    else
                                                        Console.WriteLine("{0} failed to start", SERVICE_DISPLAY_NAME);
                                                }
                                                CleanupAndExit(-1);
                                            }
                                        }
                                        else
                                        {
                                            // Service not installed - run in console mode
                                            Console.WriteLine("Service not installed, starting in console mode...");
                                            StartServer();
                                            // StartServer() has its own wait loop, will exit when done
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        // If we can't create installer or check status, run in console mode
                                        Console.WriteLine("Cannot check service status ({0}), starting in console mode...", e.Message);
                                        StartServer();
                                    }
                                }
                                break;

                            // Stop service command
                            case "stop":
                                {
                                    try
                                    {
                                        var installer = ServiceInstallerBase.CreateInstaller(
                                            SERVICE_NAME, SERVICE_DISPLAY_NAME, exePath, SERVICE_DESCRIPTION);

                                        if (!installer.IsInstalled())
                                        {
                                            Console.WriteLine("{0} is not installed", SERVICE_DISPLAY_NAME);
                                            CleanupAndExit(-1);
                                        }

                                        installer.Stop();
                                        Console.WriteLine("{0} stopped", SERVICE_DISPLAY_NAME);
                                    }
                                    catch (UnauthorizedAccessException)
                                    {
                                        if (!Utils.IsWindows)
                                        {
                                            Console.WriteLine("Please run with sudo: sudo mono TinyOPDSCLI.exe stop");
                                        }
                                        else
                                        {
                                            // Windows - try to elevate
                                            if (RunElevated("stop"))
                                                Console.WriteLine("{0} stopped", SERVICE_DISPLAY_NAME);
                                            else
                                                Console.WriteLine("{0} failed to stop", SERVICE_DISPLAY_NAME);
                                        }
                                        CleanupAndExit(-1);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine("{0} failed to stop: {1}", SERVICE_DISPLAY_NAME, e.Message);
                                        CleanupAndExit(-1);
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
                                        for (int i = 0; i < (args.Length - 1) / 2; i++)
                                            s += args[(i * 2) + 1] + ":" + args[(i * 2) + 2] + ";";
                                        Console.WriteLine(Crypt.EncryptStringAES(s, _urlTemplate));
                                    }
                                    else
                                    {
                                        Console.WriteLine("To encode credentials, please provide additional parameters: user1 password1 user2 password2 ...");
                                    }
                                    CleanupAndExit(0);
                                }
                                break;

                            default:
                                {
                                    Console.WriteLine("Unknown command: {0}", args[0]);
                                    ShowHelp();
                                    CleanupAndExit(-1);
                                }
                                break;
                        }
                    }
                    else
                    {
                        ShowHelp();
                    }
                }
                else
                {
                    // Running as Windows service (non-interactive)
                    if (Utils.IsWindows)
                    {
                        ServiceBase[] ServicesToRun;
                        ServicesToRun = new ServiceBase[] { new Program() };
                        Run(ServicesToRun);
                    }
                }

                CleanupAndExit(0);
                return 0; // This line will never be reached, but satisfies compiler
            }
            finally
            {
                // Release mutex only if we're checking for single instance and not on Linux
                if (Settings.Default.OnlyOneInstance && !Utils.IsLinux)
                {
                    try { mutex.ReleaseMutex(); } catch { }
                }
            }
        }

        /// <summary>
        /// Show help information
        /// </summary>
        private static void ShowHelp()
        {
            string prefix = !Utils.IsWindows ? "sudo mono TinyOPDSCLI.exe" : "TinyOPDSCLI.exe";

            Console.WriteLine("\nUsage: {0} [command]", prefix);
            Console.WriteLine("\nAvailable commands:");
            Console.WriteLine("\t install \t - install and start TinyOPDS service");
            Console.WriteLine("\t uninstall \t - uninstall TinyOPDS service");
            Console.WriteLine("\t start \t\t - start service (or run in console mode if not installed)");
            Console.WriteLine("\t stop \t\t - stop service");
            Console.WriteLine("\t scan \t\t - scan book directory");
            Console.WriteLine("\t encred user pwd - encode credentials");

            if (Utils.IsLinux)
            {
                Console.WriteLine("\nNote: Service commands require sudo privileges.");
                Console.WriteLine("The service will be installed as systemd or init.d service.");
            }
            else if (Utils.IsMacOS)
            {
                Console.WriteLine("\nNote: Service commands require sudo privileges.");
                Console.WriteLine("The service will be installed as launchd daemon.");
            }
            else
            {
                Console.WriteLine("\nNote: Service commands require Administrator privileges.");
            }

            Console.WriteLine("\nFor more information visit: https://github.com/sensboston/tinyopds");
        }

        /// <summary>
        /// Cleanup extracted DLLs and exit gracefully
        /// </summary>
        /// <param name="exitCode"></param>
        private static void CleanupAndExit(int exitCode = 0)
        {
            try
            {
                Task.Run(async () =>
                {
                    await FlushRemainingBooksAsync();
                    Log.WriteLine("Application cleanup completed");
                });
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error during cleanup: {0}", ex.Message);
            }

            Environment.Exit(exitCode);
        }

        private static bool RunElevated(string param)
        {
            var info = new ProcessStartInfo(exePath, param)
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

        #region Batch processing methods

        /// <summary>
        /// Add book to pending batch - console shows progress, actual writing is deferred
        /// </summary>
        /// <param name="book"></param>
        /// <returns></returns>
        private static bool AddBookToBatch(Book book)
        {
            lock (batchLock)
            {
                pendingBooks.Add(book);

                // Check if we need to flush the batch
                if (pendingBooks.Count >= batchSize)
                {
                    _ = FlushPendingBooksAsync();
                }

                return true; // For console counting purposes, always return true here
            }
        }

        /// <summary>
        /// Flush pending books to database using batch insert asynchronously
        /// </summary>
        private static async Task FlushPendingBooksAsync()
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
                // Process batch asynchronously to avoid blocking
                var batchResult = await Library.AddBatchAsync(booksToProcess);

                // Update counters based on actual results
                if (batchResult.Duplicates > 0 || batchResult.Errors > 0)
                {
                    // Adjust counters based on actual batch results
                    int booksToSubtract = batchResult.Duplicates + batchResult.Errors;

                    // Add duplicates to duplicate counter
                    dups += batchResult.Duplicates;

                    // Subtract duplicates and errors from type-specific counters
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
        /// Flush any remaining pending books asynchronously (call at scan completion)
        /// </summary>
        private static async Task FlushRemainingBooksAsync()
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

                Log.WriteLine("SQLite database successfully initialized");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error initializing SQLite: {0}", ex.Message);
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
            // Check if running as service and set the flag
            if (Environment.GetEnvironmentVariable("TINYOPDS_SERVICE") == "1")
            {
                Log.IsRunningAsService = true;
                Log.WriteLine("Running as system service");
            }

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
            upnpRefreshTimer = new Timer(UpdateUPnPForwarding, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            // Create file watcher
            watcher = new Watcher(Library.LibraryPath);
            watcher.OnBookAdded += (object sender, BookAddedEventArgs e) =>
            {
                if (e.BookType == BookType.FB2) fb2Count++; else epubCount++;
                Log.WriteLine(LogLevel.Info, "Added: \"{0}\"", e.BookPath);
            };
            watcher.OnInvalidBook += (_, __) =>
            {
                invalidFiles++;
            };
            watcher.OnFileSkipped += (object _sender, FileSkippedEventArgs _e) =>
            {
                skippedFiles = _e.Count;
            };

            watcher.OnBookDeleted += (object sender, BookDeletedEventArgs e) =>
            {
                Log.WriteLine(LogLevel.Info, "Deleted: \"{0}\"", e.BookPath);
            };
            watcher.IsEnabled = Settings.Default.WatchLibrary;

            upnpController.DiscoverCompleted += UpnpController_DiscoverCompleted;
            upnpController.DiscoverAsync(Settings.Default.UseUPnP);

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
            HttpProcessor.AuthorizedClients = new List<string>();
            HttpProcessor.BannedClients.Clear();
            server = new OPDSServer(IPAddress.Any, int.Parse(Settings.Default.ServerPort));

            serverThread = new Thread(new ThreadStart(server.Listen))
            {
                Priority = ThreadPriority.BelowNormal
            };
            serverThread.Start();
            server.ServerReady.WaitOne(TimeSpan.FromMilliseconds(500));
            if (!server.IsActive)
            {
                if (server.ServerException != null)
                {
                    if (server.ServerException is System.Net.Sockets.SocketException)
                    {
                        string msg = string.Format("Probably, port {0} is already in use. Please try the different port.", Settings.Default.ServerPort);
                        Console.WriteLine(msg);
                        Log.WriteLine(msg);
                    }
                    else
                    {
                        Console.WriteLine(server.ServerException.Message);
                        Log.WriteLine(LogLevel.Error, server.ServerException.Message);
                    }

                    StopServer();
                }
            }
            else
            {
                Log.WriteLine("HTTP server started");
                if (Utils.IsLinux || Utils.IsMacOS || Environment.UserInteractive)
                {
                    Console.WriteLine("Server is running... Press <Ctrl+c> to shutdown server.");
                    while (server != null && server.IsActive) Thread.Sleep(500);
                }
            }
        }

        /// <summary>
        /// Stop OPDS server
        /// </summary>
        private static void StopServer()
        {
            upnpRefreshTimer?.Dispose();

            if (server != null)
            {
                server.StopServer();
                serverThread = null;
                server = null;
                Log.WriteLine("HTTP server stopped");
            }

            if (watcher != null)
            {
                watcher.IsEnabled = false;
                watcher = null;
            }

            if (upnpController != null)
            {
                if (upnpController.UPnPReady)
                {
                    int port = int.Parse(Settings.Default.ServerPort);
                    upnpController.DeleteForwardingRule(port, System.Net.Sockets.ProtocolType.Tcp);
                    Log.WriteLine("Port {0} closed", port);
                }
                upnpController.DiscoverCompleted -= UpnpController_DiscoverCompleted;
                upnpController.Dispose();
            }
        }

        private static void UpnpController_DiscoverCompleted(object sender, EventArgs e)
        {
            if (upnpController != null && upnpController.UPnPReady)
            {
                if (Settings.Default.OpenNATPort)
                {
                    int port = int.Parse(Settings.Default.ServerPort);
                    upnpController.ForwardPort(port, System.Net.Sockets.ProtocolType.Tcp, "TinyOPDS server");
                    Log.WriteLine("Port {0} forwarded by UPnP", port);
                }
            }
        }

        private static void UpdateUPnPForwarding(Object state)
        {
            if (Settings.Default.UseUPnP)
            {
                if (server != null && server.IsActive && server.IsIdle && upnpController != null && upnpController.UPnPReady)
                {
                    if (!upnpController.Discovered)
                    {
                        upnpController.DiscoverAsync(true);
                    }
                    else if (Settings.Default.OpenNATPort && upnpController.UPnPReady)
                    {
                        int port = int.Parse(Settings.Default.ServerPort);
                        upnpController.ForwardPort(port, System.Net.Sockets.ProtocolType.Tcp, "TinyOPDS server");
                    }
                }
            }
        }

        private static async void ScanFolder()
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

            scanner = new FileScanner();
            scanner.OnBookFound += Scanner_OnBookFound;
            scanner.OnInvalidBook += (_, __) => { invalidFiles++; };
            scanner.OnFileSkipped += (object _sender, FileSkippedEventArgs _e) =>
            {
                skippedFiles = _e.Count;
                UpdateInfo();
            };
            scanner.OnScanCompleted += async (_, __) =>
            {
                // Flush any remaining books at scan completion
                await FlushRemainingBooksAsync();
                UpdateInfo();

                Log.WriteLine("Directory scanner completed");
                Console.WriteLine("\nScan completed. Press any key to exit...");
                Console.ReadKey();
            };
            fb2Count = epubCount = skippedFiles = invalidFiles = dups = 0;
            scanStartTime = DateTime.Now;
            scanner.Start(Library.LibraryPath);
            Console.WriteLine("Scanning directory {0}", Library.LibraryPath);
            Log.WriteLine("Directory scanner started with batch size: {0}", batchSize);
            UpdateInfo();
            while (scanner != null && scanner.Status == FileScannerStatus.SCANNING) Thread.Sleep(500);

            // Final flush and save at the end
            await FlushRemainingBooksAsync();
        }

        /// <summary>
        /// Optimized book found handler with batching and proper duplicate counting
        /// </summary>
        static void Scanner_OnBookFound(object sender, BookFoundEventArgs e)
        {
            // Add book to batch and count it for console display
            // The actual duplicate detection happens in FlushPendingBooks()
            AddBookToBatch(e.Book);

            // Count for console display - duplicates will be corrected during flush
            if (e.Book.BookType == BookType.FB2)
                fb2Count++;
            else
                epubCount++;

            var totalProcessed = fb2Count + epubCount + dups;

            // Force flush every 1000 books to prevent "freezing"
            if (totalProcessed % 1000 == 0)
            {
                _ = FlushPendingBooksAsync(); // Async flush every 1000 books
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

        private static void UpdateInfo()
        {
            try
            {
                int totalProcessed = fb2Count + epubCount + skippedFiles + invalidFiles + dups;

                // Always show info - no additional checks
                var (TotalBooks, FB2, EPUB) = GetLibraryStats();
                TimeSpan dt = DateTime.Now.Subtract(scanStartTime);
                string rate = (dt.TotalSeconds) > 0 ? string.Format("{0:0.} books/min", totalProcessed / dt.TotalSeconds * 60) : "---";

                string info = string.Format("Elapsed: {0}, rate: {1}, found fb2: {2}, epub: {3}, skipped: {4}, dups: {5}, invalid: {6}, total: {7}, in DB: {8}     ",
                    dt.ToString(@"hh\:mm\:ss"), rate, FB2, EPUB, skippedFiles, dups, invalidFiles, totalProcessed, TotalBooks);

                if (Utils.IsWindows)
                {
                    Console.Write(info + "\r");
                    Console.SetCursorPosition(0, Console.CursorTop - info.Length / Console.WindowWidth);
                }
                else if (Utils.IsLinux)
                {
                    // For Linux keep the working version with CSI sequences
                    Console.Write("\u001b[s" + info + "\u001b[u");
                }
                else if (Utils.IsMacOS)
                {
                    // For macOS - use DEC private sequences ESC 7/8 instead of CSI s/u
                    Console.Write("\u001b7" + info + "\u001b8");
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in UpdateInfo: {0}", ex.Message);
                Console.WriteLine("Books processed: {0}", fb2Count + epubCount + dups);
            }
        }

        #endregion
    }
}