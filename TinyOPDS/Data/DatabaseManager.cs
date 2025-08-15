/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * Database manager for SQLite operations
 * 
 ************************************************************/

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace TinyOPDS.Data
{
    public class DatabaseManager : IDisposable
    {
        private SQLiteConnection _connection;
        private readonly string _connectionString;
        private bool _disposed = false;

        public DatabaseManager(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Version=3;";

            // Create database file if it doesn't exist
            if (!File.Exists(databasePath))
            {
                SQLiteConnection.CreateFile(databasePath);
            }

            _connection = new SQLiteConnection(_connectionString);
            _connection.Open();

            // Enable foreign keys
            ExecuteNonQuery("PRAGMA foreign_keys = ON");

            InitializeSchema();
        }

        private void InitializeSchema()
        {
            // Create tables
            ExecuteNonQuery(DatabaseSchema.CreateBooksTable);
            ExecuteNonQuery(DatabaseSchema.CreateAuthorsTable);
            ExecuteNonQuery(DatabaseSchema.CreateGenresTable);
            ExecuteNonQuery(DatabaseSchema.CreateTranslatorsTable);
            ExecuteNonQuery(DatabaseSchema.CreateBookAuthorsTable);
            ExecuteNonQuery(DatabaseSchema.CreateBookGenresTable);
            ExecuteNonQuery(DatabaseSchema.CreateBookTranslatorsTable);

            // Create indexes
            ExecuteNonQuery(DatabaseSchema.CreateIndexes);

            Log.WriteLine("Database schema initialized");
        }

        public int ExecuteNonQuery(string sql, params SQLiteParameter[] parameters)
        {
            var command = new SQLiteCommand(sql, _connection);
            try
            {
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }
                return command.ExecuteNonQuery();
            }
            finally
            {
                command.Dispose();
            }
        }

        public object ExecuteScalar(string sql, params SQLiteParameter[] parameters)
        {
            var command = new SQLiteCommand(sql, _connection);
            try
            {
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }
                return command.ExecuteScalar();
            }
            finally
            {
                command.Dispose();
            }
        }

        public SQLiteDataReader ExecuteReader(string sql, params SQLiteParameter[] parameters)
        {
            var command = new SQLiteCommand(sql, _connection);
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            return command.ExecuteReader();
        }

        public List<T> ExecuteQuery<T>(string sql, Func<SQLiteDataReader, T> mapper, params SQLiteParameter[] parameters)
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

        public T ExecuteQuerySingle<T>(string sql, Func<SQLiteDataReader, T> mapper, params SQLiteParameter[] parameters) where T : class
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

        // Helper methods for parameter creation
        public static SQLiteParameter CreateParameter(string name, object value)
        {
            return new SQLiteParameter(name, value ?? DBNull.Value);
        }

        public static SQLiteParameter CreateParameter(string name, string value)
        {
            return new SQLiteParameter(name, string.IsNullOrEmpty(value) ? DBNull.Value : (object)value);
        }

        public static SQLiteParameter CreateParameter(string name, DateTime? value)
        {
            if (value.HasValue)
                return new SQLiteParameter(name, value.Value.ToBinary());
            else
                return new SQLiteParameter(name, DBNull.Value);
        }

        public static SQLiteParameter CreateParameter(string name, bool value)
        {
            return new SQLiteParameter(name, value ? 1 : 0);
        }

        // Helper methods for reading from SQLiteDataReader
        public static string GetString(SQLiteDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        public static DateTime? GetDateTime(SQLiteDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal)) return null;
            return DateTime.FromBinary(reader.GetInt64(ordinal));
        }

        public static bool GetBoolean(SQLiteDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return !reader.IsDBNull(ordinal) && reader.GetInt32(ordinal) != 0;
        }

        public static int GetInt32(SQLiteDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
        }

        public static uint GetUInt32(SQLiteDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0u : (uint)reader.GetInt64(ordinal);
        }

        public static float GetFloat(SQLiteDataReader reader, string columnName)
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