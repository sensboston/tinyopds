/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * SQLite-based implementation of Library class
 * This replaces the in-memory Dictionary approach with SQLite database
 * OPTIMIZED: Enhanced caching for better performance with large databases
 *
 */

using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TinyOPDS.Data
{
    public static class Library
    {
        public static event EventHandler LibraryLoaded;

        private static DatabaseManager db;
        private static BookRepository bookRepository;
        private static string databaseFullPath;
        private static List<Genre> genres;
        private static Dictionary<string, string> soundexedGenres;
        private static readonly TimeSpan[] periods = new TimeSpan[7];

        // Author aliases - now stored in memory for fast access
        private static readonly Dictionary<string, string> aliases = new Dictionary<string, string>();
        private static readonly Dictionary<string, List<string>> reverseAliases = new Dictionary<string, List<string>>();

        // Enhanced cache for frequently accessed data
        private static readonly object cacheLock = new object();
        private static DateTime lastStatsUpdate = DateTime.MinValue;
        private static int cachedTotalCount = 0;
        private static int cachedFB2Count = 0;
        private static int cachedEPUBCount = 0;
        private static int cachedAuthorsCount = 0;
        private static int cachedSequencesCount = 0;
        private static int cachedNewBooksCount = 0;
        private static DateTime lastNewBooksCountUpdate = DateTime.MinValue;

        // Extended cache timeout - data will be cached until invalidated
        private static readonly TimeSpan CacheTimeout = TimeSpan.FromHours(1);
        // Separate timeout for new books count (changes more frequently)
        private static readonly TimeSpan NewBooksCacheTimeout = TimeSpan.FromMinutes(5);

        // Genre cache
        private static DateTime lastGenresUpdate = DateTime.MinValue;
        private static List<Genre> cachedGenres = null;

        // Lists cache (for when full lists are needed)
        private static List<string> cachedAuthorsList = null;
        private static List<string> cachedSequencesList = null;
        private static DateTime lastAuthorsListUpdate = DateTime.MinValue;
        private static DateTime lastSequencesListUpdate = DateTime.MinValue;
        private static readonly TimeSpan ListsCacheTimeout = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Static constructor - initializes periods
        /// </summary>
        static Library()
        {
            // Initialize "new books" periods
            periods[0] = TimeSpan.FromDays(7);
            periods[1] = TimeSpan.FromDays(14);
            periods[2] = TimeSpan.FromDays(21);
            periods[3] = TimeSpan.FromDays(30);
            periods[4] = TimeSpan.FromDays(44);
            periods[5] = TimeSpan.FromDays(60);
            periods[6] = TimeSpan.FromDays(90);
        }

        #region Database Initialization

        /// <summary>
        /// Initialize database connection and repository
        /// </summary>
        /// <param name="databasePath">Path to SQLite database file</param>
        public static void Initialize(string databasePath = "")
        {
            if (string.IsNullOrEmpty(databasePath))
            {
                databasePath = Path.Combine(Utils.ServiceFilesLocation, "books.sqlite");
            }

            databaseFullPath = databasePath;

            try
            {
                db?.Dispose();
                db = new DatabaseManager(databaseFullPath);
                bookRepository = new BookRepository(db);

                // Load genres from database into memory for fast access
                LoadGenresFromDatabase();

                // Load author aliases into memory
                LoadAuthorAliases();

                // Warm up cache on initialization
                WarmUpCache();

                Log.WriteLine("SQLite database initialized: {0}", databaseFullPath);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Failed to initialize database: {0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Close database connection
        /// </summary>
        public static void Close()
        {
            db?.Dispose();
            db = null;
            bookRepository = null;
        }

        /// <summary>
        /// Warm up cache with frequently accessed data
        /// </summary>
        public static void WarmUpCache()
        {
            if (bookRepository == null) return;

            try
            {
                Log.WriteLine("Warming up Library cache...");
                var startTime = DateTime.Now;

                lock (cacheLock)
                {
                    // Load all counts at once
                    cachedTotalCount = bookRepository.GetTotalBooksCount();
                    cachedFB2Count = bookRepository.GetFB2BooksCount();
                    cachedEPUBCount = bookRepository.GetEPUBBooksCount();
                    cachedAuthorsCount = bookRepository.GetAuthorsCount();
                    cachedSequencesCount = bookRepository.GetSequencesCount();

                    // Load new books count
                    var period = periods[Properties.Settings.Default.NewBooksPeriod];
                    var fromDate = DateTime.Now.Subtract(period);
                    cachedNewBooksCount = bookRepository.GetNewBooksCount(fromDate);

                    lastStatsUpdate = DateTime.Now;
                    lastNewBooksCountUpdate = DateTime.Now;
                }

                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Log.WriteLine("Cache warmed up in {0} ms. Books: {1}, Authors: {2}, Series: {3}",
                    elapsed, cachedTotalCount, cachedAuthorsCount, cachedSequencesCount);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error warming up cache: {0}", ex.Message);
            }
        }

        #endregion

        #region Properties (maintaining original API)

        /// <summary>
        /// Library path property
        /// </summary>
        public static string LibraryPath { get; set; } = string.Empty;

        /// <summary>
        /// Database changed flag
        /// </summary>
        public static bool IsChanged { get; set; }

        /// <summary>
        /// Total number of books in library
        /// </summary>
        public static int Count
        {
            get
            {
                RefreshStatsCache();
                return cachedTotalCount;
            }
        }

        /// <summary>
        /// New books count - cached separately with shorter timeout
        /// </summary>
        public static int NewBooksCount
        {
            get
            {
                if (bookRepository == null) return 0;

                lock (cacheLock)
                {
                    if (DateTime.Now - lastNewBooksCountUpdate > NewBooksCacheTimeout)
                    {
                        var period = periods[Properties.Settings.Default.NewBooksPeriod];
                        var fromDate = DateTime.Now.Subtract(period);
                        cachedNewBooksCount = bookRepository.GetNewBooksCount(fromDate);
                        lastNewBooksCountUpdate = DateTime.Now;
                    }
                    return cachedNewBooksCount;
                }
            }
        }

        /// <summary>
        /// Returns FB2 books count
        /// </summary>
        public static int FB2Count
        {
            get
            {
                RefreshStatsCache();
                return cachedFB2Count;
            }
        }

        /// <summary>
        /// Returns EPUB books count
        /// </summary>
        public static int EPUBCount
        {
            get
            {
                RefreshStatsCache();
                return cachedEPUBCount;
            }
        }

        /// <summary>
        /// Returns list of the authors sorted in alphabetical order
        /// OPTIMIZED: Now uses cached list when available, with separate count property
        /// </summary>
        public static List<string> Authors
        {
            get
            {
                if (bookRepository == null) return new List<string>();

                lock (cacheLock)
                {
                    if (cachedAuthorsList == null || DateTime.Now - lastAuthorsListUpdate > ListsCacheTimeout)
                    {
                        var authors = bookRepository.GetAllAuthors();
                        var comparer = new OPDSComparer(Properties.Settings.Default.SortOrder > 0);
                        cachedAuthorsList = authors.Where(a => a.Length > 1).OrderBy(a => a, comparer).ToList();
                        lastAuthorsListUpdate = DateTime.Now;
                    }
                    return new List<string>(cachedAuthorsList);
                }
            }
        }

        /// <summary>
        /// Get authors count without loading all authors
        /// OPTIMIZED: Uses cached count from fast SQL COUNT query
        /// </summary>
        public static int AuthorsCount
        {
            get
            {
                RefreshStatsCache();
                return cachedAuthorsCount;
            }
        }

        /// <summary>
        /// Returns list of the library books series sorted in alphabetical order
        /// OPTIMIZED: Now uses cached list when available, with separate count property
        /// </summary>
        public static List<string> Sequences
        {
            get
            {
                if (bookRepository == null) return new List<string>();

                lock (cacheLock)
                {
                    if (cachedSequencesList == null || DateTime.Now - lastSequencesListUpdate > ListsCacheTimeout)
                    {
                        var sequences = bookRepository.GetAllSequences();
                        var comparer = new OPDSComparer(Properties.Settings.Default.SortOrder > 0);
                        cachedSequencesList = sequences.Where(s => s.Length > 1).OrderBy(s => s, comparer).ToList();
                        lastSequencesListUpdate = DateTime.Now;
                    }
                    return new List<string>(cachedSequencesList);
                }
            }
        }

        /// <summary>
        /// Get sequences count without loading all sequences
        /// OPTIMIZED: Uses cached count from fast SQL COUNT query
        /// </summary>
        public static int SequencesCount
        {
            get
            {
                RefreshStatsCache();
                return cachedSequencesCount;
            }
        }

        /// <summary>
        /// All genres supported by fb2 format (loaded from database)
        /// </summary>
        public static List<Genre> FB2Genres
        {
            get
            {
                RefreshGenresCache();
                return cachedGenres ?? new List<Genre>();
            }
        }

        public static Dictionary<string, string> SoundexedGenres
        {
            get
            {
                if (soundexedGenres == null)
                {
                    RefreshGenresCache();
                }
                return soundexedGenres ?? new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Returns sorted in alphabetical order list of library books genres (only genres with books)
        /// </summary>
        public static List<Genre> Genres
        {
            get
            {
                if (bookRepository == null) return new List<Genre>();

                try
                {
                    // Get genres with books from database
                    var genresWithBooks = bookRepository.GetGenresWithBooks();

                    var useCyrillic = Properties.Settings.Default.SortOrder > 0;
                    var comparer = new OPDSComparer(useCyrillic);

                    return genresWithBooks
                        .Select(g => g.genre)
                        .OrderBy(g => useCyrillic ? g.Translation : g.Name, comparer)
                        .ToList();
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Error getting genres: {0}", ex.Message);
                    return new List<Genre>();
                }
            }
        }

        /// <summary>
        /// Return list of new books (DEPRECATED - use GetNewBooksPaginated for large datasets)
        /// </summary>
        public static List<Book> NewBooks
        {
            get
            {
                if (bookRepository == null) return new List<Book>();

                var period = periods[Properties.Settings.Default.NewBooksPeriod];
                var fromDate = DateTime.Now.Subtract(period);
                return bookRepository.GetNewBooks(fromDate);
            }
        }

        /// <summary>
        /// Returns list of the books titles sorted in alphabetical order
        /// </summary>
        public static List<string> Titles
        {
            get
            {
                if (bookRepository == null) return new List<string>();

                var books = bookRepository.GetAllBooks();
                var comparer = new OPDSComparer(Properties.Settings.Default.SortOrder > 0);
                return books.Select(b => b.Title).Distinct().OrderBy(t => t, comparer).ToList();
            }
        }

        #endregion

        #region New Books Pagination

        /// <summary>
        /// Get new books with pagination support
        /// </summary>
        /// <param name="sortByDate">Sort by date (true) or alphabetically by title (false)</param>
        /// <param name="pageNumber">Zero-based page number</param>
        /// <param name="pageSize">Number of books per page</param>
        /// <returns>Paginated result with books and pagination info</returns>
        public static NewBooksPaginatedResult GetNewBooksPaginated(bool sortByDate, int pageNumber = 0, int pageSize = 100)
        {
            var result = new NewBooksPaginatedResult();

            if (bookRepository == null)
            {
                return result;
            }

            try
            {
                var period = periods[Properties.Settings.Default.NewBooksPeriod];
                var fromDate = DateTime.Now.Subtract(period);

                // Get total count for pagination
                result.TotalBooks = bookRepository.GetNewBooksCount(fromDate);
                result.PageSize = pageSize;
                result.CurrentPage = pageNumber;
                result.TotalPages = (int)Math.Ceiling((double)result.TotalBooks / pageSize);

                // Calculate offset
                int offset = pageNumber * pageSize;

                // Get books for current page
                result.Books = bookRepository.GetNewBooksPaginated(fromDate, offset, pageSize, sortByDate);

                result.HasPreviousPage = pageNumber > 0;
                result.HasNextPage = pageNumber < result.TotalPages - 1;

                return result;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error getting paginated new books: {0}", ex.Message);
                return result;
            }
        }

        /// <summary>
        /// Get total count of new books for the current period
        /// </summary>
        /// <returns>Total count of new books</returns>
        public static int GetNewBooksTotalCount()
        {
            if (bookRepository == null) return 0;

            var period = periods[Properties.Settings.Default.NewBooksPeriod];
            var fromDate = DateTime.Now.Subtract(period);
            return bookRepository.GetNewBooksCount(fromDate);
        }

        #endregion

        #region Core Methods (maintaining original API)

        /// <summary>
        /// Load library - now initializes SQLite database
        /// </summary>
        public static void Load()
        {
            var start = DateTime.Now;

            if (db == null)
            {
                Initialize();
            }

            // Refresh genre cache from database
            LoadGenresFromDatabase();

            // Warm up statistics cache
            WarmUpCache();

            Log.WriteLine("Library loaded from SQLite database in {0} ms",
                (DateTime.Now - start).TotalMilliseconds);

            LibraryLoaded?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Load library asynchronously - for API compatibility
        /// </summary>
        public static void LoadAsync()
        {
            // In SQLite implementation, we load synchronously but call it on background thread if needed
            System.Threading.Tasks.Task.Run(() => Load());
        }

        /// <summary>
        /// Save library - for SQLite this is essentially a no-op since data is written immediately
        /// </summary>
        public static void Save()
        {
            // In SQLite mode, data is saved immediately, so this is just for API compatibility
            Log.WriteLine("Library save requested - data already persisted in SQLite");
        }

        /// <summary>
        /// Add unique book descriptor to the library
        /// MODIFIED: Removed redundant duplicate checking, let BookRepository handle it
        /// </summary>
        /// <param name="book"></param>
        public static bool Add(Book book)
        {
            if (bookRepository == null) return false;

            try
            {
                // Apply author aliases before saving to database
                ApplyAliasesToBookAuthors(book);

                // Normalize genre tags using soundex if needed
                NormalizeBookGenres(book);

                // Set AddedDate if not set
                if (book.AddedDate == DateTime.MinValue)
                    book.AddedDate = DateTime.Now;

                // MODIFIED: Let BookRepository handle all duplicate detection
                // It has the proper logic with DuplicateDetector
                bool success = bookRepository.AddBook(book);

                if (success)
                {
                    IsChanged = true;
                    InvalidateStatsCache();
                }

                return success;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error adding book {0}: {1}", book.FileName, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Add multiple books in batch - optimized for performance with detailed results
        /// MODIFIED: Removed redundant duplicate checking, let BookRepository handle it
        /// </summary>
        /// <param name="books">List of books to add</param>
        /// <returns>BatchResult with detailed statistics</returns>
        public static BookRepository.BatchResult AddBatch(List<Book> books)
        {
            var result = new BookRepository.BatchResult();
            var startTime = DateTime.Now;

            if (bookRepository == null || books == null || books.Count == 0)
            {
                result.ProcessingTime = DateTime.Now - startTime;
                return result;
            }

            try
            {
                result.TotalProcessed = books.Count;

                // Process each book to apply aliases and normalize genres
                foreach (var book in books)
                {
                    // Apply author aliases before processing
                    ApplyAliasesToBookAuthors(book);

                    // Normalize genre tags
                    NormalizeBookGenres(book);

                    // Set AddedDate if not set
                    if (book.AddedDate == DateTime.MinValue)
                        book.AddedDate = DateTime.Now;
                }

                // MODIFIED: Let BookRepository handle all duplicate detection and batch processing
                // It has the proper logic with DuplicateDetector
                result = bookRepository.AddBooksBatch(books);

                if (result.Added > 0 || result.Replaced > 0)
                {
                    IsChanged = true;
                    InvalidateStatsCache();
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Library.AddBatch: {0}", ex.Message);
                result.Errors = result.TotalProcessed;
                result.ErrorMessages.Add($"Library batch operation failed: {ex.Message}");
                result.ProcessingTime = DateTime.Now - startTime;
                return result;
            }
        }

        /// <summary>
        /// Delete all books with specific file path from the library
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static bool Delete(string fileName)
        {
            if (bookRepository == null) return false;

            try
            {
                if (!string.IsNullOrEmpty(fileName) && fileName.Length > LibraryPath.Length + 1)
                {
                    // Extract relative file name
                    fileName = fileName.Substring(LibraryPath.Length + 1);
                    string ext = Path.GetExtension(fileName.ToLower());

                    // Single file deletion
                    if (ext.Equals(".epub") || ext.Equals(".fb2") ||
                        (ext.Equals(".zip") && fileName.ToLower().Contains(".fb2.zip")))
                    {
                        if (Contains(fileName))
                        {
                            Book book = bookRepository.GetBookByFileName(fileName);
                            if (book != null)
                            {
                                bool result = bookRepository.DeleteBook(book.ID);
                                if (result)
                                {
                                    IsChanged = true;
                                    InvalidateStatsCache();
                                }
                                return result;
                            }
                        }
                    }
                    // Archive deletion - remove all books contained in this archive
                    else
                    {
                        var booksToRemove = bookRepository.GetBooksByFileNamePrefix(fileName + "@");
                        bool result = false;
                        foreach (Book book in booksToRemove)
                        {
                            if (bookRepository.DeleteBook(book.ID))
                            {
                                result = true;
                                IsChanged = true;
                            }
                        }
                        if (result)
                        {
                            InvalidateStatsCache();
                        }
                        return result;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error deleting book {0}: {1}", fileName, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Check if library contains book by file path
        /// </summary>
        /// <param name="bookPath"></param>
        /// <returns></returns>
        public static bool Contains(string bookPath)
        {
            if (bookRepository == null) return false;
            return bookRepository.BookExists(bookPath);
        }

        /// <summary>
        /// Get book by ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static Book GetBook(string id)
        {
            if (bookRepository == null)
            {
                Log.WriteLine(LogLevel.Error, "BookRepository is null when getting book {0}", id);
                return null;
            }

            Log.WriteLine(LogLevel.Info, "Searching for book with ID: {0}", id);
            var book = bookRepository.GetBookById(id);

            if (book == null)
            {
                Log.WriteLine(LogLevel.Warning, "Book not found by ID: {0}", id);
            }
            else
            {
                Log.WriteLine(LogLevel.Info, "Found book: {0}, FilePath: {1}", book.Title, book.FilePath);
            }

            return book;
        }

        /// <summary>
        /// Remove books that no longer exist on disk
        /// </summary>
        /// <returns></returns>
        public static bool RemoveNotExistingBooks()
        {
            if (bookRepository == null) return false;

            try
            {
                var allBooks = bookRepository.GetAllBooks();
                var booksToRemove = new List<string>();

                foreach (var book in allBooks)
                {
                    if (!File.Exists(book.FilePath))
                    {
                        booksToRemove.Add(book.ID);
                    }
                }

                bool result = false;
                foreach (var bookId in booksToRemove)
                {
                    if (bookRepository.DeleteBook(bookId))
                    {
                        result = true;
                        IsChanged = true;
                    }
                }

                if (result)
                {
                    InvalidateStatsCache();
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error removing non-existing books: {0}", ex.Message);
                return false;
            }
        }

        #endregion

        #region Genre Management

        /// <summary>
        /// Load genres from database into memory cache
        /// </summary>
        private static void LoadGenresFromDatabase()
        {
            try
            {
                if (db == null) return;

                genres = db.GetAllGenres();

                // Build soundexed genres dictionary for fuzzy matching
                soundexedGenres = new Dictionary<string, string>();
                foreach (var genre in genres.SelectMany(g => g.Subgenres))
                {
                    string soundexEn = StringUtils.Soundex(genre.Name);
                    string soundexRu = StringUtils.Soundex(genre.Translation);

                    if (!string.IsNullOrEmpty(soundexEn) && !soundexedGenres.ContainsKey(soundexEn))
                        soundexedGenres[soundexEn] = genre.Tag;

                    if (!string.IsNullOrEmpty(soundexRu) && !soundexedGenres.ContainsKey(soundexRu))
                        soundexedGenres[soundexRu] = genre.Tag;
                }

                cachedGenres = genres;
                lastGenresUpdate = DateTime.Now;

                Log.WriteLine("Loaded {0} genres from database into memory cache",
                    genres.SelectMany(g => g.Subgenres).Count());
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error loading genres from database: {0}", ex.Message);
                genres = new List<Genre>();
                soundexedGenres = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Refresh genres cache if needed
        /// </summary>
        private static void RefreshGenresCache()
        {
            if (db == null) return;

            // Refresh every 5 minutes or if never loaded
            if (cachedGenres == null || DateTime.Now - lastGenresUpdate > TimeSpan.FromMinutes(5))
            {
                LoadGenresFromDatabase();
            }
        }

        /// <summary>
        /// Normalize book genres using soundex matching
        /// </summary>
        private static void NormalizeBookGenres(Book book)
        {
            if (book.Genres == null || book.Genres.Count == 0) return;

            RefreshGenresCache();

            var normalizedGenres = new List<string>();

            foreach (var genreTag in book.Genres)
            {
                // First check if it's already a valid tag
                if (genres != null && genres.SelectMany(g => g.Subgenres).Any(g => g.Tag == genreTag))
                {
                    normalizedGenres.Add(genreTag);
                    continue;
                }

                // Try soundex matching
                string soundex = StringUtils.Soundex(genreTag);
                if (!string.IsNullOrEmpty(soundex) && soundexedGenres != null && soundexedGenres.ContainsKey(soundex))
                {
                    string normalizedTag = soundexedGenres[soundex];
                    if (!normalizedGenres.Contains(normalizedTag))
                    {
                        normalizedGenres.Add(normalizedTag);
                        Log.WriteLine(LogLevel.Info, "Normalized genre '{0}' to '{1}' using soundex",
                            genreTag, normalizedTag);
                    }
                }
                else
                {
                    // Keep unknown genre - it will be validated by BookRepository
                    normalizedGenres.Add(genreTag);
                }
            }

            book.Genres = normalizedGenres;
        }

        /// <summary>
        /// Reload genres from XML to database
        /// </summary>
        public static void ReloadGenresFromXML()
        {
            try
            {
                if (db == null) return;

                db.ReloadGenres();
                LoadGenresFromDatabase();

                // Refresh valid genre tags in repository
                bookRepository?.RefreshValidGenreTags();

                Log.WriteLine("Genres reloaded from XML to database");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error reloading genres: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Validate and fix book genres in database
        /// </summary>
        public static int ValidateAndFixGenres()
        {
            if (bookRepository == null) return -1;

            int fixedCount = bookRepository.ValidateAndFixBookGenres();

            if (fixedCount > 0)
            {
                InvalidateStatsCache();
                Log.WriteLine("Fixed {0} invalid genre entries", fixedCount);
            }

            return fixedCount;
        }

        #endregion

        #region Author Aliases Management

        /// <summary>
        /// Check if string contains Cyrillic characters
        /// </summary>
        /// <param name="text">Text to check</param>
        /// <returns>True if text contains at least one Cyrillic character</returns>
        private static bool ContainsCyrillic(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            // Check for Cyrillic characters (Unicode range 0x0400-0x04FF)
            foreach (char c in text)
            {
                if (c >= 0x0400 && c <= 0x04FF)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Apply author aliases to book before saving to database
        /// Only applies aliases if at least one author name contains Cyrillic characters
        /// </summary>
        /// <param name="book">Book to process</param>
        private static void ApplyAliasesToBookAuthors(Book book)
        {
            if (!Properties.Settings.Default.UseAuthorsAliases || book.Authors == null)
                return;

            // Check if any author contains Cyrillic characters
            bool hasCyrillicAuthor = false;
            foreach (string author in book.Authors)
            {
                if (ContainsCyrillic(author))
                {
                    hasCyrillicAuthor = true;
                    break;
                }
            }

            // Only apply aliases if we have at least one Cyrillic author
            if (hasCyrillicAuthor)
            {
                for (int i = 0; i < book.Authors.Count; i++)
                {
                    string originalAuthor = book.Authors[i];
                    // Apply alias only if the author name is in Cyrillic
                    if (ContainsCyrillic(originalAuthor) && aliases.ContainsKey(originalAuthor))
                    {
                        book.Authors[i] = aliases[originalAuthor];
                        Log.WriteLine(LogLevel.Info, "Applied alias for Cyrillic author: '{0}' -> '{1}'",
                            originalAuthor, aliases[originalAuthor]);
                    }
                }
            }
        }

        /// <summary>
        /// Apply author aliases to author name (for OPDS output)
        /// </summary>
        /// <param name="originalAuthor">Original author name from database (already canonical)</param>
        /// <returns>Canonical author name (same as input since database already contains canonical names)</returns>
        public static string ApplyAuthorAlias(string originalAuthor)
        {
            return originalAuthor;
        }

        /// <summary>
        /// Get all alias names that map to the canonical name
        /// </summary>
        /// <param name="canonicalName">Canonical author name</param>
        /// <returns>List of alias names</returns>
        public static List<string> GetAliasesForCanonicalName(string canonicalName)
        {
            if (!reverseAliases.ContainsKey(canonicalName))
                return new List<string>();

            return reverseAliases[canonicalName];
        }

        /// <summary>
        /// Get canonical name for alias (for internal use)
        /// </summary>
        /// <param name="aliasName">Alias name</param>
        /// <returns>Canonical name if alias exists, otherwise original name</returns>
        public static string GetCanonicalName(string aliasName)
        {
            return aliases.ContainsKey(aliasName) ? aliases[aliasName] : aliasName;
        }

        #endregion

        #region Author Search Methods

        /// <summary>
        /// Get authors by name - simplified dispatch method
        /// </summary>
        /// <param name="name">Search pattern</param>
        /// <param name="isOpenSearch">True for OpenSearch, false for navigation</param>
        /// <returns>List of matching canonical author names</returns>
        public static List<string> GetAuthorsByName(string name, bool isOpenSearch)
        {
            if (bookRepository == null) return new List<string>();

            try
            {
                Log.WriteLine(LogLevel.Info, "Searching authors by name: '{0}', isOpenSearch: {1}", name, isOpenSearch);

                List<string> authors;

                if (isOpenSearch)
                {
                    authors = bookRepository.GetAuthorsForOpenSearch(name);
                }
                else
                {
                    authors = bookRepository.GetAuthorsForNavigation(name);
                }

                // Remove duplicates and sort
                var comparer = new OPDSComparer(Properties.Settings.Default.SortOrder > 0);
                var result = authors.Where(a => a.Length > 1).Distinct().OrderBy(a => a, comparer).ToList();

                Log.WriteLine(LogLevel.Info, "Found {0} authors for pattern '{1}'", result.Count, name);
                return result;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GetAuthorsByName: {0}", ex.Message);
                return new List<string>();
            }
        }

        /// <summary>
        /// Get authors by name with search method information
        /// </summary>
        /// <param name="name">Search pattern</param>
        /// <param name="isOpenSearch">True for OpenSearch, false for navigation</param>
        /// <returns>Tuple with list of matching canonical author names and search method used</returns>
        public static (List<string> authors, AuthorSearchMethod method) GetAuthorsByNameWithMethod(string name, bool isOpenSearch)
        {
            if (bookRepository == null) return (new List<string>(), AuthorSearchMethod.NotFound);

            try
            {
                Log.WriteLine(LogLevel.Info, "Searching authors by name with method: '{0}', isOpenSearch: {1}", name, isOpenSearch);

                if (isOpenSearch)
                {
                    var (authors, method) = bookRepository.GetAuthorsForOpenSearchWithMethod(name);

                    // Remove duplicates and sort
                    var comparer = new OPDSComparer(Properties.Settings.Default.SortOrder > 0);
                    var result = authors.Where(a => a.Length > 1).Distinct().OrderBy(a => a, comparer).ToList();

                    Log.WriteLine(LogLevel.Info, "Found {0} authors for pattern '{1}' using method: {2}", result.Count, name, method);
                    return (result, method);
                }
                else
                {
                    // Navigation doesn't use advanced search methods
                    var authors = bookRepository.GetAuthorsForNavigation(name);
                    var comparer = new OPDSComparer(Properties.Settings.Default.SortOrder > 0);
                    var result = authors.Where(a => a.Length > 1).Distinct().OrderBy(a => a, comparer).ToList();

                    // For navigation, we don't track method, but if found, it's partial match
                    var method = result.Count > 0 ? AuthorSearchMethod.PartialMatch : AuthorSearchMethod.NotFound;
                    return (result, method);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GetAuthorsByNameWithMethod: {0}", ex.Message);
                return (new List<string>(), AuthorSearchMethod.NotFound);
            }
        }

        /// <summary>
        /// Get books by title - original method for compatibility (no Soundex)
        /// </summary>
        /// <param name="title">Title to search for</param>
        /// <returns>List of matching books</returns>
        public static List<Book> GetBooksByTitle(string title)
        {
            return GetBooksByTitle(title, false);
        }

        /// <summary>
        /// Get books by title - OpenSearch version with FTS5 search and transliteration
        /// </summary>
        /// <param name="title">Title to search for</param>
        /// <param name="isOpenSearch">Whether this is OpenSearch (uses FTS5 when true)</param>
        /// <returns>List of matching books</returns>
        public static List<Book> GetBooksByTitle(string title, bool isOpenSearch)
        {
            if (bookRepository == null) return new List<Book>();

            try
            {
                Log.WriteLine(LogLevel.Info, "Searching books by title: '{0}', isOpenSearch: {1}", title, isOpenSearch);

                List<Book> books;

                if (isOpenSearch)
                {
                    // Use FTS5 search with transliteration fallback
                    books = bookRepository.GetBooksForOpenSearch(title);
                }
                else
                {
                    // Use traditional LIKE search for navigation
                    books = bookRepository.GetBooksByTitle(title);
                }

                Log.WriteLine(LogLevel.Info, "Found {0} books by title '{1}'", books.Count, title);
                return books.Distinct().ToList();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GetBooksByTitle: {0}", ex.Message);
                return new List<Book>();
            }
        }

        /// <summary>
        /// Return number of books by specific author
        /// </summary>
        /// <param name="author"></param>
        /// <returns></returns>
        public static int GetBooksByAuthorCount(string author)
        {
            if (bookRepository == null) return 0;
            return bookRepository.GetBooksByAuthor(author).Count;
        }

        /// <summary>
        /// Return list of books by selected author(s)
        /// </summary>
        /// <param name="author"></param>
        /// <returns></returns>
        public static List<Book> GetBooksByAuthor(string author)
        {
            if (bookRepository == null) return new List<Book>();
            return bookRepository.GetBooksByAuthor(author);
        }

        /// <summary>
        /// Return list of books by selected sequence
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public static List<Book> GetBooksBySequence(string sequence)
        {
            if (bookRepository == null) return new List<Book>();
            return bookRepository.GetBooksBySequence(sequence);
        }

        /// <summary>
        /// Return list of books by selected genre
        /// </summary>
        /// <param name="genre"></param>
        /// <returns></returns>
        public static List<Book> GetBooksByGenre(string genre)
        {
            if (bookRepository == null) return new List<Book>();
            return bookRepository.GetBooksByGenre(genre);
        }

        /// <summary>
        /// Get access to BookRepository for internal use by catalogs
        /// </summary>
        /// <returns>BookRepository instance or null if not initialized</returns>
        internal static BookRepository GetBookRepository()
        {
            return bookRepository;
        }

        #endregion

        #region Helper Methods

        private static void RefreshStatsCache()
        {
            if (bookRepository == null) return;

            lock (cacheLock)
            {
                if (DateTime.Now - lastStatsUpdate > CacheTimeout)
                {
                    cachedTotalCount = bookRepository.GetTotalBooksCount();
                    cachedFB2Count = bookRepository.GetFB2BooksCount();
                    cachedEPUBCount = bookRepository.GetEPUBBooksCount();
                    cachedAuthorsCount = bookRepository.GetAuthorsCount();
                    cachedSequencesCount = bookRepository.GetSequencesCount();
                    lastStatsUpdate = DateTime.Now;
                }
            }
        }

        private static void InvalidateStatsCache()
        {
            lock (cacheLock)
            {
                lastStatsUpdate = DateTime.MinValue;
                lastNewBooksCountUpdate = DateTime.MinValue;
                lastAuthorsListUpdate = DateTime.MinValue;
                lastSequencesListUpdate = DateTime.MinValue;
                cachedAuthorsList = null;
                cachedSequencesList = null;
            }
        }

        /// <summary>
        /// Load author aliases into memory - much faster than database lookups
        /// </summary>
        private static void LoadAuthorAliases()
        {
            try
            {
                // Clear existing aliases
                aliases.Clear();
                reverseAliases.Clear();

                // Load external file first (with old format)
                string aliasesFileName = Path.Combine(Utils.ServiceFilesLocation, "a_aliases.txt");
                if (File.Exists(aliasesFileName))
                {
                    Log.WriteLine("Loading author aliases from external file...");
                    using (var stream = File.OpenRead(aliasesFileName))
                    using (var reader = new StreamReader(stream))
                    {
                        LoadAliasesFromReader(reader);
                    }
                }
                else
                {
                    // Load from embedded gzipped resource
                    Log.WriteLine("Loading author aliases from embedded resource...");
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                        Assembly.GetExecutingAssembly().GetName().Name + ".Resources.a_aliases.txt.gz"))
                    {
                        if (stream != null)
                        {
                            using (var gzipStream = new GZipStream(stream, CompressionMode.Decompress))
                            using (var reader = new StreamReader(gzipStream))
                            {
                                LoadAliasesFromReader(reader);
                            }
                        }
                    }
                }

                // Build reverse lookup dictionary for fast access
                BuildReverseAliasesLookup();

                Log.WriteLine(LogLevel.Info, "Loaded {0} author aliases into memory", aliases.Count);
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Error loading author aliases: {0}", e.Message);
            }
        }

        /// <summary>
        /// Load aliases from a TextReader
        /// </summary>
        private static void LoadAliasesFromReader(TextReader reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    string[] parts = line.Split(new char[] { '\t', ',' });
                    try
                    {
                        if (parts.Length >= 8)
                        {
                            string canonicalName = string.Format("{2} {0} {1}", parts[1], parts[2], parts[3]).Trim();
                            string aliasName = string.Format("{2} {0} {1}", parts[5], parts[6], parts[7]).Trim();
                            if (!string.IsNullOrEmpty(aliasName) && !string.IsNullOrEmpty(canonicalName))
                            {
                                aliases[aliasName] = canonicalName;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore malformed lines
                    }
                }
            }
        }

        /// <summary>
        /// Build reverse lookup dictionary for fast alias queries
        /// </summary>
        private static void BuildReverseAliasesLookup()
        {
            foreach (var alias in aliases)
            {
                string canonicalName = alias.Value;
                if (!reverseAliases.ContainsKey(canonicalName))
                    reverseAliases[canonicalName] = new List<string>();

                reverseAliases[canonicalName].Add(alias.Key);
            }
        }

        #endregion
    }

    #region New Books Pagination Support Classes

    /// <summary>
    /// Result class for paginated New Books queries
    /// </summary>
    public class NewBooksPaginatedResult
    {
        public List<Book> Books { get; set; } = new List<Book>();
        public int TotalBooks { get; set; } = 0;
        public int TotalPages { get; set; } = 0;
        public int CurrentPage { get; set; } = 0;
        public int PageSize { get; set; } = 100;
        public bool HasPreviousPage { get; set; } = false;
        public bool HasNextPage { get; set; } = false;
    }

    #endregion
}