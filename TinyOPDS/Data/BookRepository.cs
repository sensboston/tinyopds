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
using System.Linq;

namespace TinyOPDS.Data
{
    public class BookRepository
    {
        private readonly DatabaseManager _db;

        public BookRepository(DatabaseManager database)
        {
            _db = database;
        }

        /// <summary>
        /// Result of batch operations with detailed statistics
        /// </summary>
        public class BatchResult
        {
            public int TotalProcessed { get; set; }
            public int Added { get; set; }
            public int Duplicates { get; set; }
            public int Errors { get; set; }
            public int FB2Count { get; set; }
            public int EPUBCount { get; set; }
            public TimeSpan ProcessingTime { get; set; }
            public List<string> ErrorMessages { get; set; } = new List<string>();

            public bool IsSuccess => Added > 0 || Duplicates > 0;
        }

        #region Book CRUD Operations

        public bool AddBook(Book book)
        {
            try
            {
                _db.BeginTransaction();

                // Insert or update book
                var bookParams = new[]
                {
                    DatabaseManager.CreateParameter("@ID", book.ID),
                    DatabaseManager.CreateParameter("@Version", book.Version),
                    DatabaseManager.CreateParameter("@FileName", book.FileName),
                    DatabaseManager.CreateParameter("@Title", book.Title),
                    DatabaseManager.CreateParameter("@Language", book.Language),
                    DatabaseManager.CreateParameter("@BookDate", book.BookDate == DateTime.MinValue ? (DateTime?)null : book.BookDate),
                    DatabaseManager.CreateParameter("@DocumentDate", book.DocumentDate == DateTime.MinValue ? (DateTime?)null : book.DocumentDate),
                    DatabaseManager.CreateParameter("@Sequence", book.Sequence),
                    DatabaseManager.CreateParameter("@NumberInSequence", (long)book.NumberInSequence),
                    DatabaseManager.CreateParameter("@Annotation", book.Annotation),
                    DatabaseManager.CreateParameter("@DocumentSize", (long)book.DocumentSize),
                    DatabaseManager.CreateParameter("@AddedDate", book.AddedDate)
                };

                _db.ExecuteNonQuery(DatabaseSchema.InsertBook, bookParams);

                // Get the rowid for FTS synchronization
                var bookRowIdResult = _db.ExecuteScalar("SELECT rowid FROM Books WHERE ID = @ID",
                    DatabaseManager.CreateParameter("@ID", book.ID));
                long bookRowId = Convert.ToInt64(bookRowIdResult);

                // Sync with FTS table
                _db.ExecuteNonQuery(DatabaseSchema.InsertBookTitleFTS,
                    DatabaseManager.CreateParameter("@rowid", bookRowId),
                    DatabaseManager.CreateParameter("@Title", book.Title));

                // Clear existing relationships
                _db.ExecuteNonQuery("DELETE FROM BookAuthors WHERE BookID = @BookID",
                    DatabaseManager.CreateParameter("@BookID", book.ID));
                _db.ExecuteNonQuery("DELETE FROM BookGenres WHERE BookID = @BookID",
                    DatabaseManager.CreateParameter("@BookID", book.ID));
                _db.ExecuteNonQuery("DELETE FROM BookTranslators WHERE BookID = @BookID",
                    DatabaseManager.CreateParameter("@BookID", book.ID));

                // Add authors
                foreach (var authorName in book.Authors)
                {
                    var (firstName, middleName, lastName) = ParseAuthorName(authorName);
                    string lastNameSoundex = !string.IsNullOrEmpty(lastName) ? StringUtils.Soundex(lastName) : "";

                    // Insert author if not exists
                    _db.ExecuteNonQuery(DatabaseSchema.InsertAuthor,
                        DatabaseManager.CreateParameter("@Name", authorName),
                        DatabaseManager.CreateParameter("@FirstName", firstName),
                        DatabaseManager.CreateParameter("@MiddleName", middleName),
                        DatabaseManager.CreateParameter("@LastName", lastName),
                        DatabaseManager.CreateParameter("@LastNameSoundex", lastNameSoundex));

                    // Link book to author
                    _db.ExecuteNonQuery(DatabaseSchema.InsertBookAuthor,
                        DatabaseManager.CreateParameter("@BookID", book.ID),
                        DatabaseManager.CreateParameter("@AuthorName", authorName));
                }

                // Add genres
                foreach (var genreTag in book.Genres)
                {
                    _db.ExecuteNonQuery(DatabaseSchema.InsertBookGenre,
                        DatabaseManager.CreateParameter("@BookID", book.ID),
                        DatabaseManager.CreateParameter("@GenreTag", genreTag));
                }

                // Add translators
                foreach (var translatorName in book.Translators)
                {
                    _db.ExecuteNonQuery(DatabaseSchema.InsertTranslator,
                        DatabaseManager.CreateParameter("@Name", translatorName));

                    _db.ExecuteNonQuery(DatabaseSchema.InsertBookTranslator,
                        DatabaseManager.CreateParameter("@BookID", book.ID),
                        DatabaseManager.CreateParameter("@TranslatorName", translatorName));
                }

                // Update author book counts
                foreach (var authorName in book.Authors)
                {
                    _db.ExecuteNonQuery(DatabaseSchema.UpdateAuthorBookCount,
                        DatabaseManager.CreateParameter("@AuthorName", authorName));
                }

                _db.CommitTransaction();
                return true;
            }
            catch (Exception ex)
            {
                _db.RollbackTransaction();
                Log.WriteLine(LogLevel.Error, "Error adding book {0}: {1}", book.FileName, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Add multiple books in batch with FTS5 synchronization
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
                _db.ExecuteNonQuery("PRAGMA synchronous = OFF");
                _db.ExecuteNonQuery("PRAGMA journal_mode = MEMORY");
                _db.ExecuteNonQuery("PRAGMA temp_store = MEMORY");
                _db.ExecuteNonQuery("PRAGMA cache_size = 10000");

                _db.BeginTransaction();

                var authorCounts = new Dictionary<string, int>();

                foreach (var book in books)
                {
                    try
                    {
                        // Check for duplicates
                        if (BookExists(book.FileName))
                        {
                            result.Duplicates++;
                            continue;
                        }

                        // Insert book
                        var bookParams = new[]
                        {
                            DatabaseManager.CreateParameter("@ID", book.ID),
                            DatabaseManager.CreateParameter("@Version", book.Version),
                            DatabaseManager.CreateParameter("@FileName", book.FileName),
                            DatabaseManager.CreateParameter("@Title", book.Title),
                            DatabaseManager.CreateParameter("@Language", book.Language),
                            DatabaseManager.CreateParameter("@BookDate", book.BookDate == DateTime.MinValue ? (DateTime?)null : book.BookDate),
                            DatabaseManager.CreateParameter("@DocumentDate", book.DocumentDate == DateTime.MinValue ? (DateTime?)null : book.DocumentDate),
                            DatabaseManager.CreateParameter("@Sequence", book.Sequence),
                            DatabaseManager.CreateParameter("@NumberInSequence", (long)book.NumberInSequence),
                            DatabaseManager.CreateParameter("@Annotation", book.Annotation),
                            DatabaseManager.CreateParameter("@DocumentSize", (long)book.DocumentSize),
                            DatabaseManager.CreateParameter("@AddedDate", book.AddedDate)
                        };
                        _db.ExecuteNonQuery(DatabaseSchema.InsertBook, bookParams);

                        // Get rowid for FTS synchronization
                        var bookRowIdResult = _db.ExecuteScalar("SELECT rowid FROM Books WHERE ID = @ID",
                            DatabaseManager.CreateParameter("@ID", book.ID));
                        long bookRowId = Convert.ToInt64(bookRowIdResult);

                        // Sync with FTS table
                        _db.ExecuteNonQuery(DatabaseSchema.InsertBookTitleFTS,
                            DatabaseManager.CreateParameter("@rowid", bookRowId),
                            DatabaseManager.CreateParameter("@Title", book.Title));

                        // Insert authors and relationships
                        foreach (var authorName in book.Authors)
                        {
                            var (firstName, middleName, lastName) = ParseAuthorName(authorName);
                            string lastNameSoundex = !string.IsNullOrEmpty(lastName) ? StringUtils.Soundex(lastName) : "";

                            // Insert author if not exists
                            _db.ExecuteNonQuery(DatabaseSchema.InsertAuthor,
                                DatabaseManager.CreateParameter("@Name", authorName),
                                DatabaseManager.CreateParameter("@FirstName", firstName),
                                DatabaseManager.CreateParameter("@MiddleName", middleName),
                                DatabaseManager.CreateParameter("@LastName", lastName),
                                DatabaseManager.CreateParameter("@LastNameSoundex", lastNameSoundex));

                            // Insert book-author relationship
                            _db.ExecuteNonQuery(DatabaseSchema.InsertBookAuthor,
                                DatabaseManager.CreateParameter("@BookID", book.ID),
                                DatabaseManager.CreateParameter("@AuthorName", authorName));

                            // Track author counts for batch update
                            if (!authorCounts.ContainsKey(authorName))
                                authorCounts[authorName] = 0;
                            authorCounts[authorName]++;
                        }

                        // Insert genres and relationships
                        foreach (var genreTag in book.Genres)
                        {
                            _db.ExecuteNonQuery(DatabaseSchema.InsertBookGenre,
                                DatabaseManager.CreateParameter("@BookID", book.ID),
                                DatabaseManager.CreateParameter("@GenreTag", genreTag));
                        }

                        // Insert translators and relationships
                        foreach (var translatorName in book.Translators)
                        {
                            _db.ExecuteNonQuery(DatabaseSchema.InsertTranslator,
                                DatabaseManager.CreateParameter("@Name", translatorName));

                            _db.ExecuteNonQuery(DatabaseSchema.InsertBookTranslator,
                                DatabaseManager.CreateParameter("@BookID", book.ID),
                                DatabaseManager.CreateParameter("@TranslatorName", translatorName));
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
                        result.ErrorMessages.Add($"Book {book.FileName}: {ex.Message}");
                        Log.WriteLine(LogLevel.Error, "BookRepository.AddBooksBatch - Book {0}: {1}", book.FileName, ex.Message);
                    }
                }

                // Update author book counts in batch
                foreach (var authorCount in authorCounts)
                {
                    _db.ExecuteNonQuery(DatabaseSchema.UpdateAuthorBookCount,
                        DatabaseManager.CreateParameter("@AuthorName", authorCount.Key));
                }

                _db.CommitTransaction();

                result.ProcessingTime = DateTime.Now - startTime;
                Log.WriteLine("Batch insert completed: {0} added, {1} duplicates skipped, {2} errors in {3}ms",
                    result.Added, result.Duplicates, result.Errors, result.ProcessingTime.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                _db.RollbackTransaction();
                result.Errors = result.TotalProcessed;
                result.ErrorMessages.Add($"Batch transaction failed: {ex.Message}");
                result.ProcessingTime = DateTime.Now - startTime;
                Log.WriteLine(LogLevel.Error, "BookRepository.AddBooksBatch: {0}", ex.Message);
                return result;
            }
            finally
            {
                // Restore normal SQLite settings
                _db.ExecuteNonQuery("PRAGMA synchronous = NORMAL");
                _db.ExecuteNonQuery("PRAGMA journal_mode = DELETE");
                _db.ExecuteNonQuery("PRAGMA temp_store = DEFAULT");
                _db.ExecuteNonQuery("PRAGMA cache_size = 2000");
            }
        }

        public Book GetBookById(string id)
        {
            var book = _db.ExecuteQuerySingle<Book>(DatabaseSchema.SelectBookById, MapBook,
                DatabaseManager.CreateParameter("@ID", id));

            if (book != null)
            {
                LoadBookRelations(book);
            }

            return book;
        }

        public Book GetBookByFileName(string fileName)
        {
            var book = _db.ExecuteQuerySingle<Book>(DatabaseSchema.SelectBookByFileName, MapBook,
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
                _db.BeginTransaction();

                // Get rowid before deletion for FTS cleanup
                var bookRowIdResult = _db.ExecuteScalar("SELECT rowid FROM Books WHERE ID = @ID",
                    DatabaseManager.CreateParameter("@ID", id));
                long bookRowId = Convert.ToInt64(bookRowIdResult);

                // Delete from main table
                var result = _db.ExecuteNonQuery(DatabaseSchema.DeleteBook,
                    DatabaseManager.CreateParameter("@ID", id));

                // Delete from FTS table
                if (result > 0)
                {
                    _db.ExecuteNonQuery(DatabaseSchema.DeleteBookTitleFTS,
                        DatabaseManager.CreateParameter("@rowid", bookRowId));
                }

                _db.CommitTransaction();
                return result > 0;
            }
            catch (Exception ex)
            {
                _db.RollbackTransaction();
                Log.WriteLine(LogLevel.Error, "Error deleting book {0}: {1}", id, ex.Message);
                return false;
            }
        }

        public bool DeleteBookByFileName(string fileName)
        {
            try
            {
                _db.BeginTransaction();

                // Get rowid before deletion for FTS cleanup
                var bookRowIdResult = _db.ExecuteScalar("SELECT rowid FROM Books WHERE FileName = @FileName",
                    DatabaseManager.CreateParameter("@FileName", fileName));
                long bookRowId = Convert.ToInt64(bookRowIdResult);

                // Delete from main table
                var result = _db.ExecuteNonQuery(DatabaseSchema.DeleteBookByFileName,
                    DatabaseManager.CreateParameter("@FileName", fileName));

                // Delete from FTS table
                if (result > 0)
                {
                    _db.ExecuteNonQuery(DatabaseSchema.DeleteBookTitleFTS,
                        DatabaseManager.CreateParameter("@rowid", bookRowId));
                }

                _db.CommitTransaction();
                return result > 0;
            }
            catch (Exception ex)
            {
                _db.RollbackTransaction();
                Log.WriteLine(LogLevel.Error, "Error deleting book by filename {0}: {1}", fileName, ex.Message);
                return false;
            }
        }

        public bool BookExists(string fileName)
        {
            var count = _db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE FileName = @FileName",
                DatabaseManager.CreateParameter("@FileName", fileName));
            return Convert.ToInt32(count) > 0;
        }

        #endregion

        #region Book Queries

        public List<Book> GetAllBooks()
        {
            var books = _db.ExecuteQuery<Book>(DatabaseSchema.SelectAllBooks, MapBook);
            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksByAuthor(string authorName)
        {
            var books = _db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksByAuthor, MapBook,
                DatabaseManager.CreateParameter("@AuthorName", authorName));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksBySequence(string sequence)
        {
            var books = _db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksBySequence, MapBook,
                DatabaseManager.CreateParameter("@Sequence", sequence));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksByGenre(string genreTag)
        {
            var books = _db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksByGenre, MapBook,
                DatabaseManager.CreateParameter("@GenreTag", genreTag));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksByTitle(string title)
        {
            var books = _db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksByTitle, MapBook,
                DatabaseManager.CreateParameter("@Title", title));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        /// <summary>
        /// Get books by title - OpenSearch version with only partial matching (NO Soundex)
        /// </summary>
        /// <param name="title">Title to search for</param>
        /// <param name="isOpenSearch">Whether this is OpenSearch (ignored - no Soundex for titles)</param>
        /// <returns>List of matching books</returns>
        public List<Book> GetBooksByTitle(string title, bool isOpenSearch)
        {
            return GetBooksByTitle(title);
        }

        /// <summary>
        /// NEW: Get books for OpenSearch using FTS5 with transliteration fallback
        /// </summary>
        /// <param name="searchPattern">Search pattern</param>
        /// <returns>List of matching books with priority ordering</returns>
        public List<Book> GetBooksForOpenSearch(string searchPattern)
        {
            try
            {
                if (string.IsNullOrEmpty(searchPattern))
                {
                    return new List<Book>();
                }

                Log.WriteLine(LogLevel.Info, "GetBooksForOpenSearch: searching for '{0}'", searchPattern);

                // First try FTS5 search
                var books = _db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksByTitleFTS, MapBook,
                    DatabaseManager.CreateParameter("@SearchPattern", searchPattern));

                // If no results and contains Latin letters, try transliteration
                if (books.Count == 0 && ContainsLatinLetters(searchPattern))
                {
                    Log.WriteLine(LogLevel.Info, "GetBooksForOpenSearch: trying transliteration fallback for '{0}'", searchPattern);

                    // Try GOST transliteration first
                    string transliteratedGOST = Transliteration.Back(searchPattern, TransliterationType.GOST);
                    if (!string.IsNullOrEmpty(transliteratedGOST) && transliteratedGOST != searchPattern)
                    {
                        var gostBooks = _db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksByTitleFTS, MapBook,
                            DatabaseManager.CreateParameter("@SearchPattern", transliteratedGOST));

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
                            var isoBooks = _db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksByTitleFTS, MapBook,
                                DatabaseManager.CreateParameter("@SearchPattern", transliteratedISO));

                            if (isoBooks.Count > 0)
                            {
                                Log.WriteLine(LogLevel.Info, "GetBooksForOpenSearch: ISO transliteration '{0}' -> '{1}' found {2} books",
                                            searchPattern, transliteratedISO, isoBooks.Count);
                                books.AddRange(isoBooks);
                            }
                        }
                    }
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
            var books = _db.ExecuteQuery<Book>(DatabaseSchema.SelectNewBooks, MapBook,
                DatabaseManager.CreateParameter("@FromDate", fromDate));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksByFileNamePrefix(string fileNamePrefix)
        {
            var books = _db.ExecuteQuery<Book>(@"
                SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                       Sequence, NumberInSequence, Annotation, DocumentSize, AddedDate
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

        #region Author Search Methods

        /// <summary>
        /// Get authors for navigation (catalog browsing with pagination)
        /// </summary>
        /// <param name="searchPattern">Search pattern for prefix matching</param>
        /// <returns>List of matching author names</returns>
        public List<string> GetAuthorsForNavigation(string searchPattern)
        {
            try
            {
                if (string.IsNullOrEmpty(searchPattern))
                {
                    return _db.ExecuteQuery<string>(DatabaseSchema.SelectAuthors, reader => reader.GetString(0));
                }

                string namePattern = $"{searchPattern}%";
                return _db.ExecuteQuery<string>(DatabaseSchema.SelectAuthorsByNamePattern,
                    reader => reader.GetString(0),
                    DatabaseManager.CreateParameter("@Pattern", namePattern));
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GetAuthorsForNavigation {0}: {1}", searchPattern, ex.Message);
                return new List<string>();
            }
        }

        /// <summary>
        /// Get authors for OpenSearch with transliteration fallback for Latin input
        /// </summary>
        /// <param name="searchPattern">Search pattern</param>
        /// <returns>List of matching author names</returns>
        public List<string> GetAuthorsForOpenSearch(string searchPattern)
        {
            try
            {
                if (string.IsNullOrEmpty(searchPattern))
                {
                    return new List<string>();
                }

                var authors = new List<string>();
                var words = searchPattern.Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                Log.WriteLine(LogLevel.Info, "GetAuthorsForOpenSearch: searching for '{0}', {1} words", searchPattern, words.Length);

                if (words.Length == 1)
                {
                    // Single word: search by LastName prefix - fix case for Cyrillic
                    string capitalizedWord = char.ToUpper(words[0][0]) + words[0].Substring(1).ToLower();
                    string wordPattern = $"{capitalizedWord}%";
                    authors = _db.ExecuteQuery<string>(@"
                            SELECT DISTINCT a.Name
                            FROM Authors a
                            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
                            WHERE a.LastName LIKE @Pattern
                            ORDER BY a.Name",
                        reader => reader.GetString(0),
                        DatabaseManager.CreateParameter("@Pattern", wordPattern));

                    // Soundex fallback if no exact results found
                    if (authors.Count == 0 && words[0].Length >= 3)
                    {
                        Log.WriteLine(LogLevel.Info, "GetAuthorsForOpenSearch: trying Soundex fallback for '{0}'", words[0]);
                        string wordSoundex = StringUtils.Soundex(capitalizedWord);

                        var soundexAuthors = _db.ExecuteQuery<string>(@"
                                SELECT DISTINCT a.Name
                                FROM Authors a
                                INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
                                WHERE a.LastNameSoundex = @Soundex
                                ORDER BY a.Name",
                            reader => reader.GetString(0),
                            DatabaseManager.CreateParameter("@Soundex", wordSoundex));

                        if (soundexAuthors.Count > 0)
                        {
                            Log.WriteLine(LogLevel.Info, "GetAuthorsForOpenSearch: Soundex found {0} authors", soundexAuthors.Count);
                            authors.AddRange(soundexAuthors);
                        }

                        // Transliteration fallback if still no results and input contains Latin letters
                        if (authors.Count == 0 && ContainsLatinLetters(capitalizedWord))
                        {
                            Log.WriteLine(LogLevel.Info, "GetAuthorsForOpenSearch: trying transliteration fallback for '{0}'", capitalizedWord);

                            // Try GOST transliteration first
                            string transliteratedGOST = Transliteration.Back(capitalizedWord, TransliterationType.GOST);
                            if (!string.IsNullOrEmpty(transliteratedGOST) && transliteratedGOST != capitalizedWord)
                            {
                                string gostSoundex = StringUtils.Soundex(transliteratedGOST);
                                var gostAuthors = _db.ExecuteQuery<string>(@"
                                        SELECT DISTINCT a.Name
                                        FROM Authors a
                                        INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
                                        WHERE a.LastNameSoundex = @Soundex
                                        ORDER BY a.Name",
                                    reader => reader.GetString(0),
                                    DatabaseManager.CreateParameter("@Soundex", gostSoundex));

                                if (gostAuthors.Count > 0)
                                {
                                    Log.WriteLine(LogLevel.Info, "GetAuthorsForOpenSearch: GOST transliteration '{0}' -> '{1}' found {2} authors",
                                                capitalizedWord, transliteratedGOST, gostAuthors.Count);
                                    authors.AddRange(gostAuthors);
                                }
                            }

                            // Try ISO transliteration if GOST didn't work
                            if (authors.Count == 0)
                            {
                                string transliteratedISO = Transliteration.Back(capitalizedWord, TransliterationType.ISO);
                                if (!string.IsNullOrEmpty(transliteratedISO) && transliteratedISO != capitalizedWord && transliteratedISO != transliteratedGOST)
                                {
                                    string isoSoundex = StringUtils.Soundex(transliteratedISO);
                                    var isoAuthors = _db.ExecuteQuery<string>(@"
                                            SELECT DISTINCT a.Name
                                            FROM Authors a
                                            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
                                            WHERE a.LastNameSoundex = @Soundex
                                            ORDER BY a.Name",
                                        reader => reader.GetString(0),
                                        DatabaseManager.CreateParameter("@Soundex", isoSoundex));

                                    if (isoAuthors.Count > 0)
                                    {
                                        Log.WriteLine(LogLevel.Info, "GetAuthorsForOpenSearch: ISO transliteration '{0}' -> '{1}' found {2} authors",
                                                    capitalizedWord, transliteratedISO, isoAuthors.Count);
                                        authors.AddRange(isoAuthors);
                                    }
                                }
                            }
                        }
                    }
                }
                else if (words.Length == 2)
                {
                    // Two words: try both "FirstName LastName" and "LastName FirstName" - fix case for Cyrillic
                    // Also check MiddleName for cases like "Ремарк Мария" where MiddleName = "Мария"
                    string word1Capitalized = char.ToUpper(words[0][0]) + words[0].Substring(1).ToLower();
                    string word2Capitalized = char.ToUpper(words[1][0]) + words[1].Substring(1).ToLower();
                    string word1Pattern = $"{word1Capitalized}%";
                    string word2Pattern = $"{word2Capitalized}%";

                    authors = _db.ExecuteQuery<string>(@"
                            SELECT DISTINCT a.Name
                            FROM Authors a
                            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
                            WHERE (
                                (a.FirstName LIKE @FirstNamePattern OR a.FirstName IS NULL OR a.FirstName = '') AND
                                (a.LastName LIKE @LastNamePattern OR a.LastName IS NULL OR a.LastName = '')
                            ) OR (
                                (a.MiddleName LIKE @FirstNamePattern OR a.MiddleName IS NULL OR a.MiddleName = '') AND
                                (a.LastName LIKE @LastNamePattern OR a.LastName IS NULL OR a.LastName = '')
                            )
                            ORDER BY a.Name",
                        reader => reader.GetString(0),
                        DatabaseManager.CreateParameter("@FirstNamePattern", word1Pattern),
                        DatabaseManager.CreateParameter("@LastNamePattern", word2Pattern));

                    // Try reverse order if nothing found
                    if (authors.Count == 0)
                    {
                        var reverseAuthors = _db.ExecuteQuery<string>(@"
                                SELECT DISTINCT a.Name
                                FROM Authors a
                                INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
                                WHERE (
                                    (a.FirstName LIKE @FirstNamePattern OR a.FirstName IS NULL OR a.FirstName = '') AND
                                    (a.LastName LIKE @LastNamePattern OR a.LastName IS NULL OR a.LastName = '')
                                ) OR (
                                    (a.MiddleName LIKE @FirstNamePattern OR a.MiddleName IS NULL OR a.MiddleName = '') AND
                                    (a.LastName LIKE @LastNamePattern OR a.LastName IS NULL OR a.LastName = '')
                                )
                                ORDER BY a.Name",
                            reader => reader.GetString(0),
                            DatabaseManager.CreateParameter("@FirstNamePattern", word2Pattern),
                            DatabaseManager.CreateParameter("@LastNamePattern", word1Pattern));

                        authors.AddRange(reverseAuthors);
                    }

                    // Transliteration fallback for two words if still no results
                    if (authors.Count == 0 && (ContainsLatinLetters(word1Capitalized) || ContainsLatinLetters(word2Capitalized)))
                    {
                        Log.WriteLine(LogLevel.Info, "GetAuthorsForOpenSearch: trying transliteration fallback for two words '{0} {1}'", word1Capitalized, word2Capitalized);

                        // Try GOST transliteration
                        string transliterated1GOST = ContainsLatinLetters(word1Capitalized) ? Transliteration.Back(word1Capitalized, TransliterationType.GOST) : word1Capitalized;
                        string transliterated2GOST = ContainsLatinLetters(word2Capitalized) ? Transliteration.Back(word2Capitalized, TransliterationType.GOST) : word2Capitalized;

                        if ((transliterated1GOST != word1Capitalized || transliterated2GOST != word2Capitalized))
                        {
                            string transliteratedFullGOST = $"{transliterated1GOST} {transliterated2GOST}";
                            var gostAuthors = GetAuthorsForOpenSearch(transliteratedFullGOST); // Recursive call with transliterated text

                            if (gostAuthors.Count > 0)
                            {
                                Log.WriteLine(LogLevel.Info, "GetAuthorsForOpenSearch: GOST transliteration '{0}' -> '{1}' found {2} authors",
                                            searchPattern, transliteratedFullGOST, gostAuthors.Count);
                                authors.AddRange(gostAuthors);
                            }
                        }
                    }
                }
                else
                {
                    // Multiple words: fallback to name contains search
                    string namePattern = $"%{searchPattern}%";
                    authors = _db.ExecuteQuery<string>(@"
                            SELECT DISTINCT a.Name
                            FROM Authors a
                            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
                            WHERE a.Name LIKE @Pattern COLLATE NOCASE
                            ORDER BY a.Name",
                        reader => reader.GetString(0),
                        DatabaseManager.CreateParameter("@Pattern", namePattern));

                    // Transliteration fallback for multiple words
                    if (authors.Count == 0 && ContainsLatinLetters(searchPattern))
                    {
                        Log.WriteLine(LogLevel.Info, "GetAuthorsForOpenSearch: trying transliteration fallback for multiple words '{0}'", searchPattern);

                        string transliteratedGOST = Transliteration.Back(searchPattern, TransliterationType.GOST);
                        if (!string.IsNullOrEmpty(transliteratedGOST) && transliteratedGOST != searchPattern)
                        {
                            var gostAuthors = GetAuthorsForOpenSearch(transliteratedGOST); // Recursive call
                            if (gostAuthors.Count > 0)
                            {
                                Log.WriteLine(LogLevel.Info, "GetAuthorsForOpenSearch: GOST transliteration '{0}' -> '{1}' found {2} authors",
                                            searchPattern, transliteratedGOST, gostAuthors.Count);
                                authors.AddRange(gostAuthors);
                            }
                        }
                    }
                }

                Log.WriteLine(LogLevel.Info, "GetAuthorsForOpenSearch: found {0} authors", authors.Count);
                return authors.Distinct().ToList();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GetAuthorsForOpenSearch {0}: {1}", searchPattern, ex.Message);
                return new List<string>();
            }
        }

        /// <summary>
        /// Check if string contains Latin letters (for transliteration detection)
        /// </summary>
        /// <param name="text">Text to check</param>
        /// <returns>True if contains Latin letters</returns>
        private bool ContainsLatinLetters(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            foreach (char c in text)
            {
                if (char.IsLetter(c) && (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Parse author name into FirstName, MiddleName, LastName components
        /// Handles Russian format: "Фамилия Имя" or "Фамилия Имя Отчество"
        /// </summary>
        /// <param name="fullName">Full author name</param>
        /// <returns>Tuple of (FirstName, MiddleName, LastName)</returns>
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
                    return (parts[1], null, parts[0]);

                case 3:
                    return (parts[1], parts[2], parts[0]);

                default:
                    var middleParts = new string[parts.Length - 2];
                    Array.Copy(parts, 2, middleParts, 0, parts.Length - 2);
                    return (parts[1], string.Join(" ", middleParts), parts[0]);
            }
        }

        #endregion

        #region Statistics

        public int GetTotalBooksCount()
        {
            var result = _db.ExecuteScalar(DatabaseSchema.CountBooks);
            return Convert.ToInt32(result);
        }

        public int GetFB2BooksCount()
        {
            var result = _db.ExecuteScalar(DatabaseSchema.CountFB2Books);
            return Convert.ToInt32(result);
        }

        public int GetEPUBBooksCount()
        {
            var result = _db.ExecuteScalar(DatabaseSchema.CountEPUBBooks);
            return Convert.ToInt32(result);
        }

        public int GetNewBooksCount(DateTime fromDate)
        {
            var result = _db.ExecuteScalar(DatabaseSchema.CountNewBooks,
                DatabaseManager.CreateParameter("@FromDate", fromDate));
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// Get new books with pagination support
        /// </summary>
        /// <param name="fromDate">Date from which to consider books as new</param>
        /// <param name="offset">Number of records to skip</param>
        /// <param name="limit">Maximum number of records to return</param>
        /// <param name="sortByDate">Sort by date (true) or alphabetically by title (false)</param>
        /// <returns>List of books for the requested page</returns>
        public List<Book> GetNewBooksPaginated(DateTime fromDate, int offset, int limit, bool sortByDate = true)
        {
            try
            {
                string query = sortByDate
                    ? DatabaseSchema.SelectNewBooksPaginatedByDate
                    : DatabaseSchema.SelectNewBooksPaginatedByTitle;

                var books = _db.ExecuteQuery<Book>(query, MapBook,
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
            return _db.ExecuteQuery<string>(DatabaseSchema.SelectAuthors, reader => reader.GetString(0));
        }

        public List<string> GetAllSequences()
        {
            return _db.ExecuteQuery<string>(DatabaseSchema.SelectSequences, reader => reader.GetString(0));
        }

        public List<string> GetAllGenreTags()
        {
            return _db.ExecuteQuery<string>(DatabaseSchema.SelectGenreTags, reader => reader.GetString(0));
        }

        public int GetAuthorsCount()
        {
            var result = _db.ExecuteScalar(DatabaseSchema.SelectAuthorsCount);
            return Convert.ToInt32(result);
        }

        public int GetSequencesCount()
        {
            var result = _db.ExecuteScalar(DatabaseSchema.SelectSequencesCount);
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// Get count of books by genre - fast count without loading book data
        /// </summary>
        /// <param name="genreTag">Genre tag to count books for</param>
        /// <returns>Number of books with this genre</returns>
        public int GetBooksByGenreCount(string genreTag)
        {
            try
            {
                var result = _db.ExecuteScalar(DatabaseSchema.CountBooksByGenre,
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
        /// Get statistics for all genres with book counts - single fast query
        /// </summary>
        /// <returns>Dictionary of genre tag to book count</returns>
        public Dictionary<string, int> GetAllGenreStatistics()
        {
            try
            {
                var result = new Dictionary<string, int>();

                var statistics = _db.ExecuteQuery<(string GenreTag, int BookCount)>(
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

        #endregion

        #region Helper Methods

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
                DocumentSize = DatabaseManager.GetUInt32(reader, "DocumentSize")
            };

            var bookDate = DatabaseManager.GetDateTime(reader, "BookDate");
            if (bookDate.HasValue) book.BookDate = bookDate.Value;

            var docDate = DatabaseManager.GetDateTime(reader, "DocumentDate");
            if (docDate.HasValue) book.DocumentDate = docDate.Value;

            var addedDate = DatabaseManager.GetDateTime(reader, "AddedDate");
            if (addedDate.HasValue) book.AddedDate = addedDate.Value;

            return book;
        }

        private void LoadBookRelations(Book book)
        {
            // Load authors
            book.Authors = _db.ExecuteQuery<string>(DatabaseSchema.SelectBookAuthors, reader => reader.GetString(0),
                DatabaseManager.CreateParameter("@BookID", book.ID));

            // Load genres
            book.Genres = _db.ExecuteQuery<string>(DatabaseSchema.SelectBookGenres, reader => reader.GetString(0),
                DatabaseManager.CreateParameter("@BookID", book.ID));

            // Load translators
            book.Translators = _db.ExecuteQuery<string>(DatabaseSchema.SelectBookTranslators, reader => reader.GetString(0),
                DatabaseManager.CreateParameter("@BookID", book.ID));
        }

        #endregion
    }
}