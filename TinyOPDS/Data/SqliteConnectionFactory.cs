/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * SQLite connection factory for cross-platform compatibility
 * Uses reflection to avoid direct dependencies
 *
 */

using System;
using System.Data;
using System.Reflection;

namespace TinyOPDS.Data
{
    /// <summary>
    /// Factory class for creating SQLite connections and commands
    /// that work on both Windows and Unix platforms
    /// </summary>
    public static class SqliteConnectionFactory
    {
        private static Type connectionType;
        private static Type commandType;
        private static bool typesInitialized = false;

        // For Windows - loaded via reflection
        private static Assembly windowsSqliteAssembly;
        private static Type windowsConnectionType;
        private static Type windowsCommandType;

        /// <summary>
        /// Initialize types for current platform
        /// </summary>
        private static void InitializeTypes()
        {
            if (typesInitialized) return;

            // Unix platforms (Linux and macOS) use Mono.Data.Sqlite
            if (Utils.IsLinux || Utils.IsMacOS)
            {
                Log.WriteLine(LogLevel.Info, "SqliteConnectionFactory: Initializing for Unix platform ({0})",
                    Utils.IsLinux ? "Linux" : "macOS");

                var unixSqliteAssembly = EmbeddedDllLoader.GetLinuxSqliteAssembly();
                if (unixSqliteAssembly != null)
                {
                    connectionType = unixSqliteAssembly.GetType("Mono.Data.Sqlite.SqliteConnection");
                    commandType = unixSqliteAssembly.GetType("Mono.Data.Sqlite.SqliteCommand");

                    if (connectionType != null && commandType != null)
                    {
                        Log.WriteLine(LogLevel.Info, "SqliteConnectionFactory: Successfully loaded Mono.Data.Sqlite types");
                    }
                    else
                    {
                        Log.WriteLine(LogLevel.Error, "SqliteConnectionFactory: Could not find required types in Mono.Data.Sqlite");
                        throw new InvalidOperationException("Mono.Data.Sqlite assembly is loaded but required types are not found");
                    }
                }
                else
                {
                    Log.WriteLine(LogLevel.Error, "SqliteConnectionFactory: Could not load Mono.Data.Sqlite assembly");
                    throw new InvalidOperationException("Mono.Data.Sqlite is required on Unix systems but could not be loaded");
                }
            }
            else
            {
                // Windows uses System.Data.SQLite loaded dynamically
                Log.WriteLine(LogLevel.Info, "SqliteConnectionFactory: Initializing for Windows platform");

                try
                {
                    // Try to load from embedded resources via reflection
                    windowsSqliteAssembly = Assembly.Load("System.Data.SQLite");

                    if (windowsSqliteAssembly == null)
                    {
                        throw new InvalidOperationException("System.Data.SQLite assembly not found");
                    }

                    windowsConnectionType = windowsSqliteAssembly.GetType("System.Data.SQLite.SQLiteConnection");
                    windowsCommandType = windowsSqliteAssembly.GetType("System.Data.SQLite.SQLiteCommand");

                    if (windowsConnectionType != null && windowsCommandType != null)
                    {
                        connectionType = windowsConnectionType;
                        commandType = windowsCommandType;
                        Log.WriteLine(LogLevel.Info, "SqliteConnectionFactory: Successfully loaded System.Data.SQLite types");
                    }
                    else
                    {
                        throw new InvalidOperationException("Required types not found in System.Data.SQLite");
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "SqliteConnectionFactory: Failed to load System.Data.SQLite: {0}", ex.Message);
                    throw new InvalidOperationException("System.Data.SQLite is required on Windows but could not be loaded", ex);
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
            InitializeTypes();

            if (connectionType == null)
            {
                throw new InvalidOperationException("SQLite connection type not initialized");
            }

            try
            {
                Log.WriteLine(LogLevel.Info, "SqliteConnectionFactory: Creating {0} connection",
                    (Utils.IsLinux || Utils.IsMacOS) ? "Mono.Data.Sqlite" : "System.Data.SQLite");

                return (IDbConnection)Activator.CreateInstance(connectionType, connectionString);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "SqliteConnectionFactory: Failed to create connection: {0}", ex.Message);
                throw new InvalidOperationException("Failed to create SQLite connection", ex);
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
            InitializeTypes();

            if (commandType == null)
            {
                throw new InvalidOperationException("SQLite command type not initialized");
            }

            try
            {
                return (IDbCommand)Activator.CreateInstance(commandType, sql, connection);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "SqliteConnectionFactory: Failed to create command: {0}", ex.Message);
                throw new InvalidOperationException("Failed to create SQLite command", ex);
            }
        }

        /// <summary>
        /// Create SQLite command without connection
        /// </summary>
        /// <param name="sql">SQL command text</param>
        /// <returns>IDbCommand instance</returns>
        public static IDbCommand CreateCommand(string sql)
        {
            InitializeTypes();

            if (commandType == null)
            {
                throw new InvalidOperationException("SQLite command type not initialized");
            }

            try
            {
                return (IDbCommand)Activator.CreateInstance(commandType, sql);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "SqliteConnectionFactory: Failed to create command: {0}", ex.Message);
                throw new InvalidOperationException("Failed to create SQLite command", ex);
            }
        }
    }
}