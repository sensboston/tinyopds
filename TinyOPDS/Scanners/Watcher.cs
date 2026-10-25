﻿/*
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

        // Added files wait until they are completely written (copied): file -> its size, time and when they changed last
        private readonly Dictionary<string, PendingFile> addedBooks = new Dictionary<string, PendingFile>();
        private readonly HashSet<string> deletedBooks = new HashSet<string>();
        private readonly object sync = new object();
        private readonly BackgroundWorker booksManager;
        private readonly FileScanner scanner;
        private BookBatchWriter bookWriter;

        private class PendingFile
        {
            public long Size = -1;
            public DateTime LastWriteTime;
            public DateTime ChangedAt = DateTime.Now;
        }

        public event BookAddedEventHandler OnBookAdded;
        public event BookDeletedEventHandler OnBookDeleted;
        public event InvalidBookEventHandler OnInvalidBook;
        public event FileSkippedEventHandler OnFileSkipped;

        /// <summary>
        /// A file is scanned when it hasn't changed for this time. Windows locks a file being copied, so a short
        /// time is enough there; on Linux a file can be read while it's written, so its size has to stay the same
        /// for a while (a slow network copy may pause)
        /// </summary>
        public TimeSpan QuietPeriod { get; set; } = Utils.IsWindows ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(30);

        /// <summary>
        /// While paused (a library scan is running) the changes are collected, but not processed.
        /// Pausing waits for the changes being processed now, so the watcher and the scan never save books at once
        /// </summary>
        public bool IsPaused
        {
            get { return Volatile.Read(ref isPaused) != 0; }
            set
            {
                Interlocked.Exchange(ref isPaused, value ? 1 : 0);
                while (value && Volatile.Read(ref isProcessing) != 0) Thread.Sleep(100);
            }
        }
        private int isPaused, isProcessing;

        [PermissionSetAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
        public Watcher(string path = "")
        {
            DirectoryToWatch = path;
            booksManager = new BackgroundWorker();
            booksManager.DoWork += BooksManager_DoWork;
            scanner = new FileScanner(false);
            scanner.OnBookFound += (object s, BookFoundEventArgs be) => { bookWriter.Add(be.Book); };
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
                    // Folders too: a folder of books moved into the library gives no events for its files
                    fileWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
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
                        lock (sync)
                        {
                            addedBooks.Clear();
                            deletedBooks.Clear();
                        }
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
            while (_isEnabled && !disposed)
            {
                bool processed = false;

                // Marked as processing before the pause is checked: a pause set meanwhile waits for this pass
                Interlocked.Exchange(ref isProcessing, 1);
                try
                {
                    if (!IsPaused) processed = ProcessChanges();
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Watcher: {0}", ex.Message);
                }
                finally
                {
                    Interlocked.Exchange(ref isProcessing, 0);
                }

                // Get some rest
                if (!processed) Thread.Sleep(500);
            }
        }

        /// <summary>
        /// Adds the books which are completely written, or deletes a deleted book
        /// </summary>
        /// <returns>true if there was something to do</returns>
        private bool ProcessChanges()
        {
            var readyFiles = TakeReadyFiles();
            if (readyFiles.Count > 0)
            {
                AddBooks(readyFiles);
                return true;
            }

            // Delete book from library (we don't care about actual file existence)
            string fileName = null;
            lock (sync)
            {
                if (deletedBooks.Count > 0)
                {
                    fileName = deletedBooks.First();
                    deletedBooks.Remove(fileName);
                }
            }
            if (fileName != null)
            {
                DeleteBook(fileName);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Takes the added files which haven't changed for the quiet period
        /// </summary>
        private List<string> TakeReadyFiles()
        {
            var ready = new List<string>();
            lock (sync)
            {
                foreach (var pending in addedBooks.ToList())
                {
                    var info = new FileInfo(pending.Key);
                    if (!info.Exists)
                    {
                        addedBooks.Remove(pending.Key);
                    }
                    else if (info.Length != pending.Value.Size || info.LastWriteTimeUtc != pending.Value.LastWriteTime)
                    {
                        pending.Value.Size = info.Length;
                        pending.Value.LastWriteTime = info.LastWriteTimeUtc;
                        pending.Value.ChangedAt = DateTime.Now;
                    }
                    else if (DateTime.Now - pending.Value.ChangedAt >= QuietPeriod && !IsFileInUse(pending.Key))
                    {
                        ready.Add(pending.Key);
                        addedBooks.Remove(pending.Key);
                    }
                }
            }
            return ready;
        }

        /// <summary>
        /// Scans the files and saves their books as a batch, the same way the library scan does
        /// </summary>
        private void AddBooks(List<string> files)
        {
            bookWriter = new BookBatchWriter(Properties.Settings.Default.BatchSize);
            foreach (var fileName in files)
            {
                // A file written again with the same name: its old books go first, otherwise it's skipped as known
                bool replaced;
                lock (sync) replaced = deletedBooks.Remove(fileName);
                if (replaced) DeleteBook(fileName);

                scanner.ScanFile(fileName);
            }
            bookWriter.FlushAsync().GetAwaiter().GetResult();

            var (fb2Count, epubCount, _) = bookWriter.GetCounters();
            string source = files.Count == 1 ? files[0] : Path.GetDirectoryName(files[0]);
            if (fb2Count > 0) OnBookAdded?.Invoke(this, new BookAddedEventArgs(source, BookType.FB2, fb2Count));
            if (epubCount > 0) OnBookAdded?.Invoke(this, new BookAddedEventArgs(source, BookType.EPUB, epubCount));
        }

        private void DeleteBook(string fileName)
        {
            if (Library.Delete(fileName))
            {
                OnBookDeleted?.Invoke(this, new BookDeletedEventArgs(fileName));
            }
        }

        private bool IsBookFile(string path)
        {
            return extensions.Contains(Path.GetExtension(path).ToLower());
        }

        // Called under sync
        private void AddFile(string path)
        {
            if (IsBookFile(path) && !addedBooks.ContainsKey(path)) addedBooks.Add(path, new PendingFile());
        }

        // Called under sync
        private void AddFolder(string path)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)) AddFile(file);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Watcher: can't read folder {0}: {1}", path, ex.Message);
            }
        }

        // Called under sync
        private void RemoveFile(string path)
        {
            if (!IsBookFile(path)) return;
            // Not added yet, but an older file with this name may be in the library
            addedBooks.Remove(path);
            deletedBooks.Add(path);
        }

        /// <summary>
        /// New file (book or zip archive) or folder added to the library
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileWatcher_Created(object sender, FileSystemEventArgs e)
        {
            lock (sync)
            {
                if (Directory.Exists(e.FullPath)) AddFolder(e.FullPath);
                else AddFile(e.FullPath);
            }
        }

        /// <summary>
        /// Library file or folder is renamed: books of the old name are deleted, the new name is added
        /// (copying programs often write to a temporary file and rename it when done)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            lock (sync)
            {
                RemoveFile(e.OldFullPath);
                if (Directory.Exists(e.FullPath)) AddFolder(e.FullPath);
                else AddFile(e.FullPath);
            }
        }

        /// <summary>
        /// Library file (book or zip archive) deleted from the library
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FileWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            lock (sync) RemoveFile(e.FullPath);
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
