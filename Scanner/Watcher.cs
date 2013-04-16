/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This is a file watcher class
 * 
 * TODO: should disable UI "scan" button during Watcher's 
 * operations
 * 
 ************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.ComponentModel;
using System.Security.Permissions;

using TinyOPDS.Data;

namespace TinyOPDS.Scanner
{
    public class Watcher : IDisposable
    {
        private class TimerObject
        {
            internal FileScanner scanner;
            internal Timer timer;
            internal string path;
        }

        private FileSystemWatcher _fileWatcher;
        private bool _disposed = false;

        public event BookAddedEventHandler OnBookAdded;
        public event BookDeletedEventHandler OnBookDeleted;

        [PermissionSetAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
        public Watcher(string path = "")
        {
            DirectoryToWatch = path;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.

            if (!this._disposed)
            {
                if (disposing)
                {
                    if (_fileWatcher != null) _fileWatcher.Dispose();
                }
                _disposed = true;
            }
        }

        public string DirectoryToWatch
        {
            [PermissionSetAttribute(SecurityAction.LinkDemand, Unrestricted=true)]
            get 
            { 
                return (_fileWatcher == null) ? string.Empty : _fileWatcher.Path; 
            }

            [PermissionSetAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
            set 
            {
                if (!string.IsNullOrEmpty(value) && Directory.Exists(value))
                {
                    if (_fileWatcher != null)
                    {
                        _fileWatcher.Created -= _fileWatcher_Created;
                        _fileWatcher.Deleted -= _fileWatcher_Deleted;
                        _fileWatcher.Renamed -= _fileWatcher_Renamed;
                        _fileWatcher.Dispose();
                    }
                    _fileWatcher = new FileSystemWatcher(value, "*");
                    _fileWatcher.InternalBufferSize = 1024 * 64;
                    _fileWatcher.Created += new FileSystemEventHandler(_fileWatcher_Created);
                    _fileWatcher.Deleted += new FileSystemEventHandler(_fileWatcher_Deleted);
                    _fileWatcher.Renamed += new RenamedEventHandler(_fileWatcher_Renamed);
                    _fileWatcher.IncludeSubdirectories = true;
                    _fileWatcher.EnableRaisingEvents = _isEnabled;
                }
            }
        }

        private bool _isEnabled = false;
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set 
            {
                if (_fileWatcher != null)
                {
                    _fileWatcher.EnableRaisingEvents = _isEnabled = value;
                }
            }
        }

        /// <summary>
        /// New file (book or zip archive) added to the library
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _fileWatcher_Created(object sender, FileSystemEventArgs e)
        {
            FileScanner scanner = new FileScanner(false);
            scanner.OnBookFound += (object s, BookFoundEventArgs be) =>
            {
                if (Library.Add(be.Book))
                {
                    if (OnBookAdded != null) OnBookAdded(this, new BookAddedEventArgs(be.Book.FileName));
                }
            };

            TimerObject timerObject = new TimerObject();
            Timer delayedTimer = new Timer(new TimerCallback(DelayedScan), timerObject, Timeout.Infinite, Timeout.Infinite);
            timerObject.scanner = scanner;
            timerObject.path = e.FullPath;
            timerObject.timer = delayedTimer;
            delayedTimer.Change(500, Timeout.Infinite);
        }

        private void DelayedScan(object state)
        {
            TimerObject timerObject = state as TimerObject;
            timerObject.timer.Dispose();
            timerObject.scanner.ScanFile(timerObject.path);
        }

        /// <summary>
        /// Library file (book or zip archive) is renamed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _fileWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            DeleteBookAsync(e.FullPath);
        }

        /// <summary>
        /// Library file (book or zip archive) deleted from the library
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _fileWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            DeleteBookAsync(e.FullPath);
        }

        private void DeleteBookAsync(string bookPath)
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += ((object s, DoWorkEventArgs ea) =>
            {
                if (Library.Delete(ea.Argument as string))
                {
                    if (OnBookDeleted != null) OnBookDeleted(this, new BookDeletedEventArgs(ea.Argument as string));
                }
                worker.Dispose();
            });
            worker.RunWorkerAsync(bookPath);
        }
    }
}
