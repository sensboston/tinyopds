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
using System.IO;
using System.Reflection;

namespace TinyOPDS.Data
{
    /// <summary>
    /// Factory class for creating SQLite connections and commands
    /// that work on both Windows and Linux platforms
    /// </summary>
    public static class SqliteConnectionFactory
    {
        private static Assembly _sqliteAssembly;
        private static Type _connectionType;
        private static Type _commandType;

        static SqliteConnectionFactory()
        {
            if (Utils.IsLinux)
            {
                LoadLinuxSqliteTypes();
            }
        }

        private static void LoadLinuxSqliteTypes()
        {
            try
            {
                // Try to load Mono.Data.Sqlite from GAC first
                _sqliteAssembly = Assembly.Load("Mono.Data.Sqlite");
            }
            catch
            {
                try
                {
                    // Try from standard Mono location
                    var monoSqlitePath = "/usr/lib/mono/4.5/Mono.Data.Sqlite.dll";
                    if (File.Exists(monoSqlitePath))
                    {
                        _sqliteAssembly = Assembly.LoadFrom(monoSqlitePath);
                    }
                }
                catch
                {
                    try
                    {
                        // Try from current directory
                        var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mono.Data.Sqlite.dll");
                        if (File.Exists(localPath))
                        {
                            _sqliteAssembly = Assembly.LoadFrom(localPath);
                        }
                    }
                    catch
                    {
                        // Last resort - try to load System.Data.SQLite even on Linux
                        // This might work with newer Mono versions
                    }
                }
            }

            if (_sqliteAssembly != null)
            {
                _connectionType = _sqliteAssembly.GetType("Mono.Data.Sqlite.SqliteConnection");
                _commandType = _sqliteAssembly.GetType("Mono.Data.Sqlite.SqliteCommand");
            }
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
                if (_connectionType != null)
                {
                    return (IDbConnection)Activator.CreateInstance(_connectionType, connectionString);
                }
                else
                {
                    // Fallback to System.Data.SQLite if Mono.Data.Sqlite not available
                    try
                    {
                        return new System.Data.SQLite.SQLiteConnection(connectionString);
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
                if (_commandType != null)
                {
                    return (IDbCommand)Activator.CreateInstance(_commandType, sql, connection);
                }
                else
                {
                    // Fallback to System.Data.SQLite
                    try
                    {
                        return new System.Data.SQLite.SQLiteCommand(sql, (System.Data.SQLite.SQLiteConnection)connection);
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
                if (_commandType != null)
                {
                    return (IDbCommand)Activator.CreateInstance(_commandType, sql);
                }
                else
                {
                    // Fallback to System.Data.SQLite
                    try
                    {
                        return new System.Data.SQLite.SQLiteCommand(sql);
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
                return new System.Data.SQLite.SQLiteCommand(sql);
            }
        }
    }
}