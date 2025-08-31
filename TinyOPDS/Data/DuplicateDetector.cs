/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Smart duplicate detection for books
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

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
    }

    /// <summary>
    /// Type of duplicate match
    /// </summary>
    public enum DuplicateMatchType
    {
        None = 0,
        TrustedID = 1,      // Same trusted FB2 ID
        ContentHash = 2,    // Exact file content
        DuplicateKey = 3,   // Title + Author + Language match
        Fuzzy = 4           // Fuzzy matching (future)
    }

    /// <summary>
    /// Smart duplicate detector for books
    /// </summary>
    public class DuplicateDetector
    {
        private readonly DatabaseManager db;
        private readonly bool aggressiveMode;

        public DuplicateDetector(DatabaseManager database, bool aggressive = false)
        {
            db = database;
            aggressiveMode = aggressive;
        }

        /// <summary>
        /// Check if book is duplicate and determine action
        /// </summary>
        public DuplicateCheckResult CheckDuplicate(Book newBook, Stream fileStream = null)
        {
            var result = new DuplicateCheckResult
            {
                IsDuplicate = false,
                ShouldReplace = false,
                MatchType = DuplicateMatchType.None
            };

            if (newBook == null || !newBook.IsValid)
                return result;

            // Generate keys for duplicate detection
            if (string.IsNullOrEmpty(newBook.DuplicateKey))
                newBook.DuplicateKey = newBook.GenerateDuplicateKey();

            if (fileStream != null && string.IsNullOrEmpty(newBook.ContentHash))
                newBook.ContentHash = newBook.GenerateContentHash(fileStream);

            // Step 1: Check by trusted ID (FB2 from FictionBookEditor)
            if (newBook.DocumentIDTrusted && newBook.BookType == BookType.FB2)
            {
                var trustedMatch = FindByTrustedID(newBook.ID);
                if (trustedMatch != null)
                {
                    result.IsDuplicate = true;
                    result.ExistingBook = trustedMatch;
                    result.MatchType = DuplicateMatchType.TrustedID;
                    result.ShouldReplace = ShouldReplaceBook(newBook, trustedMatch);
                    result.Reason = $"Matched by trusted FB2 ID: {newBook.ID}";

                    Log.WriteLine(LogLevel.Info, "Found duplicate by trusted ID: {0}, should replace: {1}",
                        newBook.ID, result.ShouldReplace);
                    return result;
                }
            }

            // Step 2: Check by content hash (exact file duplicate)
            if (!string.IsNullOrEmpty(newBook.ContentHash))
            {
                var contentMatch = FindByContentHash(newBook.ContentHash);
                if (contentMatch != null)
                {
                    result.IsDuplicate = true;
                    result.ExistingBook = contentMatch;
                    result.MatchType = DuplicateMatchType.ContentHash;
                    result.ShouldReplace = false; // Exact same file, no need to replace
                    result.Reason = "Exact file duplicate (same content hash)";

                    Log.WriteLine(LogLevel.Warning, "Found exact duplicate file: {0}", newBook.FileName);
                    return result;
                }
            }

            // Step 3: Check by duplicate key (Title + Author + Language)
            if (!string.IsNullOrEmpty(newBook.DuplicateKey))
            {
                var keyMatches = FindByDuplicateKey(newBook.DuplicateKey);
                if (keyMatches != null && keyMatches.Count > 0)
                {
                    // Find the best existing version
                    var bestExisting = keyMatches.OrderByDescending(b => b.CompareTo(newBook)).First();

                    result.IsDuplicate = true;
                    result.ExistingBook = bestExisting;
                    result.MatchType = DuplicateMatchType.DuplicateKey;

                    // Determine if we should replace
                    int comparison = newBook.CompareTo(bestExisting);

                    // In aggressive mode, replace if new is better or equal
                    // In normal mode, only replace if significantly better (score > 2)
                    result.ShouldReplace = aggressiveMode ? (comparison >= 0) : (comparison > 2);

                    result.Reason = $"Matched by title/author: '{newBook.Title}' by {newBook.Authors.FirstOrDefault()}";

                    if (result.ShouldReplace)
                    {
                        result.Reason += $" (newer/better version, score: {comparison})";
                    }

                    Log.WriteLine(LogLevel.Info, "Found duplicate by key: {0}, comparison score: {1}, should replace: {2}",
                        newBook.DuplicateKey, comparison, result.ShouldReplace);

                    // If there are multiple duplicates, mark older ones for replacement too
                    if (result.ShouldReplace && keyMatches.Count > 1)
                    {
                        foreach (var oldBook in keyMatches.Where(b => b.ID != bestExisting.ID))
                        {
                            MarkAsReplaced(oldBook.ID, newBook.ID);
                        }
                    }

                    return result;
                }
            }

            // Step 4: Additional fuzzy matching (if enabled and no matches found)
            if (aggressiveMode && !result.IsDuplicate)
            {
                var fuzzyMatch = FindByFuzzyMatch(newBook);
                if (fuzzyMatch != null)
                {
                    result.IsDuplicate = true;
                    result.ExistingBook = fuzzyMatch;
                    result.MatchType = DuplicateMatchType.Fuzzy;
                    result.ShouldReplace = ShouldReplaceBook(newBook, fuzzyMatch);
                    result.Reason = "Fuzzy match (similar title/author)";

                    Log.WriteLine(LogLevel.Info, "Found fuzzy duplicate: {0}, should replace: {1}",
                        newBook.Title, result.ShouldReplace);
                }
            }

            return result;
        }

        /// <summary>
        /// Process duplicate - either skip, replace, or add as new
        /// </summary>
        public bool ProcessDuplicate(Book newBook, DuplicateCheckResult checkResult)
        {
            if (!checkResult.IsDuplicate)
            {
                // Not a duplicate, can be added normally
                return true;
            }

            if (!checkResult.ShouldReplace)
            {
                // Duplicate exists but we shouldn't replace it
                Log.WriteLine(LogLevel.Info, "Skipping duplicate: {0} - {1}",
                    newBook.FileName, checkResult.Reason);
                return false;
            }

            // Replace the existing book
            try
            {
                // Mark old book as replaced
                MarkAsReplaced(checkResult.ExistingBook.ID, newBook.ID);

                Log.WriteLine(LogLevel.Info, "Replacing book {0} with {1} - {2}",
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

        private Book FindByTrustedID(string id)
        {
            try
            {
                return db.ExecuteQuerySingle<Book>(
                    DatabaseSchema.SelectBookByTrustedID,
                    MapBook,
                    DatabaseManager.CreateParameter("@ID", id));
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error finding book by trusted ID: {0}", ex.Message);
                return null;
            }
        }

        private Book FindByContentHash(string hash)
        {
            try
            {
                return db.ExecuteQuerySingle<Book>(
                    DatabaseSchema.SelectBookByContentHash,
                    MapBook,
                    DatabaseManager.CreateParameter("@ContentHash", hash));
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error finding book by content hash: {0}", ex.Message);
                return null;
            }
        }

        private List<Book> FindByDuplicateKey(string key)
        {
            try
            {
                return db.ExecuteQuery<Book>(
                    DatabaseSchema.SelectBooksByDuplicateKey,
                    MapBook,
                    DatabaseManager.CreateParameter("@DuplicateKey", key));
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error finding books by duplicate key: {0}", ex.Message);
                return new List<Book>();
            }
        }

        private Book FindByFuzzyMatch(Book newBook)
        {
            // TODO: Implement fuzzy matching using Levenshtein distance or similar
            // For now, return null
            return null;
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
                Log.WriteLine(LogLevel.Error, "Error marking book as replaced: {0}", ex.Message);
            }
        }

        #endregion

        #region Helper methods

        private bool ShouldReplaceBook(Book newBook, Book existingBook)
        {
            int comparison = newBook.CompareTo(existingBook);

            // In aggressive mode, replace if new is equal or better
            // In normal mode, only replace if significantly better
            return aggressiveMode ? (comparison >= 0) : (comparison > 2);
        }

        private Book MapBook(IDataReader reader)
        {
            var fileName = DatabaseManager.GetString(reader, "FileName");
            var book = new Book(fileName)
            {
                ID = DatabaseManager.GetString(reader, "ID"),
                Version = DatabaseManager.GetFloat(reader, "Version"),
                Title = DatabaseManager.GetString(reader, "Title"),
                Language = DatabaseManager.GetString(reader, "Language"),
                Sequence = DatabaseManager.GetString(reader, "Sequence"),
                NumberInSequence = DatabaseManager.GetUInt32(reader, "NumberInSequence"),
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

            return book;
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get statistics about replaced books
        /// </summary>
        public DuplicateStatistics GetStatistics()
        {
            var stats = new DuplicateStatistics();

            try
            {
                // Count replaced books
                var replacedCount = db.ExecuteScalar(
                    "SELECT COUNT(*) FROM Books WHERE ReplacedByID IS NOT NULL");
                stats.ReplacedBooksCount = Convert.ToInt32(replacedCount);

                // Count books with trusted IDs
                var trustedCount = db.ExecuteScalar(
                    "SELECT COUNT(*) FROM Books WHERE DocumentIDTrusted = 1 AND ReplacedByID IS NULL");
                stats.TrustedIDCount = Convert.ToInt32(trustedCount);

                // Count duplicate groups
                var duplicateGroups = db.ExecuteScalar(
                    "SELECT COUNT(DISTINCT DuplicateKey) FROM Books WHERE ReplacedByID IS NULL");
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
    /// Statistics about duplicates in the library
    /// </summary>
    public class DuplicateStatistics
    {
        public int ReplacedBooksCount { get; set; }
        public int TrustedIDCount { get; set; }
        public int DuplicateGroupsCount { get; set; }
    }
}