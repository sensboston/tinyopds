﻿using System;
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
        private static Dictionary<string, bool> _paths = new Dictionary<string, bool>();
        private static Dictionary<string, Book> _books = new Dictionary<string, Book>();
        private static string _databaseFileName;
        private static List<Genre> _genres;

        /// <summary>
        /// Default constructor
        /// Opens library"books.db" from the executable file location
        /// </summary>
        static Library()
        {
            // Load database in the background thread
            _databaseFileName = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\books.db";
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += (_, __) =>
            {
                Load();
                if (LibraryLoaded != null) LibraryLoaded(null, null);
                worker.Dispose();
            };
            worker.RunWorkerAsync();

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
            }
            catch { }
        }

        /// <summary>
        /// 
        /// </summary>
        private static string _libraryPath = string.Empty;
        private static int _libraryPathLength = 0;
        public static string LibraryPath
        {
            get 
            { 
                return _libraryPath; 
            }
            set
            {
                _libraryPath = value;
                if (!string.IsNullOrEmpty(_libraryPath))
                {
                    _libraryPathLength = (_libraryPath.Last() == '\\') ? _libraryPath.Length : _libraryPath.Length + 1;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bookPath"></param>
        /// <returns></returns>
        public static bool Contains(string bookPath)
        {
            lock (_paths)
            {
                return _paths.ContainsKey(bookPath.Substring(_libraryPathLength));
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
                    book.FilePath = Path.Combine(LibraryPath, book.FilePath);
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
                if (!_books.ContainsKey(book.ID) || _books[book.ID].Version < book.Version)
                {
                    // Make relative path
                    book.FilePath = book.FilePath.Substring(_libraryPathLength);
                    _books[book.ID] = book;
                    lock (_paths) _paths[book.FilePath] = true;
                    if (book.BookType == BookType.FB2) FB2Count++; else EPUBCount++;
                    return true;
                }
                return false;
            }
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
        /// Returns list of the library books titles sorted in alphabetical order
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

        public static List<string> BooksWithCover
        {
            get
            {
                return _books.Values.Where(b => b.HasCover).Select(b => b.Title).Distinct().OrderBy(a => a, new OPDSComparer(Localizer.Language.Equals("ru"))).ToList();
            }
        }

        /// <summary>
        /// Returns list of the library books authors sorted in alphabetical order 
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
        /// Return books by title
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        public static List<Book> GetBooksByTitle(string title)
        {
            lock (_books) return _books.Values.Where(b => b.Title.StartsWith(title)).ToList();
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
#if DEBUG
            DateTime start = DateTime.Now;
#endif
            // MemoryStream can save us about 1 second on 106 Mb database load
            MemoryStream memStream = null;
            if (File.Exists(_databaseFileName))
            {
                _books.Clear();
                memStream = new MemoryStream();
                try
                {
                    using (Stream fileStream = new FileStream(_databaseFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        fileStream.CopyTo(memStream);
                    }
                    memStream.Position = 0;
                    using (BinaryReader reader = new BinaryReader(memStream))
                    {
                        memStream = null;
                        while (reader.BaseStream.Position < reader.BaseStream.Length)
                        {
                            try
                            {
                                string filePath = reader.ReadString();
                                Book book = new Book(filePath);
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
                                book.Authors = new List<string>();
                                for (int i = 0; i < count; i++) book.Authors.Add(reader.ReadString());
                                count = reader.ReadInt32();
                                book.Translators = new List<string>();
                                for (int i = 0; i < count; i++) book.Translators.Add(reader.ReadString());
                                count = reader.ReadInt32();
                                book.Genres = new List<string>();
                                for (int i = 0; i < count; i++) book.Genres.Add(reader.ReadString());
                                lock (_books) _books[book.ID] = book;
                                lock (_paths) _paths[book.FilePath] = true;
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
                }
            }
#if DEBUG
            Log.WriteLine("Database load time = {0}", DateTime.Now.Subtract(start));
#endif
        }

        /// <summary>
        /// Save whole library
        /// </summary>
        public static void Save()
        {
            Stream fileStream = null;
            try
            {
                fileStream = new FileStream(_databaseFileName, FileMode.Create, FileAccess.Write, FileShare.Write);
                using (BinaryWriter writer = new BinaryWriter(fileStream))
                {
                    fileStream = null;
                    foreach (Book book in _books.Values)
                        writeBook(book, writer);
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Save books exception {0}", e.Message);
            }
            finally
            {
                if (fileStream != null) fileStream.Dispose();
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
                fileStream = new FileStream(_databaseFileName, FileMode.Append, FileAccess.Write, FileShare.Write);
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
                if (fileStream != null) fileStream.Dispose();
            }
        }

        private static void writeBook(Book book, BinaryWriter writer)
        {
            writer.Write(book.FilePath);
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
        }

        #endregion
    }
}
