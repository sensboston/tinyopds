/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module handles books download requests (FB2 and EPUB)
 * ENHANCED: Added download tracking to database
 * FIXED: Always convert FB2 to EPUB when .epub is requested
 * 
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TinyOPDS.Data;

namespace TinyOPDS.Server
{
    /// <summary>
    /// Handles book download requests for FB2 and EPUB formats
    /// </summary>
    public class BookDownloadHandler
    {
        /// <summary>
        /// Main entry point for handling book download requests
        /// </summary>
        public void HandleBookDownloadRequest(HttpProcessor processor, string request, string ext, bool acceptFB2)
        {
            try
            {
                string bookID = ExtractBookIdFromRequest(request);
                if (string.IsNullOrEmpty(bookID))
                {
                    Log.WriteLine(LogLevel.Warning, "Invalid book ID in download request: {0}", request);
                    processor.WriteBadRequest();
                    return;
                }

                Book book = Library.GetBook(bookID);
                if (book == null)
                {
                    Log.WriteLine(LogLevel.Warning, "Book {0} not found in library", bookID);
                    processor.WriteFailure();
                    return;
                }

                // Determine download format and type
                string downloadFormat = null;
                string downloadType = "download";

                if (request.Contains("/fb2") || request.Contains(".fb2.zip"))
                {
                    downloadFormat = "fb2";
                    HandleFB2Download(processor, book);
                }
                else if (request.Contains("/epub") || ext.Equals(".epub"))
                {
                    downloadFormat = "epub";
                    HandleEpubDownload(processor, book, acceptFB2);
                }

                // Record download to database through Library
                RecordDownload(bookID, downloadType, downloadFormat, processor.HttpHeaders);

                HttpServer.ServerStatistics.IncrementBooksSent();
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Book download error: {0}", e.Message);
                processor.WriteFailure();
            }
        }

        /// <summary>
        /// Records download to database through Library
        /// </summary>
        private void RecordDownload(string bookId, string downloadType, string format, Dictionary<string, string> headers)
        {
            try
            {
                // Extract client info from headers if available
                string clientInfo = null;
                if (headers != null)
                {
                    string userAgent = headers.ContainsKey("User-Agent") ? headers["User-Agent"] : null;
                    string clientIp = headers.ContainsKey("X-Forwarded-For") ? headers["X-Forwarded-For"] : (headers.ContainsKey("REMOTE_ADDR") ? headers["REMOTE_ADDR"] : null);

                    if (!string.IsNullOrEmpty(userAgent) || !string.IsNullOrEmpty(clientIp))
                    {
                        clientInfo = string.Format("{0}|{1}",
                            userAgent ?? "Unknown",
                            clientIp ?? "Unknown");

                        // Limit client info length to prevent database issues
                        if (clientInfo.Length > 255)
                        {
                            clientInfo = clientInfo.Substring(0, 255);
                        }
                    }
                }

                // Use Library method instead of creating new DatabaseManager
                Library.RecordDownload(bookId, downloadType, format, clientInfo);

                Log.WriteLine(LogLevel.Info, "Recorded {0} download for book {1}, format: {2}",
                    downloadType, bookId, format ?? "n/a");
            }
            catch (Exception ex)
            {
                // Don't fail the download if we can't record it
                Log.WriteLine(LogLevel.Warning, "Failed to record download for book {0}: {1}",
                    bookId, ex.Message);
            }
        }

        /// <summary>
        /// Extracts book ID from download request URL
        /// </summary>
        private string ExtractBookIdFromRequest(string request)
        {
            try
            {
                Log.WriteLine(LogLevel.Info, "Extracting book ID from request: {0}", request);

                // Remove leading slash and split by slashes
                if (request.StartsWith("/"))
                    request = request.Substring(1);

                string[] parts = request.Split('/');
                Log.WriteLine(LogLevel.Info, "Request parts: [{0}]", string.Join(", ", parts));

                // Expected: download/{guid}/fb2 or download/{guid}/epub 
                if (parts.Length >= 3 && parts[0] == "download")
                {
                    string guid = parts[1];

                    // Clean up common GUID encoding artifacts
                    guid = guid.Replace("%7B", "{").Replace("%7D", "}");

                    Log.WriteLine(LogLevel.Info, "Extracted book ID: {0}", guid);
                    return guid;
                }

                Log.WriteLine(LogLevel.Warning, "Could not extract book ID from request: {0}", request);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error extracting book ID from {0}: {1}", request, ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Handles FB2 book download (creates ZIP with FB2 inside)
        /// </summary>
        private void HandleFB2Download(HttpProcessor processor, Book book)
        {
            using (var memStream = new MemoryStream())
            {
                // Use cancellation token with timeout for downloads
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
                {
                    if (!ExtractBookContentWithCancellation(book, memStream, cts.Token))
                    {
                        processor.WriteFailure();
                        return;
                    }
                }

                // Use System.IO.Compression for creating ZIP files in .NET 4.8
                using (var outputStream = new MemoryStream())
                {
                    using (var zipArchive = new System.IO.Compression.ZipArchive(outputStream, System.IO.Compression.ZipArchiveMode.Create, true))
                    {
                        string fileName = Transliteration.Front(
                            string.Format("{0}_{1}.fb2",
                                book.Authors.FirstOrDefault() ?? "Unknown",
                                book.Title));

                        var zipEntry = zipArchive.CreateEntry(fileName);
                        using (var entryStream = zipEntry.Open())
                        {
                            memStream.Position = 0;
                            memStream.CopyTo(entryStream);
                        }
                    }

                    outputStream.Position = 0;

                    // Set proper filename for download
                    string downloadFileName = Transliteration.Front(
                        string.Format("{0}_{1}.fb2.zip",
                            book.Authors.FirstOrDefault() ?? "Unknown",
                            book.Title));

                    // Write response headers manually
                    processor.OutputStream.WriteLine("HTTP/1.0 200 OK");
                    processor.OutputStream.WriteLine("Content-Type: application/zip");
                    processor.OutputStream.WriteLine("Content-Disposition: attachment; filename=\"{0}\"", downloadFileName);
                    processor.OutputStream.WriteLine("Content-Length: {0}", outputStream.Length);
                    processor.OutputStream.WriteLine("Connection: close");
                    processor.OutputStream.WriteLine();

                    outputStream.CopyTo(processor.OutputStream.BaseStream);
                    processor.OutputStream.BaseStream.Flush();
                }
            }
        }

        /// <summary>
        /// Handles EPUB book download (with FB2 to EPUB conversion when needed)
        /// </summary>
        private void HandleEpubDownload(HttpProcessor processor, Book book, bool acceptFB2)
        {
            using (var memStream = new MemoryStream())
            {
                // Use cancellation token with timeout for downloads
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
                {
                    if (!ExtractBookContentWithCancellation(book, memStream, cts.Token))
                    {
                        processor.WriteFailure();
                        return;
                    }
                }

                // FIXED: Always convert FB2 to EPUB when .epub is requested,
                // regardless of whether the client accepts FB2 or not.
                // When user explicitly requests .epub, they want EPUB format!
                if (book.BookType == BookType.FB2)
                {
                    Log.WriteLine(LogLevel.Info, "Converting FB2 to EPUB for book: {0}", book.FileName);

                    if (!ConvertFB2ToEpub(book, memStream))
                    {
                        Log.WriteLine(LogLevel.Error, "Failed to convert FB2 to EPUB for book: {0}", book.FileName);
                        processor.WriteFailure();
                        return;
                    }
                }
                else if (book.BookType == BookType.EPUB)
                {
                    Log.WriteLine(LogLevel.Info, "Book is already in EPUB format: {0}", book.FileName);
                }

                // Set proper filename for download
                string downloadFileName = Transliteration.Front(
                    string.Format("{0}_{1}.epub",
                        book.Authors.FirstOrDefault() ?? "Unknown",
                        book.Title));

                // Write response headers manually
                processor.OutputStream.WriteLine("HTTP/1.0 200 OK");
                processor.OutputStream.WriteLine("Content-Type: application/epub+zip");
                processor.OutputStream.WriteLine("Content-Disposition: attachment; filename=\"{0}\"", downloadFileName);
                processor.OutputStream.WriteLine("Content-Length: {0}", memStream.Length);
                processor.OutputStream.WriteLine("Connection: close");
                processor.OutputStream.WriteLine();

                memStream.Position = 0;
                memStream.CopyTo(processor.OutputStream.BaseStream);
                processor.OutputStream.BaseStream.Flush();

                Log.WriteLine(LogLevel.Info, "Successfully sent EPUB file: {0}", downloadFileName);
            }
        }

        /// <summary>
        /// Extracts book content from file or ZIP archive
        /// </summary>
        public bool ExtractBookContent(Book book, MemoryStream memStream)
        {
            // Wrapper for legacy code - uses default cancellation token
            return ExtractBookContentWithCancellation(book, memStream, CancellationToken.None);
        }

        /// <summary>
        /// Extracts book content with cancellation support
        /// </summary>
        public bool ExtractBookContentWithCancellation(Book book, MemoryStream memStream, CancellationToken cancellationToken)
        {
            try
            {
                // Build full path using library path if needed
                string bookPath = book.FilePath;

                // Check if path is relative and build full path
                if (!Path.IsPathRooted(bookPath))
                {
                    string libraryPath = Properties.Settings.Default.LibraryPath;
                    if (!string.IsNullOrEmpty(libraryPath))
                    {
                        bookPath = Path.Combine(libraryPath, bookPath);
                    }
                }

                Log.WriteLine(LogLevel.Info, "Attempting to extract book content from: {0}", bookPath);

                if (bookPath.ToLower().Contains(".zip@"))
                {
                    string[] pathParts = bookPath.Split('@');

                    if (!File.Exists(pathParts[0]))
                    {
                        Log.WriteLine(LogLevel.Warning, "ZIP archive not found: {0}", pathParts[0]);
                        return false;
                    }

                    // Check cancellation before opening ZIP
                    cancellationToken.ThrowIfCancellationRequested();

                    // Use System.IO.Compression.ZipFile for .NET 4.8
                    using (var zipArchive = System.IO.Compression.ZipFile.OpenRead(pathParts[0]))
                    {
                        var entry = zipArchive.Entries.FirstOrDefault(e =>
                            e.FullName.IndexOf(pathParts[1], StringComparison.OrdinalIgnoreCase) >= 0);

                        if (entry != null)
                        {
                            // Check cancellation before extraction
                            cancellationToken.ThrowIfCancellationRequested();

                            using (var entryStream = entry.Open())
                            {
                                // Copy with cancellation support
                                const int bufferSize = 81920; // 80KB buffer
                                byte[] buffer = new byte[bufferSize];
                                int bytesRead;

                                while ((bytesRead = entryStream.Read(buffer, 0, bufferSize)) > 0)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    memStream.Write(buffer, 0, bytesRead);
                                }

                                memStream.Position = 0;
                                Log.WriteLine(LogLevel.Info, "Successfully extracted book from ZIP: {0}", entry.FullName);
                                return true;
                            }
                        }
                        else
                        {
                            Log.WriteLine(LogLevel.Warning, "Entry not found in ZIP: {0}", pathParts[1]);
                            return false;
                        }
                    }
                }
                else
                {
                    if (!File.Exists(bookPath))
                    {
                        Log.WriteLine(LogLevel.Warning, "Book file not found: {0}", bookPath);
                        return false;
                    }

                    // Check cancellation before reading file
                    cancellationToken.ThrowIfCancellationRequested();

                    using (var stream = new FileStream(bookPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        // Copy with cancellation support
                        const int bufferSize = 81920;
                        byte[] buffer = new byte[bufferSize];
                        int bytesRead;

                        while ((bytesRead = stream.Read(buffer, 0, bufferSize)) > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            memStream.Write(buffer, 0, bytesRead);
                        }

                        memStream.Position = 0;
                        Log.WriteLine(LogLevel.Info, "Successfully extracted book from file: {0}", bookPath);
                        return true;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.WriteLine(LogLevel.Info, "Book extraction cancelled for: {0}", book.FilePath);
                throw;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error extracting book content from {0}: {1}", book.FilePath, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Converts FB2 book to EPUB format
        /// </summary>
        private bool ConvertFB2ToEpub(Book book, MemoryStream memStream)
        {
            try
            {
                var converter = new FB2EpubConverter();

                using (var epubStream = new MemoryStream())
                {
                    memStream.Position = 0;
                    bool converted = converter.ConvertToEpubStream(book, memStream, epubStream);

                    if (converted && epubStream.Length > 0)
                    {
                        memStream.SetLength(0);
                        memStream.Position = 0;
                        epubStream.Position = 0;
                        epubStream.CopyTo(memStream);
                        memStream.Position = 0;

                        Log.WriteLine(LogLevel.Info, "Successfully converted {0} using FB2EpubConverter", book.FileName);
                        return true;
                    }
                    else
                    {
                        Log.WriteLine(LogLevel.Warning, "FB2EpubConverter failed for {0}", book.FileName);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "FB2EpubConverter error for {0}: {1}", book.FileName, ex.Message);
                return false;
            }
        }
    }
}