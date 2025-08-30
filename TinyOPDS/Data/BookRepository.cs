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
        private readonly DatabaseManager db;

        public BookRepository(DatabaseManager database)
        {
            db = database;
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
                db.BeginTransaction();

                // Insert or update book
                var bookParams = new[]
                {
                    DatabaseManager.CreateParameter("@ID", book.ID),
                    DatabaseManager.CreateParameter("@Version", book.Version),
                    DatabaseManager.CreateParameter("@FileName", book.FileName),
                    DatabaseManager.CreateParameter("@Title", book.Title),
                    DatabaseManager.CreateParameter("@Language", book.Language),
                    DatabaseManager.CreateParameter("@BookDate", book.BookDate),
                    DatabaseManager.CreateParameter("@DocumentDate", book.DocumentDate),
                    DatabaseManager.CreateParameter("@Sequence", book.Sequence),
                    DatabaseManager.CreateParameter("@NumberInSequence", (long)book.NumberInSequence),
                    DatabaseManager.CreateParameter("@Annotation", book.Annotation),
                    DatabaseManager.CreateParameter("@DocumentSize", (long)book.DocumentSize),
                    DatabaseManager.CreateParameter("@AddedDate", book.AddedDate)
                };

                db.ExecuteNonQuery(DatabaseSchema.InsertBook, bookParams);

                // Clear existing relationships
                db.ExecuteNonQuery("DELETE FROM BookAuthors WHERE BookID = @BookID",
                    DatabaseManager.CreateParameter("@BookID", book.ID));
                db.ExecuteNonQuery("DELETE FROM BookGenres WHERE BookID = @BookID",
                    DatabaseManager.CreateParameter("@BookID", book.ID));
                db.ExecuteNonQuery("DELETE FROM BookTranslators WHERE BookID = @BookID",
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

                // Add genres
                foreach (var genreTag in book.Genres)
                {
                    db.ExecuteNonQuery(DatabaseSchema.InsertBookGenre,
                        DatabaseManager.CreateParameter("@BookID", book.ID),
                        DatabaseManager.CreateParameter("@GenreTag", genreTag));
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
                db.ExecuteNonQuery("PRAGMA synchronous = OFF");
                db.ExecuteNonQuery("PRAGMA journal_mode = MEMORY");
                db.ExecuteNonQuery("PRAGMA temp_store = MEMORY");
                db.ExecuteNonQuery("PRAGMA cache_size = 10000");

                db.BeginTransaction();

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
                            DatabaseManager.CreateParameter("@BookDate", book.BookDate),
                            DatabaseManager.CreateParameter("@DocumentDate", book.DocumentDate),
                            DatabaseManager.CreateParameter("@Sequence", book.Sequence),
                            DatabaseManager.CreateParameter("@NumberInSequence", (long)book.NumberInSequence),
                            DatabaseManager.CreateParameter("@Annotation", book.Annotation),
                            DatabaseManager.CreateParameter("@DocumentSize", (long)book.DocumentSize),
                            DatabaseManager.CreateParameter("@AddedDate", book.AddedDate)
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

                        // Insert genres and relationships
                        foreach (var genreTag in book.Genres)
                        {
                            db.ExecuteNonQuery(DatabaseSchema.InsertBookGenre,
                                DatabaseManager.CreateParameter("@BookID", book.ID),
                                DatabaseManager.CreateParameter("@GenreTag", genreTag));
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

                db.CommitTransaction();

                result.ProcessingTime = DateTime.Now - startTime;
                Log.WriteLine("Batch insert completed: {0} added, {1} duplicates skipped, {2} errors in {3}ms",
                    result.Added, result.Duplicates, result.Errors, result.ProcessingTime.TotalMilliseconds);

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
            var count = db.ExecuteScalar("SELECT COUNT(*) FROM Books WHERE FileName = @FileName",
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

        public List<Book> GetBooksBySequence(string sequence)
        {
            var books = db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksBySequence, MapBook,
                DatabaseManager.CreateParameter("@Sequence", sequence));

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
            var books = db.ExecuteQuery<Book>(@"
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
            try
            {
                if (string.IsNullOrEmpty(searchPattern))
                {
                    return new List<string>();
                }

                searchPattern = searchPattern.Trim();
                Log.WriteLine(LogLevel.Info, "GetAuthorsForOpenSearch: searching for '{0}'", searchPattern);

                var authors = new List<string>();
                var words = searchPattern.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (words.Length == 0)
                {
                    return new List<string>();
                }

                // STEP 1: For two-word input, try EXACT match using FTS
                if (words.Length == 2)
                {
                    Log.WriteLine(LogLevel.Info, "Step 1: Trying exact match for two-word query '{0}' using FTS", searchPattern);

                    // Use FTS to find exact match - it handles case-insensitive Unicode properly
                    string ftsQuery = $"\"{searchPattern}\""; // Exact phrase in FTS

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
                        return authors;
                    }

                    // Try reversed order in FTS
                    string reversedPattern = $"{words[1]} {words[0]}";
                    string reversedFtsQuery = $"\"{reversedPattern}\"";

                    authors = db.ExecuteQuery<string>(sql,
                        reader => reader.GetString(0),
                        DatabaseManager.CreateParameter("@SearchPattern", reversedFtsQuery));

                    if (authors.Count > 0)
                    {
                        Log.WriteLine(LogLevel.Info, "Found exact match with reversed order using FTS: {0} authors", authors.Count);
                        return authors;
                    }
                }

                // STEP 2: Partial match ONLY for single word using FTS
                if (words.Length == 1)
                {
                    string pattern = words[0];
                    Log.WriteLine(LogLevel.Info, "Step 2: Trying partial match for single word '{0}' using FTS", pattern);

                    // Use FTS with wildcard for prefix search
                    string ftsPattern = $"{pattern}*";

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
                        return authors;
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
                        // Recursive call with transliterated text
                        var translitAuthors = GetAuthorsForOpenSearch(transliteratedGOST);
                        if (translitAuthors.Count > 0)
                        {
                            Log.WriteLine(LogLevel.Info, "Found via GOST transliteration: {0} authors", translitAuthors.Count);
                            return translitAuthors;
                        }
                    }

                    // Try ISO transliteration
                    string transliteratedISO = Transliteration.Back(searchPattern, TransliterationType.ISO);
                    if (!string.IsNullOrEmpty(transliteratedISO) && transliteratedISO != searchPattern && transliteratedISO != transliteratedGOST)
                    {
                        // Recursive call with transliterated text
                        var translitAuthors = GetAuthorsForOpenSearch(transliteratedISO);
                        if (translitAuthors.Count > 0)
                        {
                            Log.WriteLine(LogLevel.Info, "Found via ISO transliteration: {0} authors", translitAuthors.Count);
                            return translitAuthors;
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
                        return authors;
                    }
                }

                Log.WriteLine(LogLevel.Info, "GetAuthorsForOpenSearch: no results found");
                return new List<string>();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GetAuthorsForOpenSearch {0}: {1}", searchPattern, ex.Message);
                return new List<string>();
            }
        }

        #endregion

        #region Statistics

        public int GetTotalBooksCount()
        {
            var result = db.ExecuteScalar(DatabaseSchema.CountBooks);
            return Convert.ToInt32(result);
        }

        public int GetFB2BooksCount()
        {
            var result = db.ExecuteScalar(DatabaseSchema.CountFB2Books);
            return Convert.ToInt32(result);
        }

        public int GetEPUBBooksCount()
        {
            var result = db.ExecuteScalar(DatabaseSchema.CountEPUBBooks);
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
            book.Authors = db.ExecuteQuery<string>(DatabaseSchema.SelectBookAuthors, reader => reader.GetString(0),
                DatabaseManager.CreateParameter("@BookID", book.ID));

            // Load genres
            book.Genres = db.ExecuteQuery<string>(DatabaseSchema.SelectBookGenres, reader => reader.GetString(0),
                DatabaseManager.CreateParameter("@BookID", book.ID));

            // Load translators
            book.Translators = db.ExecuteQuery<string>(DatabaseSchema.SelectBookTranslators, reader => reader.GetString(0),
                DatabaseManager.CreateParameter("@BookID", book.ID));
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
        /// Prepare search pattern for FTS5 (add wildcards for partial matching)
        /// </summary>
        private string PrepareSearchPatternForFTS(string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return pattern;

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