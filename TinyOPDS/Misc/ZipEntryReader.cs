/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * ZIP entry reader with fallback for compression methods
 * not supported by System.IO.Compression (PPMd, BZip2, LZMA, Deflate64)
 *
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TinyOPDS
{
    /// <summary>
    /// Opens ZIP entries with System.IO.Compression and falls back to SharpCompress
    /// when the entry uses an unsupported compression method
    /// </summary>
    public sealed class ZipEntryReader : IDisposable
    {
        private readonly string zipFileName;
        private IDisposable fallbackArchive;
        private List<object> fallbackEntries;

        public ZipEntryReader(string zipFileName)
        {
            this.zipFileName = zipFileName;
        }

        /// <summary>
        /// Open entry stream; index is the entry position in ZipArchive.Entries
        /// </summary>
        public Stream Open(ZipArchiveEntry entry, int index)
        {
            // Once the archive is known to contain unsupported entries, read the rest
            // with fallback reader (it supports Deflate too) to avoid exception per entry
            if (fallbackArchive != null) return OpenFallback(entry, index);

            try
            {
                return entry.Open();
            }
            catch (InvalidDataException)
            {
                return OpenFallback(entry, index);
            }
        }

        /// <summary>
        /// Open entry stream (looks up the entry index in its archive)
        /// </summary>
        public Stream Open(ZipArchiveEntry entry)
        {
            return Open(entry, entry.Archive.Entries.IndexOf(entry));
        }

        // Kept in separate non-inlined methods so SharpCompress is loaded only when needed
        [MethodImpl(MethodImplOptions.NoInlining)]
        private Stream OpenFallback(ZipArchiveEntry entry, int index)
        {
            if (fallbackArchive == null)
            {
                var archive = SharpCompress.Archives.Zip.ZipArchive.Open(zipFileName);
                fallbackArchive = archive;
                fallbackEntries = archive.Entries.Cast<object>().ToList();
            }

            // Both libraries enumerate the central directory in the same order,
            // so match by index (immune to file name encoding differences), then verify
            SharpCompress.Archives.Zip.ZipArchiveEntry fallbackEntry = null;
            if (index >= 0 && index < fallbackEntries.Count)
            {
                var candidate = (SharpCompress.Archives.Zip.ZipArchiveEntry)fallbackEntries[index];
                if (candidate.Size == entry.Length) fallbackEntry = candidate;
            }
            if (fallbackEntry == null)
            {
                fallbackEntry = fallbackEntries.Cast<SharpCompress.Archives.Zip.ZipArchiveEntry>()
                    .FirstOrDefault(e => e.Key == entry.FullName && e.Size == entry.Length);
            }
            if (fallbackEntry == null)
                throw new InvalidDataException("Entry not found by fallback ZIP reader: " + entry.FullName);

            return fallbackEntry.OpenEntryStream();
        }

        public void Dispose()
        {
            if (fallbackArchive != null)
            {
                fallbackArchive.Dispose();
                fallbackArchive = null;
                fallbackEntries = null;
            }
        }
    }
}
