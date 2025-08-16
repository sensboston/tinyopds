/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * Repository for Book operations with SQLite database
 * 
 ************************************************************/

using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace TinyOPDS.Data
{
    public class BookRepository
    {
        private readonly DatabaseManager _db;

        public BookRepository(DatabaseManager database)
        {
            _db = database;
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
                    // Insert author if not exists
                    _db.ExecuteNonQuery(DatabaseSchema.InsertAuthor,
                        DatabaseManager.CreateParameter("@Name", authorName));

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

        public Book GetBookById(string id)
        {
            var book = _db.ExecuteQuerySingle(DatabaseSchema.SelectBookById, MapBook,
                DatabaseManager.CreateParameter("@ID", id));

            if (book != null)
            {
                LoadBookRelations(book);
            }

            return book;
        }

        public Book GetBookByFileName(string fileName)
        {
            var book = _db.ExecuteQuerySingle(DatabaseSchema.SelectBookByFileName, MapBook,
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
            var books = _db.ExecuteQuery(DatabaseSchema.SelectAllBooks, MapBook);
            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksByAuthor(string authorName)
        {
            var books = _db.ExecuteQuery(DatabaseSchema.SelectBooksByAuthor, MapBook,
                DatabaseManager.CreateParameter("@AuthorName", authorName));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksBySequence(string sequence)
        {
            var books = _db.ExecuteQuery(DatabaseSchema.SelectBooksBySequence, MapBook,
                DatabaseManager.CreateParameter("@Sequence", sequence));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksByGenre(string genreTag)
        {
            var books = _db.ExecuteQuery(DatabaseSchema.SelectBooksByGenre, MapBook,
                DatabaseManager.CreateParameter("@GenreTag", genreTag));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksByTitle(string title)
        {
            var books = _db.ExecuteQuery(DatabaseSchema.SelectBooksByTitle, MapBook,
                DatabaseManager.CreateParameter("@Title", title));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetNewBooks(DateTime fromDate)
        {
            var books = _db.ExecuteQuery(DatabaseSchema.SelectNewBooks, MapBook,
                DatabaseManager.CreateParameter("@FromDate", fromDate));

            foreach (var book in books)
            {
                LoadBookRelations(book);
            }
            return books;
        }

        public List<Book> GetBooksByFileNamePrefix(string fileNamePrefix)
        {
            var books = _db.ExecuteQuery(@"
                SELECT ID, Version, FileName, Title, Language, HasCover, BookDate, DocumentDate,
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

        public List<string> GetAllAuthors()
        {
            return _db.ExecuteQuery(DatabaseSchema.SelectAuthors, reader => reader.GetString(0));
        }

        public List<string> GetAllSequences()
        {
            return _db.ExecuteQuery(DatabaseSchema.SelectSequences, reader => reader.GetString(0));
        }

        public List<string> GetAllGenreTags()
        {
            return _db.ExecuteQuery(DatabaseSchema.SelectGenreTags, reader => reader.GetString(0));
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

        #endregion

        #region Helper Methods

        private Book MapBook(SQLiteDataReader reader)
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
            book.Authors = _db.ExecuteQuery(DatabaseSchema.SelectBookAuthors, reader => reader.GetString(0),
                DatabaseManager.CreateParameter("@BookID", book.ID));

            // Load genres
            book.Genres = _db.ExecuteQuery(DatabaseSchema.SelectBookGenres, reader => reader.GetString(0),
                DatabaseManager.CreateParameter("@BookID", book.ID));

            // Load translators
            book.Translators = _db.ExecuteQuery(DatabaseSchema.SelectBookTranslators, reader => reader.GetString(0),
                DatabaseManager.CreateParameter("@BookID", book.ID));
        }

        #endregion
    }
}