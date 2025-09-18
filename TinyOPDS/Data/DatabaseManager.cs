/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Database manager for SQLite operations with FTS5 support
 * ENHANCED: Added library statistics persistence support
 * ENHANCED: Added downloads tracking support
 * ENHANCED: Added performance optimizations and cold start prevention
 *
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;

namespace TinyOPDS.Data
{
    public class DatabaseManager : IDisposable
    {
        private IDbConnection connection;
        private readonly string connectionString;
        private bool disposed = false;

        // Performance optimization fields
        private Timer _keepAliveTimer;
        private DateTime _lastAccessTime = DateTime.Now;
        private readonly TimeSpan _idleThreshold = TimeSpan.FromMinutes(5);

        public DatabaseManager(string databasePath)
        {
            connectionString = $"Data Source={databasePath};Version=3;";

            // Create database file if it doesn't exist (only on Windows with System.Data.SQLite)
            if (!Utils.IsLinux && !File.Exists(databasePath))
            {
                try
                {
                    var sqliteType = Type.GetType("System.Data.SQLite.SQLiteConnection, System.Data.SQLite");
                    if (sqliteType != null)
                    {
                        var createFileMethod = sqliteType.GetMethod("CreateFile", new[] { typeof(string) });
                        createFileMethod?.Invoke(null, new object[] { databasePath });
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Warning, "Could not create SQLite file: {0}", ex.Message);
                }
            }

            connection = SqliteConnectionFactory.CreateConnection(connectionString);
            connection.Open();

            // Apply performance optimizations
            ApplyPerformanceOptimizations();

            InitializeSchema();

            // Start keep-alive timer to prevent cold starts
            StartKeepAliveTimer();
        }

        /// <summary>
        /// Apply SQLite performance optimizations
        /// </summary>
        private void ApplyPerformanceOptimizations()
        {
            try
            {
                // Enable foreign keys
                ExecuteNonQuery("PRAGMA foreign_keys = ON");

                // Performance optimizations
                ExecuteNonQuery("PRAGMA journal_mode = WAL");        // Write-Ahead Logging for better concurrency
                ExecuteNonQuery("PRAGMA synchronous = NORMAL");      // Faster writes with reasonable safety
                ExecuteNonQuery("PRAGMA cache_size = -64000");       // 64MB cache in RAM
                ExecuteNonQuery("PRAGMA temp_store = MEMORY");       // Temp tables in memory
                ExecuteNonQuery("PRAGMA mmap_size = 268435456");     // 256MB memory-mapped I/O
                ExecuteNonQuery("PRAGMA page_size = 4096");          // 4KB page size
                ExecuteNonQuery("PRAGMA busy_timeout = 10000");      // 10 seconds timeout for locked database

                Log.WriteLine("Applied SQLite performance optimizations");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Could not apply all performance optimizations: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Start keep-alive timer to prevent connection from going cold
        /// </summary>
        private void StartKeepAliveTimer()
        {
            _keepAliveTimer = new Timer(KeepConnectionAlive, null,
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Keep-alive callback to maintain warm connection
        /// </summary>
        private void KeepConnectionAlive(object state)
        {
            try
            {
                ExecuteScalar("SELECT 1");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Keep-alive query failed: {0}", ex.Message);
                // Try to reconnect if connection is broken
                TryReconnect();
            }
        }

        /// <summary>
        /// Try to reconnect if connection is broken
        /// </summary>
        private void TryReconnect()
        {
            try
            {
                if (connection?.State != ConnectionState.Open)
                {
                    connection?.Close();
                    connection?.Dispose();

                    connection = SqliteConnectionFactory.CreateConnection(connectionString);
                    connection.Open();
                    ApplyPerformanceOptimizations();

                    Log.WriteLine(LogLevel.Info, "Successfully reconnected to database");
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Failed to reconnect to database: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Warm up database caches if idle for too long
        /// </summary>
        public void WarmUpIfNeeded()
        {
            if (DateTime.Now - _lastAccessTime > _idleThreshold)
            {
                try
                {
                    // Warm up cache with lightweight queries
                    ExecuteScalar("SELECT COUNT(*) FROM Books LIMIT 1");
                    ExecuteScalar("SELECT COUNT(*) FROM Authors LIMIT 1");
                    ExecuteScalar("SELECT COUNT(*) FROM Genres LIMIT 1");

                    // Update SQLite statistics for query optimizer
                    ExecuteNonQuery("ANALYZE");

                    Log.WriteLine(LogLevel.Info, "Database cache warmed up after idle period");
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Warning, "Warm-up failed: {0}", ex.Message);
                }
            }
            _lastAccessTime = DateTime.Now;
        }

        private void InitializeSchema()
        {
            try
            {
                // Create tables in dependency order
                ExecuteNonQuery(DatabaseSchema.CreateBooksTable);
                ExecuteNonQuery(DatabaseSchema.CreateAuthorsTable);
                ExecuteNonQuery(DatabaseSchema.CreateGenresTable);
                ExecuteNonQuery(DatabaseSchema.CreateTranslatorsTable);
                ExecuteNonQuery(DatabaseSchema.CreateSequencesTable); // NEW: Create sequences table
                ExecuteNonQuery(DatabaseSchema.CreateDownloadsTable); // Create downloads table

                // Create relationship tables
                ExecuteNonQuery(DatabaseSchema.CreateBookAuthorsTable);
                ExecuteNonQuery(DatabaseSchema.CreateBookGenresTable);
                ExecuteNonQuery(DatabaseSchema.CreateBookTranslatorsTable);
                ExecuteNonQuery(DatabaseSchema.CreateBookSequencesTable); // NEW: Create book-sequences relationship table

                // Create library statistics table
                ExecuteNonQuery(DatabaseSchema.CreateLibraryStatsTable);

                // Create FTS5 tables
                ExecuteNonQuery(DatabaseSchema.CreateBooksFTSTable);
                ExecuteNonQuery(DatabaseSchema.CreateAuthorsFTSTable);
                ExecuteNonQuery(DatabaseSchema.CreateSequencesFTSTable); // NEW: Create sequences FTS table

                // Create views
                ExecuteNonQuery(DatabaseSchema.CreateAuthorStatisticsView);
                ExecuteNonQuery(DatabaseSchema.CreateGenreStatisticsView);
                ExecuteNonQuery(DatabaseSchema.CreateSequenceStatisticsView); // NEW: Create sequences statistics view

                // Create indexes
                ExecuteNonQuery(DatabaseSchema.CreateIndexes);

                // Create triggers for FTS synchronization
                // Books triggers
                ExecuteNonQuery(DatabaseSchema.CreateBookInsertTrigger);
                ExecuteNonQuery(DatabaseSchema.CreateBookUpdateTrigger);
                ExecuteNonQuery(DatabaseSchema.CreateBookDeleteTrigger);

                // Authors triggers
                ExecuteNonQuery(DatabaseSchema.CreateAuthorInsertTrigger);
                ExecuteNonQuery(DatabaseSchema.CreateAuthorUpdateTrigger);
                ExecuteNonQuery(DatabaseSchema.CreateAuthorDeleteTrigger);

                // NEW: Sequences triggers for FTS synchronization
                ExecuteNonQuery(DatabaseSchema.CreateSequenceInsertTrigger);
                ExecuteNonQuery(DatabaseSchema.CreateSequenceUpdateTrigger);
                ExecuteNonQuery(DatabaseSchema.CreateSequenceDeleteTrigger);

                // Initialize genres from XML if table is empty
                InitializeGenres();

                // Initialize library statistics if table is empty
                InitializeLibraryStats();

                Log.WriteLine("Database schema initialized with FTS5 support, triggers, genres, statistics and downloads tracking");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error initializing database schema: {0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Initialize genres from embedded XML resource
        /// Only adds new genres, never deletes existing ones to preserve BookGenres relationships
        /// Stores parent genre translations with _MAIN_ prefix
        /// </summary>
        private void InitializeGenres()
        {
            try
            {
                Log.WriteLine("Checking genres for updates from embedded XML resource...");

                // Load genres from embedded XML resource
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = assembly.GetName().Name + ".Resources.genres.xml";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Log.WriteLine(LogLevel.Warning, "genres.xml resource not found");
                        return;
                    }

                    var doc = XDocument.Load(stream);

                    // Count genres in XML
                    int xmlGenreCount = 0;
                    int xmlMainGenreCount = 0;

                    foreach (var genreElement in doc.Descendants("genre"))
                    {
                        xmlMainGenreCount++;
                        xmlGenreCount += genreElement.Descendants("subgenre").Count();
                    }

                    // Check current count in database (excluding special _MAIN_ entries)
                    var currentCount = ExecuteScalar(DatabaseSchema.CountGenresExcludingMain);
                    int dbGenreCount = Convert.ToInt32(currentCount ?? 0);

                    // Only proceed if XML has more genres or database is empty
                    if (dbGenreCount >= xmlGenreCount)
                    {
                        Log.WriteLine("Genres are up to date ({0} genres in database, {1} in XML)",
                            dbGenreCount, xmlGenreCount);
                        return;
                    }

                    Log.WriteLine("Updating genres: {0} in database, {1} in XML", dbGenreCount, xmlGenreCount);

                    BeginTransaction();
                    try
                    {
                        int genreCount = 0;
                        int mainGenreCount = 0;

                        // Parse and insert genres
                        foreach (var genreElement in doc.Descendants("genre"))
                        {
                            string parentName = genreElement.Attribute("name")?.Value;
                            string parentTranslation = genreElement.Attribute("ru")?.Value;

                            // Insert main genre translation (if not exists)
                            if (!string.IsNullOrEmpty(parentName) && !string.IsNullOrEmpty(parentTranslation))
                            {
                                ExecuteNonQuery(DatabaseSchema.InsertGenreIfNotExists,
                                    CreateParameter("@Tag", "_MAIN_" + parentName),
                                    CreateParameter("@ParentName", DBNull.Value),
                                    CreateParameter("@Name", parentName),
                                    CreateParameter("@Translation", parentTranslation));
                                mainGenreCount++;
                            }

                            // Insert all subgenres (if not exist)
                            foreach (var subgenreElement in genreElement.Descendants("subgenre"))
                            {
                                string tag = subgenreElement.Attribute("tag")?.Value;
                                string name = subgenreElement.Value;
                                string translation = subgenreElement.Attribute("ru")?.Value;

                                if (!string.IsNullOrEmpty(tag))
                                {
                                    ExecuteNonQuery(DatabaseSchema.InsertGenreIfNotExists,
                                        CreateParameter("@Tag", tag),
                                        CreateParameter("@ParentName", parentName),
                                        CreateParameter("@Name", name),
                                        CreateParameter("@Translation", translation));

                                    genreCount++;
                                }
                            }
                        }

                        CommitTransaction();
                        Log.WriteLine("Successfully processed {0} main genres and {1} subgenres from XML",
                            mainGenreCount, genreCount);
                    }
                    catch
                    {
                        RollbackTransaction();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error initializing genres: {0}", ex.Message);
                // Don't throw - genres are not critical for basic operation
            }
        }

        /// <summary>
        /// Initialize library statistics table with default values
        /// </summary>
        private void InitializeLibraryStats()
        {
            try
            {
                // Check if stats table needs initialization
                var count = ExecuteScalar(DatabaseSchema.CheckLibraryStatsExist);
                if (Convert.ToInt32(count) >= 6)
                {
                    Log.WriteLine("Library statistics table already initialized");
                    return;
                }

                Log.WriteLine("Initializing library statistics with default values...");
                ExecuteNonQuery(DatabaseSchema.InitializeLibraryStats);
                Log.WriteLine("Library statistics initialized");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error initializing library stats: {0}", ex.Message);
                // Don't throw - stats are not critical for basic operation
            }
        }

        #region Downloads Methods

        /// <summary>
        /// Record a book download or read event
        /// </summary>
        public void RecordDownload(string bookId, string downloadType, string format = null, string clientInfo = null)
        {
            try
            {
                WarmUpIfNeeded();
                ExecuteNonQuery(DatabaseSchema.InsertDownload,
                    CreateParameter("@BookID", bookId),
                    CreateParameter("@DownloadDate", DateTime.Now.ToBinary()),
                    CreateParameter("@DownloadType", downloadType),
                    CreateParameter("@Format", format),
                    CreateParameter("@ClientInfo", clientInfo));

                Log.WriteLine(LogLevel.Info, "Recorded {0} for book {1}", downloadType, bookId);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error recording download for book {0}: {1}", bookId, ex.Message);
            }
        }

        /// <summary>
        /// Get recently downloaded books with pagination
        /// </summary>
        public List<Book> GetRecentDownloads(int limit, int offset)
        {
            try
            {
                WarmUpIfNeeded();
                return ExecuteQuery(DatabaseSchema.SelectRecentDownloads,
                    reader => BookFromReader(reader),
                    CreateParameter("@Limit", limit),
                    CreateParameter("@Offset", offset));
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error getting recent downloads: {0}", ex.Message);
                return new List<Book>();
            }
        }

        /// <summary>
        /// Get downloaded books sorted alphabetically with pagination
        /// </summary>
        public List<Book> GetDownloadsAlphabetic(int limit, int offset)
        {
            try
            {
                WarmUpIfNeeded();
                return ExecuteQuery(DatabaseSchema.SelectDownloadsAlphabetic,
                    reader => BookFromReader(reader),
                    CreateParameter("@Limit", limit),
                    CreateParameter("@Offset", offset));
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error getting downloads alphabetically: {0}", ex.Message);
                return new List<Book>();
            }
        }

        /// <summary>
        /// Get count of unique downloaded books
        /// </summary>
        public int GetUniqueDownloadsCount()
        {
            try
            {
                WarmUpIfNeeded();
                var result = ExecuteScalar(DatabaseSchema.CountUniqueDownloads);
                return Convert.ToInt32(result ?? 0);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error getting unique downloads count: {0}", ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Get total downloads count (including multiple downloads of same book)
        /// </summary>
        public int GetTotalDownloadsCount()
        {
            try
            {
                WarmUpIfNeeded();
                var result = ExecuteScalar(DatabaseSchema.CountTotalDownloads);
                return Convert.ToInt32(result ?? 0);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error getting total downloads count: {0}", ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Clear all download history
        /// </summary>
        public void ClearDownloadHistory()
        {
            try
            {
                int deletedRows = ExecuteNonQuery(DatabaseSchema.ClearDownloadHistory);
                Log.WriteLine("Cleared {0} download history records", deletedRows);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error clearing download history: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Clear download history older than specified date
        /// </summary>
        public void ClearOldDownloadHistory(DateTime beforeDate)
        {
            try
            {
                int deletedRows = ExecuteNonQuery(DatabaseSchema.ClearOldDownloadHistory,
                    CreateParameter("@BeforeDate", beforeDate.ToBinary()));
                Log.WriteLine("Cleared {0} old download history records", deletedRows);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error clearing old download history: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Clear download history for specific book
        /// </summary>
        public void ClearBookDownloadHistory(string bookId)
        {
            try
            {
                int deletedRows = ExecuteNonQuery(DatabaseSchema.ClearBookDownloadHistory,
                    CreateParameter("@BookID", bookId));
                Log.WriteLine("Cleared {0} download history records for book {1}", deletedRows, bookId);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error clearing download history for book {0}: {1}", bookId, ex.Message);
            }
        }

        /// <summary>
        /// Helper method to create Book object from reader
        /// </summary>
        private Book BookFromReader(IDataReader reader)
        {
            var book = new Book
            {
                ID = GetString(reader, "ID"),
                Version = GetFloat(reader, "Version"),
                Title = GetString(reader, "Title"),
                Language = GetString(reader, "Language"),
                BookDate = GetDateTime(reader, "BookDate") ?? DateTime.MinValue,
                DocumentDate = GetDateTime(reader, "DocumentDate") ?? DateTime.MinValue,
                Annotation = GetString(reader, "Annotation"),
                DocumentSize = GetUInt32(reader, "DocumentSize"),
                AddedDate = GetDateTime(reader, "AddedDate") ?? DateTime.MinValue
            };
            try
            {
                var downloadDate = GetDateTime(reader, "LastDownloadDate");
                if (downloadDate.HasValue) book.LastDownloadDate = downloadDate.Value;
            }
            catch { }

            return book;
        }

        #endregion

        #region Library Statistics Methods

        /// <summary>
        /// Get library statistic value by key
        /// </summary>
        /// <param name="key">Statistic key (e.g., 'total_books', 'authors_count')</param>
        /// <returns>Statistic value, or 0 if not found</returns>
        public int GetLibraryStatistic(string key)
        {
            try
            {
                WarmUpIfNeeded();
                var result = ExecuteScalar(DatabaseSchema.SelectLibraryStats, CreateParameter("@Key", key));
                if (result != null && result != DBNull.Value)
                {
                    using (var reader = ExecuteReader(DatabaseSchema.SelectLibraryStats, CreateParameter("@Key", key)))
                    {
                        if (reader.Read())
                        {
                            return GetInt32(reader, "value");
                        }
                    }
                }
                return 0;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error getting library statistic '{0}': {1}", key, ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Save library statistic value
        /// </summary>
        /// <param name="key">Statistic key</param>
        /// <param name="value">Statistic value</param>
        /// <param name="periodDays">For new_books statistic - period in days</param>
        public void SaveLibraryStatistic(string key, int value, int? periodDays = null)
        {
            try
            {
                ExecuteNonQuery(DatabaseSchema.UpsertLibraryStats,
                    CreateParameter("@Key", key),
                    CreateParameter("@Value", value),
                    CreateParameter("@UpdatedAt", DateTime.Now.ToBinary()),
                    CreateParameter("@PeriodDays", periodDays));

                Log.WriteLine(LogLevel.Info, "Saved library statistic '{0}' = {1}", key, value);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error saving library statistic '{0}': {1}", key, ex.Message);
            }
        }

        /// <summary>
        /// Get all library statistics
        /// </summary>
        /// <returns>Dictionary of all statistics</returns>
        public Dictionary<string, LibraryStatistic> GetAllLibraryStats()
        {
            var stats = new Dictionary<string, LibraryStatistic>();

            try
            {
                WarmUpIfNeeded();
                using (var reader = ExecuteReader(DatabaseSchema.SelectAllLibraryStats))
                {
                    while (reader.Read())
                    {
                        var stat = new LibraryStatistic
                        {
                            Key = GetString(reader, "key"),
                            Value = GetInt32(reader, "value"),
                            UpdatedAt = GetDateTime(reader, "updated_at") ?? DateTime.MinValue,
                            PeriodDays = reader.IsDBNull(reader.GetOrdinal("period_days"))
                                ? (int?)null
                                : GetInt32(reader, "period_days")
                        };

                        stats[stat.Key] = stat;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error getting all library statistics: {0}", ex.Message);
            }

            return stats;
        }

        /// <summary>
        /// Update multiple library statistics in a transaction
        /// </summary>
        /// <param name="statistics">Dictionary of statistics to save</param>
        public void SaveLibraryStatistics(Dictionary<string, int> statistics, int? newBooksPeriod = null)
        {
            if (statistics == null || statistics.Count == 0) return;

            try
            {
                BeginTransaction();
                try
                {
                    foreach (var stat in statistics)
                    {
                        int? periodDays = stat.Key == "new_books" ? newBooksPeriod : null;
                        ExecuteNonQuery(DatabaseSchema.UpsertLibraryStats,
                            CreateParameter("@Key", stat.Key),
                            CreateParameter("@Value", stat.Value),
                            CreateParameter("@UpdatedAt", DateTime.Now.ToBinary()),
                            CreateParameter("@PeriodDays", periodDays));
                    }

                    CommitTransaction();
                    Log.WriteLine("Successfully saved {0} library statistics", statistics.Count);
                }
                catch
                {
                    RollbackTransaction();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error saving library statistics: {0}", ex.Message);
            }
        }

        #endregion

        public void ReloadGenres()
        {
            try
            {
                Log.WriteLine("Reloading genres from XML...");

                BeginTransaction();
                try
                {
                    // Clear existing genres
                    ExecuteNonQuery(DatabaseSchema.DeleteAllGenres);

                    // Reload from XML
                    InitializeGenres();

                    CommitTransaction();
                }
                catch
                {
                    RollbackTransaction();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error reloading genres: {0}", ex.Message);
                throw;
            }
        }

        public List<Genre> GetAllGenres()
        {
            var genres = new List<Genre>();
            var parentGenres = new Dictionary<string, Genre>();

            WarmUpIfNeeded();

            // Load main genre translations first
            var mainGenreTranslations = new Dictionary<string, string>();
            var translationReader = ExecuteReader(DatabaseSchema.SelectMainGenreTranslations);
            try
            {
                while (translationReader.Read())
                {
                    string name = GetString(translationReader, "Name");
                    string translation = GetString(translationReader, "Translation");
                    mainGenreTranslations[name] = translation;
                }
            }
            finally
            {
                translationReader.Close();
            }

            // Load all regular genres
            var reader = ExecuteReader(DatabaseSchema.SelectAllGenres);
            try
            {
                while (reader.Read())
                {
                    string tag = GetString(reader, "Tag");

                    // Skip special main genre translation entries
                    if (tag.StartsWith("_MAIN_"))
                        continue;

                    string parentName = GetString(reader, "ParentName");
                    string name = GetString(reader, "Name");
                    string translation = GetString(reader, "Translation");

                    // Create subgenre
                    var subgenre = new Genre
                    {
                        Tag = tag,
                        Name = name,
                        Translation = translation
                    };

                    // Get or create parent genre
                    if (!string.IsNullOrEmpty(parentName))
                    {
                        if (!parentGenres.ContainsKey(parentName))
                        {
                            parentGenres[parentName] = new Genre
                            {
                                Tag = "",  // Main genres don't have tags
                                Name = parentName,
                                Translation = mainGenreTranslations.ContainsKey(parentName)
                                    ? mainGenreTranslations[parentName]
                                    : parentName,
                                Subgenres = new List<Genre>()
                            };
                            genres.Add(parentGenres[parentName]);
                        }

                        parentGenres[parentName].Subgenres.Add(subgenre);
                    }
                }
            }
            finally
            {
                reader.Close();
            }

            return genres;
        }

        public int ExecuteNonQuery(string sql, params IDbDataParameter[] parameters)
        {
            _lastAccessTime = DateTime.Now;
            var command = SqliteConnectionFactory.CreateCommand(sql, connection);
            try
            {
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.Add(param);
                    }
                }
                return command.ExecuteNonQuery();
            }
            finally
            {
                command.Dispose();
            }
        }

        public object ExecuteScalar(string sql, params IDbDataParameter[] parameters)
        {
            _lastAccessTime = DateTime.Now;
            var command = SqliteConnectionFactory.CreateCommand(sql, connection);
            try
            {
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.Add(param);
                    }
                }
                return command.ExecuteScalar();
            }
            finally
            {
                command.Dispose();
            }
        }

        public IDataReader ExecuteReader(string sql, params IDbDataParameter[] parameters)
        {
            _lastAccessTime = DateTime.Now;
            var command = SqliteConnectionFactory.CreateCommand(sql, connection);
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.Add(param);
                }
            }
            return command.ExecuteReader();
        }

        public List<T> ExecuteQuery<T>(string sql, Func<IDataReader, T> mapper, params IDbDataParameter[] parameters)
        {
            var results = new List<T>();
            var reader = ExecuteReader(sql, parameters);
            try
            {
                while (reader.Read())
                {
                    results.Add(mapper(reader));
                }
            }
            finally
            {
                reader.Close();
                reader = null;
            }
            return results;
        }

        public T ExecuteQuerySingle<T>(string sql, Func<IDataReader, T> mapper, params IDbDataParameter[] parameters) where T : class
        {
            var reader = ExecuteReader(sql, parameters);
            try
            {
                if (reader.Read())
                {
                    return mapper(reader);
                }
            }
            finally
            {
                reader.Close();
                reader = null;
            }
            return null;
        }

        public void BeginTransaction()
        {
            ExecuteNonQuery("BEGIN TRANSACTION");
        }

        public void CommitTransaction()
        {
            ExecuteNonQuery("COMMIT");
        }

        public void RollbackTransaction()
        {
            ExecuteNonQuery("ROLLBACK");
        }

        // Helper methods for parameter creation using factory
        public static IDbDataParameter CreateParameter(string name, object value)
        {
            if (Utils.IsLinux)
            {
                var linuxSqliteAssembly = EmbeddedDllLoader.GetLinuxSqliteAssembly();
                if (linuxSqliteAssembly != null)
                {
                    var paramType = linuxSqliteAssembly.GetType("Mono.Data.Sqlite.SqliteParameter");
                    return (IDbDataParameter)Activator.CreateInstance(paramType, name, value ?? DBNull.Value);
                }
                else
                {
                    return new System.Data.SQLite.SQLiteParameter(name, value ?? DBNull.Value);
                }
            }
            else
            {
                return new System.Data.SQLite.SQLiteParameter(name, value ?? DBNull.Value);
            }
        }

        public static IDbDataParameter CreateParameter(string name, string value)
        {
            return CreateParameter(name, string.IsNullOrEmpty(value) ? DBNull.Value : (object)value);
        }

        public static IDbDataParameter CreateParameter(string name, DateTime? value)
        {
            if (value.HasValue && value.Value != DateTime.MinValue)
            {
                try
                {
                    return CreateParameter(name, value.Value.ToBinary());
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    Log.WriteLine(LogLevel.Warning, "Invalid DateTime parameter '{0}' ({1}), using current date instead: {2}", name, value.Value, ex.Message);
                    return CreateParameter(name, DateTime.Now.ToBinary());
                }
            }
            else
            {
                return CreateParameter(name, DBNull.Value);
            }
        }

        public static IDbDataParameter CreateParameter(string name, bool value)
        {
            return CreateParameter(name, value ? 1 : 0);
        }

        // Helper methods for reading from IDataReader
        public static string GetString(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        public static DateTime? GetDateTime(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal)) return null;

            long ticks = reader.GetInt64(ordinal);
            return ticks == 0 ? (DateTime?)null : DateTime.FromBinary(ticks);
        }

        public static bool GetBoolean(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return !reader.IsDBNull(ordinal) && reader.GetInt32(ordinal) != 0;
        }

        public static int GetInt32(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
        }

        public static uint GetUInt32(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0u : (uint)reader.GetInt64(ordinal);
        }

        public static float GetFloat(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0f : reader.GetFloat(ordinal);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Stop keep-alive timer
                    _keepAliveTimer?.Dispose();

                    if (connection != null)
                    {
                        // Optimize database before closing
                        try
                        {
                            ExecuteNonQuery("PRAGMA optimize");
                            ExecuteNonQuery("PRAGMA wal_checkpoint(TRUNCATE)");
                        }
                        catch { }

                        connection.Close();
                        connection.Dispose();
                    }
                }
                disposed = true;
            }
        }

        ~DatabaseManager()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Library statistic data structure
    /// </summary>
    public class LibraryStatistic
    {
        public string Key { get; set; }
        public int Value { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? PeriodDays { get; set; }  // For new_books statistic - period in days
    }
}