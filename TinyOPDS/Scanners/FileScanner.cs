/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the FileScanner class
 *
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;

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

        private ZipScanner zipScanner = null;
        private readonly bool isRecursive;

        public FileScanner(bool IsRecursive = true)
        {
            Status = FileScannerStatus.STOPPED;
            isRecursive = IsRecursive;
        }

        public void Stop()
        {
            Status = FileScannerStatus.STOPPED;
            if (zipScanner != null) zipScanner.Status = FileScannerStatus.STOPPED;

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
                OnScanCompleted?.Invoke(this, new EventArgs());
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
            if (isRecursive)
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
                    OnFileSkipped?.Invoke(this, new FileSkippedEventArgs(SkippedFiles));
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
                    zipScanner = new ZipScanner(fullName);
                    zipScanner.OnBookFound += (object sender, BookFoundEventArgs e) => { OnBookFound?.Invoke(sender, e); };
                    zipScanner.OnInvalidBook += (object sender, InvalidBookEventArgs e) => { OnInvalidBook?.Invoke(sender, e); };
                    zipScanner.OnFileSkipped += (object sender, FileSkippedEventArgs e) =>
                    {
                        SkippedFiles++;
                        OnFileSkipped?.Invoke(sender, new FileSkippedEventArgs(SkippedFiles));
                    };
                    zipScanner.Scan();
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
                OnInvalidBook?.Invoke(this, new InvalidBookEventArgs(fullName));
            }
        }
    }
}
