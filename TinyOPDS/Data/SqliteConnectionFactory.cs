/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * SQLite connection factory for cross-platform compatibility
 * Uses System.Data.SQLite on Windows and Mono.Data.Sqlite on Linux
 * 
 ************************************************************/

using System;
using System.Data;

namespace TinyOPDS.Data
{
    /// <summary>
    /// Factory class for creating SQLite connections and commands
    /// that work on both Windows and Linux platforms
    /// </summary>
    public static class SqliteConnectionFactory
    {
        /// <summary>
        /// Create SQLite connection appropriate for current platform
        /// </summary>
        /// <param name="connectionString">SQLite connection string</param>
        /// <returns>IDbConnection instance</returns>
        public static IDbConnection CreateConnection(string connectionString)
        {
            if (Utils.IsLinux)
            {
                // Use reflection to create Mono.Data.Sqlite connection at runtime
                var assembly = System.Reflection.Assembly.LoadFrom("/usr/lib/mono/4.5/Mono.Data.Sqlite.dll");
                var connectionType = assembly.GetType("Mono.Data.Sqlite.SqliteConnection");
                return (IDbConnection)Activator.CreateInstance(connectionType, connectionString);
            }
            else
            {
                // Use System.Data.SQLite on Windows
                return new System.Data.SQLite.SQLiteConnection(connectionString);
            }
        }

        /// <summary>
        /// Create SQLite command appropriate for current platform
        /// </summary>
        /// <param name="sql">SQL command text</param>
        /// <param name="connection">Database connection</param>
        /// <returns>IDbCommand instance</returns>
        public static IDbCommand CreateCommand(string sql, IDbConnection connection)
        {
            if (Utils.IsLinux)
            {
                // Use reflection to create Mono.Data.Sqlite command at runtime
                var assembly = System.Reflection.Assembly.Load("Mono.Data.Sqlite");
                var commandType = assembly.GetType("Mono.Data.Sqlite.SqliteCommand");
                return (IDbCommand)Activator.CreateInstance(commandType, sql, connection);
            }
            else
            {
                return new System.Data.SQLite.SQLiteCommand(sql, (System.Data.SQLite.SQLiteConnection)connection);
            }
        }

        /// <summary>
        /// Create SQLite command without connection
        /// </summary>
        /// <param name="sql">SQL command text</param>
        /// <returns>IDbCommand instance</returns>
        public static IDbCommand CreateCommand(string sql)
        {
            if (Utils.IsLinux)
            {
                // Use reflection to create Mono.Data.Sqlite command at runtime
                var assembly = System.Reflection.Assembly.Load("Mono.Data.Sqlite");
                var commandType = assembly.GetType("Mono.Data.Sqlite.SqliteCommand");
                return (IDbCommand)Activator.CreateInstance(commandType, sql);
            }
            else
            {
                return new System.Data.SQLite.SQLiteCommand(sql);
            }
        }
    }
}