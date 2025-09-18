/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Smart duplicate detection for books - OPTIMIZED with batch loading
 * FIXED: Proper GUID-based duplicate detection for FB Editor documents
 * UPDATED: Using same trusted GUID logic as Book class
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
        TrustedID = 1,      // Valid GUID from FB Editor
        ContentHash = 2,    // Exact file content
        DuplicateKey = 3,   // Title + Author + Language match
        Fuzzy = 4           // Fuzzy matching (future)
    }

    /// <summary>
    /// Smart duplicate detector for books
    /// OPTIMIZED: Batch loading with JOIN for better performance
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

            // Step 1: Check if Document ID is a valid GUID (trusted source like FB Editor)
            if (!string.IsNullOrEmpty(newBook.ID) && IsValidTrustedGuid(newBook.ID))
            {
                // Note: DocumentIDTrusted is already set by Book.ID setter, no need to set it here

                // This is a trusted ID from FB Editor or similar
                var sameDocumentBooks = FindByDocumentID(newBook.ID);
                if (sameDocumentBooks != null && sameDocumentBooks.Count > 0)
                {
                    // Found same document, compare versions
                    var existingBook = sameDocumentBooks[0]; // Should typically be only one active

                    result.IsDuplicate = true;
                    result.ExistingBook = existingBook;
                    result.MatchType = DuplicateMatchType.TrustedID;

                    // Compare versions - higher version wins
                    if (newBook.Version > existingBook.Version)
                    {
                        result.ShouldReplace = true;
                        result.ComparisonScore = (int)((newBook.Version - existingBook.Version) * 100);
                        result.Reason = $"Newer version of same document (v{existingBook.Version:F1} → v{newBook.Version:F1})";

                        Log.WriteLine(LogLevel.Info,
                            "Found newer version: {0} v{1} → v{2}",
                            newBook.Title, existingBook.Version, newBook.Version);
                    }
                    else if (Math.Abs(newBook.Version - existingBook.Version) < 0.01f) // Same version (with float tolerance)
                    {
                        // Same version - check by document date and size
                        if (newBook.DocumentDate > existingBook.DocumentDate)
                        {
                            result.ShouldReplace = true;
                            result.ComparisonScore = 10;
                            result.Reason = $"Same version (v{newBook.Version:F1}) but newer document date";
                        }
                        else if (newBook.DocumentDate == existingBook.DocumentDate &&
                                 newBook.DocumentSize > existingBook.DocumentSize)
                        {
                            result.ShouldReplace = true;
                            result.ComparisonScore = 5;
                            result.Reason = $"Same version (v{newBook.Version:F1}) but larger file size";
                        }
                        else
                        {
                            result.ShouldReplace = false;
                            result.Reason = $"Same version (v{newBook.Version:F1}) already exists";
                        }
                    }
                    else
                    {
                        result.ShouldReplace = false;
                        result.ComparisonScore = (int)((newBook.Version - existingBook.Version) * 100);
                        result.Reason = $"Older version (v{newBook.Version:F1} < v{existingBook.Version:F1})";

                        Log.WriteLine(LogLevel.Info,
                            "Skipping older version: {0} v{1} < v{2}",
                            newBook.Title, newBook.Version, existingBook.Version);
                    }

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
                    result.ComparisonScore = 0;  // Exact same file
                    result.ShouldReplace = false; // Exact same file, no need to replace
                    result.Reason = "Exact file duplicate (same content hash)";

                    Log.WriteLine(LogLevel.Warning, "Found exact duplicate file: {0}", newBook.FileName);
                    return result;
                }
            }

            // Step 3: Check by duplicate key (Title + Author + Language + Translator + Volume info)
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
        /// Check if string is a valid and trusted GUID
        /// Using same logic as Book.IsTrustedGuid to ensure consistency
        /// </summary>
        private bool IsValidTrustedGuid(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            // Try parsing as GUID
            Guid guid;
            if (Guid.TryParse(id, out guid))
            {
                return IsTrustedGuid(guid);
            }

            return false;
        }

        /// <summary>
        /// Check if GUID is trusted (not a placeholder from bad converters)
        /// Copied from Book class for consistency
        /// </summary>
        private bool IsTrustedGuid(Guid guid)
        {
            // Check for common placeholder GUIDs
            if (guid == Guid.Empty) return false;

            string guidStr = guid.ToString().ToLowerInvariant();

            // Only check for the most obvious placeholders
            if (guidStr == "00000000-0000-0000-0000-000000000000") return false;
            if (guidStr == "11111111-1111-1111-1111-111111111111") return false;
            if (guidStr == "12345678-1234-1234-1234-123456789012") return false;
            if (guidStr == "ffffffff-ffff-ffff-ffff-ffffffffffff") return false;

            // Check for timestamp strings that are not proper GUIDs
            // LibRusEc kit generates IDs like "Mon Jun 10 19:52:43 2013" which are NOT proper GUIDs
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

            // If it passed all checks, it's likely from FictionBookEditor or similar proper tool
            return true;
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

        private List<Book> FindByDocumentID(string documentID)
        {
            try
            {
                return db.ExecuteQuery<Book>(
                    "SELECT * FROM Books WHERE ID = @ID AND ReplacedByID IS NULL",
                    MapBook,
                    DatabaseManager.CreateParameter("@ID", documentID));
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error finding books by document ID: {0}", ex.Message);
                return new List<Book>();
            }
        }

        /// <summary>
        /// Helper class for batch loading results
        /// </summary>
        private class BookDetailRow
        {
            public string BookId { get; set; }
            public Book Book { get; set; }
            public string ItemType { get; set; }
            public string ItemName { get; set; }
        }

        /// <summary>
        /// OPTIMIZED: Find books by duplicate key with batch loading of authors and translators
        /// Uses single query with UNION ALL instead of N+1 queries
        /// </summary>
        private List<Book> FindByDuplicateKey(string key)
        {
            try
            {
                var booksDict = new Dictionary<string, Book>();

                // Single query to get all books with their authors and translators
                var rows = db.ExecuteQuery<BookDetailRow>(
                    DatabaseSchema.SelectBooksWithDetailsByDuplicateKey,
                    reader =>
                    {
                        return new BookDetailRow
                        {
                            BookId = DatabaseManager.GetString(reader, "ID"),
                            Book = MapBook(reader),
                            ItemType = DatabaseManager.GetString(reader, "ItemType"),
                            ItemName = DatabaseManager.GetString(reader, "ItemName")
                        };
                    },
                    DatabaseManager.CreateParameter("@DuplicateKey", key));

                // Process rows to build books with their authors and translators
                foreach (var row in rows)
                {
                    if (!booksDict.ContainsKey(row.BookId))
                    {
                        row.Book.Authors = new List<string>();
                        row.Book.Translators = new List<string>();
                        booksDict[row.BookId] = row.Book;
                    }

                    // Add author or translator based on ItemType
                    if (!string.IsNullOrEmpty(row.ItemName))
                    {
                        if (row.ItemType == "AUTHOR")
                        {
                            if (!booksDict[row.BookId].Authors.Contains(row.ItemName))
                                booksDict[row.BookId].Authors.Add(row.ItemName);
                        }
                        else if (row.ItemType == "TRANSLATOR")
                        {
                            if (!booksDict[row.BookId].Translators.Contains(row.ItemName))
                                booksDict[row.BookId].Translators.Add(row.ItemName);
                        }
                    }
                }

                return booksDict.Values.ToList();
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
                Annotation = DatabaseManager.GetString(reader, "Annotation"),
                DocumentSize = DatabaseManager.GetUInt32(reader, "DocumentSize"),
                DocumentIDTrusted = DatabaseManager.GetBoolean(reader, "DocumentIDTrusted"),
                DuplicateKey = DatabaseManager.GetString(reader, "DuplicateKey"),
                ReplacedByID = DatabaseManager.GetString(reader, "ReplacedByID"),
                ContentHash = DatabaseManager.GetString(reader, "ContentHash")
            };

            // Note: Sequence and NumberInSequence are now in separate table
            // They should be loaded separately if needed

            var bookDate = DatabaseManager.GetDateTime(reader, "BookDate");
            if (bookDate.HasValue) book.BookDate = bookDate.Value;

            var docDate = DatabaseManager.GetDateTime(reader, "DocumentDate");
            if (docDate.HasValue) book.DocumentDate = docDate.Value;

            var addedDate = DatabaseManager.GetDateTime(reader, "AddedDate");
            if (addedDate.HasValue) book.AddedDate = addedDate.Value;

            // Authors and Translators will be populated in FindByDuplicateKey
            book.Authors = new List<string>();
            book.Translators = new List<string>();

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

                // Count books with trusted IDs
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