/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines the FileScanner class
 * 
 ************************************************************/

using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows;
using System.Threading;

using TinyOPDS.Data;
using TinyOPDS.Parsers;

namespace TinyOPDS.Scanner
{
    public enum FileScannerStatus
    {
        SCANNING,
        STOPPED
    }

    public class FileScanner
    {
        public int SkippedFiles { get; set; }
        public FileScannerStatus Status { get; set; }

        public event BookFoundEventHandler OnBookFound;
        private IEnumerable<BookFoundEventHandler> BookFoundEventHandlers() { return from d in OnBookFound.GetInvocationList() select (BookFoundEventHandler)d; }

        public event InvalidBookEventHandler OnInvalidBook;
        private IEnumerable<InvalidBookEventHandler> InvalidBookEventHandlers() { return from d in OnInvalidBook.GetInvocationList() select (InvalidBookEventHandler)d; }

        public event FileSkippedEventHandler OnFileSkipped;
        private IEnumerable<FileSkippedEventHandler> FileSkippedEventHandlers() { return from d in OnFileSkipped.GetInvocationList() select (FileSkippedEventHandler)d; }

        public event ScanCompletedEventHandler OnScanCompleted;
        private IEnumerable<ScanCompletedEventHandler> ScanCompletedEventHandlers() { return from d in OnScanCompleted.GetInvocationList() select (ScanCompletedEventHandler)d; }

        private ZipScanner _zipScanner = null;
        private bool _isRecursive;

        public FileScanner(bool IsRecursive = true)
        {
            Status = FileScannerStatus.STOPPED;
            _isRecursive = IsRecursive;
        }

        public void Stop()
        {
            Status = FileScannerStatus.STOPPED;
            if (_zipScanner != null) _zipScanner.Status = FileScannerStatus.STOPPED;

            if (OnBookFound != null) OnBookFound -= BookFoundEventHandlers().Last();
            if (OnInvalidBook != null) OnInvalidBook -= InvalidBookEventHandlers().Last();
            if (OnFileSkipped != null) OnFileSkipped -= FileSkippedEventHandlers().Last();
            if (OnScanCompleted != null) OnScanCompleted -= ScanCompletedEventHandlers().Last();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Path"></param>
        public void Start(string Path)
        {
            SkippedFiles = 0;

            BackgroundWorker scanner = new BackgroundWorker();
            scanner.DoWork += (__, ___) =>
            {
                ScanDirectory(new DirectoryInfo(Path));
                Status = FileScannerStatus.STOPPED;
                if (OnScanCompleted != null) OnScanCompleted(this, new EventArgs());
            };
            Status = FileScannerStatus.SCANNING;
            scanner.RunWorkerAsync();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="IsRoot"></param>
        private void ScanDirectory(DirectoryInfo directory)
        {
            foreach (FileInfo file in directory.GetFiles())
            {
                if (!Utils.IsLinux && Status == FileScannerStatus.STOPPED) break;
                ScanFile(file.FullName);
            }

            // Recursively scan all subdirectories
            DirectoryInfo[] subDirectories = directory.GetDirectories();
            if (_isRecursive)
                foreach (DirectoryInfo subDirectory in subDirectories)
                    if (Status == FileScannerStatus.SCANNING)
                        ScanDirectory(subDirectory);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        public void ScanFile(string fullName)
        {
            Book book = null;
            string ext = Path.GetExtension(fullName).ToLower();

            // Process accepted files
            try
            {
                if (Library.Contains(fullName.Substring(Library.LibraryPath.Length + 1)))
                {
                    SkippedFiles++;
                    if (OnFileSkipped != null) OnFileSkipped(this, new FileSkippedEventArgs(SkippedFiles));
                }
                else if (ext.Contains(".epub"))
                {
                    book = new ePubParser().Parse(fullName);
                }
                else if (ext.Contains(".fb2"))
                {
                    book = new FB2Parser().Parse(fullName);
                }
                else if (ext.Contains(".zip"))
                {
                    _zipScanner = new ZipScanner(fullName);
                    _zipScanner.OnBookFound += (object sender, BookFoundEventArgs e) => { if (OnBookFound != null) OnBookFound(sender, e); };
                    _zipScanner.OnInvalidBook += (object sender, InvalidBookEventArgs e) => { if (OnInvalidBook != null) OnInvalidBook(sender, e); };
                    _zipScanner.OnFileSkipped += (object sender, FileSkippedEventArgs e) =>
                    {
                        SkippedFiles++;
                        if (OnFileSkipped != null) OnFileSkipped(sender, new FileSkippedEventArgs(SkippedFiles));
                    };
                    _zipScanner.Scan();
                }

                // Inform caller
                if (book != null)
                {
                    if (book.IsValid && OnBookFound != null) OnBookFound(this, new BookFoundEventArgs(book));
                    else if (!book.IsValid && OnInvalidBook != null) OnInvalidBook(this, new InvalidBookEventArgs(fullName));
                }

            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, ".ScanFile: exception {0} on file: {1}", e.Message, fullName);
                if (OnInvalidBook != null) OnInvalidBook(this, new InvalidBookEventArgs(fullName));
            }
        }
    }
}
