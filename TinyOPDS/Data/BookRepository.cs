/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Repository for Book operations with SQLite database with FTS5 support
 * MODIFIED: Support for normalized sequences in separate tables
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

        public BookRepository(DatabaseManager database)
        {
            db = database;
            validGenreTags = new HashSet<string>();
            LoadValidGenreTags();

            // Initialize duplicate detector (normal mode by default)
            duplicateDetector = new DuplicateDetector(database);
        }

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

        public bool AddBook(Book book)
        {
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

                db.BeginTransaction();

                // MODIFIED: Insert book WITHOUT Sequence/NumberInSequence fields
                var bookParams = new[]
                {
                    DatabaseManager.CreateParameter("@ID", book.ID),
                    DatabaseManager.CreateParameter("@Version", book.Version),
                    DatabaseManager.CreateParameter("@FileName", book.FileName),
                    DatabaseManager.CreateParameter("@Title", book.Title),
                    DatabaseManager.CreateParameter("@Language", book.Language),
                    DatabaseManager.CreateParameter("@BookDate", book.BookDate),
                    DatabaseManager.CreateParameter("@DocumentDate", book.DocumentDate),
                    DatabaseManager.CreateParameter("@Annotation", book.Annotation),
                    DatabaseManager.CreateParameter("@DocumentSize", (long)book.DocumentSize),
                    DatabaseManager.CreateParameter("@AddedDate", book.AddedDate),
                    DatabaseManager.CreateParameter("@DocumentIDTrusted", book.DocumentIDTrusted),
                    DatabaseManager.CreateParameter("@DuplicateKey", book.DuplicateKey),
                    DatabaseManager.CreateParameter("@ReplacedByID", book.ReplacedByID),
                    DatabaseManager.CreateParameter("@ContentHash", book.ContentHash)
                };

                db.ExecuteNonQuery(DatabaseSchema.InsertBook, bookParams);

                // Clear existing relationships
                db.ExecuteNonQuery("DELETE FROM BookAuthors WHERE BookID = @BookID",
                    DatabaseManager.CreateParameter("@BookID", book.ID));
                db.ExecuteNonQuery("DELETE FROM BookGenres WHERE BookID = @BookID",
                    DatabaseManager.CreateParameter("@BookID", book.ID));
                db.ExecuteNonQuery("DELETE FROM BookTranslators WHERE BookID = @BookID",
                    DatabaseManager.CreateParameter("@BookID", book.ID));

                // NEW: Clear existing sequences
                db.ExecuteNonQuery("DELETE FROM BookSequences WHERE BookID = @BookID",
                    DatabaseManager.CreateParameter("@BookID", book.ID));

                // Add authors with enhanced fields
                foreach (var authorName in book.Authors)
                {
                    var (firstName, middleName, lastName) = ParseAuthorName(authorName);
                    string searchName = NormalizeForSearch(authorName);
                    string lastNameSoundex = !string.IsNullOrEmpty(lastName) ? StringUtils.Soundex(lastName) : "";
                    string nameTranslit = GenerateTransliteration(authorName);

                    // Insert author if not exists
                    db.ExecuteNonQuery(DatabaseSchema.InsertAuthor,
                        DatabaseManager.CreateParameter("@Name", authorName),
                        DatabaseManager.CreateParameter("@FirstName", firstName),
                        DatabaseManager.CreateParameter("@MiddleName", middleName),
                        DatabaseManager.CreateParameter("@LastName", lastName),
                        DatabaseManager.CreateParameter("@SearchName", searchName),
                        DatabaseManager.CreateParameter("@LastNameSoundex", lastNameSoundex),
                        DatabaseManager.CreateParameter("@NameTranslit", nameTranslit));

                    // Link book to author
                    db.ExecuteNonQuery(DatabaseSchema.InsertBookAuthor,
                        DatabaseManager.CreateParameter("@BookID", book.ID),
                        DatabaseManager.CreateParameter("@AuthorName", authorName));
                }

                // Add genres with validation
                int validGenreCount = 0;
                foreach (var genreTag in book.Genres)
                {
                    if (IsValidGenreTag(genreTag))
                    {
                        db.ExecuteNonQuery(DatabaseSchema.InsertBookGenre,
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
                    db.ExecuteNonQuery(DatabaseSchema.InsertTranslator,
                        DatabaseManager.CreateParameter("@Name", translatorName));

                    db.ExecuteNonQuery(DatabaseSchema.InsertBookTranslator,
                        DatabaseManager.CreateParameter("@BookID", book.ID),
                        DatabaseManager.CreateParameter("@TranslatorName", translatorName));
                }

                // NEW: Add sequences
                if (book.Sequences != null)
                {
                    foreach (var sequenceInfo in book.Sequences)
                    {
                        if (!string.IsNullOrEmpty(sequenceInfo.Name))
                        {
                            // Insert sequence if not exists
                            string searchName = NormalizeForSearch(sequenceInfo.Name);
                            db.ExecuteNonQuery(DatabaseSchema.InsertSequence,
                                DatabaseManager.CreateParameter("@Name", sequenceInfo.Name),
                                DatabaseManager.CreateParameter("@SearchName", searchName));

                            // Link book to sequence
                            db.ExecuteNonQuery(DatabaseSchema.InsertBookSequence,
                                DatabaseManager.CreateParameter("@BookID", book.ID),
                                DatabaseManager.CreateParameter("@SequenceName", sequenceInfo.Name),
                                DatabaseManager.CreateParameter("@NumberInSequence", (long)sequenceInfo.NumberInSequence));
                        }
                    }
                }

                db.CommitTransaction();
                return true;
            }
            catch (Exception ex)
            {
                db.RollbackTransaction();
                Log.WriteLine(LogLevel.Error, "Error adding book {0}: {1}", book.FileName, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Add multiple books in batch with FTS5 synchronization and duplicate detection
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

            try
            {
                // Optimize SQLite settings for bulk insert
                db.ExecuteNonQuery("PRAGMA synchronous = OFF");
                db.ExecuteNonQuery("PRAGMA journal_mode = MEMORY");
                db.ExecuteNonQuery("PRAGMA temp_store = MEMORY");
                db.ExecuteNonQuery("PRAGMA cache_size = 10000");

                db.BeginTransaction();

                foreach (var book in books)
                {
                    DuplicateCheckResult duplicateResult = null;

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

                            // Check for duplicates
                            duplicateResult = duplicateDetector.CheckDuplicate(book, fileStream);

                            if (duplicateResult.IsDuplicate)
                            {
                                bool shouldAdd = duplicateDetector.ProcessDuplicate(book, duplicateResult);

                                if (!shouldAdd)
                                {
                                    // Definite duplicate that should be skipped
                                    result.Duplicates++;
                                    continue;
                                }
                                else if (duplicateResult.ShouldReplace)
                                {
                                    // Mark as replacement AND as duplicate (since old book is being replaced)
                                    result.Replaced++;
                                    result.Duplicates++;
                                    result.ReplacedBooks.Add($"{duplicateResult.ExistingBook.FileName} -> {book.FileName}");
                                }
                                else
                                {
                                    // Enhanced logging for uncertain duplicates
                                    result.UncertainDuplicatesAdded++;
                                }
                            }
                        }
                        finally
                        {
                            fileStream?.Dispose();
                        }

                        // MODIFIED: Insert book WITHOUT Sequence/NumberInSequence fields
                        var bookParams = new[]
                        {
                            DatabaseManager.CreateParameter("@ID", book.ID),
                            DatabaseManager.CreateParameter("@Version", book.Version),
                            DatabaseManager.CreateParameter("@FileName", book.FileName),
                            DatabaseManager.CreateParameter("@Title", book.Title),
                            DatabaseManager.CreateParameter("@Language", book.Language),
                            DatabaseManager.CreateParameter("@BookDate", book.BookDate),
                            DatabaseManager.CreateParameter("@DocumentDate", book.DocumentDate),
                            DatabaseManager.CreateParameter("@Annotation", book.Annotation),
                            DatabaseManager.CreateParameter("@DocumentSize", (long)book.DocumentSize),
                            DatabaseManager.CreateParameter("@AddedDate", book.AddedDate),
                            DatabaseManager.CreateParameter("@DocumentIDTrusted", book.DocumentIDTrusted),
                            DatabaseManager.CreateParameter("@DuplicateKey", book.DuplicateKey),
                            DatabaseManager.CreateParameter("@ReplacedByID", book.ReplacedByID),
                            DatabaseManager.CreateParameter("@ContentHash", book.ContentHash)
                        };
                        db.ExecuteNonQuery(DatabaseSchema.InsertBook, bookParams);

                        // Insert authors and relationships
                        foreach (var authorName in book.Authors)
                        {
                            var (firstName, middleName, lastName) = ParseAuthorName(authorName);
                            string searchName = NormalizeForSearch(authorName);
                            string lastNameSoundex = !string.IsNullOrEmpty(lastName) ? StringUtils.Soundex(lastName) : "";
                            string nameTranslit = GenerateTransliteration(authorName);

                            // Insert author if not exists
                            db.ExecuteNonQuery(DatabaseSchema.InsertAuthor,
                                DatabaseManager.CreateParameter("@Name", authorName),
                                DatabaseManager.CreateParameter("@FirstName", firstName),
                                DatabaseManager.CreateParameter("@MiddleName", middleName),
                                DatabaseManager.CreateParameter("@LastName", lastName),
                                DatabaseManager.CreateParameter("@SearchName", searchName),
                                DatabaseManager.CreateParameter("@LastNameSoundex", lastNameSoundex),
                                DatabaseManager.CreateParameter("@NameTranslit", nameTranslit));

                            // Insert book-author relationship
                            db.ExecuteNonQuery(DatabaseSchema.InsertBookAuthor,
                                DatabaseManager.CreateParameter("@BookID", book.ID),
                                DatabaseManager.CreateParameter("@AuthorName", authorName));
                        }

                        // Insert genres with validation
                        foreach (var genreTag in book.Genres)
                        {
                            if (IsValidGenreTag(genreTag))
                            {
                                db.ExecuteNonQuery(DatabaseSchema.InsertBookGenre,
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
                            db.ExecuteNonQuery(DatabaseSchema.InsertTranslator,
                                DatabaseManager.CreateParameter("@Name", translatorName));

                            db.ExecuteNonQuery(DatabaseSchema.InsertBookTranslator,
                                DatabaseManager.CreateParameter("@BookID", book.ID),
                                DatabaseManager.CreateParameter("@TranslatorName", translatorName));
                        }

                        // NEW: Insert sequences
                        if (book.Sequences != null)
                        {
                            foreach (var sequenceInfo in book.Sequences)
                            {
                                if (!string.IsNullOrEmpty(sequenceInfo.Name))
                                {
                                    // Insert sequence if not exists
                                    string searchName = NormalizeForSearch(sequenceInfo.Name);
                                    db.ExecuteNonQuery(DatabaseSchema.InsertSequence,
                                        DatabaseManager.CreateParameter("@Name", sequenceInfo.Name),
                                        DatabaseManager.CreateParameter("@SearchName", searchName));

                                    // Link book to sequence
                                    db.ExecuteNonQuery(DatabaseSchema.InsertBookSequence,
                                        DatabaseManager.CreateParameter("@BookID", book.ID),
                                        DatabaseManager.CreateParameter("@SequenceName", sequenceInfo.Name),
                                        DatabaseManager.CreateParameter("@NumberInSequence", (long)sequenceInfo.NumberInSequence));
                                }
                            }
                        }

                        // Only count as added if it's not a replacement
                        if (duplicateResult == null || !duplicateResult.IsDuplicate || !duplicateResult.ShouldReplace)
                        {
                            result.Added++;

                            // Count by type for statistics
                            if (book.BookType == BookType.FB2)
                                result.FB2Count++;
                            else
                                result.EPUBCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors++;
                        result.ErrorMessages.Add($"Book {book.FileName}: {ex.Message}");
                        Log.WriteLine(LogLevel.Error, "BookRepository.AddBooksBatch - Book {0}: {1}", book.FileName, ex.Message);
                    }
                }

                db.CommitTransaction();

                result.ProcessingTime = DateTime.Now - startTime;

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

                return result;
            }
            catch (Exception ex)
            {
                db.RollbackTransaction();
                result.Errors = result.TotalProcessed;
                result.ErrorMessages.Add($"Batch transaction failed: {ex.Message}");
                result.ProcessingTime = DateTime.Now - startTime;
                Log.WriteLine(LogLevel.Error, "BookRepository.AddBooksBatch: {0}", ex.Message);
                return result;
            }
            finally
            {
                // Restore normal SQLite settings
                db.ExecuteNonQuery("PRAGMA synchronous = NORMAL");
                db.ExecuteNonQuery("PRAGMA journal_mode = DELETE");
                db.ExecuteNonQuery("PRAGMA temp_store = DEFAULT");
                db.ExecuteNonQuery("PRAGMA cache_size = 2000");
            }
        }

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

        public bool BookExists(string fileName)
        {
            var count = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE FileName = @FileName AND ReplacedByID IS NULL",
                DatabaseManager.CreateParameter("@FileName", fileName));
            return Convert.ToInt32(count) > 0;
        }

        #endregion

        #region Book Queries

        public List<Book> GetAllBooks()
        {
            var books = db.ExecuteQuery<Book>(DatabaseSchema.SelectAllBooks, MapBook);
            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksByAuthor(string authorName)
        {
            var books = db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksByAuthor, MapBook,
                DatabaseManager.CreateParameter("@AuthorName", authorName));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        // MODIFIED: Now uses optimized query with JOIN
        public List<Book> GetBooksBySequence(string sequence)
        {
            var books = db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksBySequence,
                reader => {
                    var book = MapBook(reader);
                    // Get NumberInSequence from the joined query
                    try
                    {
                        var number = DatabaseManager.GetUInt32(reader, "NumberInSequence");
                        if (book.Sequences == null)
                            book.Sequences = new List<BookSequenceInfo>();
                        book.Sequences.Add(new BookSequenceInfo(sequence, number));
                    }
                    catch { }
                    return book;
                },
                DatabaseManager.CreateParameter("@SequenceName", sequence));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksByGenre(string genreTag)
        {
            var books = db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksByGenre, MapBook,
                DatabaseManager.CreateParameter("@GenreTag", genreTag));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksByTitle(string title)
        {
            // Simple LIKE search for navigation
            var books = db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksByTitleLike, MapBook,
                DatabaseManager.CreateParameter("@Title", title));

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

                // Prepare FTS search pattern with wildcards for partial matching
                string ftsPattern = PrepareSearchPatternForFTS(searchPattern);

                // First try FTS5 search
                books = db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksByTitleFTS, MapBook,
                    DatabaseManager.CreateParameter("@SearchPattern", ftsPattern),
                    DatabaseManager.CreateParameter("@LikePattern", searchPattern));

                // If no results and contains Latin letters, try transliteration
                if (books.Count == 0 && ContainsLatinLetters(searchPattern))
                {
                    Log.WriteLine(LogLevel.Info, "GetBooksForOpenSearch: trying transliteration fallback for '{0}'", searchPattern);

                    // Try GOST transliteration first
                    string transliteratedGOST = Transliteration.Back(searchPattern, TransliterationType.GOST);
                    if (!string.IsNullOrEmpty(transliteratedGOST) && transliteratedGOST != searchPattern)
                    {
                        string ftsPatternGOST = PrepareSearchPatternForFTS(transliteratedGOST);
                        var gostBooks = db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksByTitleFTS, MapBook,
                            DatabaseManager.CreateParameter("@SearchPattern", ftsPatternGOST),
                            DatabaseManager.CreateParameter("@LikePattern", transliteratedGOST));

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
                            var isoBooks = db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksByTitleFTS, MapBook,
                                DatabaseManager.CreateParameter("@SearchPattern", ftsPatternISO),
                                DatabaseManager.CreateParameter("@LikePattern", transliteratedISO));

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
                    books = db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksByTitleLike, MapBook,
                        DatabaseManager.CreateParameter("@Title", searchPattern));
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
            var books = db.ExecuteQuery<Book>(DatabaseSchema.SelectNewBooks, MapBook,
                DatabaseManager.CreateParameter("@FromDate", fromDate));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksByFileNamePrefix(string fileNamePrefix)
        {
            // MODIFIED: Query without Sequence/NumberInSequence fields
            var books = db.ExecuteQuery<Book>(@"
                SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                       Annotation, DocumentSize, AddedDate
                FROM Books 
                WHERE FileName LIKE @FileNamePrefix || '%'",
                MapBook,
                DatabaseManager.CreateParameter("@FileNamePrefix", fileNamePrefix));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        #endregion

        #region Sequence Methods - NEW

        /// <summary>
        /// Get all sequences for navigation
        /// </summary>
        public List<string> GetSequencesForNavigation(string searchPattern)
        {
            try
            {
                if (string.IsNullOrEmpty(searchPattern))
                {
                    return db.ExecuteQuery<string>(DatabaseSchema.SelectSequences,
                        reader => reader.GetString(0));
                }

                return db.ExecuteQuery<string>(DatabaseSchema.SelectSequencesByPrefix,
                    reader => reader.GetString(0),
                    DatabaseManager.CreateParameter("@Pattern", searchPattern));
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GetSequencesForNavigation {0}: {1}", searchPattern, ex.Message);
                return new List<string>();
            }
        }

        /// <summary>
        /// Get sequences with book count
        /// </summary>
        public List<(string Name, int BookCount)> GetSequencesWithCount(string searchPattern = null)
        {
            try
            {
                List<(string, int)> result;

                if (string.IsNullOrEmpty(searchPattern))
                {
                    result = db.ExecuteQuery<(string, int)>(DatabaseSchema.SelectSequencesWithCount,
                        reader => (reader.GetString(0), reader.GetInt32(1)));
                }
                else
                {
                    result = db.ExecuteQuery<(string, int)>(DatabaseSchema.SelectSequencesWithCountByPrefix,
                        reader => (reader.GetString(0), reader.GetInt32(1)),
                        DatabaseManager.CreateParameter("@Pattern", searchPattern));
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
        /// Get authors for navigation (catalog browsing with pagination)
        /// </summary>
        public List<string> GetAuthorsForNavigation(string searchPattern)
        {
            try
            {
                if (string.IsNullOrEmpty(searchPattern))
                {
                    return db.ExecuteQuery<string>(DatabaseSchema.SelectAuthors, reader => reader.GetString(0));
                }

                return db.ExecuteQuery<string>(DatabaseSchema.SelectAuthorsByPrefix,
                    reader => reader.GetString(0),
                    DatabaseManager.CreateParameter("@Pattern", searchPattern));
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
        /// Get authors for OpenSearch with search method information
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

                if (words.Length == 0)
                {
                    return (new List<string>(), AuthorSearchMethod.NotFound);
                }

                // STEP 1: For two-word input, try EXACT match using FTS
                if (words.Length == 2)
                {
                    Log.WriteLine(LogLevel.Info, "Step 1: Trying exact match for two-word query '{0}' using FTS", searchPattern);

                    // Escape the search pattern for FTS
                    string escapedPattern = EscapeForFTS(searchPattern);
                    // Use FTS to find exact match - it handles case-insensitive Unicode properly
                    string ftsQuery = $"\"{escapedPattern}\""; // Exact phrase in FTS

                    string sql = @"
                        SELECT DISTINCT a.Name
                        FROM Authors a
                        INNER JOIN AuthorsFTS fts ON a.ID = fts.AuthorID
                        INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
                        WHERE AuthorsFTS MATCH @SearchPattern
                        ORDER BY a.Name";

                    authors = db.ExecuteQuery<string>(sql,
                        reader => reader.GetString(0),
                        DatabaseManager.CreateParameter("@SearchPattern", ftsQuery));

                    if (authors.Count > 0)
                    {
                        Log.WriteLine(LogLevel.Info, "Found exact match using FTS: {0} authors", authors.Count);
                        return (authors, AuthorSearchMethod.ExactMatch);
                    }

                    // Try reversed order in FTS
                    string reversedPattern = $"{words[1]} {words[0]}";
                    string escapedReversedPattern = EscapeForFTS(reversedPattern);
                    string reversedFtsQuery = $"\"{escapedReversedPattern}\"";

                    authors = db.ExecuteQuery<string>(sql,
                        reader => reader.GetString(0),
                        DatabaseManager.CreateParameter("@SearchPattern", reversedFtsQuery));

                    if (authors.Count > 0)
                    {
                        Log.WriteLine(LogLevel.Info, "Found exact match with reversed order using FTS: {0} authors", authors.Count);
                        return (authors, AuthorSearchMethod.ExactMatch);
                    }
                }

                // STEP 2: Partial match ONLY for single word using FTS
                if (words.Length == 1)
                {
                    string pattern = words[0];
                    Log.WriteLine(LogLevel.Info, "Step 2: Trying partial match for single word '{0}' using FTS", pattern);

                    // Escape and prepare for wildcard search
                    string escapedWord = EscapeForFTS(pattern);
                    // Use FTS with wildcard for prefix search
                    string ftsPattern = $"{escapedWord}*";

                    string sql = @"
                        SELECT DISTINCT a.Name
                        FROM Authors a
                        INNER JOIN AuthorsFTS fts ON a.ID = fts.AuthorID
                        INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
                        WHERE AuthorsFTS MATCH @SearchPattern
                        ORDER BY a.Name";

                    authors = db.ExecuteQuery<string>(sql,
                        reader => reader.GetString(0),
                        DatabaseManager.CreateParameter("@SearchPattern", ftsPattern));

                    if (authors.Count > 0)
                    {
                        Log.WriteLine(LogLevel.Info, "Found partial match using FTS: {0} authors", authors.Count);
                        return (authors, AuthorSearchMethod.PartialMatch);
                    }
                }
                // For multi-word queries - NO partial search after exact match fails

                // STEP 3: Transliteration search (if input contains Latin letters)
                if (ContainsLatinLetters(searchPattern))
                {
                    Log.WriteLine(LogLevel.Info, "Step 3: Trying transliteration search");

                    // Try GOST transliteration
                    string transliteratedGOST = Transliteration.Back(searchPattern, TransliterationType.GOST);
                    if (!string.IsNullOrEmpty(transliteratedGOST) && transliteratedGOST != searchPattern)
                    {
                        // Recursive call with transliterated text - use the original method to avoid infinite recursion
                        var (translitAuthors, _) = GetAuthorsForOpenSearchWithMethod(transliteratedGOST);
                        if (translitAuthors.Count > 0)
                        {
                            Log.WriteLine(LogLevel.Info, "Found via GOST transliteration: {0} authors", translitAuthors.Count);
                            return (translitAuthors, AuthorSearchMethod.Transliteration);
                        }
                    }

                    // Try ISO transliteration
                    string transliteratedISO = Transliteration.Back(searchPattern, TransliterationType.ISO);
                    if (!string.IsNullOrEmpty(transliteratedISO) && transliteratedISO != searchPattern && transliteratedISO != transliteratedGOST)
                    {
                        // Recursive call with transliterated text
                        var (translitAuthors, _) = GetAuthorsForOpenSearchWithMethod(transliteratedISO);
                        if (translitAuthors.Count > 0)
                        {
                            Log.WriteLine(LogLevel.Info, "Found via ISO transliteration: {0} authors", translitAuthors.Count);
                            return (translitAuthors, AuthorSearchMethod.Transliteration);
                        }
                    }
                }

                // STEP 4: Soundex search (last resort) - still using regular table as Soundex is pre-calculated
                string soundexPattern = words.Length == 1 ? words[0] : words[words.Length - 1];
                string soundex = StringUtils.Soundex(soundexPattern);

                if (!string.IsNullOrEmpty(soundex))
                {
                    Log.WriteLine(LogLevel.Info, "Step 4: Trying Soundex search for '{0}' -> '{1}'", soundexPattern, soundex);

                    authors = db.ExecuteQuery<string>(DatabaseSchema.SelectAuthorsBySoundex,
                        reader => reader.GetString(0),
                        DatabaseManager.CreateParameter("@Soundex", soundex));

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
        /// Get genres with book count > 0
        /// </summary>
        public List<(Genre genre, int bookCount)> GetGenresWithBooks()
        {
            try
            {
                var result = new List<(Genre genre, int bookCount)>();

                var data = db.ExecuteQuery<(string Tag, string ParentName, string Name, string Translation, int BookCount)>(
                    DatabaseSchema.SelectGenresWithBookCount,
                    reader => (
                        reader.GetString(0),  // Tag
                        DatabaseManager.GetString(reader, "ParentName"),
                        reader.GetString(2),  // Name
                        DatabaseManager.GetString(reader, "Translation"),
                        reader.GetInt32(4)   // BookCount
                    ));

                foreach (var item in data)
                {
                    var genre = new Genre
                    {
                        Tag = item.Tag,
                        Name = item.Name,
                        Translation = item.Translation
                    };
                    result.Add((genre, item.BookCount));
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

        #region Statistics

        public int GetTotalBooksCount()
        {
            var result = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE ReplacedByID IS NULL");
            return Convert.ToInt32(result);
        }

        public int GetFB2BooksCount()
        {
            var result = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE FileName LIKE '%.fb2%' AND ReplacedByID IS NULL");
            return Convert.ToInt32(result);
        }

        public int GetEPUBBooksCount()
        {
            var result = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE FileName LIKE '%.epub%' AND ReplacedByID IS NULL");
            return Convert.ToInt32(result);
        }

        public int GetNewBooksCount(DateTime fromDate)
        {
            var result = db.ExecuteScalar(DatabaseSchema.CountNewBooks,
                DatabaseManager.CreateParameter("@FromDate", fromDate));
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// Get new books with pagination support
        /// </summary>
        public List<Book> GetNewBooksPaginated(DateTime fromDate, int offset, int limit, bool sortByDate = true)
        {
            try
            {
                string query = sortByDate
                    ? DatabaseSchema.SelectNewBooksPaginatedByDate
                    : DatabaseSchema.SelectNewBooksPaginatedByTitle;

                var books = db.ExecuteQuery<Book>(query, MapBook,
                    DatabaseManager.CreateParameter("@FromDate", fromDate),
                    DatabaseManager.CreateParameter("@Offset", offset),
                    DatabaseManager.CreateParameter("@Limit", limit));

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
            return db.ExecuteQuery<string>(DatabaseSchema.SelectAuthors, reader => reader.GetString(0));
        }

        // MODIFIED: Now gets sequences from Sequences table
        public List<string> GetAllSequences()
        {
            return db.ExecuteQuery<string>(DatabaseSchema.SelectSequences, reader => reader.GetString(0));
        }

        public List<string> GetAllGenreTags()
        {
            return db.ExecuteQuery<string>(DatabaseSchema.SelectGenreTags, reader => reader.GetString(0));
        }

        public int GetAuthorsCount()
        {
            var result = db.ExecuteScalar(DatabaseSchema.SelectAuthorsCount);
            return Convert.ToInt32(result);
        }

        public int GetSequencesCount()
        {
            var result = db.ExecuteScalar(DatabaseSchema.SelectSequencesCount);
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// Get count of books by genre
        /// </summary>
        public int GetBooksByGenreCount(string genreTag)
        {
            try
            {
                var result = db.ExecuteScalar(DatabaseSchema.CountBooksByGenre,
                    DatabaseManager.CreateParameter("@GenreTag", genreTag));
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error counting books for genre {0}: {1}", genreTag, ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Get count of books by sequence
        /// </summary>
        public int GetBooksBySequenceCount(string sequenceName)
        {
            try
            {
                var result = db.ExecuteScalar(DatabaseSchema.CountBooksBySequence,
                    DatabaseManager.CreateParameter("@SequenceName", sequenceName));
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error counting books for sequence {0}: {1}", sequenceName, ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Get statistics for all genres with book counts
        /// </summary>
        public Dictionary<string, int> GetAllGenreStatistics()
        {
            try
            {
                var result = new Dictionary<string, int>();

                var statistics = db.ExecuteQuery<(string GenreTag, int BookCount)>(
                    DatabaseSchema.SelectGenreStatistics,
                    reader => (reader.GetString(0), reader.GetInt32(1)));

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
        /// Get full genre statistics including parent and translation info
        /// </summary>
        public List<(Genre genre, int bookCount)> GetFullGenreStatistics()
        {
            try
            {
                var result = new List<(Genre genre, int bookCount)>();

                var statistics = db.ExecuteQuery<(string Tag, string ParentName, string Name, string Translation, int BookCount)>(
                    DatabaseSchema.SelectGenreStatisticsFull,
                    reader => (
                        reader.GetString(0),
                        DatabaseManager.GetString(reader, "ParentName"),
                        reader.GetString(2),
                        DatabaseManager.GetString(reader, "Translation"),
                        reader.GetInt32(4)
                    ));

                foreach (var stat in statistics)
                {
                    var genre = new Genre
                    {
                        Tag = stat.Tag,
                        Name = stat.Name,
                        Translation = stat.Translation
                    };
                    result.Add((genre, stat.BookCount));
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

        // MODIFIED: MapBook no longer tries to read Sequence/NumberInSequence from database
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

        // MODIFIED: LoadBookRelations now loads sequences from BookSequences table
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

            // NEW: Load sequences from BookSequences table
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
        /// Check if string contains Latin letters
        /// </summary>
        private bool ContainsLatinLetters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            foreach (char c in text)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
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
                if ((c >= 'А' && c <= 'я') || c == 'Ё' || c == 'ё')
                    return true;
            }
            return false;
        }

        #endregion
    }
}