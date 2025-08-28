/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the ZipScanner class (FileScanner for 
 * files in zip archives)
 * 
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using TinyOPDS.Data;
using TinyOPDS.Parsers;
using System.IO.Compression;

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
        /// Scan zip file using System.IO.Compression for proper memory management
        /// </summary>
        public void Scan()
        {
            Status = FileScannerStatus.SCANNING;
            string entryFileName = string.Empty;

            try
            {
                using (var zipArchive = ZipFile.OpenRead(ZipFileName))
                {
                    foreach (var entry in zipArchive.Entries)
                    {
                        if (Status != FileScannerStatus.SCANNING) break;

                        if (!string.IsNullOrEmpty(entry.FullName))
                        {
                            entryFileName = entry.FullName;

                            // Process accepted files
                            try
                            {
                                Book book = null;
                                string ext = Path.GetExtension(entry.FullName).ToLower();

                                if (Library.Contains(ZipFileName.Substring(Library.LibraryPath.Length + 1) + "@" + entryFileName))
                                {
                                    SkippedFiles++;
                                    if (OnFileSkipped != null) OnFileSkipped(this, new FileSkippedEventArgs(SkippedFiles));
                                }
                                else if (ext.Contains(".epub"))
                                {
                                    using (var entryStream = entry.Open())
                                    using (var memStream = new MemoryStream())
                                    {
                                        entryStream.CopyTo(memStream);
                                        memStream.Position = 0;
                                        book = new ePubParser().Parse(memStream, ZipFileName + "@" + entryFileName);

                                        // Ensure correct DocumentSize for archive entries
                                        if (book != null)
                                        {
                                            book.DocumentSize = (uint)entry.Length;
                                        }
                                    }
                                }
                                else if (ext.Contains(".fb2"))
                                {
                                    using (var entryStream = entry.Open())
                                    using (var memStream = new MemoryStream())
                                    {
                                        entryStream.CopyTo(memStream);
                                        memStream.Position = 0;
                                        book = new FB2Parser().Parse(memStream, ZipFileName + "@" + entryFileName);

                                        // Ensure correct DocumentSize for archive entries
                                        if (book != null)
                                        {
                                            book.DocumentSize = (uint)entry.Length;
                                        }
                                    }
                                }

                                if (book != null)
                                {
                                    if (book.IsValid && OnBookFound != null)
                                    {
                                        OnBookFound(this, new BookFoundEventArgs(book));
                                    }
                                    else if (!book.IsValid && OnInvalidBook != null)
                                    {
                                        OnInvalidBook(this, new InvalidBookEventArgs(ZipFileName + "@" + entryFileName));
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Log.WriteLine(LogLevel.Error, ".ScanDirectory: exception {0} on file: {1}", e.Message, ZipFileName + "@" + entryFileName);
                                if (OnInvalidBook != null) OnInvalidBook(this, new InvalidBookEventArgs(ZipFileName + "@" + entryFileName));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error scanning ZIP file {0}: {1}", ZipFileName, ex.Message);
                Status = FileScannerStatus.STOPPED;
            }
            finally
            {
                if (Status == FileScannerStatus.SCANNING)
                {
                    Status = FileScannerStatus.STOPPED;
                }
            }
        }
    }
}