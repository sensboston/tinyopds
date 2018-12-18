/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines helper to run console processes in 
 * background, with output (including errout) collection
 * 
 ************************************************************/

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows;
using System.Threading;
using System.Security.Permissions;
using System.Diagnostics;

namespace TinyOPDS
{
    /// <summary>
    /// Helper for the external console apps execution in background (no visible window)
    /// Stores process output to the observable collection (so, we can bind output to the ListBox)
    /// </summary>
    public class ProcessHelper : IDisposable
    {
        private bool _disposed = false;
        private Process _process = null; 
        private ObservableCollection<string> _output = new ObservableCollection<string>();

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="commandPath"></param>
        /// <param name="arguments"></param>
        /// <param name="ParseOutput"></param>
        /// <param name="priority"></param>
        [PermissionSetAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
        public ProcessHelper(string commandPath, string arguments)
        {
            _process = new Process();
            _process.StartInfo.FileName = commandPath;
            _process.StartInfo.Arguments = arguments;

            // set up output redirection
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.RedirectStandardError = true;
            _process.EnableRaisingEvents = true;
            _process.StartInfo.CreateNoWindow = true;
            _process.StartInfo.UseShellExecute = false;
            // see below for output handler
            _process.ErrorDataReceived += proc_DataReceived;
            _process.OutputDataReceived += proc_DataReceived;
            _process.Exited += (__, ____) => { if (OnExited != null) OnExited(this, new EventArgs()); };
        }

        [PermissionSetAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed && disposing)
            {
                if (_process != null)
                {
                    if (!_process.HasExited && IsRunning) _process.Kill();
                    _process.Dispose();
                }
                _disposed = true;
            }
        }

        [PermissionSetAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void proc_DataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                _output.Add(e.Data);
            }
        }

        public void Run()
        {
            if (_process.Start())
            {
                _process.PriorityClass = ProcessPriorityClass.Normal;
                _process.BeginErrorReadLine();
                _process.BeginOutputReadLine();
                _isRunning = true;
                _process.WaitForExit();
                ExitCode = _process.ExitCode;
                _isRunning = false;
            }
        }

        public void RunAsync(AutoResetEvent waitEvent)
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (_, __) =>
            {
                try
                {
                    if (_process.Start())
                    {
                        _process.PriorityClass = ProcessPriorityClass.Normal;
                        _process.BeginErrorReadLine();
                        _process.BeginOutputReadLine();
                        _isRunning = true;
                        _process.WaitForExit();
                        ExitCode = _process.ExitCode;
                    }
                }
                catch(Exception e)
                {
                    Log.WriteLine(LogLevel.Error, "exception {0}", e.Message);
                }
                finally
                {
                    if (waitEvent != null) waitEvent.Set();
                    _isRunning = false;
                    worker.Dispose();
                }
            };
            worker.RunWorkerAsync();
        }

        /// <summary>
        /// Raised on process completion
        /// </summary>
        public event EventHandler OnExited;

        /// <summary>
        /// Process output to stdout
        /// </summary>
        public ObservableCollection<string> ProcessOutput { get { return _output; } }

        /// <summary>
        /// Return current state of process
        /// </summary>
        private bool _isRunning = false;
        public bool IsRunning { get { return _isRunning; } }

        /// <summary>
        /// Return process exit code
        /// </summary>
        public int ExitCode { get; private set; }
    }
}
