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

namespace TinyOPDS
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (MainForm mainForm = new MainForm())
            {
                if (Properties.Settings.Default.StartMinimized) mainForm.WindowState = FormWindowState.Minimized;
                if (Properties.Settings.Default.CloseToTray) mainForm.ShowInTaskbar = false;
                Application.Run(mainForm);
            }
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
