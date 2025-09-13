/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * SQLite connection factory for cross-platform compatibility
 * Uses System.Data.SQLite on Windows and Mono.Data.Sqlite on Linux
 *
 */

using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Data.SQLite;

namespace TinyOPDS.Data
{
    /// <summary>
    /// Factory class for creating SQLite connections and commands
    /// that work on both Windows and Linux platforms
    /// </summary>
    public static class SqliteConnectionFactory
    {
        private static Type connectionType;
        private static Type commandType;
        private static bool typesInitialized = false;

        /// <summary>
        /// Initialize types for Linux platform
        /// </summary>
        private static void InitializeTypes()
        {
            if (typesInitialized) return;

            if (Utils.IsLinux)
            {
                var linuxSqliteAssembly = EmbeddedDllLoader.GetLinuxSqliteAssembly();
                if (linuxSqliteAssembly != null)
                {
                    connectionType = linuxSqliteAssembly.GetType("Mono.Data.Sqlite.SqliteConnection");
                    commandType = linuxSqliteAssembly.GetType("Mono.Data.Sqlite.SqliteCommand");
                }
            }

            typesInitialized = true;
        }

        /// <summary>
        /// Create SQLite connection appropriate for current platform
        /// </summary>
        /// <param name="connectionString">SQLite connection string</param>
        /// <returns>IDbConnection instance</returns>
        public static IDbConnection CreateConnection(string connectionString)
        {
            if (Utils.IsLinux)
            {
                InitializeTypes();

                if (connectionType != null)
                {
                    return (IDbConnection)Activator.CreateInstance(connectionType, connectionString);
                }
                else
                {
                    try
                    {
                        return new SQLiteConnection(connectionString);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            "Neither Mono.Data.Sqlite nor System.Data.SQLite is available on this Linux system. " +
                            "Please install mono-data-sqlite package or ensure Mono.Data.Sqlite.dll is available.", ex);
                    }
                }
            }
            else
            {
                return new SQLiteConnection(connectionString);
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
                InitializeTypes();

                if (commandType != null)
                {
                    return (IDbCommand)Activator.CreateInstance(commandType, sql, connection);
                }
                else
                {
                    try
                    {
                        return new SQLiteCommand(sql, (SQLiteConnection)connection);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            "Neither Mono.Data.Sqlite nor System.Data.SQLite is available on this Linux system.", ex);
                    }
                }
            }
            else
            {
                return new SQLiteCommand(sql, (SQLiteConnection)connection);
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
                InitializeTypes();

                if (commandType != null)
                {
                    return (IDbCommand)Activator.CreateInstance(commandType, sql);
                }
                else
                {
                    try
                    {
                        return new SQLiteCommand(sql);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            "Neither Mono.Data.Sqlite nor System.Data.SQLite is available on this Linux system.", ex);
                    }
                }
            }
            else
            {
                return new SQLiteCommand(sql);
            }
        }
    }
}