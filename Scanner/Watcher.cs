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
        private FileSystemWatcher _fileWatcher;
        private bool _disposed = false;

        private List<string> _addedBooks = new List<string>();
        private List<string> _deletedBooks = new List<string>();
        private BackgroundWorker _booksManager;
        private FileScanner _scanner;

        public event BookAddedEventHandler OnBookAdded;
        public event BookDeletedEventHandler OnBookDeleted;
        public event InvalidBookEventHandler OnInvalidBook;
        public event FileSkippedEventHandler OnFileSkipped;

        [PermissionSetAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
        public Watcher(string path = "")
        {
            DirectoryToWatch = path;
            _booksManager = new BackgroundWorker();
            _booksManager.DoWork += _booksManager_DoWork;
            _scanner = new FileScanner(false);
            _scanner.OnBookFound += (object s, BookFoundEventArgs be) =>
            {
                if (Library.Add(be.Book))
                {
                    //Library.Append(be.Book);
                    if (OnBookAdded != null) OnBookAdded(this, new BookAddedEventArgs(be.Book.FileName));
                }
            };
            _scanner.OnInvalidBook += (object _sender, InvalidBookEventArgs _e) => { if (OnInvalidBook != null) OnInvalidBook(_sender, _e); };
            _scanner.OnFileSkipped += (object _sender, FileSkippedEventArgs _e) => { if (OnFileSkipped != null) OnFileSkipped(_sender, _e); };
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
                    if (_booksManager != null)
                    {
                        _isEnabled = false;
                        _booksManager.Dispose();
                    }
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
                    if (_isEnabled) _booksManager.RunWorkerAsync();
                    else
                    {
                        _addedBooks.Clear();
                        _deletedBooks.Clear();
                    }
                }
            }
        }

        /// <summary>
        /// Book manager thread
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _booksManager_DoWork(object sender, DoWorkEventArgs e)
        {
            string fileName = string.Empty;
            while (_isEnabled && !_disposed)
            {
                // First, check added books
                if (_addedBooks.Count > 0)
                {
                    fileName = _addedBooks.First();
                    // If book scheduled for deletion, do not add it
                    if (_deletedBooks.Contains(fileName))
                    {
                        _deletedBooks.Remove(fileName);
                        _addedBooks.Remove(fileName);
                    }
                    else
                    {
                        if (!IsFileInUse(fileName))
                        {
                            _scanner.ScanFile(fileName);
                            _addedBooks.Remove(fileName);
                        }
                        else
                        {
                            _addedBooks.Remove(fileName);
                            _addedBooks.Add(fileName);
                        }
                    }
                }
                // Delete book from library (we don't care about actual file existence)
                else if (_deletedBooks.Count > 0)
                {
                    fileName = _deletedBooks.First();
                    if (Library.Delete(fileName))
                    {
                        if (OnBookDeleted != null) OnBookDeleted(this, new BookDeletedEventArgs(fileName));
                    }
                    _deletedBooks.Remove(fileName);
                }
                // Get some rest for UI
                else
                {
                    Thread.Sleep(100);
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
            lock (_addedBooks) _addedBooks.Add(e.FullPath);
        }

        /// <summary>
        /// Library file (book or zip archive) is renamed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _fileWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            lock (_deletedBooks) _deletedBooks.Add(e.FullPath);
        }

        /// <summary>
        /// Library file (book or zip archive) deleted from the library
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _fileWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            lock (_deletedBooks) _deletedBooks.Add(e.FullPath);
        }


        private bool IsFileInUse(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("'path' cannot be null or empty.", "path");

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read)) { }
            }
            catch (IOException)
            {
                return true;
            }
            return false;
        }
    }
}
