/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 * All rights reserved.
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

using TinyOPDS.Data;

namespace TinyOPDS.Scanner
{
    public class Watcher
    {
        private class TimerObject
        {
            internal FileScanner scanner;
            internal Timer timer;
            internal string path;
        }

        private FileSystemWatcher _fileWatcher;

        public event BookAddedEventHandler OnBookAdded;
        public event BookDeletedEventHandler OnBookDeleted;

        public Watcher(string path = "")
        {
            _fileWatcher = new FileSystemWatcher(path, "*");
            _fileWatcher.InternalBufferSize = 1024 * 64;
            _fileWatcher.Created += new FileSystemEventHandler(_fileWatcher_Created);
            _fileWatcher.Deleted += new FileSystemEventHandler(_fileWatcher_Deleted);
            _fileWatcher.Renamed += new RenamedEventHandler(_fileWatcher_Renamed);
            _fileWatcher.IncludeSubdirectories = true;
        }

        public string PathToWatch
        {
            get { return _fileWatcher.Path; }
            set { _fileWatcher.Path = value; }
        }

        public bool IsEnabled
        {
            get { return _fileWatcher.EnableRaisingEvents; }
            set { _fileWatcher.EnableRaisingEvents = value; }
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
                    //Log.WriteLine(LogLevel.Info, "Book \"{0}\" added to the library", be.Book.FileName);
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
            });
            worker.RunWorkerAsync(bookPath);
        }
    }
}
