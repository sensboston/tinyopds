using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows;
using System.Threading;
using System.Diagnostics;

namespace TinyOPDS
{
    /// <summary>
    /// Helper for the external console apps execution in background (no visible window)
    /// Stores process output to the observable collection (so, we can bind output to the ListBox)
    /// </summary>
    public class ProcessHelper : IDisposable
    {
        private bool disposed = false;
        private Process process = new Process();
        private ObservableCollection<string> output = new ObservableCollection<string>();
        ProcessPriorityClass priority;
        public AutoResetEvent WaitForOutput = new AutoResetEvent(false);

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="CommandPath"></param>
        /// <param name="Arguments"></param>
        /// <param name="ParseOutput"></param>
        /// <param name="Priority"></param>
        public ProcessHelper(string CommandPath, string Arguments, bool ParseOutput = false, ProcessPriorityClass Priority = ProcessPriorityClass.Normal)
        {
            process.StartInfo.FileName = CommandPath;
            process.StartInfo.Arguments = Arguments;

            DoParseOutput = ParseOutput;

            // set up output redirection
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.EnableRaisingEvents = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            priority = Priority;
            // see below for output handler
            process.ErrorDataReceived += proc_DataReceived;
            process.OutputDataReceived += proc_DataReceived;
            process.Exited += (__, ____) =>
                {
                    if (OnExited != null)
                    {
                        if (DoParseOutput) WaitForOutput.WaitOne(3000);
                        OnExited(this, new EventArgs());
                    }
                };
        }

        /// <summary>
        /// Default destructor
        /// </summary>
        ~ProcessHelper()
        {
            Dispose();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed && disposing)
            {
                if (!process.HasExited && IsRunning) process.Kill();
                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool DoParseOutput { set; get; }

        public virtual void ParseOutput(string outString) { }

        private void proc_DataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                output.Add(e.Data);
                ParseOutput(e.Data);
            }
        }

        public void RunAsync()
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (_, __) =>
            {
                try
                {
                    if (process.Start())
                    {
                        process.PriorityClass = priority;
                        process.BeginErrorReadLine();
                        process.BeginOutputReadLine();
                        _isRunning = true;
                        process.WaitForExit();
                    }
                }
                catch(Exception e)
                {
                    Debug.WriteLine("ProcessHelper exception: " + e.ToString());
                }
                finally
                {
                    _isRunning = false;
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
        public ObservableCollection<string> ProcessOutput { get { return output; } }

        /// <summary>
        /// Return current state of process
        /// </summary>
        bool _isRunning = false;
        public bool IsRunning { get { return _isRunning; } }

        /// <summary>
        /// Return status of the current process
        /// </summary>
        public bool IsCompleted { get { return IsRunning?process.HasExited:false; } }

        /// <summary>
        /// Return process exit code
        /// </summary>
        public int ExitCode { get { return IsCompleted ? process.ExitCode : 0; } }

        /// <summary>
        /// Associated process priority class
        /// </summary>
        public ProcessPriorityClass PriorityClass
        {
            get { return process.PriorityClass; }
            set { process.PriorityClass = value; }
        }
    }
}
