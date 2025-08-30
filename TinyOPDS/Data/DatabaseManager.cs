/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Database manager for SQLite operations with FTS5 support
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
        private IDbConnection connection;
        private readonly string connectionString;
        private bool disposed = false;

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

                // Create FTS5 tables
                ExecuteNonQuery(DatabaseSchema.CreateBooksFTSTable);
                ExecuteNonQuery(DatabaseSchema.CreateAuthorsFTSTable);

                // Create views
                ExecuteNonQuery(DatabaseSchema.CreateAuthorStatisticsView);

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

                Log.WriteLine("Database schema initialized with FTS5 support and triggers");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error initializing database schema: {0}", ex.Message);
                throw;
            }
        }

        public int ExecuteNonQuery(string sql, params IDbDataParameter[] parameters)
        {
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
                    if (connection != null)
                    {
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
}