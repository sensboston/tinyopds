﻿/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013-2025 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * TinyOPDS application entry point
 * 
 ************************************************************/

using System;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using Bluegrams.Application;
using TinyOPDS.Properties;
using System.Drawing;

namespace TinyOPDS
{
    static class Program
    {
        static readonly Mutex mutex = new Mutex(false, "tiny_opds_mutex");

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Check for macOS WinForms compatibility
            if (Utils.IsMacOS)
            {
                string message = "GUI version is not supported on macOS due to WinForms incompatibility with 64-bit Mono.\n" +
                                "CLI version provides full functionality including library scanning and OPDS/WEB server.";

                Console.WriteLine(message);
                Environment.Exit(0);
            }

            // Initialize embedded DLL loader first (before any other operations)
            EmbeddedDllLoader.Initialize();
            EmbeddedDllLoader.PreloadNativeDlls();

            // DPI Awareness for Windows 8.1 and later
            // shcore.dll is only available starting from Windows 8.1 (version 6.3)
            if (Utils.IsWindows)
            {
                try
                {
                    Version osVersion = Environment.OSVersion.Version;
                    // Windows 8.1 = 6.3, Windows 10 = 10.0
                    if ((osVersion.Major == 6 && osVersion.Minor >= 3) || osVersion.Major >= 10)
                    {
                        SetProcessDpiAwareness(PROCESS_DPI_AWARENESS.Process_Per_Monitor_DPI_Aware);
                    }
                }
                catch
                {
                    // Ignore DPI awareness errors on older Windows versions
                }
            }

            PortableSettingsProvider.SettingsFileName = "TinyOPDS.config";
            PortableSettingsProvider.ApplyProvider(Settings.Default);

            Application.SetCompatibleTextRenderingDefault(false);
            Application.EnableVisualStyles();

            // Check for single instance only if enabled in settings
            if (Settings.Default.OnlyOneInstance)
            {
                if (Utils.IsLinux)
                {
                    if (IsApplicationRunningOnMono("TinyOPDS.exe")) return;
                }
                else
                {
                    if (!mutex.WaitOne(TimeSpan.FromSeconds(1), false)) return;
                }
            }

            try
            {
                using (MainForm mainForm = new MainForm())
                {
                    mainForm.WindowState = Settings.Default.StartMinimized ? FormWindowState.Minimized : FormWindowState.Normal;
                    mainForm.ShowInTaskbar = !Settings.Default.StartMinimized || !Settings.Default.CloseToTray;
                    if (Utils.IsLinux) mainForm.Font = new Font("DejaVu Sans", 16, FontStyle.Regular);
                    Application.Run(mainForm);
                }
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

        // DPI Awareness API declarations
        [System.Runtime.InteropServices.DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(PROCESS_DPI_AWARENESS value);

        private enum PROCESS_DPI_AWARENESS
        {
            Process_DPI_Unaware = 0,
            Process_System_DPI_Aware = 1,
            Process_Per_Monitor_DPI_Aware = 2
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
    }
}