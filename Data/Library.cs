/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * One of the base project classes, the Library class
 * We are using static dictionaries instead of database
 * 
 ************************************************************/

using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;

namespace TinyOPDS.Data
{
    public static class Library
    {
        public static event EventHandler LibraryLoaded;
        private static Dictionary<string, string> _paths = new Dictionary<string, string>();
        private static Dictionary<string, Book> _books = new Dictionary<string, Book>();
        private static string _databaseFullPath;
        private static List<Genre> _genres;
        private static Dictionary<string, string> _soundexedGenres;
        private static bool _converted = false;

        /// <summary>
        /// Default constructor
        /// Opens library"books.db" from the executable file location
        /// </summary>
        static Library()
        {
            LoadAsync();

            // Load and parse genres
            try
            {
                var doc = XDocument.Load(Assembly.GetExecutingAssembly().GetManifestResourceStream("TinyOPDS.genres.xml"));
                _genres = doc.Root.Descendants("genre").Select(g =>
                    new Genre()
                    {
                        Name = g.Attribute("name").Value,
                        Translation = g.Attribute("ru").Value,
                        Subgenres = g.Descendants("subgenre").Select(sg =>
                            new Genre()
                            {
                                Name = sg.Value,
                                Tag = sg.Attribute("tag").Value,
                                Translation = sg.Attribute("ru").Value,
                            }).ToList()
                    }).ToList();

                _soundexedGenres = new Dictionary<string, string>();
                foreach (Genre genre in _genres)
                    foreach (Genre subgenre in genre.Subgenres)
                    {
                        _soundexedGenres[subgenre.Name.SoundexByWord()] = subgenre.Tag;
                        string reversed = string.Join(" ", subgenre.Name.Split(' ', ',').Reverse()).Trim();
                        _soundexedGenres[reversed.SoundexByWord()] = subgenre.Tag;
                    }

            }
            catch { }
        }

        /// <summary>
        /// Load library database in background
        /// </summary>
        public static void LoadAsync()
        {
            // Clear library and free memory
            FB2Count = EPUBCount = 0;
            _books.Clear();
            _paths.Clear();
            GC.Collect();

            // Create unique database name, based on library path
            LibraryPath = Properties.Settings.Default.LibraryPath;
            string databaseFileName = Utils.CreateGuid(Utils.IsoOidNamespace, LibraryPath).ToString() + ".db";
            _databaseFullPath = Path.Combine(Utils.ServiceFilesLocation, databaseFileName);

            // Load database in the background thread
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (_, __) =>
            {
                _converted = false;
                Load();
                if (LibraryLoaded != null) LibraryLoaded(null, null);
                if (_converted)
                {
                    Save();
                    Log.WriteLine(LogLevel.Info, "Database successfully converted to the format 1.1");
                }
                worker.Dispose();
            };
            worker.RunWorkerAsync();
        }

        /// <summary>
        /// Full path to the library folder
        /// </summary>
        public static string LibraryPath { get; set; }

        /// <summary>
        /// Library changed flag (we should save!)
        /// </summary>
        public static bool IsChanged { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bookPath"></param>
        /// <returns></returns>
        public static bool Contains(string bookPath)
        {
            lock (_paths)
            {
                return _paths.ContainsKey(bookPath);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static Book GetBook(string id)
        {
            lock (_books)
            {
                Book book = null;
                if (_books.ContainsKey(id))
                {
                    book = _books[id];
                }
                return book;
            }
        }

        /// <summary>
        /// Add unique book descriptor to the library and saves the library
        /// </summary>
        /// <param name="book"></param>
        public static bool Add(Book book)
        {
            lock (_books)
            {
                // Prevent incorrect duplicates detection (same ID but different titles)
                if (_books.ContainsKey(book.ID) && !book.Title.Equals(_books[book.ID].Title))
                {
                    book.ID = Utils.CreateGuid(Utils.IsoOidNamespace, book.FileName).ToString();
                }

                // Check for duplicates
                if (!_books.ContainsKey(book.ID) || (_books.ContainsKey(book.ID) && _books[book.ID].Version < book.Version))
                {
                    // Remember duplicate flag
                    bool isDuplicate = _books.ContainsKey(book.ID);
                    book.AddedDate = DateTime.Now;
                    // Make relative path
                    _books[book.ID] = book;
                    lock (_paths) _paths[book.FileName] = book.ID;
                    if (!isDuplicate)
                    {
                        IsChanged = true;
                        if (book.BookType == BookType.FB2) FB2Count++; else EPUBCount++;
                    }
                    else
                    {
                        Log.WriteLine(LogLevel.Warning, "Replaced duplicate. File name {0}, book version {1}", book.FileName, book.Version);
                    }
                    return !isDuplicate;
                }
                Log.WriteLine(LogLevel.Warning, "Found duplicate. File name {0}, book version {1}", book.FileName, book.Version);
                return false;
            }
        }

        /// <summary>
        /// Delete all books with specific file path from the library
        /// </summary>
        /// <param name="pathName"></param>
        public static bool Delete(string fileName)
        {
            bool result = false;
            lock (_books)
            {
                if (!string.IsNullOrEmpty(fileName) && fileName.Length > Library.LibraryPath.Length + 1)
                {
                    // Extract relative file name
                    fileName = fileName.Substring(Library.LibraryPath.Length + 1);
                    string ext = Path.GetExtension(fileName.ToLower());

                    // Assume it's a single file
                    if (ext.Equals(".epub") || ext.Equals(".fb2") || (ext.Equals(".zip") && fileName.ToLower().Contains(".fb2.zip")))
                    {
                        if (Contains(fileName))
                        {
                            Book book = _books[_paths[fileName]];
                            if (book != null)
                            {
                                _books.Remove(book.ID);
                                _paths.Remove(book.FileName);
                                if (book.BookType == BookType.FB2) FB2Count--; else EPUBCount--;
                                result = IsChanged = true;
                            }
                        }
                    }
                    // removed object should be archive or directory: let's remove all books with that path or zip
                    else 
                    {
                        List<Book> booksForRemove = _books.Where(b => b.Value.FileName.Contains(fileName)).Select(b => b.Value).ToList();
                        foreach (Book book in booksForRemove)
                        {
                            _books.Remove(book.ID);
                            _paths.Remove(book.FileName);
                            if (book.BookType == BookType.FB2) FB2Count--; else EPUBCount--;
                        }
                        if (booksForRemove.Count > 0)
                        {
                            result = IsChanged = true;
                        }
                    }
                }
            }
            return result;
        }


        /// <summary>
        /// Total number of books in library
        /// </summary>
        public static int Count
        {
            get
            {
                lock (_books) return _books.Count;
            }
        }

        /// <summary>
        /// Returns FB2 books count
        /// </summary>
        public static int FB2Count { get; private set; }

        /// <summary>
        /// Returns EPUB books count
        /// </summary>
        public static int EPUBCount { get; private set; }

        /// <summary>
        /// Returns list of the books titles sorted in alphabetical order
        /// </summary>
        public static List<string> Titles
        {
            get
            {
                lock (_books)
                {
                    return _books.Values.Select(b => b.Title).Distinct().OrderBy(a => a, new OPDSComparer(Localizer.Language.Equals("ru"))).ToList();
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
                lock (_books)
                {
                    return ((_books.Values.SelectMany(b => b.Authors)).ToList()).Distinct().OrderBy(a => a, new OPDSComparer(Localizer.Language.Equals("ru"))).Where(с => с.Length > 1).ToList();
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
                lock (_books)
                {
                    return ((_books.Values.Select(b => b.Sequence)).ToList()).Distinct().OrderBy(a => a, new OPDSComparer(Localizer.Language.Equals("ru"))).Where(с => с.Length > 1).ToList();
                }
            }
        }

        /// <summary>
        /// All genres supported by fb2 format
        /// </summary>
        public static List<Genre> FB2Genres
        {
            get
            {
                return _genres;
            }
        }

        public static Dictionary<string, string> SoundexedGenres
        {
            get
            {
                return _soundexedGenres;
            }
        }

        /// <summary>
        /// Returns sorted in alphabetical order list of library books genres
        /// </summary>
        public static List<Genre> Genres
        {
            get
            {
                lock (_books)
                {
                    var libGenres = _books.Values.SelectMany(b => b.Genres).ToList().Distinct().OrderBy(a => a, new OPDSComparer(Localizer.Language.Equals("ru"))).Where(с => с.Length > 1).Select(g => g.ToLower().Trim()).ToList();
                    return _genres.SelectMany(g => g.Subgenres).Where(sg => libGenres.Contains(sg.Tag) || libGenres.Contains(sg.Name.ToLower()) || libGenres.Contains(sg.Translation.ToLower())).ToList();
                }
            }
        }

        /// <summary>
        /// Search authors by name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static List<string> GetAuthorsByName(string name, bool isOpenSearch)
        {
            List<string> authors = new List<string>();
            lock (_books)
            {
                if (isOpenSearch) authors = Authors.Where(a => a.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                else authors = Authors.Where(a => a.StartsWith(name, StringComparison.OrdinalIgnoreCase)).ToList();
                if (isOpenSearch && authors.Count == 0)
                {
                    string reversedName = name.Reverse();
                    authors = Authors.Where(a => a.IndexOf(reversedName, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                }
                return authors;
            }
        }

        /// <summary>
        /// Return books by title
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        public static List<Book> GetBooksByTitle(string title)
        {
            lock (_books) return _books.Values.Where(b => b.Title.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0 || b.Sequence.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        /// <summary>
        /// Return books by selected author(s)
        /// </summary>
        /// <param name="author"></param>
        /// <returns></returns>
        public static List<Book> GetBooksByAuthor(string author)
        {
            lock (_books) return _books.Values.Where(b => b.Authors.Contains(author)).ToList();
        }

        /// <summary>
        /// Return books by selected sequence
        /// </summary>
        /// <param name="author"></param>
        /// <returns></returns>
        public static List<Book> GetBooksBySequence(string sequence)
        {
            lock (_books) return _books.Values.Where(b => b.Sequence.Contains(sequence)).ToList();
        }

        /// <summary>
        /// Return books by selected genre
        /// </summary>
        /// <param name="author"></param>
        /// <returns></returns>
        public static List<Book> GetBooksByGenre(string genre)
        {
            lock (_books) return _books.Values.Where(b => b.Genres.Contains(genre)).ToList();
        }

        #region Serialization and deserialization

        /// <summary>
        /// Load library
        /// </summary>
        public static void Load()
        {
            int numRecords = 0;
            DateTime start = DateTime.Now;

            // MemoryStream can save us about 1 second on 106 Mb database load time
            MemoryStream memStream = null;
            if (File.Exists(_databaseFullPath))
            {
                _books.Clear();
                memStream = new MemoryStream();

                try
                {
                    using (Stream fileStream = new FileStream(_databaseFullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        fileStream.CopyTo(memStream);
                    }
                    memStream.Position = 0;
                    using (BinaryReader reader = new BinaryReader(memStream))
                    {
                        bool newFormat = reader.ReadString().Equals("VER1.1");
                        if (!newFormat)
                        {
                            reader.BaseStream.Position = 0;
                            _converted = true;
                        }

                        DateTime now = DateTime.Now;

                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            try
                            {
                                string fileName = reader.ReadString();
                                Book book = new Book(Path.Combine(LibraryPath,fileName));
                                book.ID = reader.ReadString();
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
                                for (int i = 0; i < count; i++) book.Authors.Add(reader.ReadString());
                                count = reader.ReadInt32();
                                for (int i = 0; i < count; i++) book.Translators.Add(reader.ReadString());
                                count = reader.ReadInt32();
                                for (int i = 0; i < count; i++) book.Genres.Add(reader.ReadString());
                                lock (_books) _books[book.ID] = book;
                                lock (_paths) _paths[book.FileName] = book.ID;
                                book.AddedDate = newFormat ? DateTime.FromBinary(reader.ReadInt64()) : now;

                                numRecords++;
                            }
                            catch (EndOfStreamException)
                            {
                                break;
                            }
                            catch (Exception e)
                            {
                                Log.WriteLine(LogLevel.Error, e.Message);
                                break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.WriteLine(LogLevel.Error, "Load books exception {0}", e.Message);
                }
                finally
                {
                    if (memStream != null)
                    {
                        memStream.Dispose();
                        memStream = null;
                    }
                    // Call garbage collector now
                    GC.Collect();

                    FB2Count = _books.Count(b => b.Value.BookType == BookType.FB2);
                    EPUBCount = _books.Count(b => b.Value.BookType == BookType.EPUB);

                    IsChanged = false;
                }
            }

            Log.WriteLine(LogLevel.Info, "Database load time = {0}, {1} book records loaded", DateTime.Now.Subtract(start), numRecords);
        }

        /// <summary>
        /// Save whole library
        /// </summary>
        /// Remark: new database format is used!
        public static void Save()
        {
            // Do nothing if we have no records
            if (_books.Count == 0) return;

            int numRecords = 0;
            DateTime start = DateTime.Now;

            Stream fileStream = null;
            try
            {
                fileStream = new FileStream(_databaseFullPath, FileMode.Create, FileAccess.Write, FileShare.Write);
                using (BinaryWriter writer = new BinaryWriter(fileStream))
                {
                    fileStream = null;
                    writer.Write("VER1.1");

                    // Create shallow copy (to prevent exception on dictionary modifications during foreach loop)
                    Dictionary<string, Book> shallowCopy = null;
                    lock (_books) shallowCopy = new Dictionary<string, Book>(_books);
                    foreach (Book book in shallowCopy.Values)
                    {
                        writeBook(book, writer);
                        numRecords++;
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Save books exception {0}", e.Message);
            }
            finally
            {
                if (fileStream != null) fileStream.Dispose();
                IsChanged = false;
                Log.WriteLine(LogLevel.Info, "Database save time = {0}, {1} book records written to disk", DateTime.Now.Subtract(start), numRecords);
            }
        }

        /// <summary>
        /// Append one book descriptor to the library file
        /// </summary>
        /// <param name="book"></param>
        public static void Append(Book book)
        {
            Stream fileStream = null;
            try
            {
                fileStream = new FileStream(_databaseFullPath, FileMode.Append, FileAccess.Write, FileShare.Write);
                using (BinaryWriter writer = new BinaryWriter(fileStream))
                {
                    fileStream = null;
                    writeBook(book, writer);
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Can't append book {0}, exception {1}", book.FilePath, e.Message);
            }
            finally
            {
                if (fileStream != null)
                {
                    fileStream.Dispose();
                }
                IsChanged = false;
            }
        }

        private static void writeBook(Book book, BinaryWriter writer)
        {
            writer.Write(book.FileName);
            writer.Write(book.ID);
            writer.Write(book.Version);
            writer.Write(book.Title);
            writer.Write(book.Language);
            writer.Write(book.HasCover);
            writer.Write(book.BookDate.ToBinary());
            writer.Write(book.DocumentDate.ToBinary());
            writer.Write(book.Sequence);
            writer.Write(book.NumberInSequence);
            writer.Write(book.Annotation);
            writer.Write(book.DocumentSize);
            writer.Write((Int32)book.Authors.Count);
            foreach (string author in book.Authors) writer.Write(author);
            writer.Write((Int32)book.Translators.Count);
            foreach (string translator in book.Translators) writer.Write(translator);
            writer.Write((Int32)book.Genres.Count);
            foreach (string genre in book.Genres) writer.Write(genre);
            writer.Write(book.AddedDate.ToBinary());
        }

        #endregion
    }
}
