/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Database schema and SQL queries for SQLite migration
 *
 */

namespace TinyOPDS.Data
{
    public static class DatabaseSchema
    {
        #region Create Table Scripts

        public const string CreateBooksTable = @"
            CREATE TABLE IF NOT EXISTS Books (
                ID TEXT PRIMARY KEY,
                Version REAL NOT NULL DEFAULT 1.0,
                FileName TEXT NOT NULL UNIQUE,
                Title TEXT NOT NULL,
                Language TEXT,
                BookDate INTEGER, -- DateTime as ticks
                DocumentDate INTEGER, -- DateTime as ticks  
                Sequence TEXT,
                NumberInSequence INTEGER NOT NULL DEFAULT 0,
                Annotation TEXT,
                DocumentSize INTEGER NOT NULL DEFAULT 0,
                AddedDate INTEGER NOT NULL -- DateTime as ticks
            )";

        public const string CreateAuthorsTable = @"
            CREATE TABLE IF NOT EXISTS Authors (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                BookCount INTEGER NOT NULL DEFAULT 0
            )";

        public const string CreateGenresTable = @"
            CREATE TABLE IF NOT EXISTS Genres (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                Tag TEXT NOT NULL UNIQUE,
                Name TEXT NOT NULL,
                Translation TEXT
            )";

        public const string CreateTranslatorsTable = @"
            CREATE TABLE IF NOT EXISTS Translators (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE
            )";

        public const string CreateBookAuthorsTable = @"
            CREATE TABLE IF NOT EXISTS BookAuthors (
                BookID TEXT NOT NULL,
                AuthorID INTEGER NOT NULL,
                PRIMARY KEY (BookID, AuthorID),
                FOREIGN KEY (BookID) REFERENCES Books(ID) ON DELETE CASCADE,
                FOREIGN KEY (AuthorID) REFERENCES Authors(ID) ON DELETE CASCADE
            )";

        public const string CreateBookGenresTable = @"
            CREATE TABLE IF NOT EXISTS BookGenres (
                BookID TEXT NOT NULL,
                GenreTag TEXT NOT NULL,
                PRIMARY KEY (BookID, GenreTag),
                FOREIGN KEY (BookID) REFERENCES Books(ID) ON DELETE CASCADE
            )";

        public const string CreateBookTranslatorsTable = @"
            CREATE TABLE IF NOT EXISTS BookTranslators (
                BookID TEXT NOT NULL,
                TranslatorID INTEGER NOT NULL,
                PRIMARY KEY (BookID, TranslatorID),
                FOREIGN KEY (BookID) REFERENCES Books(ID) ON DELETE CASCADE,
                FOREIGN KEY (TranslatorID) REFERENCES Translators(ID) ON DELETE CASCADE
            )";

        #endregion

        #region Indexes

        public const string CreateIndexes = @"
            CREATE INDEX IF NOT EXISTS idx_books_filename ON Books(FileName);
            CREATE INDEX IF NOT EXISTS idx_books_title ON Books(Title);
            CREATE INDEX IF NOT EXISTS idx_books_sequence ON Books(Sequence);
            CREATE INDEX IF NOT EXISTS idx_books_addeddate ON Books(AddedDate);
            CREATE INDEX IF NOT EXISTS idx_authors_name ON Authors(Name);
            CREATE INDEX IF NOT EXISTS idx_bookauthors_bookid ON BookAuthors(BookID);
            CREATE INDEX IF NOT EXISTS idx_bookauthors_authorid ON BookAuthors(AuthorID);
            CREATE INDEX IF NOT EXISTS idx_bookgenres_bookid ON BookGenres(BookID);
            CREATE INDEX IF NOT EXISTS idx_bookgenres_genretag ON BookGenres(GenreTag);
        ";

        #endregion

        #region Insert Queries

        public const string InsertBook = @"
            INSERT OR REPLACE INTO Books 
            (ID, Version, FileName, Title, Language, BookDate, DocumentDate, 
             Sequence, NumberInSequence, Annotation, DocumentSize, AddedDate)
            VALUES 
            (@ID, @Version, @FileName, @Title, @Language, @BookDate, @DocumentDate,
             @Sequence, @NumberInSequence, @Annotation, @DocumentSize, @AddedDate)";

        public const string InsertAuthor = @"
            INSERT OR IGNORE INTO Authors (Name, BookCount) VALUES (@Name, 0)";

        public const string InsertTranslator = @"
            INSERT OR IGNORE INTO Translators (Name) VALUES (@Name)";

        public const string InsertBookAuthor = @"
            INSERT OR IGNORE INTO BookAuthors (BookID, AuthorID) 
            VALUES (@BookID, (SELECT ID FROM Authors WHERE Name = @AuthorName))";

        public const string InsertBookGenre = @"
            INSERT OR IGNORE INTO BookGenres (BookID, GenreTag) VALUES (@BookID, @GenreTag)";

        public const string InsertBookTranslator = @"
            INSERT OR IGNORE INTO BookTranslators (BookID, TranslatorID) 
            VALUES (@BookID, (SELECT ID FROM Translators WHERE Name = @TranslatorName))";

        #endregion

        #region Update Queries

        public const string UpdateAuthorBookCount = @"
            UPDATE Authors SET BookCount = (
                SELECT COUNT(*) FROM BookAuthors WHERE AuthorID = Authors.ID
            ) WHERE Name = @AuthorName";

        #endregion

        #region Select Queries

        public const string SelectAllBooks = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Sequence, NumberInSequence, Annotation, DocumentSize, AddedDate
            FROM Books";

        public const string SelectBookById = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Sequence, NumberInSequence, Annotation, DocumentSize, AddedDate
            FROM Books WHERE ID = @ID";

        public const string SelectBookByFileName = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Sequence, NumberInSequence, Annotation, DocumentSize, AddedDate
            FROM Books WHERE FileName = @FileName";

        public const string SelectBooksByAuthor = @"
            SELECT b.ID, b.Version, b.FileName, b.Title, b.Language, b.BookDate, b.DocumentDate,
                   b.Sequence, b.NumberInSequence, b.Annotation, b.DocumentSize, b.AddedDate
            FROM Books b
            INNER JOIN BookAuthors ba ON b.ID = ba.BookID
            INNER JOIN Authors a ON ba.AuthorID = a.ID
            WHERE a.Name = @AuthorName";

        public const string SelectBooksBySequence = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Sequence, NumberInSequence, Annotation, DocumentSize, AddedDate
            FROM Books 
            WHERE Sequence LIKE '%' || @Sequence || '%'
            ORDER BY NumberInSequence";

        public const string SelectBooksByGenre = @"
            SELECT b.ID, b.Version, b.FileName, b.Title, b.Language, b.BookDate, b.DocumentDate,
                   b.Sequence, b.NumberInSequence, b.Annotation, b.DocumentSize, b.AddedDate
            FROM Books b
            INNER JOIN BookGenres bg ON b.ID = bg.BookID
            WHERE bg.GenreTag = @GenreTag";

        public const string SelectBooksByTitle = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Sequence, NumberInSequence, Annotation, DocumentSize, AddedDate
            FROM Books 
            WHERE Title LIKE '%' || @Title || '%' OR Sequence LIKE '%' || @Title || '%'";

        public const string SelectNewBooks = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Sequence, NumberInSequence, Annotation, DocumentSize, AddedDate
            FROM Books 
            WHERE AddedDate >= @FromDate";

        public const string SelectAuthors = @"
            SELECT DISTINCT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            ORDER BY a.Name";

        public const string SelectSequences = @"
            SELECT DISTINCT Sequence
            FROM Books 
            WHERE Sequence IS NOT NULL AND Sequence != ''
            ORDER BY Sequence";

        public const string SelectGenreTags = @"
            SELECT DISTINCT GenreTag
            FROM BookGenres
            ORDER BY GenreTag";

        public const string SelectAuthorsCount = @"
            SELECT COUNT(DISTINCT a.ID) FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID";

        public const string SelectSequencesCount = @"
            SELECT COUNT(DISTINCT Sequence) FROM Books 
            WHERE Sequence IS NOT NULL AND Sequence != ''";

        public const string SelectBookAuthors = @"
            SELECT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            WHERE ba.BookID = @BookID
            ORDER BY a.Name";

        public const string SelectBookGenres = @"
            SELECT GenreTag
            FROM BookGenres
            WHERE BookID = @BookID";

        public const string SelectBookTranslators = @"
            SELECT t.Name
            FROM Translators t
            INNER JOIN BookTranslators bt ON t.ID = bt.TranslatorID
            WHERE bt.BookID = @BookID
            ORDER BY t.Name";

        public const string CountBooks = @"SELECT COUNT(*) FROM Books";

        public const string CountFB2Books = @"SELECT COUNT(*) FROM Books WHERE FileName LIKE '%.fb2%'";

        public const string CountEPUBBooks = @"SELECT COUNT(*) FROM Books WHERE FileName LIKE '%.epub%'";

        public const string CountNewBooks = @"SELECT COUNT(*) FROM Books WHERE AddedDate >= @FromDate";

        #endregion

        #region Delete Queries

        public const string DeleteBook = @"DELETE FROM Books WHERE ID = @ID";

        public const string DeleteBookByFileName = @"DELETE FROM Books WHERE FileName = @FileName";

        #endregion
    }
}