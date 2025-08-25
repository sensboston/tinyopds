/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * SQLite-based implementation of Library class
 * This replaces the in-memory Dictionary approach with SQLite database
 *
 */

using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;

namespace TinyOPDS.Data
{
    public static class Library
    {
        public static event EventHandler LibraryLoaded;

        private static DatabaseManager _database;
        private static BookRepository _bookRepository;
        private static string _databaseFullPath;
        private static readonly List<Genre> _genres;
        private static readonly Dictionary<string, string> _soundexedGenres;
        private static readonly TimeSpan[] _periods = new TimeSpan[7];

        // Author aliases - now stored in memory for fast access
        private static readonly Dictionary<string, string> _aliases = new Dictionary<string, string>();
        private static readonly Dictionary<string, List<string>> _reverseAliases = new Dictionary<string, List<string>>();

        // Cache for frequently accessed data
        private static DateTime _lastStatsUpdate = DateTime.MinValue;
        private static int _cachedTotalCount = 0;
        private static int _cachedFB2Count = 0;
        private static int _cachedEPUBCount = 0;
        private static int _cachedAuthorsCount = 0;
        private static int _cachedSequencesCount = 0;
        private static readonly TimeSpan CacheTimeout = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Static constructor - initializes periods and genres like the original
        /// </summary>
        static Library()
        {
            // Initialize "new books" periods
            _periods[0] = TimeSpan.FromDays(7);
            _periods[1] = TimeSpan.FromDays(14);
            _periods[2] = TimeSpan.FromDays(21);
            _periods[3] = TimeSpan.FromDays(30);
            _periods[4] = TimeSpan.FromDays(44);
            _periods[5] = TimeSpan.FromDays(60);
            _periods[6] = TimeSpan.FromDays(90);

            // Load and parse genres (same as original)
            try
            {
                var doc = XDocument.Load(Assembly.GetExecutingAssembly().GetManifestResourceStream(
                    Assembly.GetExecutingAssembly().GetName().Name + ".genres.xml"));

                _genres = (from g in doc.Descendants("genre")
                           select new Genre
                           {
                               Tag = "",
                               Name = g.Attribute("name").Value,
                               Translation = g.Attribute("ru").Value,
                               Subgenres = (from sg in g.Descendants("subgenre")
                                            select new Genre
                                            {
                                                Tag = sg.Attribute("tag").Value,
                                                Name = sg.Value,
                                                Translation = sg.Attribute("ru").Value
                                            }).ToList()
                           }).ToList();

                _soundexedGenres = new Dictionary<string, string>();
                foreach (var genre in _genres.SelectMany(g => g.Subgenres))
                {
                    _soundexedGenres[StringUtils.Soundex(genre.Name)] = genre.Tag;
                    _soundexedGenres[StringUtils.Soundex(genre.Translation)] = genre.Tag;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error loading genres: {0}", ex.Message);
                _genres = new List<Genre>();
                _soundexedGenres = new Dictionary<string, string>();
            }
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

            _databaseFullPath = databasePath;

            try
            {
                _database?.Dispose();
                _database = new DatabaseManager(_databaseFullPath);
                _bookRepository = new BookRepository(_database);

                // Load author aliases into memory
                LoadAuthorAliases();

                Log.WriteLine("SQLite database initialized: {0}", _databaseFullPath);
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
            _database?.Dispose();
            _database = null;
            _bookRepository = null;
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
                return _cachedTotalCount;
            }
        }

        /// <summary>
        /// New books count
        /// </summary>
        public static int NewBooksCount
        {
            get
            {
                if (_bookRepository == null) return 0;

                var period = _periods[TinyOPDS.Properties.Settings.Default.NewBooksPeriod];
                var fromDate = DateTime.Now.Subtract(period);
                return _bookRepository.GetNewBooksCount(fromDate);
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
                return _cachedFB2Count;
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
                return _cachedEPUBCount;
            }
        }

        /// <summary>
        /// Returns list of the authors sorted in alphabetical order 
        /// </summary>
        public static List<string> Authors
        {
            get
            {
                if (_bookRepository == null) return new List<string>();

                var authors = _bookRepository.GetAllAuthors();
                var comparer = new OPDSComparer(TinyOPDS.Properties.Settings.Default.SortOrder > 0);
                return authors.Where(a => a.Length > 1).OrderBy(a => a, comparer).ToList();
            }
        }

        /// <summary>
        /// Returns list of the library books series sorted in alphabetical order
        /// </summary>
        public static List<string> Sequences
        {
            get
            {
                if (_bookRepository == null) return new List<string>();

                var sequences = _bookRepository.GetAllSequences();
                var comparer = new OPDSComparer(TinyOPDS.Properties.Settings.Default.SortOrder > 0);
                return sequences.Where(s => s.Length > 1).OrderBy(s => s, comparer).ToList();
            }
        }

        /// <summary>
        /// All genres supported by fb2 format
        /// </summary>
        public static List<Genre> FB2Genres
        {
            get { return _genres; }
        }

        public static Dictionary<string, string> SoundexedGenres
        {
            get { return _soundexedGenres; }
        }

        /// <summary>
        /// Returns sorted in alphabetical order list of library books genres
        /// </summary>
        public static List<Genre> Genres
        {
            get
            {
                // Simply return all subgenres from the loaded XML genres
                var useCyrillic = TinyOPDS.Properties.Settings.Default.SortOrder > 0;
                var comparer = new OPDSComparer(useCyrillic);
                return _genres.SelectMany(g => g.Subgenres).OrderBy(g => useCyrillic ? g.Translation : g.Name, comparer).ToList();
            }
        }

        /// <summary>
        /// Return list of new books (DEPRECATED - use GetNewBooksPaginated for large datasets)
        /// </summary>
        public static List<Book> NewBooks
        {
            get
            {
                if (_bookRepository == null) return new List<Book>();

                var period = _periods[TinyOPDS.Properties.Settings.Default.NewBooksPeriod];
                var fromDate = DateTime.Now.Subtract(period);
                return _bookRepository.GetNewBooks(fromDate);
            }
        }

        /// <summary>
        /// Returns list of the books titles sorted in alphabetical order
        /// </summary>
        public static List<string> Titles
        {
            get
            {
                if (_bookRepository == null) return new List<string>();

                var books = _bookRepository.GetAllBooks();
                var comparer = new OPDSComparer(TinyOPDS.Properties.Settings.Default.SortOrder > 0);
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

            if (_bookRepository == null)
            {
                return result;
            }

            try
            {
                var period = _periods[TinyOPDS.Properties.Settings.Default.NewBooksPeriod];
                var fromDate = DateTime.Now.Subtract(period);

                // Get total count for pagination
                result.TotalBooks = _bookRepository.GetNewBooksCount(fromDate);
                result.PageSize = pageSize;
                result.CurrentPage = pageNumber;
                result.TotalPages = (int)Math.Ceiling((double)result.TotalBooks / pageSize);

                // Calculate offset
                int offset = pageNumber * pageSize;

                // Get books for current page
                result.Books = _bookRepository.GetNewBooksPaginated(fromDate, offset, pageSize, sortByDate);

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
            if (_bookRepository == null) return 0;

            var period = _periods[TinyOPDS.Properties.Settings.Default.NewBooksPeriod];
            var fromDate = DateTime.Now.Subtract(period);
            return _bookRepository.GetNewBooksCount(fromDate);
        }

        #endregion

        #region Core Methods (maintaining original API)

        /// <summary>
        /// Load library - now initializes SQLite database
        /// </summary>
        public static void Load()
        {
            var start = DateTime.Now;

            if (_database == null)
            {
                Initialize();
            }

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
        /// </summary>
        /// <param name="book"></param>
        public static bool Add(Book book)
        {
            if (_bookRepository == null) return false;

            try
            {
                // Apply author aliases before saving to database
                ApplyAliasesToBookAuthors(book);

                // Check for duplicates (similar to original logic)
                var existingBook = _bookRepository.GetBookById(book.ID);
                if (existingBook != null && !book.Title.Equals(existingBook.Title))
                {
                    book.ID = Utils.CreateGuid(Utils.IsoOidNamespace, book.FileName).ToString();
                }

                existingBook = _bookRepository.GetBookById(book.ID);
                bool isDuplicate = existingBook != null;

                if (!isDuplicate || (isDuplicate && existingBook.Version < book.Version))
                {
                    if (book.AddedDate == DateTime.MinValue)
                        book.AddedDate = DateTime.Now;

                    bool success = _bookRepository.AddBook(book);
                    if (success)
                    {
                        IsChanged = true;
                        InvalidateStatsCache();

                        if (isDuplicate)
                        {
                            Log.WriteLine(LogLevel.Warning, "Replaced duplicate. File name {0}, book version {1}",
                                book.FileName, book.Version);
                        }
                    }

                    return success && !isDuplicate;
                }

                Log.WriteLine(LogLevel.Warning, "Found duplicate. File name {0}, book ID {1}",
                    book.FileName, book.ID);
                return false;
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
        /// <param name="books">List of books to add</param>
        /// <returns>BatchResult with detailed statistics</returns>
        public static BookRepository.BatchResult AddBatch(List<Book> books)
        {
            var result = new BookRepository.BatchResult();
            var startTime = DateTime.Now;

            if (_bookRepository == null || books == null || books.Count == 0)
            {
                result.ProcessingTime = DateTime.Now - startTime;
                return result;
            }

            try
            {
                result.TotalProcessed = books.Count;

                // Process each book similar to individual Add method
                var processedBooks = new List<Book>();
                int skippedDuplicates = 0;

                foreach (var book in books)
                {
                    // Apply author aliases before processing
                    ApplyAliasesToBookAuthors(book);

                    // Check for duplicates and handle ID conflicts
                    var existingBook = _bookRepository.GetBookById(book.ID);
                    if (existingBook != null && !book.Title.Equals(existingBook.Title))
                    {
                        book.ID = Utils.CreateGuid(Utils.IsoOidNamespace, book.FileName).ToString();
                    }

                    existingBook = _bookRepository.GetBookById(book.ID);
                    bool isDuplicate = existingBook != null;

                    if (!isDuplicate || (isDuplicate && existingBook.Version < book.Version))
                    {
                        if (book.AddedDate == DateTime.MinValue)
                            book.AddedDate = DateTime.Now;

                        processedBooks.Add(book);

                        if (isDuplicate)
                        {
                            Log.WriteLine(LogLevel.Warning, "Will replace duplicate in batch. File name {0}, book version {1}",
                                book.FileName, book.Version);
                        }
                    }
                    else
                    {
                        skippedDuplicates++;
                        Log.WriteLine(LogLevel.Warning, "Skipping duplicate in batch. File name {0}, book ID {1}",
                            book.FileName, book.ID);
                    }
                }

                // Use repository batch method
                var batchResult = _bookRepository.AddBooksBatch(processedBooks);

                // Combine results - add duplicates found at Library level
                result.Added = batchResult.Added;
                result.Duplicates = batchResult.Duplicates + skippedDuplicates;
                result.Errors = batchResult.Errors;
                result.FB2Count = batchResult.FB2Count;
                result.EPUBCount = batchResult.EPUBCount;
                result.ErrorMessages = batchResult.ErrorMessages;
                result.ProcessingTime = DateTime.Now - startTime;

                if (result.Added > 0)
                {
                    IsChanged = true;
                    InvalidateStatsCache();
                    Log.WriteLine("Library.AddBatch completed: {0} added, {1} duplicates, {2} errors in {3}ms",
                        result.Added, result.Duplicates, result.Errors, result.ProcessingTime.TotalMilliseconds);
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
            if (_bookRepository == null) return false;

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
                            Book book = _bookRepository.GetBookByFileName(fileName);
                            if (book != null)
                            {
                                bool result = _bookRepository.DeleteBook(book.ID);
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
                        var booksToRemove = _bookRepository.GetBooksByFileNamePrefix(fileName + "@");
                        bool result = false;
                        foreach (Book book in booksToRemove)
                        {
                            if (_bookRepository.DeleteBook(book.ID))
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
            if (_bookRepository == null) return false;
            return _bookRepository.BookExists(bookPath);
        }

        /// <summary>
        /// Get book by ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static Book GetBook(string id)
        {
            if (_bookRepository == null)
            {
                Log.WriteLine(LogLevel.Error, "BookRepository is null when getting book {0}", id);
                return null;
            }

            Log.WriteLine(LogLevel.Info, "Searching for book with ID: {0}", id);
            var book = _bookRepository.GetBookById(id);

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
            if (_bookRepository == null) return false;

            try
            {
                var allBooks = _bookRepository.GetAllBooks();
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
                    if (_bookRepository.DeleteBook(bookId))
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

        #region Author Aliases Management

        /// <summary>
        /// Apply author aliases to book before saving to database
        /// </summary>
        /// <param name="book">Book to process</param>
        private static void ApplyAliasesToBookAuthors(Book book)
        {
            if (!Properties.Settings.Default.UseAuthorsAliases || book.Authors == null)
                return;

            for (int i = 0; i < book.Authors.Count; i++)
            {
                string originalAuthor = book.Authors[i];
                if (_aliases.ContainsKey(originalAuthor))
                {
                    book.Authors[i] = _aliases[originalAuthor];
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
            if (!_reverseAliases.ContainsKey(canonicalName))
                return new List<string>();

            return _reverseAliases[canonicalName];
        }

        /// <summary>
        /// Get canonical name for alias (for internal use)
        /// </summary>
        /// <param name="aliasName">Alias name</param>
        /// <returns>Canonical name if alias exists, otherwise original name</returns>
        public static string GetCanonicalName(string aliasName)
        {
            return _aliases.ContainsKey(aliasName) ? _aliases[aliasName] : aliasName;
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
            if (_bookRepository == null) return new List<string>();

            try
            {
                Log.WriteLine(LogLevel.Info, "Searching authors by name: '{0}', isOpenSearch: {1}", name, isOpenSearch);

                List<string> authors;

                if (isOpenSearch)
                {
                    authors = _bookRepository.GetAuthorsForOpenSearch(name);
                }
                else
                {
                    authors = _bookRepository.GetAuthorsForNavigation(name);
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
            if (_bookRepository == null) return new List<Book>();

            try
            {
                Log.WriteLine(LogLevel.Info, "Searching books by title: '{0}', isOpenSearch: {1}", title, isOpenSearch);

                List<Book> books;

                if (isOpenSearch)
                {
                    // Use FTS5 search with transliteration fallback
                    books = _bookRepository.GetBooksForOpenSearch(title);
                }
                else
                {
                    // Use traditional LIKE search for navigation
                    books = _bookRepository.GetBooksByTitle(title);
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
            if (_bookRepository == null) return 0;
            return _bookRepository.GetBooksByAuthor(author).Count;
        }

        /// <summary>
        /// Return list of books by selected author(s)
        /// </summary>
        /// <param name="author"></param>
        /// <returns></returns>
        public static List<Book> GetBooksByAuthor(string author)
        {
            if (_bookRepository == null) return new List<Book>();
            return _bookRepository.GetBooksByAuthor(author);
        }

        /// <summary>
        /// Return list of books by selected sequence
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public static List<Book> GetBooksBySequence(string sequence)
        {
            if (_bookRepository == null) return new List<Book>();
            return _bookRepository.GetBooksBySequence(sequence);
        }

        /// <summary>
        /// Return list of books by selected genre
        /// </summary>
        /// <param name="genre"></param>
        /// <returns></returns>
        public static List<Book> GetBooksByGenre(string genre)
        {
            if (_bookRepository == null) return new List<Book>();
            return _bookRepository.GetBooksByGenre(genre);
        }

        /// <summary>
        /// Get access to BookRepository for internal use by catalogs
        /// </summary>
        /// <returns>BookRepository instance or null if not initialized</returns>
        internal static BookRepository GetBookRepository()
        {
            return _bookRepository;
        }

        #endregion

        #region Helper Methods

        private static void RefreshStatsCache()
        {
            if (_bookRepository == null) return;

            if (DateTime.Now - _lastStatsUpdate > CacheTimeout)
            {
                _cachedTotalCount = _bookRepository.GetTotalBooksCount();
                _cachedFB2Count = _bookRepository.GetFB2BooksCount();
                _cachedEPUBCount = _bookRepository.GetEPUBBooksCount();
                _cachedAuthorsCount = _bookRepository.GetAuthorsCount();
                _cachedSequencesCount = _bookRepository.GetSequencesCount();
                _lastStatsUpdate = DateTime.Now;
            }
        }

        private static void InvalidateStatsCache()
        {
            _lastStatsUpdate = DateTime.MinValue;
        }

        /// <summary>
        /// Load author aliases into memory - much faster than database lookups
        /// </summary>
        private static void LoadAuthorAliases()
        {
            try
            {
                // Clear existing aliases
                _aliases.Clear();
                _reverseAliases.Clear();

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
                        Assembly.GetExecutingAssembly().GetName().Name + ".a_aliases.txt.gz"))
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

                Log.WriteLine(LogLevel.Info, "Loaded {0} author aliases into memory", _aliases.Count);
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
                                _aliases[aliasName] = canonicalName;
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
            foreach (var alias in _aliases)
            {
                string canonicalName = alias.Value;
                if (!_reverseAliases.ContainsKey(canonicalName))
                    _reverseAliases[canonicalName] = new List<string>();

                _reverseAliases[canonicalName].Add(alias.Key);
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