/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * TinyOPDS application entry point
 * 
 ************************************************************/

using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Reflection;
using System.Threading;
using System.Diagnostics;

namespace TinyOPDS
{
    static class Program
    {
        static Mutex mutex = new Mutex(false, "tiny_opds_mutex");

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Check for single instance 
            if (Utils.IsLinux)
            {
                if (IsApplicationRunningOnMono("TinyOPDS.exe")) return;
            }
            else
            {
                if (!mutex.WaitOne(TimeSpan.FromSeconds(1), false)) return;
            }

            try
            {
                using (MainForm mainForm = new MainForm())
                {
                    mainForm.WindowState = (Properties.Settings.Default.StartMinimized) ? FormWindowState.Minimized : FormWindowState.Normal;
                    mainForm.ShowInTaskbar = (Properties.Settings.Default.StartMinimized && Properties.Settings.Default.CloseToTray) ? false : true;
                    Application.Run(mainForm);
                }
            }
            finally
            {
                if (!Utils.IsLinux) mutex.ReleaseMutex();
            }
        }

        static bool IsApplicationRunningOnMono(string processName)
        {
            var processFound = 0;

            Process[] monoProcesses;
            ProcessModuleCollection processModuleCollection;

            // find all processes called 'mono', that's necessary because our app runs under the mono process! 
            monoProcesses = Process.GetProcessesByName("mono");

            for (var i = 0; i <= monoProcesses.GetUpperBound(0); ++i)
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

            //we don't find the current process, but if there is already another one running, return true! 
            return (processFound == 1);
        } 

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
    }
}
