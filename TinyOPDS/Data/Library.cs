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
        private static readonly Dictionary<string, string> _aliases = new Dictionary<string, string>();

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
                    _soundexedGenres[StringExtensions.Soundex(genre.Name)] = genre.Tag;
                    _soundexedGenres[StringExtensions.Soundex(genre.Translation)] = genre.Tag;
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

                // Load author aliases after repository is initialized
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
        /// Return list of new books
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

                    // Store original author names in database (removed alias application)

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
        /// Add multiple books in batch - optimized for performance
        /// </summary>
        /// <param name="books">List of books to add</param>
        /// <returns>Number of books actually added</returns>
        public static int AddBatch(List<Book> books)
        {
            if (_bookRepository == null || books == null || books.Count == 0) return 0;

            try
            {
                var start = DateTime.Now;

                // Process each book similar to individual Add method
                var processedBooks = new List<Book>();
                foreach (var book in books)
                {
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

                        // Store original author names in database (removed alias application)

                        processedBooks.Add(book);

                        if (isDuplicate)
                        {
                            Log.WriteLine(LogLevel.Warning, "Will replace duplicate in batch. File name {0}, book version {1}",
                                book.FileName, book.Version);
                        }
                    }
                    else
                    {
                        Log.WriteLine(LogLevel.Warning, "Skipping duplicate in batch. File name {0}, book ID {1}",
                            book.FileName, book.ID);
                    }
                }

                var addedCount = _bookRepository.AddBooksBatch(processedBooks);

                if (addedCount > 0)
                {
                    IsChanged = true;
                    InvalidateStatsCache();
                    Log.WriteLine("Added {0} books in batch ({1} ms)",
                        addedCount, (DateTime.Now - start).TotalMilliseconds);
                }

                return addedCount;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Library.AddBatch: {0}", ex.Message);
                return 0;
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

        /// <summary>
        /// Apply author aliases to author name (for OPDS output)
        /// </summary>
        /// <param name="originalAuthor">Original author name from database</param>
        /// <returns>Canonical author name if alias exists, otherwise original</returns>
        public static string ApplyAuthorAlias(string originalAuthor)
        {
            if (!TinyOPDS.Properties.Settings.Default.UseAuthorsAliases)
                return originalAuthor;

            // First try in-memory cache for speed
            if (_aliases.ContainsKey(originalAuthor))
                return _aliases[originalAuthor];

            // If not in cache but repository is available, try database lookup
            if (_bookRepository != null)
            {
                string canonical = _bookRepository.GetCanonicalAuthorName(originalAuthor);
                if (!canonical.Equals(originalAuthor))
                {
                    // Cache the result for future use
                    _aliases[originalAuthor] = canonical;
                    return canonical;
                }
            }

            return originalAuthor;
        }

        /// <summary>
        /// Get authors by name pattern with Soundex fallback for fuzzy search
        /// </summary>
        /// <param name="name">Search pattern</param>
        /// <param name="isOpenSearch">Whether this is open search (contains) or prefix search</param>
        /// <returns>List of matching author names</returns>
        public static List<string> GetAuthorsByName(string name, bool isOpenSearch)
        {
            if (_bookRepository == null) return new List<string>();

            try
            {
                // First try exact/pattern match using optimized SQL query
                var authors = _bookRepository.GetAuthorsByNamePattern(name, isOpenSearch);

                // If no results found and pattern is long enough, try Soundex search
                if (authors.Count == 0 && !string.IsNullOrEmpty(name) && name.Length >= 3)
                {
                    string nameSoundex = name.SoundexByWord();
                    var soundexAuthors = _bookRepository.GetAuthorsBySoundex(nameSoundex);

                    if (soundexAuthors.Count > 0)
                    {
                        Log.WriteLine(LogLevel.Info, "Soundex fallback for '{0}' found {1} authors", name, soundexAuthors.Count);
                        authors.AddRange(soundexAuthors);
                    }
                }

                // Remove duplicates and sort
                var comparer = new OPDSComparer(TinyOPDS.Properties.Settings.Default.SortOrder > 0);
                return authors.Where(a => a.Length > 1).Distinct().OrderBy(a => a, comparer).ToList();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GetAuthorsByName: {0}", ex.Message);
                return new List<string>();
            }
        }

        /// <summary>
        /// Get books by title with Soundex fallback for fuzzy search
        /// </summary>
        /// <param name="title">Title to search for</param>
        /// <returns>List of matching books</returns>
        public static List<Book> GetBooksByTitle(string title)
        {
            if (_bookRepository == null) return new List<Book>();

            try
            {
                // First try exact pattern match
                var books = _bookRepository.GetBooksByTitle(title);

                // If no results found and title is long enough, try Soundex search
                if (books.Count == 0 && !string.IsNullOrEmpty(title) && title.Length >= 3)
                {
                    string titleSoundex = title.SoundexByWord();
                    var soundexBooks = _bookRepository.GetBooksByTitleSoundex(titleSoundex);

                    if (soundexBooks.Count > 0)
                    {
                        Log.WriteLine(LogLevel.Info, "Soundex fallback for '{0}' found {1} books", title, soundexBooks.Count);
                        books.AddRange(soundexBooks);
                    }
                }

                return books.Distinct().ToList();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GetBooksByTitle: {0}", ex.Message);
                return new List<Book>();
            }
        }

        #endregion

        #region Query Methods (maintaining original API)

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

        private static void LoadAuthorAliases()
        {
            if (_bookRepository == null) return;

            try
            {
                // Clear existing aliases in database
                _bookRepository.ClearAuthorAliases();
                _aliases.Clear();

                int loadedCount = 0;

                // Load external file first (with old format)
                string aliasesFileName = Path.Combine(Utils.ServiceFilesLocation, "a_aliases.txt");
                if (File.Exists(aliasesFileName))
                {
                    using (var stream = File.OpenRead(aliasesFileName))
                    using (var reader = new StreamReader(stream))
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
                                        string aliasName = string.Format("{2} {0} {1}", parts[1], parts[2], parts[3]).Trim();
                                        string canonicalName = string.Format("{2} {0} {1}", parts[5], parts[6], parts[7]).Trim();

                                        if (!string.IsNullOrEmpty(aliasName) && !string.IsNullOrEmpty(canonicalName))
                                        {
                                            _aliases[aliasName] = canonicalName;
                                            _bookRepository.AddAuthorAlias(aliasName, canonicalName);
                                            loadedCount++;
                                        }
                                    }
                                }
                                catch
                                {
                                    Log.WriteLine(LogLevel.Warning, "Error parsing alias '{0}'", line);
                                }
                            }
                        }
                    }
                    Log.WriteLine(LogLevel.Info, "Loaded {0} authors aliases from {1}", loadedCount, aliasesFileName);
                }
                else
                {
                    // Load from embedded gzipped resource
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                        Assembly.GetExecutingAssembly().GetName().Name + ".a_aliases.txt.gz"))
                    {
                        if (stream != null)
                        {
                            using (var gzipStream = new GZipStream(stream, CompressionMode.Decompress))
                            using (var reader = new StreamReader(gzipStream))
                            {
                                string line;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    if (!string.IsNullOrEmpty(line))
                                    {
                                        string[] parts = line.Split('\t');
                                        try
                                        {
                                            if (parts.Length >= 8)
                                            {
                                                string aliasName = string.Format("{2} {0} {1}", parts[1], parts[2], parts[3]).Trim();
                                                string canonicalName = string.Format("{2} {0} {1}", parts[5], parts[6], parts[7]).Trim();

                                                if (!string.IsNullOrEmpty(aliasName) && !string.IsNullOrEmpty(canonicalName))
                                                {
                                                    _aliases[aliasName] = canonicalName;
                                                    _bookRepository.AddAuthorAlias(aliasName, canonicalName);
                                                    loadedCount++;
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
                            Log.WriteLine(LogLevel.Info, "Loaded {0} authors aliases from embedded resource", loadedCount);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Error loading author aliases: {0}", e.Message);
            }
        }

        #endregion

        #region Migration Helper

        /// <summary>
        /// Migrate existing binary database to SQLite
        /// </summary>
        /// <param name="binaryDbPath">Path to existing books.db file</param>
        public static void MigrateFromBinaryDatabase(string binaryDbPath)
        {
            if (!File.Exists(binaryDbPath))
            {
                Log.WriteLine("Binary database file not found: {0}", binaryDbPath);
                return;
            }

            Log.WriteLine("Starting migration from binary database to SQLite...");
            var start = DateTime.Now;
            int migratedCount = 0;

            try
            {
                using (var memStream = new MemoryStream())
                {
                    using (var fileStream = new FileStream(binaryDbPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        fileStream.CopyTo(memStream);
                    }

                    memStream.Position = 0;
                    using (var reader = new BinaryReader(memStream))
                    {
                        bool newFormat = reader.ReadString().Equals("VER1.1");
                        if (!newFormat)
                        {
                            reader.BaseStream.Position = 0;
                        }

                        // Don't apply aliases during migration - store original names
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            try
                            {
                                string fileName = reader.ReadString();
                                var book = new Book(Path.Combine(LibraryPath, fileName));

                                string id = reader.ReadString().Replace("{", "").Replace("}", "");
                                book.ID = id;
                                book.Version = reader.ReadSingle();
                                book.Title = reader.ReadString();
                                book.Language = reader.ReadString();
                                book.BookDate = DateTime.FromBinary(reader.ReadInt64());
                                book.DocumentDate = DateTime.FromBinary(reader.ReadInt64());
                                book.Sequence = reader.ReadString();
                                book.NumberInSequence = reader.ReadUInt32();
                                book.Annotation = reader.ReadString();
                                book.DocumentSize = reader.ReadUInt32();

                                int count = reader.ReadInt32();
                                for (int i = 0; i < count; i++)
                                {
                                    string authorName = reader.ReadString();
                                    // Store original name during migration
                                    book.Authors.Add(authorName);
                                }

                                count = reader.ReadInt32();
                                for (int i = 0; i < count; i++)
                                    book.Translators.Add(reader.ReadString());

                                count = reader.ReadInt32();
                                for (int i = 0; i < count; i++)
                                    book.Genres.Add(reader.ReadString());

                                book.AddedDate = newFormat ? DateTime.FromBinary(reader.ReadInt64()) : DateTime.Now;

                                if (_bookRepository.AddBook(book))
                                {
                                    migratedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.WriteLine(LogLevel.Warning, "Error migrating book: {0}", ex.Message);
                            }
                        }
                    }
                }

                Log.WriteLine("Migration completed: {0} books migrated in {1} ms",
                    migratedCount, (DateTime.Now - start).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Migration failed: {0}", ex.Message);
                throw;
            }
        }

        #endregion
    }
}