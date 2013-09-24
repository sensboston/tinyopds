using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;
using TinyOPDS;

namespace TinyOPDSConsole
{
    class Program : ServiceBase
    {
        private static readonly string _exePath = Assembly.GetExecutingAssembly().Location;
        private const string SERVICE_NAME = "TinyOPDSSvc";
        private const string SERVICE_DESC = "TinyOPDS service";

        static int Main(string[] args)
        {
            if (System.Environment.UserInteractive)
            {
                Console.WriteLine("\nTinyOPDS console, {0}, copyright (c) 2013 SeNSSoFT", string.Format(Localizer.Text("version {0}.{1} {2}"), Utils.Version.Major, Utils.Version.Minor, Utils.Version.Major == 0 ? " (beta)" : ""));

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
                                if (Utils.IsElevated)
                                {
                                    try
                                    {
                                        TinyOPDS.ServiceInstaller.Uninstall(SERVICE_NAME);
                                        Console.WriteLine(SERVICE_DESC + " uninstalled");
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
                                if (TinyOPDS.ServiceInstaller.ServiceIsInstalled(SERVICE_NAME))
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
                                break;
                            }

                        // Stop service command
                        case "stop":
                            {
                                if (TinyOPDS.ServiceInstaller.ServiceIsInstalled(SERVICE_NAME))
                                {
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
                                        }
                                    }
                                    else
                                    {
                                        // Re-run app with elevated privileges 
                                        if (RunElevated("stop")) Console.WriteLine(SERVICE_DESC + " stopped");
                                        else Console.WriteLine(SERVICE_DESC + " failed to stop");
                                    }
                                }
                                else StopServer();
                                break;
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
                                    Console.WriteLine(Crypt.EncryptStringAES(s, "http://{0}:{1}/{2}"));
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
                Console.WriteLine("Use: TinyOPDSConsole.exe [command], where [command] is \n\n" +
                                        "\t install \t - install and run TinyOPDS service\n" +
                                        "\t uninstall \t - uninstall TinyOPDS service\n" +
                                        "\t start \t\t - start service\n" +
                                        "\t stop \t\t - stop service\n" +
                                        "\t scan \t\t - scan book directory\n" +
                                        "\t encred usr pwd\t - encode credentials\n\n" +
                                        "For more info please visit https://tinyopds.codeplex.com");
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] { new Program() };
                ServiceBase.Run(ServicesToRun);
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

        protected override void OnStart(string[] args)
        {
            StartServer();
        }

        protected override void OnStop()
        {
            StopServer();
        }

        private static void StartServer()
        {
        }

        private static void StopServer()
        {
        }

        private static void ScanFolder()
        {
        }
    }
}
