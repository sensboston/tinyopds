/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Repository for Book operations with SQLite database
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

                // Calculate Soundex for title
                string titleSoundex = !string.IsNullOrEmpty(book.Title) ? book.Title.SoundexByWord() : "";

                // Insert or update book
                var bookParams = new[]
                {
                    DatabaseManager.CreateParameter("@ID", book.ID),
                    DatabaseManager.CreateParameter("@Version", book.Version),
                    DatabaseManager.CreateParameter("@FileName", book.FileName),
                    DatabaseManager.CreateParameter("@Title", book.Title),
                    DatabaseManager.CreateParameter("@TitleSoundex", titleSoundex),
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
                    // Calculate Soundex for author name
                    string authorSoundex = !string.IsNullOrEmpty(authorName) ? authorName.SoundexByWord() : "";

                    // Insert author if not exists
                    _db.ExecuteNonQuery(DatabaseSchema.InsertAuthor,
                        DatabaseManager.CreateParameter("@Name", authorName),
                        DatabaseManager.CreateParameter("@NameSoundex", authorSoundex));

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
        /// Add multiple books in batch - optimized for performance with detailed result
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

                        // Calculate Soundex for title
                        string titleSoundex = !string.IsNullOrEmpty(book.Title) ? book.Title.SoundexByWord() : "";

                        // Insert book
                        var bookParams = new[]
                        {
                            DatabaseManager.CreateParameter("@ID", book.ID),
                            DatabaseManager.CreateParameter("@Version", book.Version),
                            DatabaseManager.CreateParameter("@FileName", book.FileName),
                            DatabaseManager.CreateParameter("@Title", book.Title),
                            DatabaseManager.CreateParameter("@TitleSoundex", titleSoundex),
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

                        // Insert authors and relationships
                        foreach (var authorName in book.Authors)
                        {
                            // Calculate Soundex for author name
                            string authorSoundex = !string.IsNullOrEmpty(authorName) ? authorName.SoundexByWord() : "";

                            // Insert author if not exists
                            _db.ExecuteNonQuery(DatabaseSchema.InsertAuthor,
                                DatabaseManager.CreateParameter("@Name", authorName),
                                DatabaseManager.CreateParameter("@NameSoundex", authorSoundex));

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

                var result = _db.ExecuteNonQuery(DatabaseSchema.DeleteBook,
                    DatabaseManager.CreateParameter("@ID", id));

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

                var result = _db.ExecuteNonQuery(DatabaseSchema.DeleteBookByFileName,
                    DatabaseManager.CreateParameter("@FileName", fileName));

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
        /// Search books by title using Soundex (fuzzy search)
        /// </summary>
        public List<Book> GetBooksByTitleSoundex(string titleSoundex)
        {
            var books = _db.ExecuteQuery<Book>(DatabaseSchema.SelectBooksByTitleSoundex, MapBook,
                DatabaseManager.CreateParameter("@TitleSoundex", titleSoundex));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
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
                SELECT ID, Version, FileName, Title, TitleSoundex, Language, BookDate, DocumentDate,
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

        #region Author Queries

        /// <summary>
        /// Get authors by name pattern - simple and fast SQL query
        /// </summary>
        public List<string> GetAuthorsByNamePattern(string pattern, bool isOpenSearch = false)
        {
            try
            {
                string searchPattern = isOpenSearch ? $"%{pattern}%" : $"{pattern}%";
                var authors = _db.ExecuteQuery<string>(DatabaseSchema.SelectAuthorsByNamePattern,
                    reader => reader.GetString(0),
                    DatabaseManager.CreateParameter("@Pattern", searchPattern));

                return authors.Distinct().ToList();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error getting authors by pattern {0}: {1}", pattern, ex.Message);
                return new List<string>();
            }
        }

        /// <summary>
        /// Search authors by name using Soundex (fuzzy search)
        /// </summary>
        public List<string> GetAuthorsBySoundex(string nameSoundex)
        {
            try
            {
                var authors = _db.ExecuteQuery<string>(DatabaseSchema.SelectAuthorsByNameSoundex,
                    reader => reader.GetString(0),
                    DatabaseManager.CreateParameter("@NameSoundex", nameSoundex));

                return authors.Distinct().ToList();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error getting authors by soundex {0}: {1}", nameSoundex, ex.Message);
                return new List<string>();
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