/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * SQLite-based implementation of Library class
 * This replaces the in-memory Dictionary approach with SQLite database
 * 
 ************************************************************/

using System;
using System.IO;
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
                    Assembly.GetExecutingAssembly().GetName().Name + ".Genres.xml"));

                _genres = (from g in doc.Descendants("genre")
                           select new Genre
                           {
                               Tag = g.Attribute("tag").Value,
                               Name = g.Attribute("name").Value,
                               Translation = g.Attribute("translation").Value,
                               Subgenres = (from sg in g.Descendants("subgenre")
                                            select new Genre
                                            {
                                                Tag = sg.Attribute("tag").Value,
                                                Name = sg.Attribute("name").Value,
                                                Translation = sg.Attribute("translation").Value
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

            // Load author aliases (same as original)
            LoadAuthorAliases();
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
                if (_bookRepository == null) return new List<Genre>();

                var libGenreTags = _bookRepository.GetAllGenreTags();
                var comparer = new OPDSComparer(TinyOPDS.Properties.Settings.Default.SortOrder > 0);
                var sortedTags = libGenreTags.Where(g => g.Length > 1)
                    .Select(g => g.ToLower().Trim())
                    .OrderBy(g => g, comparer)
                    .ToList();

                return _genres.SelectMany(g => g.Subgenres)
                    .Where(sg => sortedTags.Contains(sg.Tag) ||
                                sortedTags.Contains(sg.Name.ToLower()) ||
                                sortedTags.Contains(sg.Translation.ToLower()))
                    .ToList();
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

                    // Replace author aliases if enabled
                    if (TinyOPDS.Properties.Settings.Default.UseAuthorsAliases)
                    {
                        for (int i = 0; i < book.Authors.Count; i++)
                        {
                            if (_aliases.ContainsKey(book.Authors[i]))
                            {
                                book.Authors[i] = _aliases[book.Authors[i]];
                            }
                        }
                    }

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
            if (_bookRepository == null) return null;
            return _bookRepository.GetBookById(id);
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

        #region Query Methods (maintaining original API)

        /// <summary>
        /// Return list of authors by name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="isOpenSearch"></param>
        /// <returns></returns>
        public static List<string> GetAuthorsByName(string name, bool isOpenSearch)
        {
            if (_bookRepository == null) return new List<string>();

            var allAuthors = Authors; // This will get sorted authors from database

            List<string> authors;
            if (isOpenSearch)
            {
                authors = allAuthors.Where(a => a.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                if (authors.Count == 0)
                {
                    string reversedName = name.Reverse();
                    authors = allAuthors.Where(a => a.IndexOf(reversedName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                }
            }
            else
            {
                authors = allAuthors.Where(a => a.StartsWith(name, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return authors;
        }

        /// <summary>
        /// Return list of books by title
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        public static List<Book> GetBooksByTitle(string title)
        {
            if (_bookRepository == null) return new List<Book>();
            return _bookRepository.GetBooksByTitle(title);
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
            try
            {
                string aliasesPath = Path.Combine(Utils.ServiceFilesLocation, "authors.aliases");
                if (File.Exists(aliasesPath))
                {
                    foreach (string line in File.ReadAllLines(aliasesPath))
                    {
                        if (!string.IsNullOrEmpty(line) && line.Contains('='))
                        {
                            string[] parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                _aliases[parts[0].Trim()] = parts[1].Trim();
                            }
                        }
                    }
                    Log.WriteLine("Loaded {0} author aliases", _aliases.Count);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error loading author aliases: {0}", ex.Message);
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

                        bool useAliases = TinyOPDS.Properties.Settings.Default.UseAuthorsAliases;

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
                                book.HasCover = reader.ReadBoolean();
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
                                    if (useAliases && _aliases.ContainsKey(authorName))
                                        authorName = _aliases[authorName];
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