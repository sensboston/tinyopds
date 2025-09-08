/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * SQLite-based implementation of Library class
 * OPTIMIZED: Architecturally correct caching with proper synchronization
 * ENHANCED: Persistent statistics for instant startup display
 *
 */

using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

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

        #region Enhanced Cache Implementation with Persistent Statistics

        // Enhanced cache for frequently accessed data with separate variables
        private static readonly object cacheLock = new object();
        private static DateTime lastStatsUpdate = DateTime.MinValue;
        private static int cachedTotalCount = 0;
        private static int cachedFB2Count = 0;
        private static int cachedEPUBCount = 0;
        private static int cachedAuthorsCount = 0;
        private static int cachedSequencesCount = 0;
        private static int cachedNewBooksCount = 0;
        private static DateTime lastNewBooksCountUpdate = DateTime.MinValue;

        // Cache state tracking
        private static volatile bool isCacheInitialized = false;
        private static volatile bool isCacheWarming = false;

        // Cache timeouts
        private static readonly TimeSpan CacheTimeout = TimeSpan.FromHours(1);
        private static readonly TimeSpan NewBooksCacheTimeout = TimeSpan.FromMinutes(5);

        // Lists cache (separate from stats cache)
        private static volatile List<string> cachedAuthorsList = null;
        private static volatile List<string> cachedSequencesList = null;
        private static DateTime lastAuthorsListUpdate = DateTime.MinValue;
        private static DateTime lastSequencesListUpdate = DateTime.MinValue;
        private static readonly TimeSpan ListsCacheTimeout = TimeSpan.FromMinutes(10);
        private static readonly object listsLock = new object();

        // Genre cache
        private static DateTime lastGenresUpdate = DateTime.MinValue;
        private static List<Genre> cachedGenres = null;

        // Authors alphabetical cache for fast OPDS navigation
        private static Dictionary<string, List<string>> cachedAuthorsByFirstLetter = null;
        private static List<string> cachedAuthorsFirstLetters = null;
        private static DateTime lastAuthorsByLetterUpdate = DateTime.MinValue;
        private static readonly TimeSpan AuthorsByLetterCacheTimeout = TimeSpan.FromHours(2);
        private static readonly object authorsByLetterLock = new object();
        private static bool isAuthorsCacheLoading = false;

        #endregion

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

                // NEW: Load statistics from database immediately for instant display
                LoadStatsFromDatabase();

                // Start async cache warming without blocking
                System.Threading.Tasks.Task.Run(() => InitializeCacheAsync());

                // Initialize authors alphabetical cache
                System.Threading.Tasks.Task.Run(() => InitializeAuthorsCacheAsync());

                Log.WriteLine("SQLite database initialized: {0}", databaseFullPath);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Failed to initialize database: {0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Initialize cache asynchronously without blocking
        /// </summary>
        private static void InitializeCacheAsync()
        {
            if (bookRepository == null || isCacheWarming) return;

            try
            {
                isCacheWarming = true;
                Log.WriteLine("Starting async cache initialization...");
                var startTime = DateTime.Now;

                // Load all counts in parallel
                var tasks = new System.Threading.Tasks.Task<int>[6];
                var period = periods[Properties.Settings.Default.NewBooksPeriod];
                var fromDate = DateTime.Now.Subtract(period);

                tasks[0] = System.Threading.Tasks.Task.Run(() => bookRepository.GetTotalBooksCount());
                tasks[1] = System.Threading.Tasks.Task.Run(() => bookRepository.GetFB2BooksCount());
                tasks[2] = System.Threading.Tasks.Task.Run(() => bookRepository.GetEPUBBooksCount());
                tasks[3] = System.Threading.Tasks.Task.Run(() => bookRepository.GetAuthorsCount());
                tasks[4] = System.Threading.Tasks.Task.Run(() => bookRepository.GetSequencesCount());
                tasks[5] = System.Threading.Tasks.Task.Run(() => bookRepository.GetNewBooksCount(fromDate));

                System.Threading.Tasks.Task.WaitAll(tasks);

                // Update cache with fresh values
                lock (cacheLock)
                {
                    cachedTotalCount = tasks[0].Result;
                    cachedFB2Count = tasks[1].Result;
                    cachedEPUBCount = tasks[2].Result;
                    cachedAuthorsCount = tasks[3].Result;
                    cachedSequencesCount = tasks[4].Result;
                    cachedNewBooksCount = tasks[5].Result;

                    lastStatsUpdate = DateTime.Now;
                    lastNewBooksCountUpdate = DateTime.Now;
                    isCacheInitialized = true;
                }

                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Log.WriteLine("Cache initialized in {0} ms. Books: {1}, Authors: {2}, Series: {3}",
                    elapsed, cachedTotalCount, cachedAuthorsCount, cachedSequencesCount);

                // NEW: Save updated statistics to database for next startup
                SaveStatsToDatabase();

                LibraryLoaded?.Invoke(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error initializing cache: {0}", ex.Message);
            }
            finally
            {
                isCacheWarming = false;
            }
        }

        /// <summary>
        /// Initialize authors alphabetical cache asynchronously
        /// </summary>
        private static void InitializeAuthorsCacheAsync()
        {
            if (bookRepository == null || isAuthorsCacheLoading) return;

            try
            {
                isAuthorsCacheLoading = true;
                Log.WriteLine("Starting authors alphabetical cache initialization...");
                var startTime = DateTime.Now;

                // Load all authors from repository
                var allAuthors = bookRepository.GetAllAuthors();

                // Build alphabetical cache
                var authorsByLetter = new Dictionary<string, List<string>>();
                var firstLetters = new HashSet<string>();
                var comparer = new OPDSComparer(Properties.Settings.Default.SortOrder > 0);

                foreach (var author in allAuthors.Where(a => !string.IsNullOrEmpty(a) && a.Length > 1))
                {
                    // Get first character (uppercase)
                    string firstChar = author.Substring(0, 1).ToUpper();

                    firstLetters.Add(firstChar);

                    if (!authorsByLetter.ContainsKey(firstChar))
                        authorsByLetter[firstChar] = new List<string>();

                    authorsByLetter[firstChar].Add(author);
                }

                // Sort authors within each letter group
                foreach (var key in authorsByLetter.Keys.ToList())
                {
                    authorsByLetter[key] = authorsByLetter[key]
                        .Distinct()
                        .OrderBy(a => a, comparer)
                        .ToList();
                }

                // Update cache atomically
                lock (authorsByLetterLock)
                {
                    cachedAuthorsByFirstLetter = authorsByLetter;
                    cachedAuthorsFirstLetters = firstLetters.OrderBy(l => l, comparer).ToList();
                    lastAuthorsByLetterUpdate = DateTime.Now;
                }

                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Log.WriteLine("Authors alphabetical cache initialized in {0} ms. Letters: {1}",
                    elapsed, firstLetters.Count);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error initializing authors cache: {0}", ex.Message);
            }
            finally
            {
                isAuthorsCacheLoading = false;
            }
        }

        /// <summary>
        /// Refresh authors alphabetical cache
        /// </summary>
        private static void RefreshAuthorsCacheAsync()
        {
            if (isAuthorsCacheLoading) return;
            System.Threading.Tasks.Task.Run(() => InitializeAuthorsCacheAsync());
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

        #endregion

        #region Statistics Persistence Methods

        /// <summary>
        /// Load statistics from database for immediate display
        /// </summary>
        private static void LoadStatsFromDatabase()
        {
            if (db == null) return;

            try
            {
                var allStats = db.GetAllLibraryStats();

                lock (cacheLock)
                {
                    // Load persisted values into cache
                    if (allStats.ContainsKey("total_books"))
                        cachedTotalCount = allStats["total_books"].Value;

                    if (allStats.ContainsKey("fb2_books"))
                        cachedFB2Count = allStats["fb2_books"].Value;

                    if (allStats.ContainsKey("epub_books"))
                        cachedEPUBCount = allStats["epub_books"].Value;

                    if (allStats.ContainsKey("authors_count"))
                        cachedAuthorsCount = allStats["authors_count"].Value;

                    if (allStats.ContainsKey("sequences_count"))
                        cachedSequencesCount = allStats["sequences_count"].Value;

                    if (allStats.ContainsKey("new_books"))
                        cachedNewBooksCount = allStats["new_books"].Value;

                    // Mark as initialized so properties return these values immediately
                    isCacheInitialized = true;
                    lastStatsUpdate = DateTime.Now;
                    lastNewBooksCountUpdate = DateTime.Now;
                }

                Log.WriteLine("Loaded library statistics from database: Books={0}, Authors={1}, Series={2}",
                    cachedTotalCount, cachedAuthorsCount, cachedSequencesCount);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error loading statistics from database: {0}", ex.Message);
                // Initialize with zeros if can't load from database
                lock (cacheLock)
                {
                    cachedTotalCount = 0;
                    cachedFB2Count = 0;
                    cachedEPUBCount = 0;
                    cachedAuthorsCount = 0;
                    cachedSequencesCount = 0;
                    cachedNewBooksCount = 0;
                    isCacheInitialized = true;
                }
            }
        }

        /// <summary>
        /// Save current statistics to database
        /// </summary>
        private static void SaveStatsToDatabase()
        {
            if (db == null) return;

            try
            {
                var stats = new Dictionary<string, int>();
                var currentPeriod = Properties.Settings.Default.NewBooksPeriod;

                lock (cacheLock)
                {
                    stats["total_books"] = cachedTotalCount;
                    stats["fb2_books"] = cachedFB2Count;
                    stats["epub_books"] = cachedEPUBCount;
                    stats["authors_count"] = cachedAuthorsCount;
                    stats["sequences_count"] = cachedSequencesCount;
                    stats["new_books"] = cachedNewBooksCount;
                }

                // Save to database with current period for new books
                db.SaveLibraryStatistics(stats, (int)periods[currentPeriod].TotalDays);

                Log.WriteLine("Saved library statistics to database");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error saving statistics to database: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Internal method to save statistics after scanning or other operations
        /// (statistics are now saved automatically on all changes)
        /// </summary>
        internal static void SaveStatistics()
        {
            SaveStatsToDatabase();
        }

        #endregion

        #region Properties with Proper Cache Access

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
                GetOrRefreshCache();
                lock (cacheLock)
                {
                    return cachedTotalCount;
                }
            }
        }

        /// <summary>
        /// New books count
        /// </summary>
        public static int NewBooksCount
        {
            get
            {
                GetOrRefreshCache();

                // Check if new books count needs separate refresh
                lock (cacheLock)
                {
                    if (DateTime.Now - lastNewBooksCountUpdate > NewBooksCacheTimeout && bookRepository != null)
                    {
                        RefreshNewBooksCount();
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
                GetOrRefreshCache();
                lock (cacheLock)
                {
                    return cachedFB2Count;
                }
            }
        }

        /// <summary>
        /// Returns EPUB books count
        /// </summary>
        public static int EPUBCount
        {
            get
            {
                GetOrRefreshCache();
                lock (cacheLock)
                {
                    return cachedEPUBCount;
                }
            }
        }

        /// <summary>
        /// Get authors count without loading all authors
        /// </summary>
        public static int AuthorsCount
        {
            get
            {
                GetOrRefreshCache();
                lock (cacheLock)
                {
                    return cachedAuthorsCount;
                }
            }
        }

        /// <summary>
        /// Get sequences count without loading all sequences
        /// </summary>
        public static int SequencesCount
        {
            get
            {
                GetOrRefreshCache();
                lock (cacheLock)
                {
                    return cachedSequencesCount;
                }
            }
        }

        /// <summary>
        /// Returns list of the authors sorted in alphabetical order
        /// </summary>
        public static List<string> Authors
        {
            get
            {
                if (bookRepository == null) return new List<string>();

                lock (listsLock)
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
        /// Returns list of the library books series sorted in alphabetical order
        /// </summary>
        public static List<string> Sequences
        {
            get
            {
                if (bookRepository == null) return new List<string>();

                lock (listsLock)
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

        #endregion

        #region Cache Management

        /// <summary>
        /// Get current cache or refresh if needed (non-blocking)
        /// </summary>
        private static void GetOrRefreshCache()
        {
            // Fast path - return if cache is valid
            if (isCacheInitialized && DateTime.Now - lastStatsUpdate < CacheTimeout)
            {
                return;
            }

            // If cache is warming, return (use current cached values)
            if (isCacheWarming)
            {
                return;
            }

            // Try to refresh cache with timeout
            if (Monitor.TryEnter(cacheLock, TimeSpan.FromMilliseconds(100)))
            {
                try
                {
                    // Double-check after acquiring lock
                    if (isCacheInitialized && DateTime.Now - lastStatsUpdate < CacheTimeout)
                    {
                        return;
                    }

                    // Refresh cache synchronously if we got the lock
                    RefreshCacheInternal();
                }
                finally
                {
                    Monitor.Exit(cacheLock);
                }
            }
            else
            {
                // If can't get lock quickly, start async refresh
                if (!isCacheWarming && bookRepository != null)
                {
                    System.Threading.Tasks.Task.Run(() => RefreshCacheAsync());
                }
            }
        }

        /// <summary>
        /// Refresh cache synchronously (must be called under lock)
        /// </summary>
        private static void RefreshCacheInternal()
        {
            if (bookRepository == null) return;

            try
            {
                var period = periods[Properties.Settings.Default.NewBooksPeriod];
                var fromDate = DateTime.Now.Subtract(period);

                // Update cached values
                cachedTotalCount = bookRepository.GetTotalBooksCount();
                cachedFB2Count = bookRepository.GetFB2BooksCount();
                cachedEPUBCount = bookRepository.GetEPUBBooksCount();
                cachedAuthorsCount = bookRepository.GetAuthorsCount();
                cachedSequencesCount = bookRepository.GetSequencesCount();
                cachedNewBooksCount = bookRepository.GetNewBooksCount(fromDate);

                lastStatsUpdate = DateTime.Now;
                lastNewBooksCountUpdate = DateTime.Now;
                isCacheInitialized = true;

                // Save updated statistics to database
                SaveStatsToDatabase();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error refreshing cache: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Refresh only new books count
        /// </summary>
        private static void RefreshNewBooksCount()
        {
            if (bookRepository == null) return;

            lock (cacheLock)
            {
                try
                {
                    var period = periods[Properties.Settings.Default.NewBooksPeriod];
                    var fromDate = DateTime.Now.Subtract(period);
                    cachedNewBooksCount = bookRepository.GetNewBooksCount(fromDate);
                    lastNewBooksCountUpdate = DateTime.Now;

                    // Save only new books count to database
                    db?.SaveLibraryStatistic("new_books", cachedNewBooksCount, (int)period.TotalDays);
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Error refreshing new books count: {0}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Invalidate all caches without zeroing values
        /// </summary>
        private static void InvalidateCache()
        {
            lock (cacheLock)
            {
                // DO NOT zero out cached values - just invalidate timestamps
                // This prevents returning 0 books during scanning
                lastStatsUpdate = DateTime.MinValue;
                lastNewBooksCountUpdate = DateTime.MinValue;

                // Keep isCacheInitialized as true if it was already initialized
                // This ensures properties continue returning cached values
                // isCacheInitialized remains unchanged

                // Clear lists cache - these are not critical and can be reloaded
                lock (listsLock)
                {
                    cachedAuthorsList = null;
                    cachedSequencesList = null;
                    lastAuthorsListUpdate = DateTime.MinValue;
                    lastSequencesListUpdate = DateTime.MinValue;
                }

                // Invalidate authors alphabetical cache
                lock (authorsByLetterLock)
                {
                    lastAuthorsByLetterUpdate = DateTime.MinValue;
                }
            }

            // Start async refresh - it will update values when ready
            System.Threading.Tasks.Task.Run(() => RefreshCacheAsync());

            // Refresh authors cache
            RefreshAuthorsCacheAsync();
        }

        /// <summary>
        /// Refresh cache asynchronously
        /// </summary>
        private static void RefreshCacheAsync()
        {
            if (isCacheWarming || bookRepository == null) return;

            try
            {
                isCacheWarming = true;

                // Load new values
                var period = periods[Properties.Settings.Default.NewBooksPeriod];
                var fromDate = DateTime.Now.Subtract(period);

                int newTotalCount = bookRepository.GetTotalBooksCount();
                int newFB2Count = bookRepository.GetFB2BooksCount();
                int newEPUBCount = bookRepository.GetEPUBBooksCount();
                int newAuthorsCount = bookRepository.GetAuthorsCount();
                int newSequencesCount = bookRepository.GetSequencesCount();
                int newNewBooksCount = bookRepository.GetNewBooksCount(fromDate);

                // Update cache atomically with new values
                lock (cacheLock)
                {
                    cachedTotalCount = newTotalCount;
                    cachedFB2Count = newFB2Count;
                    cachedEPUBCount = newEPUBCount;
                    cachedAuthorsCount = newAuthorsCount;
                    cachedSequencesCount = newSequencesCount;
                    cachedNewBooksCount = newNewBooksCount;

                    lastStatsUpdate = DateTime.Now;
                    lastNewBooksCountUpdate = DateTime.Now;
                    isCacheInitialized = true; // Ensure it stays initialized
                }

                // Save updated statistics to database
                SaveStatsToDatabase();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error refreshing cache: {0}", ex.Message);
            }
            finally
            {
                isCacheWarming = false;
            }
        }

        #endregion

        #region All other properties and methods remain the same

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
            if (db == null) Initialize();
            LoadGenresFromDatabase();
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
        /// </summary>
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

                bool success = bookRepository.AddBook(book);

                if (success)
                {
                    IsChanged = true;
                    InvalidateCache();
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
        /// </summary>
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

                result = bookRepository.AddBooksBatch(books);

                if (result.Added > 0 || result.Replaced > 0)
                {
                    IsChanged = true;
                    InvalidateCache();
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
                                    InvalidateCache();
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
                            InvalidateCache();
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
        public static bool Contains(string bookPath)
        {
            if (bookRepository == null) return false;
            return bookRepository.BookExists(bookPath);
        }

        /// <summary>
        /// Get book by ID
        /// </summary>
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
                    InvalidateCache();
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

        #region Genre Management (remains the same)

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
                InvalidateCache();
                Log.WriteLine("Fixed {0} invalid genre entries", fixedCount);
            }

            return fixedCount;
        }

        #endregion

        #region Author Aliases Management (remains the same)

        /// <summary>
        /// Check if string contains Cyrillic characters
        /// </summary>
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
        /// </summary>
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
                    }
                }
            }
        }

        /// <summary>
        /// Apply author aliases to author name (for OPDS output)
        /// </summary>
        public static string ApplyAuthorAlias(string originalAuthor)
        {
            return originalAuthor;
        }

        /// <summary>
        /// Get all alias names that map to the canonical name
        /// </summary>
        public static List<string> GetAliasesForCanonicalName(string canonicalName)
        {
            if (!reverseAliases.ContainsKey(canonicalName))
                return new List<string>();

            return reverseAliases[canonicalName];
        }

        /// <summary>
        /// Get canonical name for alias (for internal use)
        /// </summary>
        public static string GetCanonicalName(string aliasName)
        {
            return aliases.ContainsKey(aliasName) ? aliases[aliasName] : aliasName;
        }

        #endregion

        #region Author Search Methods (MODIFIED for cache optimization)

        /// <summary>
        /// Get authors by name - optimized with cache for short patterns
        /// </summary>
        public static List<string> GetAuthorsByName(string name, bool isOpenSearch)
        {
            if (bookRepository == null) return new List<string>();

            try
            {
                Log.WriteLine(LogLevel.Info, "Searching authors by name: '{0}', isOpenSearch: {1}", name, isOpenSearch);

                // Use cache for short patterns (0 or 1 character) in non-OpenSearch mode
                if (!isOpenSearch && name != null && name.Length <= 1)
                {
                    return GetAuthorsFromCache(name);
                }

                // For longer patterns or OpenSearch, use database
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
        /// Get authors from cache for short patterns
        /// </summary>
        private static List<string> GetAuthorsFromCache(string pattern)
        {
            // Ensure cache is loaded
            if (cachedAuthorsByFirstLetter == null || DateTime.Now - lastAuthorsByLetterUpdate > AuthorsByLetterCacheTimeout)
            {
                // Try to get lock without blocking
                if (Monitor.TryEnter(authorsByLetterLock, TimeSpan.FromMilliseconds(100)))
                {
                    try
                    {
                        // Double-check after acquiring lock
                        if (cachedAuthorsByFirstLetter == null || DateTime.Now - lastAuthorsByLetterUpdate > AuthorsByLetterCacheTimeout)
                        {
                            // Need to refresh cache - do it async
                            RefreshAuthorsCacheAsync();

                            // For now, fall back to database
                            return GetAuthorsFromDatabase(pattern);
                        }
                    }
                    finally
                    {
                        Monitor.Exit(authorsByLetterLock);
                    }
                }
                else
                {
                    // Could not get lock, fall back to database
                    return GetAuthorsFromDatabase(pattern);
                }
            }

            // Use cache
            lock (authorsByLetterLock)
            {
                if (string.IsNullOrEmpty(pattern))
                {
                    // For empty pattern, AuthorsCatalog expects ALL authors to build the alphabet
                    // We need to return all authors from all letters
                    if (cachedAuthorsByFirstLetter != null)
                    {
                        var allAuthors = new List<string>();
                        foreach (var letterGroup in cachedAuthorsByFirstLetter.Values)
                        {
                            allAuthors.AddRange(letterGroup);
                        }

                        // Sort all authors
                        var comparer = new OPDSComparer(Properties.Settings.Default.SortOrder > 0);
                        var result = allAuthors.Distinct().OrderBy(a => a, comparer).ToList();

                        Log.WriteLine(LogLevel.Info, "Returning {0} total authors from cache for alphabet building", result.Count);
                        return result;
                    }
                }
                else if (pattern.Length == 1)
                {
                    // Return authors for specific letter
                    string upperPattern = pattern.ToUpper();
                    if (cachedAuthorsByFirstLetter != null && cachedAuthorsByFirstLetter.ContainsKey(upperPattern))
                    {
                        var authors = cachedAuthorsByFirstLetter[upperPattern];
                        Log.WriteLine(LogLevel.Info, "Returning {0} authors for letter '{1}' from cache", authors.Count, upperPattern);
                        return new List<string>(authors);
                    }
                }

                // Fall back to database if cache miss
                return GetAuthorsFromDatabase(pattern);
            }
        }

        /// <summary>
        /// Helper method to get authors from database (fallback)
        /// </summary>
        private static List<string> GetAuthorsFromDatabase(string pattern)
        {
            if (bookRepository == null) return new List<string>();

            var authors = bookRepository.GetAuthorsForNavigation(pattern);
            var comparer = new OPDSComparer(Properties.Settings.Default.SortOrder > 0);
            return authors.Where(a => a.Length > 1).Distinct().OrderBy(a => a, comparer).ToList();
        }

        /// <summary>
        /// Get authors by name with search method information
        /// </summary>
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
                    var authors = GetAuthorsByName(name, false); // This will use cache for short patterns

                    // For navigation, we don't track method, but if found, it's partial match
                    var method = authors.Count > 0 ? AuthorSearchMethod.PartialMatch : AuthorSearchMethod.NotFound;
                    return (authors, method);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GetAuthorsByNameWithMethod: {0}", ex.Message);
                return (new List<string>(), AuthorSearchMethod.NotFound);
            }
        }

        /// <summary>
        /// Get books by title - original method for compatibility
        /// </summary>
        public static List<Book> GetBooksByTitle(string title)
        {
            return GetBooksByTitle(title, false);
        }

        /// <summary>
        /// Get books by title - OpenSearch version with FTS5 search and transliteration
        /// </summary>
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
        public static int GetBooksByAuthorCount(string author)
        {
            if (bookRepository == null) return 0;
            return bookRepository.GetBooksByAuthor(author).Count;
        }

        /// <summary>
        /// Return list of books by selected author(s)
        /// </summary>
        public static List<Book> GetBooksByAuthor(string author)
        {
            if (bookRepository == null) return new List<Book>();
            return bookRepository.GetBooksByAuthor(author);
        }

        /// <summary>
        /// Return list of books by selected sequence
        /// </summary>
        public static List<Book> GetBooksBySequence(string sequence)
        {
            if (bookRepository == null) return new List<Book>();
            return bookRepository.GetBooksBySequence(sequence);
        }

        /// <summary>
        /// Return list of books by selected genre
        /// </summary>
        public static List<Book> GetBooksByGenre(string genre)
        {
            if (bookRepository == null) return new List<Book>();
            return bookRepository.GetBooksByGenre(genre);
        }

        /// <summary>
        /// Get access to BookRepository for internal use by catalogs
        /// </summary>
        internal static BookRepository GetBookRepository()
        {
            return bookRepository;
        }

        #endregion

        #region Helper Methods

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