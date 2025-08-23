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
            try
            {
                // Create tables in dependency order
                ExecuteNonQuery(DatabaseSchema.CreateBooksTable);
                ExecuteNonQuery(DatabaseSchema.CreateAuthorsTable);
                ExecuteNonQuery(DatabaseSchema.CreateGenresTable);
                ExecuteNonQuery(DatabaseSchema.CreateTranslatorsTable);
                ExecuteNonQuery(DatabaseSchema.CreateBookAuthorsTable);
                ExecuteNonQuery(DatabaseSchema.CreateBookGenresTable);
                ExecuteNonQuery(DatabaseSchema.CreateBookTranslatorsTable);

                // Upgrade existing schema if necessary
                UpgradeSchema();

                // Create indexes
                ExecuteNonQuery(DatabaseSchema.CreateIndexes);

                Log.WriteLine("Database schema initialized");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error initializing database schema: {0}", ex.Message);
                throw;
            }
        }

        private void UpgradeSchema()
        {
            try
            {
                // Check if Soundex columns exist, add them if they don't
                if (!ColumnExists("Books", "TitleSoundex"))
                {
                    Log.WriteLine("Adding TitleSoundex column to Books table");
                    ExecuteNonQuery(DatabaseSchema.AddTitleSoundexColumn);
                    ExecuteNonQuery(DatabaseSchema.UpdateExistingTitleSoundex);
                }

                if (!ColumnExists("Authors", "NameSoundex"))
                {
                    Log.WriteLine("Adding NameSoundex column to Authors table");
                    ExecuteNonQuery(DatabaseSchema.AddAuthorSoundexColumn);
                    ExecuteNonQuery(DatabaseSchema.UpdateExistingAuthorSoundex);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error during schema upgrade: {0}", ex.Message);
            }
        }

        private bool ColumnExists(string tableName, string columnName)
        {
            try
            {
                var result = ExecuteScalar($"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name='{columnName}'");
                return Convert.ToInt32(result) > 0;
            }
            catch
            {
                return false;
            }
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