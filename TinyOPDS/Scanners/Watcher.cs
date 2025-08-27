/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This is a file watcher class
 * 
 * TODO: should disable UI "scan" button during Watcher's operations
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.ComponentModel;
using System.Security.Permissions;

using TinyOPDS.Data;

namespace TinyOPDS.Scanner
{
    public class Watcher : IDisposable
    {
        private FileSystemWatcher fileWatcher;
        private bool disposed = false;
        private readonly string[] extensions = { ".zip", ".fb2", ".epub" };

        private readonly List<string> addedBooks = new List<string>();
        private readonly List<string> deletedBooks = new List<string>();
        private readonly BackgroundWorker booksManager;
        private readonly FileScanner scanner;

        public event BookAddedEventHandler OnBookAdded;
        public event BookDeletedEventHandler OnBookDeleted;
        public event InvalidBookEventHandler OnInvalidBook;
        public event FileSkippedEventHandler OnFileSkipped;

        [PermissionSetAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
        public Watcher(string path = "")
        {
            DirectoryToWatch = path;
            booksManager = new BackgroundWorker();
            booksManager.DoWork += BooksManager_DoWork;
            scanner = new FileScanner(false);
            scanner.OnBookFound += (object s, BookFoundEventArgs be) =>
            {
                if (Library.Add(be.Book))
                {
                    //Library.Append(be.Book);
                    OnBookAdded?.Invoke(this, new BookAddedEventArgs(be.Book.FileName));
                }
            };
            scanner.OnInvalidBook += (object _sender, InvalidBookEventArgs _e) => { OnInvalidBook?.Invoke(_sender, _e); };
            scanner.OnFileSkipped += (object _sender, FileSkippedEventArgs _e) => { OnFileSkipped?.Invoke(_sender, _e); };
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this.disposed)
            {
                if (disposing)
                {
                    if (booksManager != null)
                    {
                        _isEnabled = false;
                        booksManager.Dispose();
                    }
                    fileWatcher?.Dispose();
                }
                disposed = true;
            }
        }

        public string DirectoryToWatch
        {
            [PermissionSetAttribute(SecurityAction.LinkDemand, Unrestricted=true)]
            get 
            { 
                return (fileWatcher == null) ? string.Empty : fileWatcher.Path; 
            }

            [PermissionSetAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
            set 
            {
                if (!string.IsNullOrEmpty(value) && Directory.Exists(value))
                {
                    if (fileWatcher != null)
                    {
                        fileWatcher.Created -= FileWatcher_Created;
                        fileWatcher.Deleted -= FileWatcher_Deleted;
                        fileWatcher.Renamed -= FileWatcher_Renamed;
                        fileWatcher.Dispose();
                    }
                    fileWatcher = new FileSystemWatcher(value, "*")
                    {
                        InternalBufferSize = 1024 * 64
                    };
                    fileWatcher.Created += new FileSystemEventHandler(FileWatcher_Created);
                    fileWatcher.Deleted += new FileSystemEventHandler(FileWatcher_Deleted);
                    fileWatcher.Renamed += new RenamedEventHandler(FileWatcher_Renamed);
                    fileWatcher.IncludeSubdirectories = true;
                    fileWatcher.NotifyFilter = NotifyFilters.FileName;
                    fileWatcher.EnableRaisingEvents = _isEnabled;
                }
            }
        }

        private bool _isEnabled = false;
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set 
            {
                if (fileWatcher != null)
                {
                    fileWatcher.EnableRaisingEvents = _isEnabled = value;
                    if (_isEnabled)
                    {
                        if (!booksManager.IsBusy) booksManager.RunWorkerAsync();
                    }
                    else
                    {
                        addedBooks.Clear();
                        deletedBooks.Clear();
                    }
                }
            }
        }

        /// <summary>
        /// Book manager thread
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void BooksManager_DoWork(object sender, DoWorkEventArgs e)
        {
            string fileName;
            while (_isEnabled && !disposed)
            {
                // First, check added books
                if (addedBooks.Count > 0)
                {
                    fileName = addedBooks.First();
                    // If book scheduled for deletion, do not add it
                    if (deletedBooks.Contains(fileName))
                    {
                        deletedBooks.Remove(fileName);
                        addedBooks.Remove(fileName);
                    }
                    else
                    {
                        if (!IsFileInUse(fileName))
                        {
                            scanner.ScanFile(fileName);
                            addedBooks.Remove(fileName);
                        }
                        else
                        {
                            addedBooks.Remove(fileName);
                            addedBooks.Add(fileName);
                        }
                    }
                }
                // Delete book from library (we don't care about actual file existence)
                else if (deletedBooks.Count > 0)
                {
                    fileName = deletedBooks.First();
                    if (Library.Delete(fileName))
                    {
                        OnBookDeleted?.Invoke(this, new BookDeletedEventArgs(fileName));
                    }
                    deletedBooks.Remove(fileName);
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
        private void FileWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (extensions.Contains(Path.GetExtension(e.FullPath).ToLower()))
            {
                lock (addedBooks) addedBooks.Add(e.FullPath);
            }
        }

        /// <summary>
        /// Library file (book or zip archive) is renamed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (extensions.Contains(Path.GetExtension(e.FullPath).ToLower()))
            {
                lock (deletedBooks) deletedBooks.Add(e.FullPath);
            }
        }

        /// <summary>
        /// Library file (book or zip archive) deleted from the library
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            if (extensions.Contains(Path.GetExtension(e.FullPath).ToLower()))
            {
                lock (deletedBooks) deletedBooks.Add(e.FullPath);
            }
        }

        private bool IsFileInUse(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;
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
