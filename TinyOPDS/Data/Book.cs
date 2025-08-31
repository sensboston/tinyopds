/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the Book class
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace TinyOPDS.Data
{
    /// <summary>
    /// Supported book types
    /// </summary>
    public enum BookType
    {
        FB2,
        EPUB,
    }

    /// <summary>
    /// Base data class
    /// </summary>
    public class Book
    {
        public Book(string fileName = "")
        {
            Version = 1;
            FileName = fileName;
            if (!string.IsNullOrEmpty(FileName) && FileName.IndexOf(Library.LibraryPath) == 0)
            {
                FileName = FileName.Substring(Library.LibraryPath.Length + 1);
            }
            Title = Sequence = Annotation = Language = string.Empty;
            BookDate = DocumentDate = DateTime.MinValue;
            NumberInSequence = 0;
            Authors = new List<string>();
            Translators = new List<string>();
            Genres = new List<string>();
            DocumentIDTrusted = false;
            DuplicateKey = string.Empty;
            ReplacedByID = null;
            ContentHash = null;
        }

        private string id = string.Empty;
        public string ID
        {
            get { return id; }
            set
            {
                // Book ID always must be in GUID form
                Guid guid;
                if (!string.IsNullOrEmpty(value) && Guid.TryParse(value, out guid))
                {
                    id = value;
                    // Check if this is a trusted ID (not a placeholder)
                    DocumentIDTrusted = IsTrustedGuid(guid);
                }
                else
                {
                    id = Utils.CreateGuid(Utils.IsoOidNamespace, FileName).ToString();
                    DocumentIDTrusted = false;
                }
                id = id.Replace("{", "").Replace("}", "");
            }
        }

        public float Version { get; set; }
        public string FileName { get; private set; }
        public string FilePath
        {
            get
            {
                string path = Path.Combine(Library.LibraryPath, FileName).Replace("\\\\", "\\");
                return Utils.IsLinux ? path.Replace('\\', '/') : path;
            }
        }
        public string Title { get; set; }
        public string Language { get; set; }
        public DateTime BookDate { get; set; }
        public DateTime DocumentDate { get; set; }
        public string Sequence { get; set; }
        public UInt32 NumberInSequence { get; set; }
        public string Annotation { get; set; }
        public UInt32 DocumentSize { get; set; }
        public List<string> Authors { get; set; }
        public List<string> Translators { get; set; }
        public List<string> Genres { get; set; }
        public BookType BookType { get { return Path.GetExtension(FilePath).ToLower().Contains("epub") ? BookType.EPUB : Data.BookType.FB2; } }
        public bool IsValid { get { return (!string.IsNullOrEmpty(Title) && Title.IsValidUTF() && Authors.Count > 0 && Genres.Count > 0); } }
        public DateTime AddedDate { get; set; }

        // New fields for improved duplicate detection
        public bool DocumentIDTrusted { get; set; }
        public string DuplicateKey { get; set; }
        public string ReplacedByID { get; set; }
        public string ContentHash { get; set; }

        /// <summary>
        /// Check if GUID is trusted (not a placeholder from bad converters)
        /// </summary>
        private bool IsTrustedGuid(Guid guid)
        {
            // Check for common placeholder GUIDs
            if (guid == Guid.Empty) return false;
            if (guid.ToString() == "00000000-0000-0000-0000-000000000000") return false;
            if (guid.ToString() == "11111111-1111-1111-1111-111111111111") return false;
            if (guid.ToString() == "12345678-1234-1234-1234-123456789012") return false;
            if (guid.ToString() == "ffffffff-ffff-ffff-ffff-ffffffffffff") return false;

            // Check if all bytes are the same (likely generated badly)
            var bytes = guid.ToByteArray();
            if (bytes.All(b => b == bytes[0])) return false;

            // Check for sequential patterns (another sign of bad generation)
            bool isSequential = true;
            for (int i = 1; i < bytes.Length; i++)
            {
                if (bytes[i] != bytes[i - 1] + 1 && bytes[i] != bytes[i - 1])
                {
                    isSequential = false;
                    break;
                }
            }
            if (isSequential) return false;

            // If it passed all checks, it's likely from FictionBookEditor or similar proper tool
            return true;
        }

        /// <summary>
        /// Generate duplicate detection key based on title, first author and language
        /// </summary>
        public string GenerateDuplicateKey()
        {
            if (string.IsNullOrEmpty(Title) || Authors == null || Authors.Count == 0)
                return string.Empty;

            string normalizedTitle = NormalizeForDuplicateKey(Title);
            string firstAuthor = Authors.First();
            string normalizedAuthor = NormalizeForDuplicateKey(firstAuthor);
            string lang = string.IsNullOrEmpty(Language) ? "unknown" : Language.ToLowerInvariant();

            string keySource = $"{normalizedTitle}|{normalizedAuthor}|{lang}";

            // Generate MD5 hash for compact storage
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(keySource);
                byte[] hash = md5.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Generate content hash from file (first 10KB to avoid full file read)
        /// </summary>
        public string GenerateContentHash(Stream fileStream)
        {
            if (fileStream == null || !fileStream.CanRead)
                return null;

            try
            {
                long originalPosition = fileStream.Position;
                fileStream.Position = 0;

                // Read first 10KB or whole file if smaller
                int bytesToRead = (int)Math.Min(10240, fileStream.Length);
                byte[] buffer = new byte[bytesToRead];
                fileStream.Read(buffer, 0, bytesToRead);

                // Reset stream position
                fileStream.Position = originalPosition;

                // Generate hash
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(buffer);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Normalize string for duplicate key generation
        /// </summary>
        private string NormalizeForDuplicateKey(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // Convert to lowercase
            text = text.ToLowerInvariant();

            // Remove content in parentheses/brackets (often subtitles, edition info, series)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\([^)]*\)", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[[^\]]*\]", "");

            // Remove punctuation and special characters
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[^\w\s]", " ");

            // Remove common words that differ between editions
            string[] wordsToRemove = { "the", "a", "an", "and", "or", "but", "edition", "version", "revised", "updated" };
            foreach (var word in wordsToRemove)
            {
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\b" + word + @"\b", " ");
            }

            // Remove multiple spaces
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

            return text.Trim();
        }

        /// <summary>
        /// Compare two books to determine which is better/newer
        /// Returns: positive if this book is better, negative if other is better, 0 if equal
        /// </summary>
        public int CompareTo(Book other)
        {
            if (other == null) return 1;

            int score = 0;

            // For books with trusted IDs (from FictionBookEditor)
            if (this.DocumentIDTrusted && other.DocumentIDTrusted && this.ID == other.ID)
            {
                // Compare version numbers
                if (this.Version > other.Version) score += 3;
                else if (this.Version < other.Version) score -= 3;

                // Compare document dates
                if (this.DocumentDate > other.DocumentDate.AddDays(1)) score += 2;
                else if (this.DocumentDate < other.DocumentDate.AddDays(-1)) score -= 2;
            }
            else
            {
                // For books without trusted IDs or different IDs

                // Prefer FB2 over EPUB (more metadata)
                if (this.BookType == BookType.FB2 && other.BookType == BookType.EPUB) score += 2;
                else if (this.BookType == BookType.EPUB && other.BookType == BookType.FB2) score -= 2;

                // Compare document dates (with tolerance of 1 day)
                if (this.DocumentDate > other.DocumentDate.AddDays(1)) score += 3;
                else if (this.DocumentDate < other.DocumentDate.AddDays(-1)) score -= 3;

                // Compare file sizes (bigger often means more complete, but only if significant difference)
                if (this.DocumentSize > other.DocumentSize * 1.1) score += 1;
                else if (this.DocumentSize < other.DocumentSize * 0.9) score -= 1;
            }

            // Prefer books with trusted IDs
            if (this.DocumentIDTrusted && !other.DocumentIDTrusted) score += 1;
            else if (!this.DocumentIDTrusted && other.DocumentIDTrusted) score -= 1;

            return score;
        }

        /// <summary>
        /// Check if this book is likely a duplicate of another
        /// </summary>
        public bool IsDuplicateOf(Book other)
        {
            if (other == null) return false;

            // Same trusted ID = definite duplicate
            if (this.DocumentIDTrusted && other.DocumentIDTrusted && this.ID == other.ID)
                return true;

            // Same content hash = exact duplicate
            if (!string.IsNullOrEmpty(this.ContentHash) && this.ContentHash == other.ContentHash)
                return true;

            // Same duplicate key = likely duplicate
            if (!string.IsNullOrEmpty(this.DuplicateKey) && this.DuplicateKey == other.DuplicateKey)
                return true;

            return false;
        }
    }
}