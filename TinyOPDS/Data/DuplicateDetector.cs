/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Smart duplicate detection for books - FIXED with archive priority support
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
        public int ComparisonScore { get; set; }
    }

    /// <summary>
    /// Type of duplicate match
    /// </summary>
    public enum DuplicateMatchType
    {
        None = 0,
        TrustedID = 1,      // DEPRECATED - No longer used since IDs are always unique
        ContentHash = 2,    // Exact file content
        DuplicateKey = 3,   // Title + Author + Language match
        Fuzzy = 4           // Fuzzy matching (future)
    }

    /// <summary>
    /// Smart duplicate detector for books
    /// MODIFIED: Improved archive priority handling and lowered replacement threshold
    /// </summary>
    public class DuplicateDetector
    {
        private readonly DatabaseManager db;
        private const int REPLACEMENT_THRESHOLD = 0;

        public DuplicateDetector(DatabaseManager database)
        {
            db = database;
        }

        /// <summary>
        /// Check if book is duplicate and determine action
        /// MODIFIED: Improved handling of archive priorities and score=0 cases
        /// </summary>
        public DuplicateCheckResult CheckDuplicate(Book newBook, Stream fileStream = null)
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

            // Generate keys for duplicate detection
            if (string.IsNullOrEmpty(newBook.DuplicateKey))
                newBook.DuplicateKey = newBook.GenerateDuplicateKey();

            if (fileStream != null && string.IsNullOrEmpty(newBook.ContentHash))
                newBook.ContentHash = newBook.GenerateContentHash(fileStream);

            // Step 1: Check by content hash (exact file duplicate)
            if (!string.IsNullOrEmpty(newBook.ContentHash))
            {
                var contentMatch = FindByContentHash(newBook.ContentHash);
                if (contentMatch != null)
                {
                    result.IsDuplicate = true;
                    result.ExistingBook = contentMatch;
                    result.MatchType = DuplicateMatchType.ContentHash;
                    result.ComparisonScore = 0;  // Exact same file
                    result.ShouldReplace = false; // Exact same file, no need to replace
                    result.Reason = "Exact file duplicate (same content hash)";

                    Log.WriteLine(LogLevel.Warning, "Found exact duplicate file: {0}", newBook.FileName);
                    return result;
                }
            }

            // Step 2: Check by duplicate key (Title + Author + Language + Translator + Volume info)
            if (!string.IsNullOrEmpty(newBook.DuplicateKey))
            {
                var keyMatches = FindByDuplicateKey(newBook.DuplicateKey);
                if (keyMatches != null && keyMatches.Count > 0)
                {
                    // Check each potential match more carefully
                    Book actualDuplicate = null;
                    int bestScore = int.MinValue;

                    foreach (var candidate in keyMatches)
                    {
                        // Use the enhanced IsDuplicateOf method which checks translators
                        if (newBook.IsDuplicateOf(candidate))
                        {
                            int score = newBook.CompareTo(candidate);
                            if (actualDuplicate == null || score > bestScore)
                            {
                                actualDuplicate = candidate;
                                bestScore = score;
                            }
                        }
                    }

                    // If we found an actual duplicate
                    if (actualDuplicate != null)
                    {
                        result.IsDuplicate = true;
                        result.ExistingBook = actualDuplicate;
                        result.MatchType = DuplicateMatchType.DuplicateKey;
                        result.ComparisonScore = bestScore;

                        // MODIFIED: Changed logic for score = 0
                        // Now we treat books with same metadata as duplicates
                        // The archive priority is already considered in CompareTo method

                        // Check archive priorities for additional logging
                        int newPriority = newBook.GetArchivePriority();
                        int existingPriority = actualDuplicate.GetArchivePriority();

                        // Decide on replacement based on score (which includes archive priority)
                        result.ShouldReplace = bestScore > REPLACEMENT_THRESHOLD;

                        // Build detailed reason
                        result.Reason = $"Matched by title/author: '{newBook.Title}' by {newBook.Authors.FirstOrDefault()}";

                        if (newPriority > 0 && existingPriority > 0)
                        {
                            result.Reason += $" (archive: {newPriority} vs {existingPriority})";
                        }

                        if (result.ShouldReplace)
                        {
                            result.Reason += $" - replacing with newer/better version (score: {bestScore})";

                            // If there are multiple duplicates, mark older ones for replacement too
                            if (keyMatches.Count > 1)
                            {
                                foreach (var oldBook in keyMatches.Where(b => b.ID != actualDuplicate.ID))
                                {
                                    // Only mark as replaced if it's actually a duplicate
                                    if (newBook.IsDuplicateOf(oldBook))
                                    {
                                        MarkAsReplaced(oldBook.ID, newBook.ID);
                                    }
                                }
                            }
                        }
                        else
                        {
                            result.Reason += $" - keeping existing (score: {bestScore})";
                        }

                        Log.WriteLine(LogLevel.Info,
                            "Found duplicate by key: {0}, score: {1}, should replace: {2}",
                            newBook.DuplicateKey, bestScore, result.ShouldReplace);

                        return result;
                    }
                    else
                    {
                        // Same key but different books (e.g., different translations or volumes)
                        Log.WriteLine(LogLevel.Info,
                            "Books with same key but different (translators/volumes?): {0}", newBook.Title);
                    }
                }
            }

            // Not a duplicate
            return result;
        }

        /// <summary>
        /// Process duplicate - either skip, replace, or add as new
        /// MODIFIED: Simplified logic with archive priority support
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
                // Skip duplicate - existing is better or same
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
                var books = db.ExecuteQuery<Book>(
                    DatabaseSchema.SelectBooksByDuplicateKey,
                    MapBook,
                    DatabaseManager.CreateParameter("@DuplicateKey", key));

                // Load translators for proper duplicate detection
                foreach (var book in books)
                {
                    book.Translators = db.ExecuteQuery<string>(
                        DatabaseSchema.SelectBookTranslators,
                        reader => reader.GetString(0),
                        DatabaseManager.CreateParameter("@BookID", book.ID));

                    // Also load authors for complete comparison
                    book.Authors = db.ExecuteQuery<string>(
                        DatabaseSchema.SelectBookAuthors,
                        reader => reader.GetString(0),
                        DatabaseManager.CreateParameter("@BookID", book.ID));
                }

                return books;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error finding books by duplicate key: {0}", ex.Message);
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
                Log.WriteLine(LogLevel.Error, "Error marking book as replaced: {0}", ex.Message);
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

            // Authors list will be loaded separately
            book.Authors = new List<string>();

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
                var replacedCount = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE ReplacedByID IS NOT NULL");
                stats.ReplacedBooksCount = Convert.ToInt32(replacedCount);

                // Count books with trusted IDs (will be 0 after our changes)
                var trustedCount = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE DocumentIDTrusted = 1 AND ReplacedByID IS NULL");
                stats.TrustedIDCount = Convert.ToInt32(trustedCount);

                // Count duplicate groups
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
    /// Statistics about duplicates in the library
    /// </summary>
    public class DuplicateStatistics
    {
        public int ReplacedBooksCount { get; set; }
        public int TrustedIDCount { get; set; }
        public int DuplicateGroupsCount { get; set; }
    }
}