/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Repository for Book operations with SQLite database with FTS5 support
 *
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace TinyOPDS.Data
{
    public class BookRepository
    {
        private readonly DatabaseManager db;
        private readonly HashSet<string> validGenreTags;
        private readonly DuplicateDetector duplicateDetector;

        // Thread safety for batch operations
        private static readonly object batchLock = new object();
        private static bool pragmasOptimized = false;

        public BookRepository(DatabaseManager database)
        {
            db = database;
            validGenreTags = new HashSet<string>();
            LoadValidGenreTags();

            // Initialize duplicate detector (normal mode by default)
            duplicateDetector = new DuplicateDetector(database);
        }

        #region Language Filter Support

        /// <summary>
        /// Get language filter based on settings
        /// Returns language code or null if filtering is disabled
        /// </summary>
        private string GetLanguageFilter()
        {
            if (!Properties.Settings.Default.FilterBooksByInterfaceLanguage) return null;
            return Properties.Settings.Default.Language;
        }

        /// <summary>
        /// Apply language filter to SQL query
        /// </summary>
        private string ApplyLanguageFilter(string baseQuery)
        {
            string languageFilter = GetLanguageFilter();
            if (string.IsNullOrEmpty(languageFilter))
                return baseQuery;

            // Add language filter to WHERE clause
            if (baseQuery.Contains("WHERE"))
            {
                // Insert after WHERE and before ORDER BY if exists
                if (baseQuery.Contains("ORDER BY"))
                {
                    int orderByIndex = baseQuery.IndexOf("ORDER BY");
                    string beforeOrderBy = baseQuery.Substring(0, orderByIndex);
                    string orderByClause = baseQuery.Substring(orderByIndex);
                    return beforeOrderBy + " AND b.Language = @Language " + orderByClause;
                }
                else
                {
                    return baseQuery + " AND b.Language = @Language";
                }
            }
            else
            {
                // Add WHERE clause before ORDER BY if exists
                if (baseQuery.Contains("ORDER BY"))
                {
                    int orderByIndex = baseQuery.IndexOf("ORDER BY");
                    string beforeOrderBy = baseQuery.Substring(0, orderByIndex);
                    string orderByClause = baseQuery.Substring(orderByIndex);
                    return beforeOrderBy + " WHERE b.Language = @Language " + orderByClause;
                }
                else
                {
                    return baseQuery + " WHERE b.Language = @Language";
                }
            }
        }

        /// <summary>
        /// Create language parameter if filtering is enabled
        /// </summary>
        private IDbDataParameter[] AddLanguageParameter(params IDbDataParameter[] existingParams)
        {
            string languageFilter = GetLanguageFilter();
            if (string.IsNullOrEmpty(languageFilter))
                return existingParams;

            var paramsList = new List<IDbDataParameter>(existingParams)
            {
                DatabaseManager.CreateParameter("@Language", languageFilter)
            };
            return paramsList.ToArray();
        }

        #endregion

        /// <summary>
        /// Load valid genre tags from database for validation
        /// </summary>
        private void LoadValidGenreTags()
        {
            try
            {
                validGenreTags.Clear();
                var tags = db.ExecuteQuery<string>(
                    "SELECT Tag FROM Genres",
                    reader => reader.GetString(0));

                foreach (var tag in tags)
                {
                    validGenreTags.Add(tag);
                }

                Log.WriteLine(LogLevel.Info, "Loaded {0} valid genre tags from database", validGenreTags.Count);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error loading valid genre tags: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Reload valid genre tags (call after genres table is updated)
        /// </summary>
        public void RefreshValidGenreTags()
        {
            LoadValidGenreTags();
        }

        /// <summary>
        /// Validate genre tag against database
        /// </summary>
        private bool IsValidGenreTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return false;

            // If cache is empty, try to reload
            if (validGenreTags.Count == 0)
            {
                LoadValidGenreTags();
            }

            return validGenreTags.Contains(tag);
        }

        /// <summary>
        /// Result of batch operations with detailed statistics
        /// </summary>
        public class BatchResult
        {
            public int TotalProcessed { get; set; }
            public int Added { get; set; }
            public int Duplicates { get; set; }
            public int Replaced { get; set; }
            public int Errors { get; set; }
            public int FB2Count { get; set; }
            public int EPUBCount { get; set; }
            public int InvalidGenresSkipped { get; set; }
            public TimeSpan ProcessingTime { get; set; }
            public List<string> ErrorMessages { get; set; } = new List<string>();
            public List<string> InvalidGenreTags { get; set; } = new List<string>();
            public List<string> ReplacedBooks { get; set; } = new List<string>();
            public int UncertainDuplicatesAdded { get; set; } = 0;

            public bool IsSuccess => Added > 0 || Duplicates > 0 || Replaced > 0;
        }

        #region Book CRUD Operations

        /// <summary>
        /// Add a single book to the database with duplicate detection and validation
        /// Thread-safe version using IDbTransaction
        /// </summary>
        /// <param name="book">Book object to add to database</param>
        /// <returns>true if book was successfully added, false if it was a duplicate or error occurred</returns>
        public bool AddBook(Book book)
        {
            IDbTransaction transaction = null;
            try
            {
                // Generate duplicate keys
                if (string.IsNullOrEmpty(book.DuplicateKey))
                    book.DuplicateKey = book.GenerateDuplicateKey();

                // Check for duplicates using DuplicateDetector
                Stream fileStream = null;
                try
                {
                    // Try to open file for content hash generation
                    if (File.Exists(book.FilePath))
                    {
                        fileStream = new FileStream(book.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }

                    var duplicateResult = duplicateDetector.CheckDuplicate(book, fileStream);

                    if (duplicateResult.IsDuplicate)
                    {
                        if (!duplicateDetector.ProcessDuplicate(book, duplicateResult))
                        {
                            // Only return false if it's a definite duplicate that shouldn't be added
                            Log.WriteLine(LogLevel.Info, "Skipping definite duplicate book: {0} - {1}",
                                book.FileName, duplicateResult.Reason);
                            return false;
                        }

                        // If ProcessDuplicate returned true, either replace or add as new
                        if (!duplicateResult.ShouldReplace)
                        {
                            // This is an uncertain duplicate that should be added as new
                            Log.WriteLine(LogLevel.Info, "Adding uncertain duplicate as new book: {0}",
                                book.FileName);
                        }
                    }
                }
                finally
                {
                    fileStream?.Dispose();
                }

                // Use new transaction method
                transaction = db.BeginTransactionWithObject();

                // Insert book WITHOUT Sequence/NumberInSequence fields
                var bookParams = new[]
                {
                    DatabaseManager.CreateParameter("@ID", book.ID),
                    DatabaseManager.CreateParameter("@Version", book.Version),
                    DatabaseManager.CreateParameter("@FileName", book.FileName),
                    DatabaseManager.CreateParameter("@Title", book.Title),
                    DatabaseManager.CreateParameter("@Language", book.Language),
                    DatabaseManager.CreateParameter("@BookDate", book.BookDate.ToBinary()),
                    DatabaseManager.CreateParameter("@DocumentDate", book.DocumentDate.ToBinary()),
                    DatabaseManager.CreateParameter("@Annotation", book.Annotation),
                    DatabaseManager.CreateParameter("@DocumentSize", (long)book.DocumentSize),
                    DatabaseManager.CreateParameter("@AddedDate", book.AddedDate.ToBinary()),
                    DatabaseManager.CreateParameter("@DocumentIDTrusted", book.DocumentIDTrusted),
                    DatabaseManager.CreateParameter("@DuplicateKey", book.DuplicateKey),
                    DatabaseManager.CreateParameter("@ReplacedByID", book.ReplacedByID),
                    DatabaseManager.CreateParameter("@ContentHash", book.ContentHash)
                };

                db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertBook, transaction, bookParams);

                // Clear existing relationships
                db.ExecuteNonQueryInTransaction("DELETE FROM BookAuthors WHERE BookID = @BookID", transaction,
                    DatabaseManager.CreateParameter("@BookID", book.ID));
                db.ExecuteNonQueryInTransaction("DELETE FROM BookGenres WHERE BookID = @BookID", transaction,
                    DatabaseManager.CreateParameter("@BookID", book.ID));
                db.ExecuteNonQueryInTransaction("DELETE FROM BookTranslators WHERE BookID = @BookID", transaction,
                    DatabaseManager.CreateParameter("@BookID", book.ID));

                // Clear existing sequences
                db.ExecuteNonQueryInTransaction("DELETE FROM BookSequences WHERE BookID = @BookID", transaction,
                    DatabaseManager.CreateParameter("@BookID", book.ID));

                // Add authors with enhanced fields
                foreach (var authorName in book.Authors)
                {
                    var (firstName, middleName, lastName) = ParseAuthorName(authorName);
                    string searchName = NormalizeForSearch(authorName);
                    string lastNameSoundex = !string.IsNullOrEmpty(lastName) ? StringUtils.Soundex(lastName) : "";
                    string nameTranslit = GenerateTransliteration(authorName);

                    // Insert author if not exists
                    db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertAuthor, transaction,
                        DatabaseManager.CreateParameter("@Name", authorName),
                        DatabaseManager.CreateParameter("@FirstName", firstName),
                        DatabaseManager.CreateParameter("@MiddleName", middleName),
                        DatabaseManager.CreateParameter("@LastName", lastName),
                        DatabaseManager.CreateParameter("@SearchName", searchName),
                        DatabaseManager.CreateParameter("@LastNameSoundex", lastNameSoundex),
                        DatabaseManager.CreateParameter("@NameTranslit", nameTranslit));

                    // Link book to author
                    db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertBookAuthor, transaction,
                        DatabaseManager.CreateParameter("@BookID", book.ID),
                        DatabaseManager.CreateParameter("@AuthorName", authorName));
                }

                // Add genres with validation
                int validGenreCount = 0;
                foreach (var genreTag in book.Genres)
                {
                    if (IsValidGenreTag(genreTag))
                    {
                        db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertBookGenre, transaction,
                            DatabaseManager.CreateParameter("@BookID", book.ID),
                            DatabaseManager.CreateParameter("@GenreTag", genreTag));
                        validGenreCount++;
                    }
                    else
                    {
                        Log.WriteLine(LogLevel.Warning, "Invalid genre tag '{0}' for book {1}, skipping",
                            genreTag, book.FileName);
                    }
                }

                // If no valid genres were added, log warning but don't fail
                if (validGenreCount == 0 && book.Genres.Count > 0)
                {
                    Log.WriteLine(LogLevel.Warning, "No valid genres added for book {0} (had {1} invalid genres)",
                        book.FileName, book.Genres.Count);
                }

                // Add translators
                foreach (var translatorName in book.Translators)
                {
                    db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertTranslator, transaction,
                        DatabaseManager.CreateParameter("@Name", translatorName));

                    db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertBookTranslator, transaction,
                        DatabaseManager.CreateParameter("@BookID", book.ID),
                        DatabaseManager.CreateParameter("@TranslatorName", translatorName));
                }

                // Add sequences
                if (book.Sequences != null)
                {
                    foreach (var sequenceInfo in book.Sequences)
                    {
                        if (!string.IsNullOrEmpty(sequenceInfo.Name))
                        {
                            // Insert sequence if not exists
                            string searchName = NormalizeForSearch(sequenceInfo.Name);
                            db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertSequence, transaction,
                                DatabaseManager.CreateParameter("@Name", sequenceInfo.Name),
                                DatabaseManager.CreateParameter("@SearchName", searchName));

                            // Link book to sequence
                            db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertBookSequence, transaction,
                                DatabaseManager.CreateParameter("@BookID", book.ID),
                                DatabaseManager.CreateParameter("@SequenceName", sequenceInfo.Name),
                                DatabaseManager.CreateParameter("@NumberInSequence", (long)sequenceInfo.NumberInSequence));
                        }
                    }
                }

                transaction.Commit();
                return true;
            }
            catch (Exception ex)
            {
                transaction?.Rollback();
                Log.WriteLine(LogLevel.Error, "Error adding book {0}: {1}", book.FileName, ex.Message);
                return false;
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        /// <summary>
        /// Add multiple books in batch with FTS5 synchronization and duplicate detection
        /// Thread-safe version with proper transaction management
        /// </summary>
        public BatchResult AddBooksBatch(List<Book> books)
        {
            var result = new BatchResult();
            var startTime = DateTime.Now;

            if (books == null || books.Count == 0)
            {
                result.ProcessingTime = DateTime.Now - startTime;
                return result;
            }

            result.TotalProcessed = books.Count;

            // Ensure thread safety for batch operations
            lock (batchLock)
            {
                try
                {
                    // Optimize SQLite settings ONCE, outside of transaction
                    if (!pragmasOptimized)
                    {
                        try
                        {
                            db.ExecuteNonQuery("PRAGMA synchronous = OFF");
                            db.ExecuteNonQuery("PRAGMA journal_mode = MEMORY");
                            db.ExecuteNonQuery("PRAGMA temp_store = MEMORY");
                            db.ExecuteNonQuery("PRAGMA cache_size = 10000");
                            pragmasOptimized = true;
                        }
                        catch (Exception ex)
                        {
                            Log.WriteLine(LogLevel.Warning, "Could not optimize SQLite settings: {0}", ex.Message);
                        }
                    }

                    // Create in-memory indices for batch duplicate detection
                    var batchByDocumentID = new Dictionary<string, Book>();
                    var batchByContentHash = new Dictionary<string, Book>();
                    var batchByDuplicateKey = new Dictionary<string, List<Book>>();

                    // Track which books to actually add (some may be skipped due to in-batch duplicates)
                    var booksToAdd = new List<Book>();
                    var inBatchReplacements = new Dictionary<string, string>(); // old_id -> new_id mappings

                    // FIRST PASS: Analyze batch for internal duplicates
                    Log.WriteLine(LogLevel.Info, "Analyzing batch of {0} books for duplicates...", books.Count);

                    foreach (var book in books)
                    {
                        DuplicateCheckResult duplicateResult = null;
                        bool skipBook = false;
                        Book replacedBook = null;

                        try
                        {
                            // Generate duplicate key
                            if (string.IsNullOrEmpty(book.DuplicateKey))
                                book.DuplicateKey = book.GenerateDuplicateKey();

                            // Generate content hash if file exists
                            Stream fileStream = null;
                            try
                            {
                                if (File.Exists(book.FilePath))
                                {
                                    fileStream = new FileStream(book.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                                    if (string.IsNullOrEmpty(book.ContentHash))
                                        book.ContentHash = book.GenerateContentHash(fileStream);
                                }

                                // STEP 1: Check for duplicates within the current batch
                                bool foundInBatch = false;

                                // Check by trusted Document ID
                                if (book.DocumentIDTrusted && !string.IsNullOrEmpty(book.ID))
                                {
                                    if (batchByDocumentID.ContainsKey(book.ID))
                                    {
                                        var existingInBatch = batchByDocumentID[book.ID];
                                        int comparisonScore = book.CompareTo(existingInBatch);

                                        if (comparisonScore > 0)
                                        {
                                            // New book is better - replace the old one
                                            Log.WriteLine(LogLevel.Info,
                                                "In-batch replacement: {0} v{1} replaces v{2}",
                                                book.Title, book.Version, existingInBatch.Version);

                                            // Remove old book from all indices
                                            RemoveFromBatchIndices(existingInBatch, batchByDocumentID, batchByContentHash, batchByDuplicateKey);
                                            booksToAdd.Remove(existingInBatch);

                                            // Track the replacement
                                            inBatchReplacements[existingInBatch.ID] = book.ID;
                                            replacedBook = existingInBatch;
                                            foundInBatch = false; // Allow this book to be added
                                            result.Replaced++;
                                        }
                                        else
                                        {
                                            // Existing book is better or same - skip new one
                                            Log.WriteLine(LogLevel.Info,
                                                "In-batch duplicate skipped: {0} v{1} (keeping v{2})",
                                                book.Title, book.Version, existingInBatch.Version);
                                            skipBook = true;
                                            foundInBatch = true;
                                            result.Duplicates++;
                                        }
                                    }
                                }

                                // Check by content hash
                                if (!foundInBatch && !string.IsNullOrEmpty(book.ContentHash))
                                {
                                    if (batchByContentHash.ContainsKey(book.ContentHash))
                                    {
                                        // Exact duplicate in batch - skip
                                        Log.WriteLine(LogLevel.Info,
                                            "In-batch exact duplicate (same content): {0}", book.FileName);
                                        skipBook = true;
                                        foundInBatch = true;
                                        result.Duplicates++;
                                    }
                                }

                                // Check by duplicate key
                                if (!foundInBatch && !string.IsNullOrEmpty(book.DuplicateKey))
                                {
                                    if (batchByDuplicateKey.ContainsKey(book.DuplicateKey))
                                    {
                                        // Check each potential duplicate more carefully
                                        var potentialDuplicates = batchByDuplicateKey[book.DuplicateKey].ToList();
                                        foreach (var existingInBatch in potentialDuplicates)
                                        {
                                            if (book.IsDuplicateOf(existingInBatch))
                                            {
                                                int comparisonScore = book.CompareTo(existingInBatch);

                                                if (comparisonScore > 0)
                                                {
                                                    // New book is better
                                                    Log.WriteLine(LogLevel.Info,
                                                        "In-batch replacement by key: {0} replaces {1}",
                                                        book.FileName, existingInBatch.FileName);

                                                    // Remove old book
                                                    RemoveFromBatchIndices(existingInBatch, batchByDocumentID, batchByContentHash, batchByDuplicateKey);
                                                    booksToAdd.Remove(existingInBatch);

                                                    replacedBook = existingInBatch;
                                                    result.Replaced++;
                                                }
                                                else
                                                {
                                                    // Existing is better - skip new
                                                    Log.WriteLine(LogLevel.Info,
                                                        "In-batch duplicate by key skipped: {0}", book.FileName);
                                                    skipBook = true;
                                                    foundInBatch = true;
                                                    result.Duplicates++;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }

                                // STEP 2: If not found in batch, check database
                                if (!skipBook && !foundInBatch)
                                {
                                    duplicateResult = duplicateDetector.CheckDuplicate(book, fileStream);

                                    if (duplicateResult.IsDuplicate)
                                    {
                                        bool shouldAdd = duplicateDetector.ProcessDuplicate(book, duplicateResult);

                                        if (!shouldAdd)
                                        {
                                            skipBook = true;
                                            result.Duplicates++;
                                        }
                                        else if (duplicateResult.ShouldReplace)
                                        {
                                            result.Replaced++;
                                            result.Duplicates++;
                                            result.ReplacedBooks.Add($"{duplicateResult.ExistingBook.FileName} -> {book.FileName}");
                                        }
                                        else
                                        {
                                            result.UncertainDuplicatesAdded++;
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                fileStream?.Dispose();
                            }

                            // If book should be added, add it to batch indices and list
                            if (!skipBook)
                            {
                                // Add to batch indices for future duplicate checking
                                AddToBatchIndices(book, batchByDocumentID, batchByContentHash, batchByDuplicateKey);
                                booksToAdd.Add(book);
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Errors++;
                            result.ErrorMessages.Add($"Book {book.FileName}: {ex.Message}");
                            Log.WriteLine(LogLevel.Error, "Error processing book {0}: {1}", book.FileName, ex.Message);
                        }
                    }

                    Log.WriteLine(LogLevel.Info,
                        "Batch analysis complete. Adding {0} books out of {1} total",
                        booksToAdd.Count, books.Count);

                    // SECOND PASS: Actually add the filtered books to database
                    // Use proper transaction management
                    IDbTransaction transaction = null;
                    try
                    {
                        transaction = db.BeginTransactionWithObject();

                        foreach (var book in booksToAdd)
                        {
                            try
                            {
                                // Insert book WITHOUT Sequence/NumberInSequence fields
                                var bookParams = new[]
                                {
                                    DatabaseManager.CreateParameter("@ID", book.ID),
                                    DatabaseManager.CreateParameter("@Version", book.Version),
                                    DatabaseManager.CreateParameter("@FileName", book.FileName),
                                    DatabaseManager.CreateParameter("@Title", book.Title),
                                    DatabaseManager.CreateParameter("@Language", book.Language),
                                    DatabaseManager.CreateParameter("@BookDate", book.BookDate.ToBinary()),
                                    DatabaseManager.CreateParameter("@DocumentDate", book.DocumentDate.ToBinary()),
                                    DatabaseManager.CreateParameter("@Annotation", book.Annotation),
                                    DatabaseManager.CreateParameter("@DocumentSize", (long)book.DocumentSize),
                                    DatabaseManager.CreateParameter("@AddedDate", book.AddedDate.ToBinary()),
                                    DatabaseManager.CreateParameter("@DocumentIDTrusted", book.DocumentIDTrusted),
                                    DatabaseManager.CreateParameter("@DuplicateKey", book.DuplicateKey),
                                    DatabaseManager.CreateParameter("@ReplacedByID", book.ReplacedByID),
                                    DatabaseManager.CreateParameter("@ContentHash", book.ContentHash)
                                };
                                db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertBook, transaction, bookParams);

                                // Insert authors and relationships
                                foreach (var authorName in book.Authors)
                                {
                                    var (firstName, middleName, lastName) = ParseAuthorName(authorName);
                                    string searchName = NormalizeForSearch(authorName);
                                    string lastNameSoundex = !string.IsNullOrEmpty(lastName) ? StringUtils.Soundex(lastName) : "";
                                    string nameTranslit = GenerateTransliteration(authorName);

                                    db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertAuthor, transaction,
                                        DatabaseManager.CreateParameter("@Name", authorName),
                                        DatabaseManager.CreateParameter("@FirstName", firstName),
                                        DatabaseManager.CreateParameter("@MiddleName", middleName),
                                        DatabaseManager.CreateParameter("@LastName", lastName),
                                        DatabaseManager.CreateParameter("@SearchName", searchName),
                                        DatabaseManager.CreateParameter("@LastNameSoundex", lastNameSoundex),
                                        DatabaseManager.CreateParameter("@NameTranslit", nameTranslit));

                                    db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertBookAuthor, transaction,
                                        DatabaseManager.CreateParameter("@BookID", book.ID),
                                        DatabaseManager.CreateParameter("@AuthorName", authorName));
                                }

                                // Insert genres with validation
                                foreach (var genreTag in book.Genres)
                                {
                                    if (IsValidGenreTag(genreTag))
                                    {
                                        db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertBookGenre, transaction,
                                            DatabaseManager.CreateParameter("@BookID", book.ID),
                                            DatabaseManager.CreateParameter("@GenreTag", genreTag));
                                    }
                                    else
                                    {
                                        result.InvalidGenresSkipped++;
                                        if (!result.InvalidGenreTags.Contains(genreTag))
                                        {
                                            result.InvalidGenreTags.Add(genreTag);
                                        }
                                    }
                                }

                                // Insert translators and relationships
                                foreach (var translatorName in book.Translators)
                                {
                                    db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertTranslator, transaction,
                                        DatabaseManager.CreateParameter("@Name", translatorName));

                                    db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertBookTranslator, transaction,
                                        DatabaseManager.CreateParameter("@BookID", book.ID),
                                        DatabaseManager.CreateParameter("@TranslatorName", translatorName));
                                }

                                // Insert sequences
                                if (book.Sequences != null)
                                {
                                    foreach (var sequenceInfo in book.Sequences)
                                    {
                                        if (!string.IsNullOrEmpty(sequenceInfo.Name))
                                        {
                                            string searchName = NormalizeForSearch(sequenceInfo.Name);
                                            db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertSequence, transaction,
                                                DatabaseManager.CreateParameter("@Name", sequenceInfo.Name),
                                                DatabaseManager.CreateParameter("@SearchName", searchName));

                                            db.ExecuteNonQueryInTransaction(DatabaseSchema.InsertBookSequence, transaction,
                                                DatabaseManager.CreateParameter("@BookID", book.ID),
                                                DatabaseManager.CreateParameter("@SequenceName", sequenceInfo.Name),
                                                DatabaseManager.CreateParameter("@NumberInSequence", (long)sequenceInfo.NumberInSequence));
                                        }
                                    }
                                }

                                result.Added++;

                                // Count by type for statistics
                                if (book.BookType == BookType.FB2)
                                    result.FB2Count++;
                                else
                                    result.EPUBCount++;
                            }
                            catch (Exception ex)
                            {
                                result.Errors++;
                                result.Added--; // Compensate for the increment above
                                result.ErrorMessages.Add($"Book {book.FileName}: {ex.Message}");
                                Log.WriteLine(LogLevel.Error, "BookRepository.AddBooksBatch - Book {0}: {1}", book.FileName, ex.Message);
                            }
                        }

                        transaction.Commit();
                        result.ProcessingTime = DateTime.Now - startTime;

                        // Log statistics
                        if (result.InvalidGenresSkipped > 0)
                        {
                            Log.WriteLine(LogLevel.Warning, "Batch insert: {0} invalid genre tags skipped. Tags: {1}",
                                result.InvalidGenresSkipped, string.Join(", ", result.InvalidGenreTags));
                        }

                        if (result.Replaced > 0)
                        {
                            Log.WriteLine(LogLevel.Info, "Batch insert: {0} books replaced with newer versions", result.Replaced);
                        }

                        if (result.UncertainDuplicatesAdded > 0)
                        {
                            Log.WriteLine(LogLevel.Info, "Batch insert: {0} uncertain duplicates added as new books",
                                result.UncertainDuplicatesAdded);
                        }

                        Log.WriteLine(LogLevel.Info,
                            "Batch processing complete: Added {0}, Duplicates {1}, Replaced {2}, Errors {3}",
                            result.Added, result.Duplicates, result.Replaced, result.Errors);

                        return result;
                    }
                    catch (Exception ex)
                    {
                        transaction?.Rollback();
                        result.Errors = result.TotalProcessed;
                        result.ErrorMessages.Add($"Batch transaction failed: {ex.Message}");
                        result.ProcessingTime = DateTime.Now - startTime;
                        Log.WriteLine(LogLevel.Error, "BookRepository.AddBooksBatch: {0}", ex.Message);
                        return result;
                    }
                    finally
                    {
                        transaction?.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    result.Errors = result.TotalProcessed;
                    result.ErrorMessages.Add($"Batch processing failed: {ex.Message}");
                    result.ProcessingTime = DateTime.Now - startTime;
                    Log.WriteLine(LogLevel.Error, "BookRepository.AddBooksBatch outer exception: {0}", ex.Message);
                    return result;
                }
            } // end of lock
        }

        #region Helper Methods for Batch Duplicate Detection

        /// <summary>
        /// Add book to batch tracking indices
        /// </summary>
        private void AddToBatchIndices(Book book,
            Dictionary<string, Book> byDocumentID,
            Dictionary<string, Book> byContentHash,
            Dictionary<string, List<Book>> byDuplicateKey)
        {
            // Add to Document ID index if trusted
            if (book.DocumentIDTrusted && !string.IsNullOrEmpty(book.ID))
            {
                byDocumentID[book.ID] = book;
            }

            // Add to content hash index
            if (!string.IsNullOrEmpty(book.ContentHash))
            {
                byContentHash[book.ContentHash] = book;
            }

            // Add to duplicate key index
            if (!string.IsNullOrEmpty(book.DuplicateKey))
            {
                if (!byDuplicateKey.ContainsKey(book.DuplicateKey))
                {
                    byDuplicateKey[book.DuplicateKey] = new List<Book>();
                }
                byDuplicateKey[book.DuplicateKey].Add(book);
            }
        }

        /// <summary>
        /// Remove book from batch tracking indices
        /// </summary>
        private void RemoveFromBatchIndices(Book book,
            Dictionary<string, Book> byDocumentID,
            Dictionary<string, Book> byContentHash,
            Dictionary<string, List<Book>> byDuplicateKey)
        {
            // Remove from Document ID index
            if (book.DocumentIDTrusted && !string.IsNullOrEmpty(book.ID) && byDocumentID.ContainsKey(book.ID))
            {
                byDocumentID.Remove(book.ID);
            }

            // Remove from content hash index
            if (!string.IsNullOrEmpty(book.ContentHash) && byContentHash.ContainsKey(book.ContentHash))
            {
                byContentHash.Remove(book.ContentHash);
            }

            // Remove from duplicate key index
            if (!string.IsNullOrEmpty(book.DuplicateKey) && byDuplicateKey.ContainsKey(book.DuplicateKey))
            {
                byDuplicateKey[book.DuplicateKey].Remove(book);
                if (byDuplicateKey[book.DuplicateKey].Count == 0)
                {
                    byDuplicateKey.Remove(book.DuplicateKey);
                }
            }
        }

        #endregion

        public Book GetBookById(string id)
        {
            var book = db.ExecuteQuerySingle<Book>(DatabaseSchema.SelectBookById, MapBook,
                DatabaseManager.CreateParameter("@ID", id));

            if (book != null)
            {
                LoadBookRelations(book);
            }

            return book;
        }

        public Book GetBookByFileName(string fileName)
        {
            var book = db.ExecuteQuerySingle<Book>(DatabaseSchema.SelectBookByFileName, MapBook,
                DatabaseManager.CreateParameter("@FileName", fileName));

            if (book != null)
            {
                LoadBookRelations(book);
            }

            return book;
        }

        public bool DeleteBook(string id)
        {
            try
            {
                // Triggers will handle FTS cleanup
                var result = db.ExecuteNonQuery(DatabaseSchema.DeleteBook,
                    DatabaseManager.CreateParameter("@ID", id));

                return result > 0;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error deleting book {0}: {1}", id, ex.Message);
                return false;
            }
        }

        public bool DeleteBookByFileName(string fileName)
        {
            try
            {
                // Triggers will handle FTS cleanup
                var result = db.ExecuteNonQuery(DatabaseSchema.DeleteBookByFileName,
                    DatabaseManager.CreateParameter("@FileName", fileName));

                return result > 0;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error deleting book by filename {0}: {1}", fileName, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Cross-platform compatible BookExists method
        /// </summary>
        public bool BookExists(string fileName)
        {
            string languageFilter = GetLanguageFilter();

            if (string.IsNullOrEmpty(languageFilter))
            {
                var count = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE FileName = @FileName AND ReplacedByID IS NULL",
                    DatabaseManager.CreateParameter("@FileName", fileName));
                // Use Convert.ToInt32 for cross-platform compatibility
                return Convert.ToInt32(count ?? 0) > 0;
            }
            else
            {
                var count = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE FileName = @FileName AND ReplacedByID IS NULL AND Language = @Language",
                    DatabaseManager.CreateParameter("@FileName", fileName),
                    DatabaseManager.CreateParameter("@Language", languageFilter));
                // Use Convert.ToInt32 for cross-platform compatibility
                return Convert.ToInt32(count ?? 0) > 0;
            }
        }

        #endregion

        #region Book Queries

        public List<Book> GetAllBooks()
        {
            string query = ApplyLanguageFilter(DatabaseSchema.SelectAllBooks);
            var books = db.ExecuteQuery<Book>(query, MapBook, AddLanguageParameter());

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksByAuthor(string authorName)
        {
            string query = ApplyLanguageFilter(DatabaseSchema.SelectBooksByAuthor);
            var books = db.ExecuteQuery<Book>(query, MapBook,
                AddLanguageParameter(DatabaseManager.CreateParameter("@AuthorName", authorName)));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        // Uses optimized query with JOIN and language filter
        public List<Book> GetBooksBySequence(string sequence)
        {
            string query = ApplyLanguageFilter(DatabaseSchema.SelectBooksBySequence);
            var books = db.ExecuteQuery<Book>(query,
                reader => {
                    var book = MapBook(reader);
                    // Get NumberInSequence from the joined query for this specific sequence
                    try
                    {
                        var number = DatabaseManager.GetUInt32(reader, "NumberInSequence");
                        if (book.Sequences == null)
                            book.Sequences = new List<BookSequenceInfo>();

                        // Temporarily store the correct number for the requested sequence
                        book.Sequences.Add(new BookSequenceInfo(sequence, number));
                    }
                    catch { }
                    return book;
                },
                AddLanguageParameter(DatabaseManager.CreateParameter("@SequenceName", sequence)));

            foreach (var book in books)
            {
                // Store the correct sequence number before loading all relations
                uint correctNumber = 0;
                var requestedSequence = book.Sequences?.FirstOrDefault(s => s.Name == sequence);
                if (requestedSequence != null)
                {
                    correctNumber = requestedSequence.NumberInSequence;
                }

                // Load all book relations (this will overwrite Sequences)
                LoadBookRelations(book);

                // Restore the correct sequence number for the requested sequence
                // by ensuring it's the first in the list (for compatibility properties)
                if (book.Sequences != null && correctNumber > 0)
                {
                    // Remove the requested sequence if it exists
                    book.Sequences.RemoveAll(s => s.Name == sequence);

                    // Insert it at the beginning with the correct number
                    book.Sequences.Insert(0, new BookSequenceInfo(sequence, correctNumber));
                }
            }

            return books;
        }

        public List<Book> GetBooksByGenre(string genreTag)
        {
            string query = ApplyLanguageFilter(DatabaseSchema.SelectBooksByGenre);
            var books = db.ExecuteQuery<Book>(query, MapBook,
                AddLanguageParameter(DatabaseManager.CreateParameter("@GenreTag", genreTag)));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksByTitle(string title)
        {
            // Apply language filter to LIKE search
            string languageFilter = GetLanguageFilter();
            string query = DatabaseSchema.SelectBooksByTitleLike;

            if (!string.IsNullOrEmpty(languageFilter))
            {
                query = query.Replace("WHERE", "WHERE Language = @Language AND");
            }

            var books = db.ExecuteQuery<Book>(query, MapBook,
                AddLanguageParameter(DatabaseManager.CreateParameter("@Title", title)));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        /// <summary>
        /// Get books by title - OpenSearch version (just calls GetBooksForOpenSearch)
        /// </summary>
        public List<Book> GetBooksByTitle(string title, bool isOpenSearch)
        {
            return isOpenSearch ? GetBooksForOpenSearch(title) : GetBooksByTitle(title);
        }

        /// <summary>
        /// Get books for OpenSearch using FTS5 with transliteration fallback
        /// </summary>
        public List<Book> GetBooksForOpenSearch(string searchPattern)
        {
            try
            {
                if (string.IsNullOrEmpty(searchPattern))
                {
                    return new List<Book>();
                }

                Log.WriteLine(LogLevel.Info, "GetBooksForOpenSearch: searching for '{0}'", searchPattern);

                var books = new List<Book>();
                string languageFilter = GetLanguageFilter();

                // Prepare FTS search pattern with wildcards for partial matching
                string ftsPattern = PrepareSearchPatternForFTS(searchPattern);

                // Apply language filter to FTS query
                string ftsQuery = DatabaseSchema.SelectBooksByTitleFTS;
                if (!string.IsNullOrEmpty(languageFilter))
                {
                    ftsQuery = ftsQuery.Replace("WHERE", "WHERE b.Language = @Language AND");
                }

                // First try FTS5 search
                books = db.ExecuteQuery<Book>(ftsQuery, MapBook,
                    AddLanguageParameter(
                        DatabaseManager.CreateParameter("@SearchPattern", ftsPattern),
                        DatabaseManager.CreateParameter("@LikePattern", searchPattern)));

                // If no results and contains Latin letters, try transliteration
                if (books.Count == 0 && ContainsLatinLetters(searchPattern))
                {
                    Log.WriteLine(LogLevel.Info, "GetBooksForOpenSearch: trying transliteration fallback for '{0}'", searchPattern);

                    // Try GOST transliteration first
                    string transliteratedGOST = Transliteration.Back(searchPattern, TransliterationType.GOST);
                    if (!string.IsNullOrEmpty(transliteratedGOST) && transliteratedGOST != searchPattern)
                    {
                        string ftsPatternGOST = PrepareSearchPatternForFTS(transliteratedGOST);
                        var gostBooks = db.ExecuteQuery<Book>(ftsQuery, MapBook,
                            AddLanguageParameter(
                                DatabaseManager.CreateParameter("@SearchPattern", ftsPatternGOST),
                                DatabaseManager.CreateParameter("@LikePattern", transliteratedGOST)));

                        if (gostBooks.Count > 0)
                        {
                            Log.WriteLine(LogLevel.Info, "GetBooksForOpenSearch: GOST transliteration '{0}' -> '{1}' found {2} books",
                                        searchPattern, transliteratedGOST, gostBooks.Count);
                            books.AddRange(gostBooks);
                        }
                    }

                    // Try ISO transliteration if GOST didn't work
                    if (books.Count == 0)
                    {
                        string transliteratedISO = Transliteration.Back(searchPattern, TransliterationType.ISO);
                        if (!string.IsNullOrEmpty(transliteratedISO) && transliteratedISO != searchPattern && transliteratedISO != transliteratedGOST)
                        {
                            string ftsPatternISO = PrepareSearchPatternForFTS(transliteratedISO);
                            var isoBooks = db.ExecuteQuery<Book>(ftsQuery, MapBook,
                                AddLanguageParameter(
                                    DatabaseManager.CreateParameter("@SearchPattern", ftsPatternISO),
                                    DatabaseManager.CreateParameter("@LikePattern", transliteratedISO)));

                            if (isoBooks.Count > 0)
                            {
                                Log.WriteLine(LogLevel.Info, "GetBooksForOpenSearch: ISO transliteration '{0}' -> '{1}' found {2} books",
                                            searchPattern, transliteratedISO, isoBooks.Count);
                                books.AddRange(isoBooks);
                            }
                        }
                    }
                }

                // Fallback to LIKE search if FTS didn't find anything
                if (books.Count == 0)
                {
                    Log.WriteLine(LogLevel.Info, "GetBooksForOpenSearch: FTS found nothing, trying LIKE search");

                    // Apply language filter to LIKE query
                    string likeQuery = DatabaseSchema.SelectBooksByTitleLike;
                    if (!string.IsNullOrEmpty(languageFilter))
                    {
                        likeQuery = likeQuery.Replace("WHERE", "WHERE Language = @Language AND");
                    }

                    books = db.ExecuteQuery<Book>(likeQuery, MapBook,
                        AddLanguageParameter(DatabaseManager.CreateParameter("@Title", searchPattern)));
                }

                // Load book relations
                foreach (var book in books)
                {
                    LoadBookRelations(book);
                }

                Log.WriteLine(LogLevel.Info, "GetBooksForOpenSearch: found {0} books", books.Count);
                return books.Distinct().ToList();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GetBooksForOpenSearch {0}: {1}", searchPattern, ex.Message);
                return new List<Book>();
            }
        }

        public List<Book> GetNewBooks(DateTime fromDate)
        {
            string languageFilter = GetLanguageFilter();
            string query = DatabaseSchema.SelectNewBooks;

            if (!string.IsNullOrEmpty(languageFilter))
            {
                query = query.Replace("WHERE", "WHERE Language = @Language AND");
            }

            var books = db.ExecuteQuery<Book>(query, MapBook,
                AddLanguageParameter(DatabaseManager.CreateParameter("@FromDate", fromDate.ToBinary())));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksByFileNamePrefix(string fileNamePrefix)
        {
            string languageFilter = GetLanguageFilter();

            string query = @"
                SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                       Annotation, DocumentSize, AddedDate
                FROM Books 
                WHERE FileName LIKE @FileNamePrefix || '%'";

            if (!string.IsNullOrEmpty(languageFilter))
            {
                query += " AND Language = @Language";
            }

            var books = db.ExecuteQuery<Book>(query, MapBook,
                AddLanguageParameter(DatabaseManager.CreateParameter("@FileNamePrefix", fileNamePrefix)));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        #endregion

        #region Sequence Methods - NEW

        /// <summary>
        /// Get all sequences for navigation (considers language filter)
        /// </summary>
        public List<string> GetSequencesForNavigation(string searchPattern)
        {
            try
            {
                string languageFilter = GetLanguageFilter();

                if (string.IsNullOrEmpty(searchPattern))
                {
                    if (string.IsNullOrEmpty(languageFilter))
                    {
                        return db.ExecuteQuery<string>(DatabaseSchema.SelectSequences,
                            reader => reader.GetString(0));
                    }
                    else
                    {
                        // Get sequences only from books in the filtered language
                        string query = @"
                            SELECT DISTINCT s.Name
                            FROM Sequences s
                            INNER JOIN BookSequences bs ON s.ID = bs.SequenceID
                            INNER JOIN Books b ON bs.BookID = b.ID
                            WHERE b.Language = @Language AND b.ReplacedByID IS NULL
                            ORDER BY s.Name";

                        return db.ExecuteQuery<string>(query,
                            reader => reader.GetString(0),
                            DatabaseManager.CreateParameter("@Language", languageFilter));
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(languageFilter))
                    {
                        return db.ExecuteQuery<string>(DatabaseSchema.SelectSequencesByPrefix,
                            reader => reader.GetString(0),
                            DatabaseManager.CreateParameter("@Pattern", searchPattern));
                    }
                    else
                    {
                        // Get sequences by prefix only from books in the filtered language
                        string query = @"
                            SELECT DISTINCT s.Name
                            FROM Sequences s
                            INNER JOIN BookSequences bs ON s.ID = bs.SequenceID
                            INNER JOIN Books b ON bs.BookID = b.ID
                            WHERE s.Name LIKE @Pattern || '%' 
                              AND b.Language = @Language 
                              AND b.ReplacedByID IS NULL
                            ORDER BY s.Name";

                        return db.ExecuteQuery<string>(query,
                            reader => reader.GetString(0),
                            DatabaseManager.CreateParameter("@Pattern", searchPattern),
                            DatabaseManager.CreateParameter("@Language", languageFilter));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GetSequencesForNavigation {0}: {1}", searchPattern, ex.Message);
                return new List<string>();
            }
        }

        /// <summary>
        /// Get sequences with book count (considers language filter)
        /// </summary>
        public List<(string Name, int BookCount)> GetSequencesWithCount(string searchPattern = null)
        {
            try
            {
                List<(string, int)> result;
                string languageFilter = GetLanguageFilter();

                if (string.IsNullOrEmpty(searchPattern))
                {
                    if (string.IsNullOrEmpty(languageFilter))
                    {
                        result = db.ExecuteQuery<(string, int)>(DatabaseSchema.SelectSequencesWithCount,
                            reader => (reader.GetString(0), Convert.ToInt32(reader.GetValue(1))));
                    }
                    else
                    {
                        string query = @"
                            SELECT s.Name, COUNT(DISTINCT b.ID) as BookCount
                            FROM Sequences s
                            INNER JOIN BookSequences bs ON s.ID = bs.SequenceID
                            INNER JOIN Books b ON bs.BookID = b.ID
                            WHERE b.Language = @Language AND b.ReplacedByID IS NULL
                            GROUP BY s.Name
                            ORDER BY s.Name";

                        result = db.ExecuteQuery<(string, int)>(query,
                            reader => (reader.GetString(0), Convert.ToInt32(reader.GetValue(1))),
                            DatabaseManager.CreateParameter("@Language", languageFilter));
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(languageFilter))
                    {
                        result = db.ExecuteQuery<(string, int)>(DatabaseSchema.SelectSequencesWithCountByPrefix,
                            reader => (reader.GetString(0), Convert.ToInt32(reader.GetValue(1))),
                            DatabaseManager.CreateParameter("@Pattern", searchPattern));
                    }
                    else
                    {
                        string query = @"
                            SELECT s.Name, COUNT(DISTINCT b.ID) as BookCount
                            FROM Sequences s
                            INNER JOIN BookSequences bs ON s.ID = bs.SequenceID
                            INNER JOIN Books b ON bs.BookID = b.ID
                            WHERE s.Name LIKE @Pattern || '%' 
                              AND b.Language = @Language 
                              AND b.ReplacedByID IS NULL
                            GROUP BY s.Name
                            ORDER BY s.Name";

                        result = db.ExecuteQuery<(string, int)>(query,
                            reader => (reader.GetString(0), Convert.ToInt32(reader.GetValue(1))),
                            DatabaseManager.CreateParameter("@Pattern", searchPattern),
                            DatabaseManager.CreateParameter("@Language", languageFilter));
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GetSequencesWithCount: {0}", ex.Message);
                return new List<(string, int)>();
            }
        }

        #endregion

        #region Author Search Methods - USING FTS5

        /// <summary>
        /// Get authors for navigation (catalog browsing with pagination) - considers language filter
        /// </summary>
        public List<string> GetAuthorsForNavigation(string searchPattern)
        {
            try
            {
                string languageFilter = GetLanguageFilter();

                if (string.IsNullOrEmpty(searchPattern))
                {
                    if (string.IsNullOrEmpty(languageFilter))
                    {
                        return db.ExecuteQuery<string>(DatabaseSchema.SelectAuthors, reader => reader.GetString(0));
                    }
                    else
                    {
                        // Get authors only from books in the filtered language
                        string query = @"
                            SELECT DISTINCT a.Name
                            FROM Authors a
                            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
                            INNER JOIN Books b ON ba.BookID = b.ID
                            WHERE b.Language = @Language AND b.ReplacedByID IS NULL
                            ORDER BY a.Name";

                        return db.ExecuteQuery<string>(query,
                            reader => reader.GetString(0),
                            DatabaseManager.CreateParameter("@Language", languageFilter));
                    }
                }

                if (string.IsNullOrEmpty(languageFilter))
                {
                    return db.ExecuteQuery<string>(DatabaseSchema.SelectAuthorsByPrefix,
                        reader => reader.GetString(0),
                        DatabaseManager.CreateParameter("@Pattern", searchPattern));
                }
                else
                {
                    // Get authors by prefix only from books in the filtered language
                    string query = @"
                        SELECT DISTINCT a.Name
                        FROM Authors a
                        INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
                        INNER JOIN Books b ON ba.BookID = b.ID
                        WHERE a.Name LIKE @Pattern || '%' COLLATE NOCASE
                          AND b.Language = @Language 
                          AND b.ReplacedByID IS NULL
                        ORDER BY a.Name";

                    return db.ExecuteQuery<string>(query,
                        reader => reader.GetString(0),
                        DatabaseManager.CreateParameter("@Pattern", searchPattern),
                        DatabaseManager.CreateParameter("@Language", languageFilter));
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GetAuthorsForNavigation {0}: {1}", searchPattern, ex.Message);
                return new List<string>();
            }
        }

        /// <summary>
        /// Get authors for OpenSearch with proper cascading search using FTS5
        /// </summary>
        public List<string> GetAuthorsForOpenSearch(string searchPattern)
        {
            var (authors, _) = GetAuthorsForOpenSearchWithMethod(searchPattern);
            return authors;
        }

        /// <summary>
        /// Get authors for OpenSearch with search method information - considers language filter
        /// </summary>
        /// <returns>Tuple with authors list and search method used</returns>
        public (List<string> authors, AuthorSearchMethod method) GetAuthorsForOpenSearchWithMethod(string searchPattern)
        {
            try
            {
                if (string.IsNullOrEmpty(searchPattern))
                {
                    return (new List<string>(), AuthorSearchMethod.NotFound);
                }

                searchPattern = searchPattern.Trim();
                Log.WriteLine(LogLevel.Info, "GetAuthorsForOpenSearchWithMethod: searching for '{0}'", searchPattern);

                var authors = new List<string>();
                var words = searchPattern.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                string languageFilter = GetLanguageFilter();

                if (words.Length == 0)
                {
                    return (new List<string>(), AuthorSearchMethod.NotFound);
                }

                // Determine if input is Latin or Cyrillic
                bool isLatinInput = ContainsLatinLetters(searchPattern) && !ContainsCyrillicLetters(searchPattern);
                bool isCyrillicInput = ContainsCyrillicLetters(searchPattern);

                Log.WriteLine(LogLevel.Info, "Input detected as: Latin={0}, Cyrillic={1}", isLatinInput, isCyrillicInput);

                // Prepare transliterated versions if Latin input
                string transliteratedGOST = null;
                string transliteratedISO = null;
                if (isLatinInput)
                {
                    transliteratedGOST = Transliteration.Back(searchPattern, TransliterationType.GOST);
                    transliteratedISO = Transliteration.Back(searchPattern, TransliterationType.ISO);
                    Log.WriteLine(LogLevel.Info, "Transliterations prepared: GOST='{0}', ISO='{1}'", transliteratedGOST, transliteratedISO);
                }

                // STEP 1: For two-word input, try EXACT match using FTS
                if (words.Length == 2)
                {
                    Log.WriteLine(LogLevel.Info, "Step 1: Trying exact match for two-word query '{0}' using FTS", searchPattern);

                    // Search with original pattern
                    authors = SearchAuthorsFTS(searchPattern, languageFilter, exactMatch: true);
                    if (authors.Count > 0)
                    {
                        Log.WriteLine(LogLevel.Info, "Found exact match using FTS: {0} authors", authors.Count);
                        return (authors, AuthorSearchMethod.ExactMatch);
                    }

                    // Try reversed order
                    string reversedPattern = $"{words[1]} {words[0]}";
                    authors = SearchAuthorsFTS(reversedPattern, languageFilter, exactMatch: true);
                    if (authors.Count > 0)
                    {
                        Log.WriteLine(LogLevel.Info, "Found exact match with reversed order using FTS: {0} authors", authors.Count);
                        return (authors, AuthorSearchMethod.ExactMatch);
                    }

                    // For Latin input, ALSO try transliterated versions
                    if (isLatinInput)
                    {
                        // Try GOST transliteration
                        if (!string.IsNullOrEmpty(transliteratedGOST) && transliteratedGOST != searchPattern)
                        {
                            authors = SearchAuthorsFTS(transliteratedGOST, languageFilter, exactMatch: true);
                            if (authors.Count > 0)
                            {
                                Log.WriteLine(LogLevel.Info, "Found exact match via GOST transliteration: {0} authors", authors.Count);
                                return (authors, AuthorSearchMethod.Transliteration);
                            }

                            // Try reversed transliterated
                            string[] translitWords = transliteratedGOST.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (translitWords.Length == 2)
                            {
                                string reversedTranslit = $"{translitWords[1]} {translitWords[0]}";
                                authors = SearchAuthorsFTS(reversedTranslit, languageFilter, exactMatch: true);
                                if (authors.Count > 0)
                                {
                                    Log.WriteLine(LogLevel.Info, "Found exact match via reversed GOST transliteration: {0} authors", authors.Count);
                                    return (authors, AuthorSearchMethod.Transliteration);
                                }
                            }
                        }

                        // Try ISO transliteration
                        if (!string.IsNullOrEmpty(transliteratedISO) && transliteratedISO != searchPattern && transliteratedISO != transliteratedGOST)
                        {
                            authors = SearchAuthorsFTS(transliteratedISO, languageFilter, exactMatch: true);
                            if (authors.Count > 0)
                            {
                                Log.WriteLine(LogLevel.Info, "Found exact match via ISO transliteration: {0} authors", authors.Count);
                                return (authors, AuthorSearchMethod.Transliteration);
                            }

                            // Try reversed transliterated
                            string[] translitWords = transliteratedISO.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                            if (translitWords.Length == 2)
                            {
                                string reversedTranslit = $"{translitWords[1]} {translitWords[0]}";
                                authors = SearchAuthorsFTS(reversedTranslit, languageFilter, exactMatch: true);
                                if (authors.Count > 0)
                                {
                                    Log.WriteLine(LogLevel.Info, "Found exact match via reversed ISO transliteration: {0} authors", authors.Count);
                                    return (authors, AuthorSearchMethod.Transliteration);
                                }
                            }
                        }
                    }
                }

                // STEP 2: Partial match ONLY for single word using FTS
                if (words.Length == 1)
                {
                    string pattern = words[0];
                    Log.WriteLine(LogLevel.Info, "Step 2: Trying partial match for single word '{0}' using FTS", pattern);

                    // Search with original pattern
                    authors = SearchAuthorsFTS(pattern, languageFilter, exactMatch: false);
                    if (authors.Count > 0)
                    {
                        Log.WriteLine(LogLevel.Info, "Found partial match using FTS: {0} authors", authors.Count);
                        return (authors, AuthorSearchMethod.PartialMatch);
                    }

                    // For Latin input, ALSO try transliterated versions
                    if (isLatinInput)
                    {
                        // Try GOST transliteration
                        if (!string.IsNullOrEmpty(transliteratedGOST) && transliteratedGOST != searchPattern)
                        {
                            authors = SearchAuthorsFTS(transliteratedGOST, languageFilter, exactMatch: false);
                            if (authors.Count > 0)
                            {
                                Log.WriteLine(LogLevel.Info, "Found partial match via GOST transliteration: {0} authors", authors.Count);
                                return (authors, AuthorSearchMethod.Transliteration);
                            }
                        }

                        // Try ISO transliteration
                        if (!string.IsNullOrEmpty(transliteratedISO) && transliteratedISO != searchPattern && transliteratedISO != transliteratedGOST)
                        {
                            authors = SearchAuthorsFTS(transliteratedISO, languageFilter, exactMatch: false);
                            if (authors.Count > 0)
                            {
                                Log.WriteLine(LogLevel.Info, "Found partial match via ISO transliteration: {0} authors", authors.Count);
                                return (authors, AuthorSearchMethod.Transliteration);
                            }
                        }
                    }
                }

                // STEP 3: Soundex search (last resort)
                // Use appropriate Soundex based on input language
                string soundexPattern = words.Length == 1 ? words[0] : words[words.Length - 1];
                string soundex = StringUtils.Soundex(soundexPattern);

                if (!string.IsNullOrEmpty(soundex))
                {
                    Log.WriteLine(LogLevel.Info, "Step 3: Trying Soundex search for '{0}' -> '{1}'", soundexPattern, soundex);

                    if (!string.IsNullOrEmpty(languageFilter))
                    {
                        // Apply language filter to Soundex query
                        string sql = @"
                            SELECT DISTINCT a.Name
                            FROM Authors a
                            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
                            INNER JOIN Books b ON ba.BookID = b.ID
                            WHERE a.LastNameSoundex = @Soundex
                              AND b.Language = @Language
                              AND b.ReplacedByID IS NULL
                            ORDER BY a.Name";

                        authors = db.ExecuteQuery<string>(sql,
                            reader => reader.GetString(0),
                            DatabaseManager.CreateParameter("@Soundex", soundex),
                            DatabaseManager.CreateParameter("@Language", languageFilter));
                    }
                    else
                    {
                        authors = db.ExecuteQuery<string>(DatabaseSchema.SelectAuthorsBySoundex,
                            reader => reader.GetString(0),
                            DatabaseManager.CreateParameter("@Soundex", soundex));
                    }

                    if (authors.Count > 0)
                    {
                        Log.WriteLine(LogLevel.Info, "Found via Soundex: {0} authors", authors.Count);
                        return (authors, AuthorSearchMethod.Soundex);
                    }
                }

                Log.WriteLine(LogLevel.Info, "GetAuthorsForOpenSearchWithMethod: no results found");
                return (new List<string>(), AuthorSearchMethod.NotFound);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GetAuthorsForOpenSearchWithMethod {0}: {1}", searchPattern, ex.Message);
                return (new List<string>(), AuthorSearchMethod.NotFound);
            }
        }

        /// <summary>
        /// Helper method to search authors using FTS
        /// </summary>
        private List<string> SearchAuthorsFTS(string searchPattern, string languageFilter, bool exactMatch)
        {
            // Escape the search pattern for FTS
            string escapedPattern = EscapeForFTS(searchPattern);

            // Prepare FTS query
            string ftsQuery = exactMatch
                ? $"\"{escapedPattern}\""  // Exact phrase in FTS
                : $"{escapedPattern}*";     // Wildcard for prefix search

            string sql = @"
                SELECT DISTINCT a.Name
                FROM Authors a
                INNER JOIN AuthorsFTS fts ON a.ID = fts.AuthorID
                INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID";

            if (!string.IsNullOrEmpty(languageFilter))
            {
                sql += @"
                INNER JOIN Books b ON ba.BookID = b.ID
                WHERE AuthorsFTS MATCH @SearchPattern
                  AND b.Language = @Language
                  AND b.ReplacedByID IS NULL
                ORDER BY a.Name";

                return db.ExecuteQuery<string>(sql,
                    reader => reader.GetString(0),
                    DatabaseManager.CreateParameter("@SearchPattern", ftsQuery),
                    DatabaseManager.CreateParameter("@Language", languageFilter));
            }
            else
            {
                sql += @" WHERE AuthorsFTS MATCH @SearchPattern ORDER BY a.Name";

                return db.ExecuteQuery<string>(sql,
                    reader => reader.GetString(0),
                    DatabaseManager.CreateParameter("@SearchPattern", ftsQuery));
            }
        }

        #endregion

        #region Genre Methods

        /// <summary>
        /// Get all genres from database
        /// </summary>
        public List<Genre> GetAllGenres()
        {
            return db.GetAllGenres();
        }

        /// <summary>
        /// Get genres with book count > 0 (considers language filter) - cross-platform
        /// </summary>
        public List<(Genre genre, int bookCount)> GetGenresWithBooks()
        {
            try
            {
                var result = new List<(Genre genre, int bookCount)>();
                string languageFilter = GetLanguageFilter();

                string query;
                if (string.IsNullOrEmpty(languageFilter))
                {
                    query = DatabaseSchema.SelectGenresWithBookCount;
                }
                else
                {
                    // Apply language filter
                    query = @"
                        SELECT g.Tag, g.ParentName, g.Name, g.Translation, COUNT(DISTINCT b.ID) as BookCount
                        FROM Genres g
                        LEFT JOIN BookGenres bg ON g.Tag = bg.GenreTag
                        LEFT JOIN Books b ON bg.BookID = b.ID AND b.Language = @Language AND b.ReplacedByID IS NULL
                        GROUP BY g.Tag, g.ParentName, g.Name, g.Translation
                        HAVING BookCount > 0
                        ORDER BY g.ParentName, g.Name";
                }

                var data = db.ExecuteQuery<(string Tag, string ParentName, string Name, string Translation, int BookCount)>(
                    query,
                    reader => (
                        reader.GetString(0),  // Tag
                        DatabaseManager.GetString(reader, "ParentName"),
                        reader.GetString(2),  // Name
                        DatabaseManager.GetString(reader, "Translation"),
                        Convert.ToInt32(reader.GetValue(4))   // BookCount - Use Convert.ToInt32
                    ),
                    string.IsNullOrEmpty(languageFilter) ? null :
                        new[] { DatabaseManager.CreateParameter("@Language", languageFilter) });

                foreach (var (Tag, ParentName, Name, Translation, BookCount) in data)
                {
                    var genre = new Genre
                    {
                        Tag = Tag,
                        Name = Name,
                        Translation = Translation
                    };
                    result.Add((genre, BookCount));
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error getting genres with books: {0}", ex.Message);
                return new List<(Genre genre, int bookCount)>();
            }
        }

        /// <summary>
        /// Validate and fix book genres against Genres table
        /// </summary>
        public int ValidateAndFixBookGenres()
        {
            try
            {
                // Find invalid genre tags
                var invalidTags = db.ExecuteQuery<string>(
                    DatabaseSchema.ValidateBookGenres,
                    reader => reader.GetString(0));

                if (invalidTags.Count > 0)
                {
                    Log.WriteLine(LogLevel.Warning, "Found {0} invalid genre tags in BookGenres: {1}",
                        invalidTags.Count, string.Join(", ", invalidTags));

                    // Remove invalid entries
                    foreach (var tag in invalidTags)
                    {
                        db.ExecuteNonQuery(
                            "DELETE FROM BookGenres WHERE GenreTag = @Tag",
                            DatabaseManager.CreateParameter("@Tag", tag));
                    }

                    Log.WriteLine("Removed {0} invalid genre entries from BookGenres", invalidTags.Count);
                    return invalidTags.Count;
                }

                return 0;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error validating book genres: {0}", ex.Message);
                return -1;
            }
        }

        #endregion

        #region Statistics - ALL cross-platform compatible

        public int GetTotalBooksCount()
        {
            string languageFilter = GetLanguageFilter();

            if (string.IsNullOrEmpty(languageFilter))
            {
                var result = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE ReplacedByID IS NULL");
                return Convert.ToInt32(result ?? 0);
            }
            else
            {
                var result = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE ReplacedByID IS NULL AND Language = @Language",
                    DatabaseManager.CreateParameter("@Language", languageFilter));
                return Convert.ToInt32(result ?? 0);
            }
        }

        public int GetFB2BooksCount()
        {
            string languageFilter = GetLanguageFilter();

            if (string.IsNullOrEmpty(languageFilter))
            {
                var result = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE FileName LIKE '%.fb2%' AND ReplacedByID IS NULL");
                return Convert.ToInt32(result ?? 0);
            }
            else
            {
                var result = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE FileName LIKE '%.fb2%' AND ReplacedByID IS NULL AND Language = @Language",
                    DatabaseManager.CreateParameter("@Language", languageFilter));
                return Convert.ToInt32(result ?? 0);
            }
        }

        public int GetEPUBBooksCount()
        {
            string languageFilter = GetLanguageFilter();

            if (string.IsNullOrEmpty(languageFilter))
            {
                var result = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE FileName LIKE '%.epub%' AND ReplacedByID IS NULL");
                return Convert.ToInt32(result ?? 0);
            }
            else
            {
                var result = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE FileName LIKE '%.epub%' AND ReplacedByID IS NULL AND Language = @Language",
                    DatabaseManager.CreateParameter("@Language", languageFilter));
                return Convert.ToInt32(result ?? 0);
            }
        }

        public int GetNewBooksCount(DateTime fromDate)
        {
            string languageFilter = GetLanguageFilter();

            if (string.IsNullOrEmpty(languageFilter))
            {
                var result = db.ExecuteScalar(DatabaseSchema.CountNewBooks,
                    DatabaseManager.CreateParameter("@FromDate", fromDate.ToBinary()));
                return Convert.ToInt32(result ?? 0);
            }
            else
            {
                var result = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE AddedDate >= @FromDate AND ReplacedByID IS NULL AND Language = @Language",
                    DatabaseManager.CreateParameter("@FromDate", fromDate.ToBinary()),
                    DatabaseManager.CreateParameter("@Language", languageFilter));
                return Convert.ToInt32(result ?? 0);
            }
        }

        /// <summary>
        /// Get new books with pagination support (considers language filter)
        /// </summary>
        public List<Book> GetNewBooksPaginated(DateTime fromDate, int offset, int limit, bool sortByDate = true)
        {
            try
            {
                string languageFilter = GetLanguageFilter();
                string query = sortByDate
                    ? DatabaseSchema.SelectNewBooksPaginatedByDate
                    : DatabaseSchema.SelectNewBooksPaginatedByTitle;

                if (!string.IsNullOrEmpty(languageFilter))
                {
                    query = query.Replace("WHERE", "WHERE Language = @Language AND");
                }

                var books = db.ExecuteQuery<Book>(query, MapBook,
                    AddLanguageParameter(
                        DatabaseManager.CreateParameter("@FromDate", fromDate.ToBinary()),
                        DatabaseManager.CreateParameter("@Offset", offset),
                        DatabaseManager.CreateParameter("@Limit", limit)));

                foreach (var book in books)
                {
                    LoadBookRelations(book);
                }

                return books;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error getting paginated new books: {0}", ex.Message);
                return new List<Book>();
            }
        }

        public List<string> GetAllAuthors()
        {
            string languageFilter = GetLanguageFilter();

            if (string.IsNullOrEmpty(languageFilter))
            {
                return db.ExecuteQuery<string>(DatabaseSchema.SelectAuthors, reader => reader.GetString(0));
            }
            else
            {
                string query = @"
                    SELECT DISTINCT a.Name
                    FROM Authors a
                    INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
                    INNER JOIN Books b ON ba.BookID = b.ID
                    WHERE b.Language = @Language AND b.ReplacedByID IS NULL
                    ORDER BY a.Name";

                return db.ExecuteQuery<string>(query,
                    reader => reader.GetString(0),
                    DatabaseManager.CreateParameter("@Language", languageFilter));
            }
        }

        // Now gets sequences from Sequences table (considers language filter)
        public List<string> GetAllSequences()
        {
            string languageFilter = GetLanguageFilter();

            if (string.IsNullOrEmpty(languageFilter))
            {
                return db.ExecuteQuery<string>(DatabaseSchema.SelectSequences, reader => reader.GetString(0));
            }
            else
            {
                string query = @"
                    SELECT DISTINCT s.Name
                    FROM Sequences s
                    INNER JOIN BookSequences bs ON s.ID = bs.SequenceID
                    INNER JOIN Books b ON bs.BookID = b.ID
                    WHERE b.Language = @Language AND b.ReplacedByID IS NULL
                    ORDER BY s.Name";

                return db.ExecuteQuery<string>(query,
                    reader => reader.GetString(0),
                    DatabaseManager.CreateParameter("@Language", languageFilter));
            }
        }

        public List<string> GetAllGenreTags()
        {
            return db.ExecuteQuery<string>(DatabaseSchema.SelectGenreTags, reader => reader.GetString(0));
        }

        public int GetAuthorsCount()
        {
            string languageFilter = GetLanguageFilter();

            if (string.IsNullOrEmpty(languageFilter))
            {
                var result = db.ExecuteScalar(DatabaseSchema.SelectAuthorsCount);
                return Convert.ToInt32(result ?? 0);
            }
            else
            {
                string query = @"
                    SELECT COUNT(DISTINCT a.ID) 
                    FROM Authors a
                    INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
                    INNER JOIN Books b ON ba.BookID = b.ID
                    WHERE b.Language = @Language AND b.ReplacedByID IS NULL";

                var result = db.ExecuteScalar(query,
                    DatabaseManager.CreateParameter("@Language", languageFilter));
                return Convert.ToInt32(result ?? 0);
            }
        }

        public int GetSequencesCount()
        {
            string languageFilter = GetLanguageFilter();

            if (string.IsNullOrEmpty(languageFilter))
            {
                var result = db.ExecuteScalar(DatabaseSchema.SelectSequencesCount);
                return Convert.ToInt32(result ?? 0);
            }
            else
            {
                string query = @"
                    SELECT COUNT(DISTINCT s.ID)
                    FROM Sequences s
                    INNER JOIN BookSequences bs ON s.ID = bs.SequenceID
                    INNER JOIN Books b ON bs.BookID = b.ID
                    WHERE b.Language = @Language AND b.ReplacedByID IS NULL";

                var result = db.ExecuteScalar(query,
                    DatabaseManager.CreateParameter("@Language", languageFilter));
                return Convert.ToInt32(result ?? 0);
            }
        }

        /// <summary>
        /// Get count of books by genre (considers language filter)
        /// </summary>
        public int GetBooksByGenreCount(string genreTag)
        {
            try
            {
                string languageFilter = GetLanguageFilter();

                if (string.IsNullOrEmpty(languageFilter))
                {
                    var result = db.ExecuteScalar(DatabaseSchema.CountBooksByGenre,
                        DatabaseManager.CreateParameter("@GenreTag", genreTag));
                    return Convert.ToInt32(result ?? 0);
                }
                else
                {
                    string query = @"
                        SELECT COUNT(*) FROM Books b
                        INNER JOIN BookGenres bg ON b.ID = bg.BookID
                        WHERE bg.GenreTag = @GenreTag 
                          AND b.ReplacedByID IS NULL 
                          AND b.Language = @Language";

                    var result = db.ExecuteScalar(query,
                        DatabaseManager.CreateParameter("@GenreTag", genreTag),
                        DatabaseManager.CreateParameter("@Language", languageFilter));
                    return Convert.ToInt32(result ?? 0);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error counting books for genre {0}: {1}", genreTag, ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Get count of books by sequence (considers language filter)
        /// </summary>
        public int GetBooksBySequenceCount(string sequenceName)
        {
            try
            {
                string languageFilter = GetLanguageFilter();

                if (string.IsNullOrEmpty(languageFilter))
                {
                    var result = db.ExecuteScalar(DatabaseSchema.CountBooksBySequence,
                        DatabaseManager.CreateParameter("@SequenceName", sequenceName));
                    return Convert.ToInt32(result ?? 0);
                }
                else
                {
                    string query = @"
                        SELECT COUNT(*) FROM Books b
                        INNER JOIN BookSequences bs ON b.ID = bs.BookID
                        INNER JOIN Sequences s ON bs.SequenceID = s.ID
                        WHERE s.Name = @SequenceName 
                          AND b.ReplacedByID IS NULL 
                          AND b.Language = @Language";

                    var result = db.ExecuteScalar(query,
                        DatabaseManager.CreateParameter("@SequenceName", sequenceName),
                        DatabaseManager.CreateParameter("@Language", languageFilter));
                    return Convert.ToInt32(result ?? 0);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error counting books for sequence {0}: {1}", sequenceName, ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Get statistics for all genres with book counts (considers language filter)
        /// </summary>
        public Dictionary<string, int> GetAllGenreStatistics()
        {
            try
            {
                var result = new Dictionary<string, int>();
                string languageFilter = GetLanguageFilter();

                string query;
                if (string.IsNullOrEmpty(languageFilter))
                {
                    query = DatabaseSchema.SelectGenreStatistics;
                }
                else
                {
                    query = @"
                        SELECT bg.GenreTag, COUNT(DISTINCT b.ID) as BookCount
                        FROM BookGenres bg
                        INNER JOIN Books b ON bg.BookID = b.ID
                        WHERE b.Language = @Language AND b.ReplacedByID IS NULL
                        GROUP BY bg.GenreTag
                        HAVING BookCount > 0";
                }

                var statistics = db.ExecuteQuery<(string GenreTag, int BookCount)>(
                    query,
                    reader => (reader.GetString(0), Convert.ToInt32(reader.GetValue(1))),
                    string.IsNullOrEmpty(languageFilter) ? null :
                        new[] { DatabaseManager.CreateParameter("@Language", languageFilter) });

                foreach (var (genreTag, bookCount) in statistics)
                {
                    result[genreTag] = bookCount;
                }

                Log.WriteLine(LogLevel.Info, "Loaded statistics for {0} genres", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error getting genre statistics: {0}", ex.Message);
                return new Dictionary<string, int>();
            }
        }

        /// <summary>
        /// Get full genre statistics including parent and translation info (considers language filter)
        /// </summary>
        public List<(Genre genre, int bookCount)> GetFullGenreStatistics()
        {
            try
            {
                var result = new List<(Genre genre, int bookCount)>();
                string languageFilter = GetLanguageFilter();

                string query;
                if (string.IsNullOrEmpty(languageFilter))
                {
                    query = DatabaseSchema.SelectGenreStatisticsFull;
                }
                else
                {
                    query = @"
                        SELECT g.Tag, g.ParentName, g.Name, g.Translation, COUNT(DISTINCT b.ID) as BookCount
                        FROM Genres g
                        INNER JOIN BookGenres bg ON g.Tag = bg.GenreTag
                        INNER JOIN Books b ON bg.BookID = b.ID
                        WHERE b.Language = @Language AND b.ReplacedByID IS NULL
                        GROUP BY g.Tag, g.ParentName, g.Name, g.Translation
                        HAVING BookCount > 0";
                }

                var statistics = db.ExecuteQuery<(string Tag, string ParentName, string Name, string Translation, int BookCount)>(
                    query,
                    reader => (
                        reader.GetString(0),
                        DatabaseManager.GetString(reader, "ParentName"),
                        reader.GetString(2),
                        DatabaseManager.GetString(reader, "Translation"),
                        Convert.ToInt32(reader.GetValue(4))  // Use Convert.ToInt32
                    ),
                    string.IsNullOrEmpty(languageFilter) ? null :
                        new[] { DatabaseManager.CreateParameter("@Language", languageFilter) });

                foreach (var (Tag, ParentName, Name, Translation, BookCount) in statistics)
                {
                    var genre = new Genre
                    {
                        Tag = Tag,
                        Name = Name,
                        Translation = Translation
                    };
                    result.Add((genre, BookCount));
                }

                Log.WriteLine(LogLevel.Info, "Loaded full statistics for {0} genres", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error getting full genre statistics: {0}", ex.Message);
                return new List<(Genre genre, int bookCount)>();
            }
        }

        #endregion

        #region Helper Methods

        // MapBook no longer tries to read Sequence/NumberInSequence from database
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
                DocumentSize = DatabaseManager.GetUInt32(reader, "DocumentSize")
            };

            // Map new duplicate detection fields if they exist
            try
            {
                book.DocumentIDTrusted = DatabaseManager.GetBoolean(reader, "DocumentIDTrusted");
                book.DuplicateKey = DatabaseManager.GetString(reader, "DuplicateKey");
                book.ReplacedByID = DatabaseManager.GetString(reader, "ReplacedByID");
                book.ContentHash = DatabaseManager.GetString(reader, "ContentHash");
            }
            catch
            {
                // Fields might not exist in older queries, ignore
            }

            var bookDate = DatabaseManager.GetDateTime(reader, "BookDate");
            if (bookDate.HasValue) book.BookDate = bookDate.Value;

            var docDate = DatabaseManager.GetDateTime(reader, "DocumentDate");
            if (docDate.HasValue) book.DocumentDate = docDate.Value;

            var addedDate = DatabaseManager.GetDateTime(reader, "AddedDate");
            if (addedDate.HasValue) book.AddedDate = addedDate.Value;

            return book;
        }

        // LoadBookRelations now loads sequences from BookSequences table
        private void LoadBookRelations(Book book)
        {
            // Load authors
            book.Authors = db.ExecuteQuery<string>(DatabaseSchema.SelectBookAuthors, reader => reader.GetString(0),
                DatabaseManager.CreateParameter("@BookID", book.ID));

            // Load genres
            book.Genres = db.ExecuteQuery<string>(DatabaseSchema.SelectBookGenres, reader => reader.GetString(0),
                DatabaseManager.CreateParameter("@BookID", book.ID));

            // Load translators
            book.Translators = db.ExecuteQuery<string>(DatabaseSchema.SelectBookTranslators, reader => reader.GetString(0),
                DatabaseManager.CreateParameter("@BookID", book.ID));

            // Load sequences from BookSequences table
            var sequences = db.ExecuteQuery<BookSequenceInfo>(DatabaseSchema.SelectBookSequences,
                reader => new BookSequenceInfo(
                    reader.GetString(0),  // Name
                    DatabaseManager.GetUInt32(reader, "NumberInSequence")  // NumberInSequence
                ),
                DatabaseManager.CreateParameter("@BookID", book.ID));

            book.Sequences = sequences;
        }

        /// <summary>
        /// Parse author name into FirstName, MiddleName, LastName components
        /// </summary>
        private (string firstName, string middleName, string lastName) ParseAuthorName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return (null, null, null);

            var parts = fullName.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            switch (parts.Length)
            {
                case 1:
                    return (null, null, parts[0]);

                case 2:
                    // For Russian format: "Lastname Firstname"
                    return (parts[1], null, parts[0]);

                case 3:
                    // For Russian format: "Lastname Firstname Middlename"
                    return (parts[1], parts[2], parts[0]);

                default:
                    // Complex name - first word is last name, rest is first/middle
                    var middleParts = new string[parts.Length - 2];
                    Array.Copy(parts, 2, middleParts, 0, parts.Length - 2);
                    return (parts[1], string.Join(" ", middleParts), parts[0]);
            }
        }

        /// <summary>
        /// Normalize name for search (lowercase, remove punctuation)
        /// </summary>
        private string NormalizeForSearch(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            // Remove punctuation and convert to lowercase
            var normalized = System.Text.RegularExpressions.Regex.Replace(name, @"[^\w\s]", "");
            return normalized.ToLowerInvariant().Trim();
        }

        /// <summary>
        /// Generate transliteration for name (for Latin->Cyrillic search)
        /// </summary>
        private string GenerateTransliteration(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            // If name is already in Latin, store as-is for reverse search
            if (ContainsLatinLetters(name) && !ContainsCyrillicLetters(name))
            {
                return name.ToLowerInvariant();
            }

            // If name is in Cyrillic, transliterate to Latin
            if (ContainsCyrillicLetters(name))
            {
                string translitGOST = Transliteration.Front(name, TransliterationType.GOST);
                string translitISO = Transliteration.Front(name, TransliterationType.ISO);
                // Store both variants separated by pipe
                return $"{translitGOST}|{translitISO}".ToLowerInvariant();
            }

            return "";
        }

        /// <summary>
        /// Escape special characters for FTS5 queries
        /// </summary>
        private string EscapeForFTS(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // FTS5 special characters that need escaping
            // Double quotes need to be escaped by doubling them
            text = text.Replace("\"", "\"\"");

            // For safety, remove other potentially problematic characters
            // or replace them with spaces
            text = text.Replace("'", " ");  // Replace apostrophe with space
            text = text.Replace("(", " ");
            text = text.Replace(")", " ");
            text = text.Replace("[", " ");
            text = text.Replace("]", " ");
            text = text.Replace("*", " ");  // Wildcard, but we'll add our own
            text = text.Replace(":", " ");
            text = text.Replace("-", " ");  // Can be problematic in FTS

            // Clean up multiple spaces
            while (text.Contains("  "))
                text = text.Replace("  ", " ");

            return text.Trim();
        }

        /// <summary>
        /// Prepare search pattern for FTS5 (add wildcards for partial matching)
        /// </summary>
        private string PrepareSearchPatternForFTS(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return pattern;

            // First escape special characters
            pattern = EscapeForFTS(pattern);

            // Split into words and add wildcards
            var words = pattern.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var ftsWords = words.Select(w => $"{w}*").ToArray();

            // Join with spaces for FTS5
            return string.Join(" ", ftsWords);
        }

        /// <summary>
        /// Check if string contains Latin letters (including diacritics)
        /// </summary>
        private bool ContainsLatinLetters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            foreach (char c in text)
            {
                // Basic Latin
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                    return true;

                // Extended Latin (Latin-1 Supplement)
                if ((c >= '\u00C0' && c <= '\u00FF'))  // À-ÿ
                    return true;

                // Latin Extended-A
                if ((c >= '\u0100' && c <= '\u017F'))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if string contains Cyrillic letters
        /// </summary>
        private bool ContainsCyrillicLetters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            foreach (char c in text)
            {
                // Full Cyrillic Unicode block (0400-04FF)
                if (c >= '\u0400' && c <= '\u04FF')
                    return true;
            }
            return false;
        }

        #endregion
    }
}