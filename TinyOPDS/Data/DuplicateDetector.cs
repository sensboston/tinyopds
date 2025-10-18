/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Smart duplicate detection for books - OPTIMIZED
 * 
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;

namespace TinyOPDS.Data
{
    /// <summary>
    /// Result of duplicate check
    /// </summary>
    public class DuplicateCheckResult
    {
        public bool IsDuplicate { get; set; }
        public Book ExistingBook { get; set; }
        public bool ShouldReplace { get; set; }
        public string Reason { get; set; }
        public DuplicateMatchType MatchType { get; set; }
        public int ComparisonScore { get; set; }
    }

    /// <summary>
    /// Type of duplicate match
    /// </summary>
    public enum DuplicateMatchType
    {
        None = 0,
        TrustedID = 1,      // Document ID match (FBD, GUID, LibRusEc)
        ContentHash = 2,    // Legacy - now same as TrustedID
        DuplicateKey = 3,   // Title + Author + Language match
        Fuzzy = 4           // Future expansion
    }

    /// <summary>
    /// Smart duplicate detector for books
    /// </summary>
    public class DuplicateDetector
    {
        private readonly DatabaseManager db;
        private const int REPLACEMENT_THRESHOLD = 0;

        // Pattern for FBD (Fiction Book Designer) IDs
        private static readonly Regex FBD_ID_PATTERN = new Regex(
            @"^FBD-[A-Z0-9]{6,10}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{12,16}$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Batch context for performance optimization
        private Dictionary<string, Book> batchContextByContentHash;
        private Dictionary<string, List<Book>> batchContextByDuplicateKey;
        private List<Book> batchContextBooks;
        private bool batchContextActive;

        public DuplicateDetector(DatabaseManager database)
        {
            db = database;
            batchContextActive = false;
        }

        /// <summary>
        /// Initialize batch context for bulk operations
        /// </summary>
        public void InitBatchContext()
        {
            batchContextByContentHash = new Dictionary<string, Book>();
            batchContextByDuplicateKey = new Dictionary<string, List<Book>>();
            batchContextBooks = new List<Book>();
            batchContextActive = true;
        }

        /// <summary>
        /// Clear batch context
        /// </summary>
        public void ClearBatchContext()
        {
            batchContextByContentHash?.Clear();
            batchContextByDuplicateKey?.Clear();
            batchContextBooks?.Clear();
            batchContextActive = false;
        }

        /// <summary>
        /// Add book to batch context
        /// </summary>
        public void AddToContext(Book book)
        {
            if (!batchContextActive || book == null)
                return;

            // Add to ContentHash index if valid
            if (!string.IsNullOrEmpty(book.ContentHash))
            {
                batchContextByContentHash[book.ContentHash] = book;
            }

            // Add to DuplicateKey index
            if (!string.IsNullOrEmpty(book.DuplicateKey))
            {
                if (!batchContextByDuplicateKey.ContainsKey(book.DuplicateKey))
                {
                    batchContextByDuplicateKey[book.DuplicateKey] = new List<Book>();
                }
                batchContextByDuplicateKey[book.DuplicateKey].Add(book);
            }

            batchContextBooks.Add(book);
        }

        /// <summary>
        /// Remove book from batch context
        /// </summary>
        public void RemoveFromContext(Book book)
        {
            if (!batchContextActive || book == null)
                return;

            // Remove from ContentHash index
            if (!string.IsNullOrEmpty(book.ContentHash) &&
                batchContextByContentHash.ContainsKey(book.ContentHash))
            {
                batchContextByContentHash.Remove(book.ContentHash);
            }

            // Remove from DuplicateKey index
            if (!string.IsNullOrEmpty(book.DuplicateKey) &&
                batchContextByDuplicateKey.ContainsKey(book.DuplicateKey))
            {
                batchContextByDuplicateKey[book.DuplicateKey].Remove(book);
                if (batchContextByDuplicateKey[book.DuplicateKey].Count == 0)
                {
                    batchContextByDuplicateKey.Remove(book.DuplicateKey);
                }
            }

            batchContextBooks.Remove(book);
        }

        /// <summary>
        /// Check if book is duplicate and determine action
        /// </summary>
        public DuplicateCheckResult CheckDuplicate(Book newBook)
        {
            var result = new DuplicateCheckResult
            {
                IsDuplicate = false,
                ShouldReplace = false,
                MatchType = DuplicateMatchType.None,
                ComparisonScore = 0
            };

            if (newBook == null || !newBook.IsValid)
                return result;

            // Generate duplicate key if missing
            if (string.IsNullOrEmpty(newBook.DuplicateKey))
                newBook.DuplicateKey = newBook.GenerateDuplicateKey();

            // Step 1: Check by ContentHash (document ID from FB2)
            if (!string.IsNullOrEmpty(newBook.ContentHash))
            {
                // Check if it's a trusted ID format
                bool isTrusted = IsValidTrustedID(newBook.ContentHash);

                if (isTrusted)
                {
                    // Check in batch context first
                    Book contextMatch = null;
                    if (batchContextActive && batchContextByContentHash.TryGetValue(newBook.ContentHash, out contextMatch))
                    {
                        return ProcessTrustedIDMatch(newBook, contextMatch, true);
                    }

                    // Check in database
                    var dbMatches = FindByDocumentID(newBook.ContentHash);
                    if (dbMatches != null && dbMatches.Count > 0)
                    {
                        return ProcessTrustedIDMatch(newBook, dbMatches[0], false);
                    }
                }
            }

            // Step 2: Check by duplicate key (Title + Author + Language)
            if (!string.IsNullOrEmpty(newBook.DuplicateKey))
            {
                // Check in batch context first
                if (batchContextActive && batchContextByDuplicateKey.TryGetValue(newBook.DuplicateKey, out var contextMatches))
                {
                    var contextResult = ProcessDuplicateKeyMatches(newBook, contextMatches, true);
                    if (contextResult.IsDuplicate)
                        return contextResult;
                }

                // Check in database
                var dbMatches = FindByDuplicateKey(newBook.DuplicateKey);
                if (dbMatches != null && dbMatches.Count > 0)
                {
                    return ProcessDuplicateKeyMatches(newBook, dbMatches, false);
                }
            }

            return result;
        }

        /// <summary>
        /// Process match by trusted document ID
        /// </summary>
        private DuplicateCheckResult ProcessTrustedIDMatch(Book newBook, Book existingBook, bool fromBatch)
        {
            var result = new DuplicateCheckResult
            {
                IsDuplicate = true,
                ExistingBook = existingBook,
                MatchType = DuplicateMatchType.TrustedID
            };

            string source = fromBatch ? " in batch" : "";

            // Compare versions
            if (newBook.Version > existingBook.Version)
            {
                result.ShouldReplace = true;
                result.ComparisonScore = (int)((newBook.Version - existingBook.Version) * 100);
                result.Reason = $"Newer version{source} (v{existingBook.Version:F1} → v{newBook.Version:F1})";
            }
            else if (Math.Abs(newBook.Version - existingBook.Version) < 0.01f)
            {
                // Same version - check dates and size
                if (newBook.DocumentDate > existingBook.DocumentDate)
                {
                    result.ShouldReplace = true;
                    result.ComparisonScore = 10;
                    result.Reason = $"Same version but newer date{source}";
                }
                else if (newBook.DocumentDate == existingBook.DocumentDate &&
                         newBook.DocumentSize > existingBook.DocumentSize)
                {
                    result.ShouldReplace = true;
                    result.ComparisonScore = 5;
                    result.Reason = $"Same version but larger size{source}";
                }
                else
                {
                    result.ShouldReplace = false;
                    result.Reason = $"Same version already exists{source}";
                }
            }
            else
            {
                result.ShouldReplace = false;
                result.ComparisonScore = (int)((newBook.Version - existingBook.Version) * 100);
                result.Reason = $"Older version{source} (v{newBook.Version:F1} < v{existingBook.Version:F1})";
            }

            return result;
        }

        /// <summary>
        /// Process matches by duplicate key
        /// </summary>
        private DuplicateCheckResult ProcessDuplicateKeyMatches(Book newBook, List<Book> matches, bool fromBatch)
        {
            var result = new DuplicateCheckResult
            {
                IsDuplicate = false,
                MatchType = DuplicateMatchType.None
            };

            Book bestMatch = null;
            int bestScore = int.MinValue;

            // Find best duplicate match
            foreach (var candidate in matches)
            {
                if (newBook.IsDuplicateOf(candidate))
                {
                    int score = newBook.CompareTo(candidate);
                    if (bestMatch == null || score > bestScore)
                    {
                        bestMatch = candidate;
                        bestScore = score;
                    }
                }
            }

            if (bestMatch != null)
            {
                result.IsDuplicate = true;
                result.ExistingBook = bestMatch;
                result.MatchType = DuplicateMatchType.DuplicateKey;
                result.ComparisonScore = bestScore;
                result.ShouldReplace = bestScore > REPLACEMENT_THRESHOLD;

                string source = fromBatch ? " in batch" : "";
                result.Reason = $"Matched by title/author{source}: '{newBook.Title}'";

                if (result.ShouldReplace)
                    result.Reason += $" - replacing (score: {bestScore})";
                else
                    result.Reason += $" - keeping existing (score: {bestScore})";
            }

            return result;
        }

        /// <summary>
        /// Check if ID is in a trusted format
        /// </summary>
        private bool IsValidTrustedID(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            // Clean the ID
            id = id.Trim();

            // Check FBD format (Fiction Book Designer)
            if (FBD_ID_PATTERN.IsMatch(id))
                return true;

            // Check LibRusEc format (numeric ID > 100000)
            if (long.TryParse(id, out long numericId) && numericId > 100000)
                return true;

            // Check if it's a trusted GUID
            if (Guid.TryParse(id, out Guid guid))
                return IsTrustedGuid(guid);

            return false;
        }

        /// <summary>
        /// Check if GUID is trusted (not a placeholder)
        /// </summary>
        private bool IsTrustedGuid(Guid guid)
        {
            if (guid == Guid.Empty) return false;

            string guidStr = guid.ToString().ToLowerInvariant();

            // Check for common placeholders
            if (guidStr == "00000000-0000-0000-0000-000000000000") return false;
            if (guidStr == "11111111-1111-1111-1111-111111111111") return false;
            if (guidStr == "12345678-1234-1234-1234-123456789012") return false;
            if (guidStr == "ffffffff-ffff-ffff-ffff-ffffffffffff") return false;

            // Check for timestamp strings (LibRusEc kit issue)
            if (guidStr.Contains("mon") || guidStr.Contains("tue") || guidStr.Contains("wed") ||
                guidStr.Contains("thu") || guidStr.Contains("fri") || guidStr.Contains("sat") ||
                guidStr.Contains("sun") || guidStr.Contains("jan") || guidStr.Contains("feb") ||
                guidStr.Contains("mar") || guidStr.Contains("apr") || guidStr.Contains("may") ||
                guidStr.Contains("jun") || guidStr.Contains("jul") || guidStr.Contains("aug") ||
                guidStr.Contains("sep") || guidStr.Contains("oct") || guidStr.Contains("nov") ||
                guidStr.Contains("dec"))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Process duplicate - either skip or replace
        /// </summary>
        public bool ProcessDuplicate(Book newBook, DuplicateCheckResult checkResult)
        {
            if (!checkResult.IsDuplicate)
                return true;

            if (!checkResult.ShouldReplace)
                return false;

            try
            {
                // Only mark as replaced in DB if it's from DB, not from batch context
                if (checkResult.ExistingBook != null &&
                    (batchContextBooks == null || !batchContextBooks.Contains(checkResult.ExistingBook)))
                {
                    MarkAsReplaced(checkResult.ExistingBook.ID, newBook.ID);
                }

                Log.WriteLine(LogLevel.Info, "Replacing: {0} with {1} - {2}",
                    checkResult.ExistingBook.FileName, newBook.FileName, checkResult.Reason);

                return true;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error replacing duplicate: {0}", ex.Message);
                return false;
            }
        }

        #region Database queries

        private List<Book> FindByDocumentID(string documentID)
        {
            try
            {
                // ContentHash now stores document ID
                var books = db.ExecuteQuery<Book>(
                    @"SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                     Annotation, DocumentSize, AddedDate, DocumentIDTrusted, 
                     DuplicateKey, ReplacedByID, ContentHash
                     FROM Books 
                     WHERE ContentHash = @DocumentID AND ReplacedByID IS NULL",
                    MapBook,
                    DatabaseManager.CreateParameter("@DocumentID", documentID));

                return books;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "FindByDocumentID error: {0}", ex.Message);
                return new List<Book>();
            }
        }

        private class BookDetailRow
        {
            public string BookId { get; set; }
            public Book Book { get; set; }
            public string ItemType { get; set; }
            public string ItemName { get; set; }
        }

        private List<Book> FindByDuplicateKey(string key)
        {
            try
            {
                var booksDict = new Dictionary<string, Book>();

                var rows = db.ExecuteQuery<BookDetailRow>(
                    DatabaseSchema.SelectBooksWithDetailsByDuplicateKey,
                    reader => new BookDetailRow
                    {
                        BookId = DatabaseManager.GetString(reader, "ID"),
                        Book = MapBook(reader),
                        ItemType = DatabaseManager.GetString(reader, "ItemType"),
                        ItemName = DatabaseManager.GetString(reader, "ItemName")
                    },
                    DatabaseManager.CreateParameter("@DuplicateKey", key));

                // Ваш код обработки - он правильный
                foreach (var row in rows)
                {
                    if (!booksDict.ContainsKey(row.BookId))
                    {
                        row.Book.Authors = new List<string>();
                        row.Book.Translators = new List<string>();
                        booksDict[row.BookId] = row.Book;
                    }

                    if (!string.IsNullOrEmpty(row.ItemName))
                    {
                        if (row.ItemType == "AUTHOR" && !booksDict[row.BookId].Authors.Contains(row.ItemName))
                            booksDict[row.BookId].Authors.Add(row.ItemName);
                        else if (row.ItemType == "TRANSLATOR" && !booksDict[row.BookId].Translators.Contains(row.ItemName))
                            booksDict[row.BookId].Translators.Add(row.ItemName);
                    }
                }

                return booksDict.Values.ToList();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "FindByDuplicateKey error: {0}", ex.Message);
                return new List<Book>();
            }
        }

        private void MarkAsReplaced(string oldID, string newID)
        {
            try
            {
                db.ExecuteNonQuery(
                    DatabaseSchema.UpdateBookAsReplaced,
                    DatabaseManager.CreateParameter("@OldID", oldID),
                    DatabaseManager.CreateParameter("@NewID", newID));
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "MarkAsReplaced error: {0}", ex.Message);
            }
        }

        #endregion

        #region Helper methods

        private Book MapBook(IDataReader reader)
        {
            var fileName = DatabaseManager.GetString(reader, "FileName");
            var book = new Book(fileName)
            {
                ID = DatabaseManager.GetString(reader, "ID"),
                Version = DatabaseManager.GetFloat(reader, "Version"),
                Title = DatabaseManager.GetString(reader, "Title"),
                Language = DatabaseManager.GetString(reader, "Language"),
                Annotation = DatabaseManager.GetString(reader, "Annotation"),
                DocumentSize = DatabaseManager.GetUInt32(reader, "DocumentSize"),
                DocumentIDTrusted = DatabaseManager.GetBoolean(reader, "DocumentIDTrusted"),
                DuplicateKey = DatabaseManager.GetString(reader, "DuplicateKey"),
                ReplacedByID = DatabaseManager.GetString(reader, "ReplacedByID"),
                ContentHash = DatabaseManager.GetString(reader, "ContentHash")
            };

            var bookDate = DatabaseManager.GetDateTime(reader, "BookDate");
            if (bookDate.HasValue) book.BookDate = bookDate.Value;

            var docDate = DatabaseManager.GetDateTime(reader, "DocumentDate");
            if (docDate.HasValue) book.DocumentDate = docDate.Value;

            var addedDate = DatabaseManager.GetDateTime(reader, "AddedDate");
            if (addedDate.HasValue) book.AddedDate = addedDate.Value;

            book.Authors = new List<string>();
            book.Translators = new List<string>();

            return book;
        }

        #endregion

        #region Statistics

        public DuplicateStatistics GetStatistics()
        {
            var stats = new DuplicateStatistics();

            try
            {
                var replacedCount = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE ReplacedByID IS NOT NULL");
                stats.ReplacedBooksCount = Convert.ToInt32(replacedCount);

                var trustedCount = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE DocumentIDTrusted = 1 AND ReplacedByID IS NULL");
                stats.TrustedIDCount = Convert.ToInt32(trustedCount);

                var duplicateGroups = db.ExecuteScalar("SELECT COUNT(DISTINCT DuplicateKey) FROM Books WHERE ReplacedByID IS NULL");
                stats.DuplicateGroupsCount = Convert.ToInt32(duplicateGroups);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error getting duplicate statistics: {0}", ex.Message);
            }

            return stats;
        }

        #endregion
    }

    /// <summary>
    /// Duplicate detection statistics
    /// </summary>
    public class DuplicateStatistics
    {
        public int ReplacedBooksCount { get; set; }
        public int TrustedIDCount { get; set; }
        public int DuplicateGroupsCount { get; set; }
    }
}