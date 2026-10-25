/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module handles books uploaded from the web interface
 *
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

using TinyOPDS.Data;
using TinyOPDS.Scanner;

namespace TinyOPDS.Server
{
    /// <summary>
    /// Saves a book uploaded from the web interface to the Uploads folder of the library and adds it to the library.
    /// The request body is the file itself, its name is given in the query string: POST /upload?name=...
    /// </summary>
    public class UploadHandler
    {
        public const string FolderName = "Uploads";

        private static readonly string[] bookExtensions = { ".fb2", ".epub" };
        private const int MaxUploadsPerHour = 30;
        private const int MaxArchiveEntries = 1000;
        // Unpacked size of an archive, in upload size limits: books are packed 3-5 times, much more is a zip bomb
        private const int MaxUnpackedRatio = 20;
        private const int BufferSize = 81920;

        private static readonly Dictionary<string, Queue<DateTime>> uploadTimes = new Dictionary<string, Queue<DateTime>>();
        private static readonly object sync = new object();

        /// <summary>
        /// Uploads work only when they are enabled in the settings and the server asks for a password
        /// </summary>
        public static bool IsEnabled => Properties.Settings.Default.EnableUploads && Properties.Settings.Default.UseHTTPAuth;

        public static long MaxUploadSize => Math.Max(1, Properties.Settings.Default.MaxUploadSizeMB) * 1024L * 1024L;

        public static string UploadsPath => Path.Combine(Library.LibraryPath, FolderName);

        public void HandleUpload(HttpProcessor processor, Stream input)
        {
            // Pretend there's no such page when uploads are off
            if (!IsEnabled)
            {
                processor.WriteFailure();
                return;
            }

            string tempPath = null;
            try
            {
                long length = -1;
                if (processor.HttpHeaders.TryGetValue("Content-Length", out string lengthHeader)) long.TryParse(lengthHeader, out length);
                if (length <= 0)
                {
                    WriteResult(processor, false, Localizer.Text("Upload error"));
                    return;
                }
                if (length > MaxUploadSize)
                {
                    WriteResult(processor, false, string.Format(Localizer.Text("The file is too large, maximum is {0} MB"), Properties.Settings.Default.MaxUploadSizeMB));
                    return;
                }

                string fileName = GetFileName(processor.HttpUrl);
                string ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
                if (fileName == null || !(bookExtensions.Contains(ext) || ext == ".zip"))
                {
                    SkipBody(input, length);
                    WriteResult(processor, false, Localizer.Text("Only FB2, EPUB and ZIP files can be uploaded"));
                    return;
                }

                if (!CountUpload(processor.RealClientIP))
                {
                    Log.WriteLine(LogLevel.Warning, "Upload: too many uploads from {0}", processor.RealClientIP);
                    SkipBody(input, length);
                    WriteResult(processor, false, Localizer.Text("Too many uploads, please try again later"));
                    return;
                }

                Directory.CreateDirectory(UploadsPath);

                // Saved under a temporary name first: the folder watcher ignores it, and a bad file never gets a book name
                tempPath = Path.Combine(UploadsPath, Guid.NewGuid().ToString("N") + ".part");
                if (!ReceiveFile(input, length, tempPath))
                {
                    WriteResult(processor, false, Localizer.Text("Upload error"));
                    return;
                }

                if (ext != ".fb2" && !CheckArchive(tempPath, ext == ".zip"))
                {
                    WriteResult(processor, false, Localizer.Text("The file is damaged or is not a book archive"));
                    return;
                }

                string bookPath = GetUniquePath(fileName);
                File.Move(tempPath, bookPath);
                tempPath = null;

                int added = AddToLibrary(bookPath, out int duplicates);
                Log.WriteLine("Upload from {0}: {1}, {2} books added, {3} duplicates", processor.RealClientIP, Path.GetFileName(bookPath), added, duplicates);

                if (added == 0 && duplicates == 0)
                {
                    File.Delete(bookPath);
                    WriteResult(processor, false, Localizer.Text("No books found in the file"));
                }
                else if (added == 0)
                {
                    // Duplicates aren't saved to the library, the file would only take the space
                    File.Delete(bookPath);
                    WriteResult(processor, true, Localizer.Text("This book is already in the library"));
                }
                else
                {
                    string books = StringUtils.ApplyPluralForm(added, Localizer.Language, string.Format(Localizer.Text("{0} books"), added));
                    WriteResult(processor, true, string.Format(Localizer.Text("Added to the library: {0}"), books));
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Upload error from {0}: {1}", processor.RealClientIP, ex.Message);
                WriteResult(processor, false, Localizer.Text("Upload error"));
            }
            finally
            {
                if (tempPath != null)
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        /// <summary>
        /// File name from the query string, without folders and characters not allowed in file names
        /// </summary>
        private static string GetFileName(string url)
        {
            int i = url.IndexOf("name=", StringComparison.Ordinal);
            if (i < 0) return null;
            string name = url.Substring(i + 5);
            int amp = name.IndexOf('&');
            if (amp >= 0) name = name.Substring(0, amp);

            name = Uri.UnescapeDataString(name.Replace('+', ' '));
            name = name.Replace('\\', '/');
            name = name.Substring(name.LastIndexOf('/') + 1);
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            name = name.Trim().TrimStart('.');

            string ext = Path.GetExtension(name);
            string baseName = Path.GetFileNameWithoutExtension(name);
            if (baseName.Length > 100) baseName = baseName.Substring(0, 100);
            if (string.IsNullOrWhiteSpace(baseName)) baseName = "book";
            return baseName + ext;
        }

        private static bool CountUpload(string ip)
        {
            lock (sync)
            {
                if (!uploadTimes.TryGetValue(ip, out var times)) uploadTimes[ip] = times = new Queue<DateTime>();
                while (times.Count > 0 && DateTime.UtcNow - times.Peek() > TimeSpan.FromHours(1)) times.Dequeue();
                if (times.Count >= MaxUploadsPerHour) return false;
                times.Enqueue(DateTime.UtcNow);
                return true;
            }
        }

        /// <summary>
        /// Reads the rejected file to the end: closed with unread data, the connection is reset
        /// and the client gets no answer. Called for files within the size limit only.
        /// </summary>
        private static void SkipBody(Stream input, long length)
        {
            var buffer = new byte[BufferSize];
            while (length > 0)
            {
                int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, length));
                if (read <= 0) break;
                length -= read;
            }
        }

        private static bool ReceiveFile(Stream input, long length, string path)
        {
            var buffer = new byte[BufferSize];
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            {
                long left = length;
                while (left > 0)
                {
                    int read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, left));
                    if (read <= 0) return false;
                    file.Write(buffer, 0, read);
                    left -= read;
                }
            }
            return true;
        }

        /// <summary>
        /// Checks that the file is a readable ZIP archive and not a zip bomb: entries are really unpacked
        /// (their sizes in the archive can be faked) until the limits are reached.
        /// A ZIP upload must contain books only, EPUB is a ZIP archive itself.
        /// </summary>
        private static bool CheckArchive(string path, bool booksOnly)
        {
            try
            {
                long maxUnpacked = MaxUploadSize * MaxUnpackedRatio;
                long unpacked = 0;
                var buffer = new byte[BufferSize];

                using (var archive = ZipFile.OpenRead(path))
                using (var entryReader = new ZipEntryReader(path))
                {
                    var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
                    if (entries.Count == 0 || entries.Count > MaxArchiveEntries) return false;

                    foreach (var entry in entries)
                    {
                        if (booksOnly && !bookExtensions.Contains(Path.GetExtension(entry.Name).ToLowerInvariant()))
                        {
                            Log.WriteLine(LogLevel.Warning, "Upload: not a book in the archive: {0}", entry.FullName);
                            return false;
                        }

                        using (var stream = entryReader.Open(entry))
                        {
                            int read;
                            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                unpacked += read;
                                if (unpacked > maxUnpacked)
                                {
                                    Log.WriteLine(LogLevel.Warning, "Upload: archive unpacks to more than {0} bytes", maxUnpacked);
                                    return false;
                                }
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Upload: bad archive: {0}", ex.Message);
                return false;
            }
        }

        private static string GetUniquePath(string fileName)
        {
            string path = Path.Combine(UploadsPath, fileName);
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            for (int i = 1; File.Exists(path); i++)
            {
                path = Path.Combine(UploadsPath, string.Format("{0} ({1}){2}", baseName, i, ext));
            }
            return path;
        }

        /// <summary>
        /// Adds the books of the file the same way the folder watcher does
        /// </summary>
        private static int AddToLibrary(string path, out int duplicates)
        {
            var writer = new BookBatchWriter(Properties.Settings.Default.BatchSize);
            var scanner = new FileScanner(false);
            scanner.OnBookFound += (sender, e) => writer.Add(e.Book);
            scanner.ScanFile(path);
            writer.FlushAsync().GetAwaiter().GetResult();

            var (fb2, epub, dups) = writer.GetCounters();
            duplicates = dups;
            return fb2 + epub;
        }

        private static void WriteResult(HttpProcessor processor, bool ok, string message)
        {
            var json = new StringBuilder("{\"ok\":").Append(ok ? "true" : "false").Append(",\"message\":\"");
            foreach (char c in message)
            {
                if (c == '"' || c == '\\') json.Append('\\').Append(c);
                else if (c < ' ') json.AppendFormat("\\u{0:x4}", (int)c);
                else json.Append(c);
            }
            json.Append("\"}");

            processor.WriteSuccess("application/json; charset=utf-8");
            processor.OutputStream.Write(json.ToString());
            processor.OutputStream.Flush();
        }
    }
}
