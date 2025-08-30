/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Database schema and SQL queries for SQLite with FTS5 support
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
                BookDate INTEGER,       -- DateTime as ticks
                DocumentDate INTEGER,   -- DateTime as ticks  
                Sequence TEXT,
                NumberInSequence INTEGER NOT NULL DEFAULT 0,
                Annotation TEXT,
                DocumentSize INTEGER NOT NULL DEFAULT 0,
                AddedDate INTEGER NOT NULL -- DateTime as ticks
            )";

        public const string CreateAuthorsTable = @"
            CREATE TABLE IF NOT EXISTS Authors (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,     -- Full canonical name
                FirstName TEXT,
                MiddleName TEXT,
                LastName TEXT,
                SearchName TEXT,                -- Normalized for search (lowercase, no punctuation)
                LastNameSoundex TEXT,
                NameTranslit TEXT               -- Transliterated name for Latin->Cyrillic search
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

        // FTS5 tables for full-text search
        public const string CreateBooksFTSTable = @"
            CREATE VIRTUAL TABLE IF NOT EXISTS BooksFTS 
            USING fts5(
                BookID UNINDEXED,
                Title,
                Annotation,
                tokenize='unicode61 remove_diacritics 1'
            )";

        public const string CreateAuthorsFTSTable = @"
            CREATE VIRTUAL TABLE IF NOT EXISTS AuthorsFTS 
            USING fts5(
                AuthorID UNINDEXED,
                FullName,           -- 'FirstName LastName'
                ReversedName,       -- 'LastName FirstName'
                LastName,
                tokenize='unicode61 remove_diacritics 1'
            )";

        #endregion

        #region Views

        public const string CreateAuthorStatisticsView = @"
            CREATE VIEW IF NOT EXISTS AuthorStatistics AS
            SELECT 
                a.ID,
                a.Name,
                COUNT(ba.BookID) as BookCount
            FROM Authors a
            LEFT JOIN BookAuthors ba ON a.ID = ba.AuthorID
            GROUP BY a.ID, a.Name";

        #endregion

        #region Indexes

        public const string CreateIndexes = @"
            CREATE INDEX IF NOT EXISTS idx_books_filename ON Books(FileName);
            CREATE INDEX IF NOT EXISTS idx_books_title ON Books(Title);
            CREATE INDEX IF NOT EXISTS idx_books_sequence ON Books(Sequence);
            CREATE INDEX IF NOT EXISTS idx_books_addeddate ON Books(AddedDate);
            
            CREATE INDEX IF NOT EXISTS idx_authors_name ON Authors(Name);
            CREATE INDEX IF NOT EXISTS idx_authors_lastname ON Authors(LastName);
            CREATE INDEX IF NOT EXISTS idx_authors_searchname ON Authors(SearchName);
            CREATE INDEX IF NOT EXISTS idx_authors_soundex ON Authors(LastNameSoundex);
            CREATE INDEX IF NOT EXISTS idx_authors_translit ON Authors(NameTranslit);
            
            CREATE INDEX IF NOT EXISTS idx_bookauthors_composite ON BookAuthors(AuthorID, BookID);
            CREATE INDEX IF NOT EXISTS idx_bookgenres_composite ON BookGenres(GenreTag, BookID);
        ";

        #endregion

        #region Triggers for FTS Synchronization

        // Books triggers - separate constants for proper execution
        public const string CreateBookInsertTrigger = @"
            CREATE TRIGGER IF NOT EXISTS books_ai AFTER INSERT ON Books BEGIN
                INSERT INTO BooksFTS(BookID, Title, Annotation) 
                VALUES (new.ID, new.Title, new.Annotation);
            END";

        public const string CreateBookUpdateTrigger = @"
            CREATE TRIGGER IF NOT EXISTS books_au AFTER UPDATE ON Books BEGIN
                UPDATE BooksFTS 
                SET Title = new.Title, Annotation = new.Annotation 
                WHERE BookID = new.ID;
            END";

        public const string CreateBookDeleteTrigger = @"
            CREATE TRIGGER IF NOT EXISTS books_ad AFTER DELETE ON Books BEGIN
                DELETE FROM BooksFTS WHERE BookID = old.ID;
            END";

        // Authors triggers - separate constants for proper execution
        public const string CreateAuthorInsertTrigger = @"
            CREATE TRIGGER IF NOT EXISTS authors_ai AFTER INSERT ON Authors BEGIN
                INSERT INTO AuthorsFTS(AuthorID, FullName, ReversedName, LastName) 
                VALUES (
                    new.ID, 
                    CASE 
                        WHEN new.FirstName IS NOT NULL AND new.LastName IS NOT NULL 
                        THEN new.FirstName || ' ' || new.LastName
                        ELSE new.Name
                    END,
                    CASE 
                        WHEN new.FirstName IS NOT NULL AND new.LastName IS NOT NULL 
                        THEN new.LastName || ' ' || new.FirstName
                        ELSE new.Name
                    END,
                    COALESCE(new.LastName, new.Name)
                );
            END";

        public const string CreateAuthorUpdateTrigger = @"
            CREATE TRIGGER IF NOT EXISTS authors_au AFTER UPDATE ON Authors BEGIN
                UPDATE AuthorsFTS 
                SET FullName = CASE 
                        WHEN new.FirstName IS NOT NULL AND new.LastName IS NOT NULL 
                        THEN new.FirstName || ' ' || new.LastName
                        ELSE new.Name
                    END,
                    ReversedName = CASE 
                        WHEN new.FirstName IS NOT NULL AND new.LastName IS NOT NULL 
                        THEN new.LastName || ' ' || new.FirstName
                        ELSE new.Name
                    END,
                    LastName = COALESCE(new.LastName, new.Name)
                WHERE AuthorID = new.ID;
            END";

        public const string CreateAuthorDeleteTrigger = @"
            CREATE TRIGGER IF NOT EXISTS authors_ad AFTER DELETE ON Authors BEGIN
                DELETE FROM AuthorsFTS WHERE AuthorID = old.ID;
            END";

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
            INSERT OR IGNORE INTO Authors 
            (Name, FirstName, MiddleName, LastName, SearchName, LastNameSoundex, NameTranslit) 
            VALUES 
            (@Name, @FirstName, @MiddleName, @LastName, @SearchName, @LastNameSoundex, @NameTranslit)";

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

        #region Select Queries - Books

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

        // FTS5 search for books with wildcard support for partial matches
        public const string SelectBooksByTitleFTS = @"
            SELECT DISTINCT b.ID, b.Version, b.FileName, b.Title, b.Language, b.BookDate, b.DocumentDate,
                   b.Sequence, b.NumberInSequence, b.Annotation, b.DocumentSize, b.AddedDate
            FROM Books b
            INNER JOIN BooksFTS fts ON b.ID = fts.BookID
            WHERE BooksFTS MATCH @SearchPattern
            ORDER BY 
                CASE WHEN b.Title LIKE @LikePattern || '%' THEN 0 ELSE 1 END,
                bm25(BooksFTS),
                b.Title";

        // Fallback LIKE search for books
        public const string SelectBooksByTitleLike = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Sequence, NumberInSequence, Annotation, DocumentSize, AddedDate
            FROM Books 
            WHERE Title LIKE '%' || @Title || '%' COLLATE NOCASE
            ORDER BY Title";

        public const string SelectNewBooks = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Sequence, NumberInSequence, Annotation, DocumentSize, AddedDate
            FROM Books 
            WHERE AddedDate >= @FromDate";

        #endregion

        #region Select Queries - Authors (Cascading search)

        // Get all authors with books
        public const string SelectAuthors = @"
            SELECT DISTINCT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            ORDER BY a.Name";

        // Step 1: Exact match for full name
        public const string SelectAuthorByExactName = @"
            SELECT DISTINCT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            WHERE a.Name = @Name COLLATE NOCASE";

        // Step 1: Exact match by FirstName + LastName components  
        public const string SelectAuthorByExactComponents = @"
            SELECT DISTINCT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            WHERE (a.FirstName = @FirstName COLLATE NOCASE AND a.LastName = @LastName COLLATE NOCASE)
               OR (a.FirstName = @LastName COLLATE NOCASE AND a.LastName = @FirstName COLLATE NOCASE)";

        // Step 2: Partial match by LastName or FirstName
        public const string SelectAuthorsByPartialName = @"
            SELECT DISTINCT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            WHERE a.LastName LIKE '%' || @Pattern || '%' COLLATE NOCASE
               OR a.FirstName LIKE '%' || @Pattern || '%' COLLATE NOCASE
            ORDER BY a.Name";

        // Step 3: Search by transliterated name
        public const string SelectAuthorsByTranslit = @"
            SELECT DISTINCT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            WHERE a.NameTranslit LIKE '%' || @Pattern || '%' COLLATE NOCASE
            ORDER BY a.Name";

        // Step 4: Soundex search
        public const string SelectAuthorsBySoundex = @"
            SELECT DISTINCT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            WHERE a.LastNameSoundex = @Soundex
            ORDER BY a.Name";

        // Navigation search (prefix match)
        public const string SelectAuthorsByPrefix = @"
            SELECT DISTINCT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            WHERE a.Name LIKE @Pattern || '%' COLLATE NOCASE
            ORDER BY a.Name";

        #endregion

        #region Select Queries - Relations

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

        #endregion

        #region Select Queries - Other

        public const string SelectSequences = @"
            SELECT DISTINCT Sequence
            FROM Books 
            WHERE Sequence IS NOT NULL AND Sequence != ''
            ORDER BY Sequence";

        public const string SelectGenreTags = @"
            SELECT DISTINCT GenreTag
            FROM BookGenres
            ORDER BY GenreTag";

        #endregion

        #region Count Queries

        public const string CountBooks = @"SELECT COUNT(*) FROM Books";

        public const string CountFB2Books = @"SELECT COUNT(*) FROM Books WHERE FileName LIKE '%.fb2%'";

        public const string CountEPUBBooks = @"SELECT COUNT(*) FROM Books WHERE FileName LIKE '%.epub%'";

        public const string CountNewBooks = @"SELECT COUNT(*) FROM Books WHERE AddedDate >= @FromDate";

        public const string CountBooksByGenre = @"
            SELECT COUNT(*) FROM Books b 
            INNER JOIN BookGenres bg ON b.ID = bg.BookID 
            WHERE bg.GenreTag = @GenreTag";

        public const string SelectGenreStatistics = @"
            SELECT bg.GenreTag, COUNT(*) as BookCount 
            FROM BookGenres bg 
            INNER JOIN Books b ON bg.BookID = b.ID
            GROUP BY bg.GenreTag
            ORDER BY bg.GenreTag";

        public const string SelectAuthorsCount = @"
            SELECT COUNT(DISTINCT a.ID) FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID";

        public const string SelectSequencesCount = @"
            SELECT COUNT(DISTINCT Sequence) FROM Books 
            WHERE Sequence IS NOT NULL AND Sequence != ''";

        // Use view for author book counts
        public const string SelectAuthorBookCount = @"
            SELECT BookCount FROM AuthorStatistics WHERE Name = @AuthorName";

        #endregion

        #region Pagination Queries

        public const string SelectNewBooksPaginatedByDate = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Sequence, NumberInSequence, Annotation, DocumentSize, AddedDate
            FROM Books 
            WHERE AddedDate >= @FromDate
            ORDER BY AddedDate DESC
            LIMIT @Limit OFFSET @Offset";

        public const string SelectNewBooksPaginatedByTitle = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Sequence, NumberInSequence, Annotation, DocumentSize, AddedDate
            FROM Books 
            WHERE AddedDate >= @FromDate
            ORDER BY Title COLLATE NOCASE
            LIMIT @Limit OFFSET @Offset";

        #endregion

        #region Delete Queries

        public const string DeleteBook = @"DELETE FROM Books WHERE ID = @ID";

        public const string DeleteBookByFileName = @"DELETE FROM Books WHERE FileName = @FileName";

        #endregion

        #region FTS5 Maintenance

        public const string RebuildBooksFTS = @"INSERT INTO BooksFTS(BooksFTS) VALUES('rebuild')";

        public const string OptimizeBooksFTS = @"INSERT INTO BooksFTS(BooksFTS) VALUES('optimize')";

        public const string RebuildAuthorsFTS = @"INSERT INTO AuthorsFTS(AuthorsFTS) VALUES('rebuild')";

        public const string OptimizeAuthorsFTS = @"INSERT INTO AuthorsFTS(AuthorsFTS) VALUES('optimize')";

        #endregion
    }
}