/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines the ZipScanner class (FileScanner analog
 * for zip archives)
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
using System.Threading;

using Ionic.Zip;
using TinyOPDS.Data;
using TinyOPDS.Parsers;

namespace TinyOPDS.Scanner
{
    public class ZipScanner
    {
        public string ZipFileName { get; set; }
        public FileScannerStatus Status { get; set; }
        public int SkippedFiles { get; set; }

        public event BookFoundEventHandler OnBookFound;
        private IEnumerable<BookFoundEventHandler> BookFoundEventHandlers() { return from d in OnBookFound.GetInvocationList() select (BookFoundEventHandler)d; }

        public event InvalidBookEventHandler OnInvalidBook;
        private IEnumerable<InvalidBookEventHandler> InvalidBookEventHandlers() { return from d in OnInvalidBook.GetInvocationList() select (InvalidBookEventHandler)d; }

        public event FileSkippedEventHandler OnFileSkipped;
        private IEnumerable<FileSkippedEventHandler> FileSkippedEventHandlers() { return from d in OnFileSkipped.GetInvocationList() select (FileSkippedEventHandler)d; }

        public ZipScanner(string zipFileName)
        {
            ZipFileName = zipFileName;
            Status = FileScannerStatus.STOPPED;
            SkippedFiles = 0;
        }

        public void Stop()
        {
            Status = FileScannerStatus.STOPPED;
            if (OnBookFound != null) OnBookFound -= BookFoundEventHandlers().Last();
            if (OnFileSkipped != null) OnFileSkipped -= FileSkippedEventHandlers().Last();
        }

        /// <summary>
        /// Scan zip file
        /// </summary>
        public void Scan()
        {
            Status = FileScannerStatus.SCANNING;
            ZipFile zipFile = null;
            string entryFileName = string.Empty;
            MemoryStream memStream = null;

            try
            {
                zipFile = new ZipFile(ZipFileName);

                foreach (ZipEntry entry in zipFile.Entries)
                {
                    if (Status != FileScannerStatus.SCANNING) break;

                    if (!string.IsNullOrEmpty(entry.FileName))
                    {
                        entryFileName = entry.FileName;

                        // Process accepted files
                        try
                        {
                            Book book = null;
                            memStream = new MemoryStream();

                            string ext = Path.GetExtension(entry.FileName).ToLower();

                            if (Library.Contains(ZipFileName.Substring(Library.LibraryPath.Length+1) + "@" + entryFileName))
                            {
                                SkippedFiles++;
                                if (OnFileSkipped != null) OnFileSkipped(this, new FileSkippedEventArgs(SkippedFiles));
                            }
                            else if (ext.Contains(".epub"))
                            {
                                entry.Extract(memStream);
                                book = new ePubParser().Parse(memStream, ZipFileName + "@" + entryFileName);
                            }
                            else if (ext.Contains(".fb2"))
                            {
                                entry.Extract(memStream);
                                book = new FB2Parser().Parse(memStream, ZipFileName + "@" + entryFileName);
                            }

                            if (book != null)
                            {
                                if (book.IsValid && OnBookFound != null) { OnBookFound(this, new BookFoundEventArgs(book)); }
                                else if (!book.IsValid && OnInvalidBook != null) OnInvalidBook(this, new InvalidBookEventArgs(ZipFileName + "@" + entryFileName));
                             }
                            
                        }
                        catch (Exception e)
                        {
                            Log.WriteLine(LogLevel.Error, ".ScanDirectory: exception {0} on file: {1}", e.Message, ZipFileName + "@" + entryFileName);
                            if (OnInvalidBook != null) OnInvalidBook(this, new InvalidBookEventArgs(ZipFileName + "@" + entryFileName));
                        }
                        finally
                        {
                            if (memStream != null)
                            {
                                memStream.Dispose();
                                memStream = null;
                            }
                        }
                    }
                }
            }
            finally
            {
                if (zipFile != null)
                {
                    zipFile.Dispose();
                    zipFile = null;
                }
            }
        }
    }
}
