/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Database schema and SQL queries for SQLite with FTS5 support
 * OPTIMIZED: Added performance-critical indexes for large databases
 * ENHANCED: Added library statistics persistence table
 * ENHANCED: Normalized sequences into separate table for better performance
 *
 */

namespace TinyOPDS.Data
{
    public static class DatabaseSchema
    {
        #region Create Table Scripts

        // MODIFIED: Removed Sequence and NumberInSequence fields
        public const string CreateBooksTable = @"
            CREATE TABLE IF NOT EXISTS Books (
                ID TEXT PRIMARY KEY,
                Version REAL NOT NULL DEFAULT 1.0,
                FileName TEXT NOT NULL UNIQUE,
                Title TEXT NOT NULL,
                Language TEXT,
                BookDate INTEGER,       -- DateTime as ticks
                DocumentDate INTEGER,   -- DateTime as ticks  
                Annotation TEXT,
                DocumentSize INTEGER NOT NULL DEFAULT 0,
                AddedDate INTEGER NOT NULL, -- DateTime as ticks
                DocumentIDTrusted INTEGER DEFAULT 0, -- Is the FB2 ID trusted (from FictionBookEditor)
                DuplicateKey TEXT,      -- Hash for duplicate detection
                ReplacedByID TEXT,      -- If this book was replaced by newer version
                ContentHash TEXT        -- Hash of file content for exact duplicate detection
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
                Tag TEXT PRIMARY KEY,
                ParentName TEXT,        -- Parent genre name (e.g. 'Science Fiction')
                Name TEXT NOT NULL,     -- English name
                Translation TEXT        -- Russian translation
            )";

        public const string CreateTranslatorsTable = @"
            CREATE TABLE IF NOT EXISTS Translators (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE
            )";

        // NEW: Sequences table
        public const string CreateSequencesTable = @"
            CREATE TABLE IF NOT EXISTS Sequences (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                SearchName TEXT                 -- Normalized for search (lowercase)
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
                FOREIGN KEY (BookID) REFERENCES Books(ID) ON DELETE CASCADE,
                FOREIGN KEY (GenreTag) REFERENCES Genres(Tag) ON DELETE CASCADE
            )";

        public const string CreateBookTranslatorsTable = @"
            CREATE TABLE IF NOT EXISTS BookTranslators (
                BookID TEXT NOT NULL,
                TranslatorID INTEGER NOT NULL,
                PRIMARY KEY (BookID, TranslatorID),
                FOREIGN KEY (BookID) REFERENCES Books(ID) ON DELETE CASCADE,
                FOREIGN KEY (TranslatorID) REFERENCES Translators(ID) ON DELETE CASCADE
            )";

        // NEW: Book-Sequences relationship table
        public const string CreateBookSequencesTable = @"
            CREATE TABLE IF NOT EXISTS BookSequences (
                BookID TEXT NOT NULL,
                SequenceID INTEGER NOT NULL,
                NumberInSequence INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (BookID, SequenceID),
                FOREIGN KEY (BookID) REFERENCES Books(ID) ON DELETE CASCADE,
                FOREIGN KEY (SequenceID) REFERENCES Sequences(ID) ON DELETE CASCADE
            )";

        // Library statistics persistence table
        public const string CreateLibraryStatsTable = @"
            CREATE TABLE IF NOT EXISTS LibraryStats (
                key TEXT PRIMARY KEY,
                value INTEGER NOT NULL DEFAULT 0,
                updated_at INTEGER NOT NULL DEFAULT 0,  -- DateTime as ticks
                period_days INTEGER DEFAULT NULL        -- For new_books_count - which period was used
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

        // NEW: FTS5 table for sequences
        public const string CreateSequencesFTSTable = @"
            CREATE VIRTUAL TABLE IF NOT EXISTS SequencesFTS 
            USING fts5(
                SequenceID UNINDEXED,
                Name,
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

        public const string CreateGenreStatisticsView = @"
            CREATE VIEW IF NOT EXISTS GenreStatistics AS
            SELECT 
                g.Tag,
                g.ParentName,
                g.Name,
                g.Translation,
                COUNT(bg.BookID) as BookCount
            FROM Genres g
            LEFT JOIN BookGenres bg ON g.Tag = bg.GenreTag
            GROUP BY g.Tag, g.ParentName, g.Name, g.Translation";

        // NEW: Sequences statistics view
        public const string CreateSequenceStatisticsView = @"
            CREATE VIEW IF NOT EXISTS SequenceStatistics AS
            SELECT 
                s.ID,
                s.Name,
                COUNT(bs.BookID) as BookCount
            FROM Sequences s
            LEFT JOIN BookSequences bs ON s.ID = bs.SequenceID
            GROUP BY s.ID, s.Name";

        #endregion

        #region Indexes - OPTIMIZED

        public const string CreateIndexes = @"
            -- Basic indexes on Books table
            CREATE INDEX IF NOT EXISTS idx_books_filename ON Books(FileName);
            CREATE INDEX IF NOT EXISTS idx_books_title ON Books(Title);
            CREATE INDEX IF NOT EXISTS idx_books_addeddate ON Books(AddedDate);
            CREATE INDEX IF NOT EXISTS idx_books_duplicatekey ON Books(DuplicateKey);
            CREATE INDEX IF NOT EXISTS idx_books_replacedby ON Books(ReplacedByID);
            CREATE INDEX IF NOT EXISTS idx_books_trusted_id ON Books(ID, DocumentIDTrusted) WHERE DocumentIDTrusted = 1;
            
            -- OPTIMIZED: Composite index for active books (most queries filter by ReplacedByID IS NULL)
            CREATE INDEX IF NOT EXISTS idx_books_active_composite ON Books(ReplacedByID, AddedDate DESC) WHERE ReplacedByID IS NULL;
            
            -- OPTIMIZED: Index for counting FB2 and EPUB books
            CREATE INDEX IF NOT EXISTS idx_books_filename_replaced ON Books(FileName, ReplacedByID) WHERE ReplacedByID IS NULL;
            
            -- Authors indexes
            CREATE INDEX IF NOT EXISTS idx_authors_name ON Authors(Name);
            CREATE INDEX IF NOT EXISTS idx_authors_lastname ON Authors(LastName);
            CREATE INDEX IF NOT EXISTS idx_authors_searchname ON Authors(SearchName);
            CREATE INDEX IF NOT EXISTS idx_authors_soundex ON Authors(LastNameSoundex);
            CREATE INDEX IF NOT EXISTS idx_authors_translit ON Authors(NameTranslit);
            
            -- Genres indexes  
            CREATE INDEX IF NOT EXISTS idx_genres_parentname ON Genres(ParentName);
            CREATE INDEX IF NOT EXISTS idx_genres_name ON Genres(Name);
            
            -- OPTIMIZED: Critical indexes for join operations
            CREATE INDEX IF NOT EXISTS idx_bookauthors_bookid ON BookAuthors(BookID);
            CREATE INDEX IF NOT EXISTS idx_bookauthors_authorid ON BookAuthors(AuthorID);
            CREATE INDEX IF NOT EXISTS idx_bookauthors_composite ON BookAuthors(AuthorID, BookID);
            
            CREATE INDEX IF NOT EXISTS idx_bookgenres_bookid ON BookGenres(BookID);
            CREATE INDEX IF NOT EXISTS idx_bookgenres_genretag ON BookGenres(GenreTag);
            CREATE INDEX IF NOT EXISTS idx_bookgenres_composite ON BookGenres(GenreTag, BookID);
            
            -- NEW: Sequences indexes
            CREATE INDEX IF NOT EXISTS idx_sequences_name ON Sequences(Name);
            CREATE INDEX IF NOT EXISTS idx_sequences_searchname ON Sequences(SearchName);
            
            -- NEW: BookSequences indexes for efficient joins
            CREATE INDEX IF NOT EXISTS idx_booksequences_bookid ON BookSequences(BookID);
            CREATE INDEX IF NOT EXISTS idx_booksequences_sequenceid ON BookSequences(SequenceID);
            CREATE INDEX IF NOT EXISTS idx_booksequences_composite ON BookSequences(SequenceID, BookID);
            CREATE INDEX IF NOT EXISTS idx_booksequences_number ON BookSequences(SequenceID, NumberInSequence);

            -- NEW: Index for LibraryStats table
            CREATE INDEX IF NOT EXISTS idx_librarystats_updated ON LibraryStats(updated_at);
        ";

        // Additional optimization commands to run after indexes are created
        public const string OptimizeDatabase = @"
            -- Update SQLite statistics for query planner
            ANALYZE;
            
            -- Set optimal pragmas for read-heavy workload
            PRAGMA cache_size = 10000;
            PRAGMA temp_store = MEMORY;
            PRAGMA mmap_size = 268435456;  -- 256MB memory map
            PRAGMA page_size = 4096;
            PRAGMA synchronous = NORMAL;
            PRAGMA journal_mode = WAL;
            PRAGMA wal_autocheckpoint = 1000;
        ";

        #endregion

        #region Triggers for FTS Synchronization

        // Books triggers
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

        // Authors triggers
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

        // NEW: Sequences triggers for FTS synchronization
        public const string CreateSequenceInsertTrigger = @"
            CREATE TRIGGER IF NOT EXISTS sequences_ai AFTER INSERT ON Sequences BEGIN
                INSERT INTO SequencesFTS(SequenceID, Name) 
                VALUES (new.ID, new.Name);
            END";

        public const string CreateSequenceUpdateTrigger = @"
            CREATE TRIGGER IF NOT EXISTS sequences_au AFTER UPDATE ON Sequences BEGIN
                UPDATE SequencesFTS 
                SET Name = new.Name
                WHERE SequenceID = new.ID;
            END";

        public const string CreateSequenceDeleteTrigger = @"
            CREATE TRIGGER IF NOT EXISTS sequences_ad AFTER DELETE ON Sequences BEGIN
                DELETE FROM SequencesFTS WHERE SequenceID = old.ID;
            END";

        #endregion

        #region Insert Queries

        // MODIFIED: Removed Sequence and NumberInSequence from book insert
        public const string InsertBook = @"
            INSERT OR REPLACE INTO Books 
            (ID, Version, FileName, Title, Language, BookDate, DocumentDate, 
             Annotation, DocumentSize, AddedDate, DocumentIDTrusted, 
             DuplicateKey, ReplacedByID, ContentHash)
            VALUES 
            (@ID, @Version, @FileName, @Title, @Language, @BookDate, @DocumentDate,
             @Annotation, @DocumentSize, @AddedDate, @DocumentIDTrusted, 
             @DuplicateKey, @ReplacedByID, @ContentHash)";

        public const string InsertAuthor = @"
            INSERT OR IGNORE INTO Authors 
            (Name, FirstName, MiddleName, LastName, SearchName, LastNameSoundex, NameTranslit) 
            VALUES 
            (@Name, @FirstName, @MiddleName, @LastName, @SearchName, @LastNameSoundex, @NameTranslit)";

        public const string InsertGenre = @"
            INSERT OR REPLACE INTO Genres (Tag, ParentName, Name, Translation) 
            VALUES (@Tag, @ParentName, @Name, @Translation)";

        public const string InsertTranslator = @"
            INSERT OR IGNORE INTO Translators (Name) VALUES (@Name)";

        // NEW: Insert sequence
        public const string InsertSequence = @"
            INSERT OR IGNORE INTO Sequences (Name, SearchName) 
            VALUES (@Name, @SearchName)";

        public const string InsertBookAuthor = @"
            INSERT OR IGNORE INTO BookAuthors (BookID, AuthorID) 
            VALUES (@BookID, (SELECT ID FROM Authors WHERE Name = @AuthorName))";

        public const string InsertBookGenre = @"
            INSERT OR IGNORE INTO BookGenres (BookID, GenreTag) VALUES (@BookID, @GenreTag)";

        public const string InsertBookTranslator = @"
            INSERT OR IGNORE INTO BookTranslators (BookID, TranslatorID) 
            VALUES (@BookID, (SELECT ID FROM Translators WHERE Name = @TranslatorName))";

        // NEW: Insert book-sequence relationship
        public const string InsertBookSequence = @"
            INSERT OR IGNORE INTO BookSequences (BookID, SequenceID, NumberInSequence) 
            VALUES (@BookID, (SELECT ID FROM Sequences WHERE Name = @SequenceName), @NumberInSequence)";

        #endregion

        #region Library Statistics Queries

        public const string UpsertLibraryStats = @"
            INSERT OR REPLACE INTO LibraryStats (key, value, updated_at, period_days) 
            VALUES (@Key, @Value, @UpdatedAt, @PeriodDays)";

        public const string SelectLibraryStats = @"
            SELECT key, value, updated_at, period_days 
            FROM LibraryStats 
            WHERE key = @Key";

        public const string SelectAllLibraryStats = @"
            SELECT key, value, updated_at, period_days 
            FROM LibraryStats 
            ORDER BY key";

        public const string InitializeLibraryStats = @"
            INSERT OR IGNORE INTO LibraryStats (key, value, updated_at, period_days) VALUES
            ('total_books', 0, 0, NULL),
            ('fb2_books', 0, 0, NULL),
            ('epub_books', 0, 0, NULL),
            ('authors_count', 0, 0, NULL),
            ('sequences_count', 0, 0, NULL),
            ('new_books', 0, 0, 7)";

        public const string CheckLibraryStatsExist = @"
            SELECT COUNT(*) FROM LibraryStats WHERE key IN 
            ('total_books', 'fb2_books', 'epub_books', 'authors_count', 'sequences_count', 'new_books')";

        #endregion

        #region Select Queries - Books

        // MODIFIED: Removed Sequence and NumberInSequence from select
        public const string SelectAllBooks = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Annotation, DocumentSize, AddedDate, DocumentIDTrusted, 
                   DuplicateKey, ReplacedByID, ContentHash
            FROM Books
            WHERE ReplacedByID IS NULL";

        public const string SelectBookById = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Annotation, DocumentSize, AddedDate, DocumentIDTrusted, 
                   DuplicateKey, ReplacedByID, ContentHash
            FROM Books WHERE ID = @ID";

        public const string SelectBookByFileName = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Annotation, DocumentSize, AddedDate, DocumentIDTrusted, 
                   DuplicateKey, ReplacedByID, ContentHash
            FROM Books WHERE FileName = @FileName";

        public const string SelectBooksByAuthor = @"
            SELECT b.ID, b.Version, b.FileName, b.Title, b.Language, b.BookDate, b.DocumentDate,
                   b.Annotation, b.DocumentSize, b.AddedDate, b.DocumentIDTrusted, 
                   b.DuplicateKey, b.ReplacedByID, b.ContentHash
            FROM Books b
            INNER JOIN BookAuthors ba ON b.ID = ba.BookID
            INNER JOIN Authors a ON ba.AuthorID = a.ID
            WHERE a.Name = @AuthorName AND b.ReplacedByID IS NULL";

        // NEW: Optimized query for books by sequence
        public const string SelectBooksBySequence = @"
            SELECT b.ID, b.Version, b.FileName, b.Title, b.Language, b.BookDate, b.DocumentDate,
                   b.Annotation, b.DocumentSize, b.AddedDate, b.DocumentIDTrusted, 
                   b.DuplicateKey, b.ReplacedByID, b.ContentHash,
                   bs.NumberInSequence
            FROM Books b
            INNER JOIN BookSequences bs ON b.ID = bs.BookID
            INNER JOIN Sequences s ON bs.SequenceID = s.ID
            WHERE s.Name = @SequenceName AND b.ReplacedByID IS NULL
            ORDER BY bs.NumberInSequence";

        public const string SelectBooksByGenre = @"
            SELECT b.ID, b.Version, b.FileName, b.Title, b.Language, b.BookDate, b.DocumentDate,
                   b.Annotation, b.DocumentSize, b.AddedDate, b.DocumentIDTrusted, 
                   b.DuplicateKey, b.ReplacedByID, b.ContentHash
            FROM Books b
            INNER JOIN BookGenres bg ON b.ID = bg.BookID
            WHERE bg.GenreTag = @GenreTag AND b.ReplacedByID IS NULL";

        // FTS5 search for books with wildcard support
        public const string SelectBooksByTitleFTS = @"
            SELECT DISTINCT b.ID, b.Version, b.FileName, b.Title, b.Language, b.BookDate, b.DocumentDate,
                   b.Annotation, b.DocumentSize, b.AddedDate
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
                   Annotation, DocumentSize, AddedDate
            FROM Books 
            WHERE Title LIKE '%' || @Title || '%' COLLATE NOCASE
            ORDER BY Title";

        public const string SelectNewBooks = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Annotation, DocumentSize, AddedDate
            FROM Books 
            WHERE AddedDate >= @FromDate";

        #endregion

        #region Select Queries - Sequences

        // NEW: Get all sequences
        public const string SelectSequences = @"
            SELECT DISTINCT s.Name
            FROM Sequences s
            INNER JOIN BookSequences bs ON s.ID = bs.SequenceID
            ORDER BY s.Name";

        // NEW: Get sequences by prefix (for navigation)
        public const string SelectSequencesByPrefix = @"
            SELECT DISTINCT s.Name
            FROM Sequences s
            INNER JOIN BookSequences bs ON s.ID = bs.SequenceID
            WHERE s.Name LIKE @Pattern || '%' COLLATE NOCASE
            ORDER BY s.Name";

        // NEW: Get sequences with book count
        public const string SelectSequencesWithCount = @"
            SELECT s.Name, COUNT(bs.BookID) as BookCount
            FROM Sequences s
            INNER JOIN BookSequences bs ON s.ID = bs.SequenceID
            INNER JOIN Books b ON bs.BookID = b.ID
            WHERE b.ReplacedByID IS NULL
            GROUP BY s.ID, s.Name
            ORDER BY s.Name";

        // NEW: Get sequences with count by prefix
        public const string SelectSequencesWithCountByPrefix = @"
            SELECT s.Name, COUNT(bs.BookID) as BookCount
            FROM Sequences s
            INNER JOIN BookSequences bs ON s.ID = bs.SequenceID
            INNER JOIN Books b ON bs.BookID = b.ID
            WHERE s.Name LIKE @Pattern || '%' COLLATE NOCASE
              AND b.ReplacedByID IS NULL
            GROUP BY s.ID, s.Name
            ORDER BY s.Name";

        // NEW: FTS search for sequences
        public const string SelectSequencesFTS = @"
            SELECT DISTINCT s.Name
            FROM Sequences s
            INNER JOIN SequencesFTS fts ON s.ID = fts.SequenceID
            INNER JOIN BookSequences bs ON s.ID = bs.SequenceID
            WHERE SequencesFTS MATCH @SearchPattern
            ORDER BY s.Name";

        #endregion

        #region Select Queries - Authors

        public const string SelectAuthors = @"
            SELECT DISTINCT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            ORDER BY a.Name";

        public const string SelectAuthorByExactName = @"
            SELECT DISTINCT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            WHERE a.Name = @Name COLLATE NOCASE";

        public const string SelectAuthorByExactComponents = @"
            SELECT DISTINCT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            WHERE (a.FirstName = @FirstName COLLATE NOCASE AND a.LastName = @LastName COLLATE NOCASE)
               OR (a.FirstName = @LastName COLLATE NOCASE AND a.LastName = @FirstName COLLATE NOCASE)";

        public const string SelectAuthorsByPartialName = @"
            SELECT DISTINCT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            WHERE a.LastName LIKE '%' || @Pattern || '%' COLLATE NOCASE
               OR a.FirstName LIKE '%' || @Pattern || '%' COLLATE NOCASE
            ORDER BY a.Name";

        public const string SelectAuthorsByTranslit = @"
            SELECT DISTINCT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            WHERE a.NameTranslit LIKE '%' || @Pattern || '%' COLLATE NOCASE
            ORDER BY a.Name";

        public const string SelectAuthorsBySoundex = @"
            SELECT DISTINCT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            WHERE a.LastNameSoundex = @Soundex
            ORDER BY a.Name";

        public const string SelectAuthorsByPrefix = @"
            SELECT DISTINCT a.Name
            FROM Authors a
            INNER JOIN BookAuthors ba ON a.ID = ba.AuthorID
            WHERE a.Name LIKE @Pattern || '%' COLLATE NOCASE
            ORDER BY a.Name";

        #endregion

        #region Select Queries - Genres

        public const string SelectAllGenres = @"
            SELECT Tag, ParentName, Name, Translation
            FROM Genres
            ORDER BY ParentName, Name";

        public const string SelectGenreByTag = @"
            SELECT Tag, ParentName, Name, Translation
            FROM Genres
            WHERE Tag = @Tag";

        public const string SelectGenresByParent = @"
            SELECT Tag, ParentName, Name, Translation
            FROM Genres
            WHERE ParentName = @ParentName
            ORDER BY Name";

        public const string SelectGenresWithBookCount = @"
            SELECT g.Tag, g.ParentName, g.Name, g.Translation, COUNT(bg.BookID) as BookCount
            FROM Genres g
            LEFT JOIN BookGenres bg ON g.Tag = bg.GenreTag
            GROUP BY g.Tag, g.ParentName, g.Name, g.Translation
            HAVING BookCount > 0
            ORDER BY g.ParentName, g.Name";

        public const string SelectParentGenres = @"
            SELECT DISTINCT ParentName
            FROM Genres
            WHERE ParentName IS NOT NULL
            ORDER BY ParentName";

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

        // NEW: Select book sequences
        public const string SelectBookSequences = @"
            SELECT s.Name, bs.NumberInSequence
            FROM Sequences s
            INNER JOIN BookSequences bs ON s.ID = bs.SequenceID
            WHERE bs.BookID = @BookID
            ORDER BY s.Name";

        #endregion

        #region Select Queries - Other

        public const string SelectGenreTags = @"
            SELECT DISTINCT GenreTag
            FROM BookGenres
            ORDER BY GenreTag";

        #endregion

        #region Count Queries - OPTIMIZED

        public const string CountBooks = @"SELECT COUNT(*) FROM Books WHERE ReplacedByID IS NULL";

        public const string CountFB2Books = @"SELECT COUNT(*) FROM Books WHERE FileName LIKE '%.fb2%' AND ReplacedByID IS NULL";

        public const string CountEPUBBooks = @"SELECT COUNT(*) FROM Books WHERE FileName LIKE '%.epub%' AND ReplacedByID IS NULL";

        public const string CountNewBooks = @"SELECT COUNT(*) FROM Books WHERE AddedDate >= @FromDate AND ReplacedByID IS NULL";

        public const string CountBooksByGenre = @"
            SELECT COUNT(*) FROM Books b 
            INNER JOIN BookGenres bg ON b.ID = bg.BookID 
            WHERE bg.GenreTag = @GenreTag AND b.ReplacedByID IS NULL";

        public const string CountGenres = @"SELECT COUNT(*) FROM Genres";

        public const string CountGenresInUse = @"
            SELECT COUNT(DISTINCT bg.GenreTag) 
            FROM BookGenres bg
            INNER JOIN Books b ON bg.BookID = b.ID
            WHERE b.ReplacedByID IS NULL";

        public const string SelectGenreStatistics = @"
            SELECT bg.GenreTag, COUNT(*) as BookCount 
            FROM BookGenres bg 
            INNER JOIN Books b ON bg.BookID = b.ID
            WHERE b.ReplacedByID IS NULL
            GROUP BY bg.GenreTag
            ORDER BY bg.GenreTag";

        public const string SelectGenreStatisticsFull = @"
            SELECT g.Tag, g.ParentName, g.Name, g.Translation, 
                   COUNT(bg.BookID) as BookCount
            FROM Genres g
            LEFT JOIN BookGenres bg ON g.Tag = bg.GenreTag
            LEFT JOIN Books b ON bg.BookID = b.ID AND b.ReplacedByID IS NULL
            GROUP BY g.Tag, g.ParentName, g.Name, g.Translation
            ORDER BY g.ParentName, g.Name";

        public const string SelectAuthorsCount = @"
            SELECT COUNT(DISTINCT a.ID) FROM Authors a
            WHERE EXISTS (
                SELECT 1 FROM BookAuthors ba 
                INNER JOIN Books b ON ba.BookID = b.ID 
                WHERE ba.AuthorID = a.ID AND b.ReplacedByID IS NULL
            )";

        // NEW: Optimized sequences count using new table
        public const string SelectSequencesCount = @"
            SELECT COUNT(DISTINCT s.ID) FROM Sequences s
            WHERE EXISTS (
                SELECT 1 FROM BookSequences bs 
                INNER JOIN Books b ON bs.BookID = b.ID 
                WHERE bs.SequenceID = s.ID AND b.ReplacedByID IS NULL
            )";

        // NEW: Count books in sequence
        public const string CountBooksBySequence = @"
            SELECT COUNT(*) FROM Books b
            INNER JOIN BookSequences bs ON b.ID = bs.BookID
            INNER JOIN Sequences s ON bs.SequenceID = s.ID
            WHERE s.Name = @SequenceName AND b.ReplacedByID IS NULL";

        public const string SelectAuthorBookCount = @"
            SELECT COUNT(*) FROM Books b
            INNER JOIN BookAuthors ba ON b.ID = ba.BookID
            INNER JOIN Authors a ON ba.AuthorID = a.ID
            WHERE a.Name = @AuthorName AND b.ReplacedByID IS NULL";

        #endregion

        #region Pagination Queries

        public const string SelectNewBooksPaginatedByDate = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Annotation, DocumentSize, AddedDate
            FROM Books 
            WHERE AddedDate >= @FromDate AND ReplacedByID IS NULL
            ORDER BY AddedDate DESC
            LIMIT @Limit OFFSET @Offset";

        public const string SelectNewBooksPaginatedByTitle = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Annotation, DocumentSize, AddedDate
            FROM Books 
            WHERE AddedDate >= @FromDate AND ReplacedByID IS NULL
            ORDER BY Title COLLATE NOCASE
            LIMIT @Limit OFFSET @Offset";

        #endregion

        #region Delete Queries

        public const string DeleteBook = @"DELETE FROM Books WHERE ID = @ID";

        public const string DeleteBookByFileName = @"DELETE FROM Books WHERE FileName = @FileName";

        public const string DeleteAllGenres = @"DELETE FROM Genres";

        #endregion

        #region FTS5 Maintenance

        public const string RebuildBooksFTS = @"INSERT INTO BooksFTS(BooksFTS) VALUES('rebuild')";

        public const string OptimizeBooksFTS = @"INSERT INTO BooksFTS(BooksFTS) VALUES('optimize')";

        public const string RebuildAuthorsFTS = @"INSERT INTO AuthorsFTS(AuthorsFTS) VALUES('rebuild')";

        public const string OptimizeAuthorsFTS = @"INSERT INTO AuthorsFTS(AuthorsFTS) VALUES('optimize')";

        // NEW: Sequences FTS maintenance
        public const string RebuildSequencesFTS = @"INSERT INTO SequencesFTS(SequencesFTS) VALUES('rebuild')";

        public const string OptimizeSequencesFTS = @"INSERT INTO SequencesFTS(SequencesFTS) VALUES('optimize')";

        #endregion

        #region Duplicate Detection Queries

        public const string SelectBookByTrustedID = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Annotation, DocumentSize, AddedDate, DocumentIDTrusted, 
                   DuplicateKey, ReplacedByID, ContentHash
            FROM Books 
            WHERE ID = @ID AND DocumentIDTrusted = 1 AND ReplacedByID IS NULL";

        public const string SelectBooksByDuplicateKey = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Annotation, DocumentSize, AddedDate, DocumentIDTrusted, 
                   DuplicateKey, ReplacedByID, ContentHash
            FROM Books 
            WHERE DuplicateKey = @DuplicateKey AND ReplacedByID IS NULL
            ORDER BY DocumentDate DESC, Version DESC, DocumentSize DESC";

        public const string SelectBookByContentHash = @"
            SELECT ID, Version, FileName, Title, Language, BookDate, DocumentDate,
                   Annotation, DocumentSize, AddedDate, DocumentIDTrusted, 
                   DuplicateKey, ReplacedByID, ContentHash
            FROM Books 
            WHERE ContentHash = @ContentHash AND ReplacedByID IS NULL";

        public const string SelectReplacedBooks = @"
            SELECT ID, FileName, Title, ReplacedByID, AddedDate
            FROM Books 
            WHERE ReplacedByID IS NOT NULL
            ORDER BY AddedDate DESC";

        public const string CheckGenresTablePopulated = @"SELECT COUNT(*) FROM Genres";

        public const string ValidateBookGenres = @"
            SELECT DISTINCT bg.GenreTag
            FROM BookGenres bg
            LEFT JOIN Genres g ON bg.GenreTag = g.Tag
            WHERE g.Tag IS NULL";

        #endregion

        #region Update Queries

        public const string UpdateBookAsReplaced = @"
            UPDATE Books 
            SET ReplacedByID = @NewID 
            WHERE ID = @OldID";

        #endregion

        // Optimized query to get books with authors and translators in single query
        public const string SelectBooksWithDetailsByDuplicateKey = @"
            SELECT 
                b.ID, b.Version, b.FileName, b.Title, b.Language, b.BookDate, b.DocumentDate,
                b.Annotation, b.DocumentSize, b.AddedDate, b.DocumentIDTrusted, 
                b.DuplicateKey, b.ReplacedByID, b.ContentHash,
                'AUTHOR' as ItemType,
                a.Name as ItemName
            FROM Books b
            LEFT JOIN BookAuthors ba ON b.ID = ba.BookID
            LEFT JOIN Authors a ON ba.AuthorID = a.ID
            WHERE b.DuplicateKey = @DuplicateKey AND b.ReplacedByID IS NULL
    
            UNION ALL
    
            SELECT 
                b.ID, b.Version, b.FileName, b.Title, b.Language, b.BookDate, b.DocumentDate,
                b.Annotation, b.DocumentSize, b.AddedDate, b.DocumentIDTrusted, 
                b.DuplicateKey, b.ReplacedByID, b.ContentHash,
                'TRANSLATOR' as ItemType,
                t.Name as ItemName
            FROM Books b
            LEFT JOIN BookTranslators bt ON b.ID = bt.BookID
            LEFT JOIN Translators t ON bt.TranslatorID = t.ID
            WHERE b.DuplicateKey = @DuplicateKey AND b.ReplacedByID IS NULL
    
            ORDER BY b.ID, ItemType";
    }
}