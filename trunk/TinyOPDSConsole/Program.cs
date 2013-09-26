/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This is a console server/service application
 * 
 ************************************************************/

using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text;
#if !MONO
using System.ServiceProcess;
#endif
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;

using TinyOPDS;
using TinyOPDS.Data;
using TinyOPDS.Scanner;
using TinyOPDS.OPDS;
using TinyOPDS.Server;
using UPnP;

namespace TinyOPDSConsole
{
#if MONO
    public class Program
#else
    public class Program : ServiceBase
#endif
    {
        private static readonly string _exePath = Assembly.GetExecutingAssembly().Location;
        private const string SERVICE_NAME = "TinyOPDSSvc";
        private const string SERVICE_DESC = "TinyOPDS service";
        private const string _urlTemplate = "http://{0}:{1}/{2}";

        private static OPDSServer _server;
        private static Thread _serverThread;
        private static FileScanner _scanner = new FileScanner();
        private static Watcher _watcher;
        private static DateTime _scanStartTime;
        private static UPnPController _upnpController = new UPnPController();

        #region Statistical information
        private static int _fb2Count, _epubCount, _skippedFiles, _invalidFiles, _duplicates;
        #endregion

        #region Startup, command line processing and service overrides

        /// <summary>
        /// Extra assembly loader
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            String resourceName = asm.GetName().Name + ".Libs." + new AssemblyName(args.Name).Name + ".dll.gz";
            using (var stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    using (MemoryStream memStream = new MemoryStream())
                    {
                        GZipStream decompress = new GZipStream(stream, CompressionMode.Decompress);
                        decompress.CopyTo(memStream);
                        return Assembly.Load(memStream.GetBuffer());
                    }
                }
                else return null;
            }
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
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            if (Utils.IsLinux || System.Environment.UserInteractive)
            {
                Console.WriteLine("TinyOPDS console, {0}, copyright (c) 2013 SeNSSoFT", string.Format(Localizer.Text("version {0}.{1} {2}"), Utils.Version.Major, Utils.Version.Minor, Utils.Version.Major == 0 ? " (beta)" : ""));

                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    StopServer();
                };

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
                                        TinyOPDS.ServiceInstaller.InstallAndStart(SERVICE_NAME, SERVICE_DESC, _exePath);
                                        Console.WriteLine(SERVICE_DESC + " installed");
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(SERVICE_DESC + " failed to install with exception: \"{0}\"", e.Message);
                                        return (-1);
                                    }
                                }
                                else
                                {
                                    // Re-run app with elevated privileges 
                                    if (RunElevated("install")) Console.WriteLine(SERVICE_DESC + " installed");
                                    else Console.WriteLine(SERVICE_DESC + " failed to install");
                                }
                                return (0);
                            }

                        // Uninstall service command
                        case "uninstall":
                            {
                                if (!TinyOPDS.ServiceInstaller.ServiceIsInstalled(SERVICE_NAME))
                                {
                                    Console.WriteLine(SERVICE_DESC + " is not installed");
                                    return (-1);
                                }

                                if (Utils.IsElevated)
                                {
                                    try
                                    {
                                        TinyOPDS.ServiceInstaller.Uninstall(SERVICE_NAME);
                                        Console.WriteLine(SERVICE_DESC + " uninstalled");

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
                                        Console.WriteLine(SERVICE_DESC + " failed to uninstall with exception: \"{0}\"", e.Message);
                                        return (-1);
                                    }
                                }
                                else
                                {
                                    // Re-run app with elevated privileges 
                                    if (RunElevated("uninstall")) Console.WriteLine(SERVICE_DESC + " uninstalled");
                                    else Console.WriteLine(SERVICE_DESC + " failed to uninstall");
                                }
                                return (0);
                            }

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
                                            Console.WriteLine(SERVICE_DESC + " started");
                                        }
                                        catch (Exception e)
                                        {
                                            Console.WriteLine(SERVICE_DESC + " failed to start with exception: \"{0}\"", e.Message);
                                            return (-1);
                                        }
                                    }
                                    else
                                    {
                                        // Re-run app with elevated privileges 
                                        if (RunElevated("start")) Console.WriteLine(SERVICE_DESC + " started");
                                        else Console.WriteLine(SERVICE_DESC + " failed to start");
                                    }
                                }
                                else StartServer();
                                return (0);
                            }

                        // Stop service command
                        case "stop":
                            {
                                if (!TinyOPDS.ServiceInstaller.ServiceIsInstalled(SERVICE_NAME))
                                {
                                    Console.WriteLine(SERVICE_DESC + " is not installed");
                                    return (-1);
                                }

                                if (Utils.IsElevated)
                                {
                                    try
                                    {
                                        TinyOPDS.ServiceInstaller.StopService(SERVICE_NAME);
                                        Console.WriteLine(SERVICE_DESC + " stopped");
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(SERVICE_DESC + " failed to stop with exception: \"{0}\"", e.Message);
                                        return (-1);
                                    }
                                }
                                else
                                {
                                    // Re-run app with elevated privileges 
                                    if (RunElevated("stop")) Console.WriteLine(SERVICE_DESC + " stopped");
                                    else Console.WriteLine(SERVICE_DESC + " failed to stop");
                                }
                                return (0);
                            }

                        case "scan":
                            {
                                ScanFolder();
                                break;
                            }

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
                                break;
                            }
                    }
                    return (0);
                }

                bool l = Utils.IsLinux;
                Console.WriteLine("Use: TinyOPDSConsole.exe [command], where [command] is \n\n" +
                              (l ? "" : "\t install \t - install and run TinyOPDS service\n") +
                              (l ? "" : "\t uninstall \t - uninstall TinyOPDS service\n") +
                                        "\t start \t\t - start service\n" +
                              (l ? "" : "\t stop \t\t - stop service\n") +
                                        "\t scan \t\t - scan book directory\n" +
                                        "\t encred usr pwd\t - encode credentials\n\n" +
                                        "For more info please visit https://tinyopds.codeplex.com");
            }
            else
            {
#if !MONO
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] { new Program() };
                ServiceBase.Run(ServicesToRun);
#endif
            }

            return (0);
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

#if !MONO
        protected override void OnStart(string[] args)
        {
            StartServer();
        }

        protected override void OnStop()
        {
            StopServer();
        }
#endif
        #endregion

        #region OPDS server routines

        /// <summary>
        /// 
        /// </summary>
        private static void StartServer()
        {
            Log.SaveToFile = TinyOPDS.Properties.Settings.Default.SaveLogToDisk;

            // Init localization service
            Localizer.Init();
            Localizer.Language = TinyOPDS.Properties.Settings.Default.Language;

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

            _upnpController.DiscoverCompleted += _upnpController_DiscoverCompleted;
            _upnpController.DiscoverAsync(TinyOPDS.Properties.Settings.Default.UseUPnP);

            Library.LibraryPath = TinyOPDS.Properties.Settings.Default.LibraryPath;
            Library.LibraryLoaded += (_, __) =>
            {
                _watcher.DirectoryToWatch = Library.LibraryPath;
                _watcher.IsEnabled = TinyOPDS.Properties.Settings.Default.WatchLibrary;
            };

            // Load saved credentials
            try
            {
                HttpProcessor.Credentials.Clear();
                string[] pairs = Crypt.DecryptStringAES(TinyOPDS.Properties.Settings.Default.Credentials, _urlTemplate).Split(';');
                foreach (string pair in pairs)
                {
                    string[] cred = pair.Split(':');
                    if (cred.Length == 2) HttpProcessor.Credentials.Add(new Credential(cred[0], cred[1]));
                }
            }
            catch { }

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
                        Console.WriteLine(string.Format("Probably, port {0} is already in use. Please try different port value."), TinyOPDS.Properties.Settings.Default.ServerPort);
                        Log.WriteLine(LogLevel.Error, string.Format("Probably, port {0} is already in use. Please try different port value."), TinyOPDS.Properties.Settings.Default.ServerPort);
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
                if (Utils.IsLinux || System.Environment.UserInteractive)
                {
                    Console.WriteLine("Server is running... Press <Ctrl+c> to shutdown server.");
                    while (_server != null && _server.IsActive) Thread.Sleep(1000);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private static void StopServer()
        {
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
                    int port = int.Parse(TinyOPDS.Properties.Settings.Default.ServerPort);
                    _upnpController.DeleteForwardingRule(port, System.Net.Sockets.ProtocolType.Tcp);
                    Log.WriteLine("Port {0} closed", port);
                }
                _upnpController.DiscoverCompleted -= _upnpController_DiscoverCompleted;
                _upnpController.Dispose();
            }
        }

        private static void _upnpController_DiscoverCompleted(object sender, EventArgs e)
        {
            if (_upnpController != null && _upnpController.UPnPReady)
            {
                if (TinyOPDS.Properties.Settings.Default.OpenNATPort)
                {
                    int port = int.Parse(TinyOPDS.Properties.Settings.Default.ServerPort);
                    _upnpController.ForwardPort(port, System.Net.Sockets.ProtocolType.Tcp, "TinyOPDS server");
                    Log.WriteLine("Port {0} forwarded by UPnP", port);
                }
            }
        }

        private static void UpdateInfo()
        {
        }

        private static void ScanFolder()
        {
        }

        #endregion
    }
}
