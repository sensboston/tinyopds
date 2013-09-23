using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Configuration.Install;
using System.Reflection;
using TinyOPDS;

namespace TinyOPDSConsole
{
    class Program : ServiceBase
    {
        private static readonly string _exePath = Assembly.GetExecutingAssembly().Location;

        static void Main(string[] args)
        {
            if (System.Environment.UserInteractive)
            {
                Console.WriteLine("\nTinyOPDS console, {0}, copyright (c) 2013 SeNSSoFT", string.Format(Localizer.Text("version {0}.{1} {2}"), Utils.Version.Major, Utils.Version.Minor, Utils.Version.Major == 0 ? " (beta)" : ""));

                if (args.Length == 0)
                {
                    Console.WriteLine("Use: TinyOPDSConsole.exe [command], where [command] is \n\n" +
                                            "\t install \t - install TinyOPDS service\n" +
                                            "\t uninstall \t - uninstall TinyOPDS service\n" +
                                            "\t start \t\t - start network service\n" +
                                            "\t stop \t\t - stop network service\n" +
                                            "\t scan \t\t - scan book directory\n\n" +
                                            "For more info please visit https://tinyopds.codeplex.com");
                                            
                }
                else
                {
                    switch (args[0].ToLower())
                    {
                        case "install":
                            {
                                try
                                {
                                    ManagedInstallerClass.InstallHelper(new string[] { _exePath });
                                    Console.WriteLine("TinyOPDS service installed");
                                }
                                catch
                                {
                                    Console.WriteLine("TinyOPDS service failed to install");
                                }
                                break;
                            }
                        case "uninstall":
                            {
                                try
                                {

                                    ManagedInstallerClass.InstallHelper(new string[] { "/u", _exePath });
                                    Console.WriteLine("TinyOPDS service uninstalled");
                                }
                                catch
                                {
                                    Console.WriteLine("TinyOPDS service failed to uninstall");
                                }
                                break;
                            }
                    }
                }
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] { new Program() };
                ServiceBase.Run(ServicesToRun);
            }
        }

        protected override void OnStart(string[] args)
        {
        }

        protected override void OnStop()
        {
        }

    }
}
