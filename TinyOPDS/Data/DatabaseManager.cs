/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Database manager for SQLite operations
 *
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

namespace TinyOPDS.Data
{
    public class DatabaseManager : IDisposable
    {
        private IDbConnection _connection;
        private readonly string _connectionString;
        private bool _disposed = false;

        public DatabaseManager(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Version=3;";

            // Create database file if it doesn't exist (only for System.Data.SQLite on Windows)
            if (!Utils.IsLinux && !File.Exists(databasePath))
            {
                // Use reflection to call SQLiteConnection.CreateFile on Windows
                try
                {
                    var sqliteType = typeof(System.Data.SQLite.SQLiteConnection);
                    var createFileMethod = sqliteType.GetMethod("CreateFile", new[] { typeof(string) });
                    createFileMethod?.Invoke(null, new object[] { databasePath });
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Warning, "Could not create SQLite file: {0}", ex.Message);
                }
            }

            _connection = SqliteConnectionFactory.CreateConnection(_connectionString);
            _connection.Open();

            // Enable foreign keys
            ExecuteNonQuery("PRAGMA foreign_keys = ON");

            InitializeSchema();
        }

        private void InitializeSchema()
        {
            // Create tables in dependency order
            ExecuteNonQuery(DatabaseSchema.CreateBooksTable);
            ExecuteNonQuery(DatabaseSchema.CreateAuthorsTable);
            ExecuteNonQuery(DatabaseSchema.CreateAuthorAliasesTable);
            ExecuteNonQuery(DatabaseSchema.CreateGenresTable);
            ExecuteNonQuery(DatabaseSchema.CreateTranslatorsTable);
            ExecuteNonQuery(DatabaseSchema.CreateBookAuthorsTable);
            ExecuteNonQuery(DatabaseSchema.CreateBookGenresTable);
            ExecuteNonQuery(DatabaseSchema.CreateBookTranslatorsTable);

            // Create indexes
            ExecuteNonQuery(DatabaseSchema.CreateIndexes);

            // Migrate existing data if needed (add Soundex columns to existing tables)
            MigrateExistingData();

            Log.WriteLine("Database schema initialized");
        }

        /// <summary>
        /// Migrate existing database to add Soundex columns if they don't exist
        /// </summary>
        private void MigrateExistingData()
        {
            try
            {
                // Check if TitleSoundex column exists in Books table
                var hasBookSoundex = CheckColumnExists("Books", "TitleSoundex");
                if (!hasBookSoundex)
                {
                    ExecuteNonQuery("ALTER TABLE Books ADD COLUMN TitleSoundex TEXT");
                    Log.WriteLine("Added TitleSoundex column to Books table");
                }

                // Check if NameSoundex column exists in Authors table
                var hasAuthorSoundex = CheckColumnExists("Authors", "NameSoundex");
                if (!hasAuthorSoundex)
                {
                    ExecuteNonQuery("ALTER TABLE Authors ADD COLUMN NameSoundex TEXT");
                    Log.WriteLine("Added NameSoundex column to Authors table");
                }

                // Recreate indexes if columns were added
                if (!hasBookSoundex || !hasAuthorSoundex)
                {
                    ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_books_title_soundex ON Books(TitleSoundex)");
                    ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_authors_name_soundex ON Authors(NameSoundex)");
                    Log.WriteLine("Created Soundex indexes");
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error during schema migration: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Check if a column exists in a table
        /// </summary>
        /// <param name="tableName">Table name</param>
        /// <param name="columnName">Column name</param>
        /// <returns>True if column exists</returns>
        private bool CheckColumnExists(string tableName, string columnName)
        {
            try
            {
                var result = ExecuteScalar($"PRAGMA table_info({tableName})");
                using (var reader = ExecuteReader($"PRAGMA table_info({tableName})"))
                {
                    while (reader.Read())
                    {
                        var colName = reader.GetString(1); // Column name is at index 1
                        if (colName.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error checking column existence: {0}", ex.Message);
            }
            return false;
        }

        public int ExecuteNonQuery(string sql, params IDbDataParameter[] parameters)
        {
            var command = SqliteConnectionFactory.CreateCommand(sql, _connection);
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
            var command = SqliteConnectionFactory.CreateCommand(sql, _connection);
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
            var command = SqliteConnectionFactory.CreateCommand(sql, _connection);
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
                // Create Mono.Data.Sqlite parameter via reflection
                var assembly = System.Reflection.Assembly.Load("Mono.Data.Sqlite");
                var paramType = assembly.GetType("Mono.Data.Sqlite.SqliteParameter");
                return (IDbDataParameter)Activator.CreateInstance(paramType, name, value ?? DBNull.Value);
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
            if (value.HasValue)
                return CreateParameter(name, value.Value.ToBinary());
            else
                return CreateParameter(name, DBNull.Value);
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
            return DateTime.FromBinary(reader.GetInt64(ordinal));
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
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_connection != null)
                    {
                        _connection.Close();
                        _connection.Dispose();
                    }
                }
                _disposed = true;
            }
        }

        ~DatabaseManager()
        {
            Dispose(false);
        }
    }
}