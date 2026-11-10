/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Simple logging class
 *
 */

using System;
using System.IO;
using System.Diagnostics;

namespace TinyOPDS
{
    /// <summary>
    /// A lightweight logging class
    /// </summary>
    public static class Log
    {
        // Store only the filename, full path will be calculated dynamically
        private static string logFileName = "TinyOPDS.log";

        /// <summary>
        /// Get full path to log file (calculated each time to ensure correct location)
        /// </summary>
        public static string LogFileName
        {
            get { return GetLogPath(); }
        }

        /// <summary>
        /// 
        /// </summary>
        public static LogLevel VerbosityLevel = LogLevel.Info;

        /// <summary>
        /// Flag indicating if running as system service
        /// Should be set by service initialization code
        /// </summary>
        public static bool IsRunningAsService = false;

        /// <summary>
        /// Enable or disable logging to file
        /// </summary>
        private static bool saveToFile = false;
        public static bool SaveToFile
        {
            get { return saveToFile; }
            set
            {
                saveToFile = value;

                // Ensure directory exists when enabling logging
                if (saveToFile)
                {
                    try
                    {
                        string logPath = GetLogPath();
                        string directory = Path.GetDirectoryName(logPath);
                        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to create log directory: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Determine log file path based on platform
        /// </summary>
        /// <returns>Full path to log file</returns>
        private static string GetLogPath()
        {
            // Direct platform check without Utils to avoid circular dependency
            PlatformID platform = Environment.OSVersion.Platform;
            bool isWindows = platform == PlatformID.Win32NT ||
                            platform == PlatformID.Win32S ||
                            platform == PlatformID.Win32Windows ||
                            platform == PlatformID.WinCE;

            // On Unix-like systems (everything that's NOT Windows), try system log directory
            if (!isWindows)
            {
                string systemLogDir = "/var/log/tinyopds";
                try
                {
                    if (!Directory.Exists(systemLogDir))
                    {
                        Directory.CreateDirectory(systemLogDir);
                    }

                    // Test write access
                    string testFile = Path.Combine(systemLogDir, ".test");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);

                    return Path.Combine(systemLogDir, logFileName);
                }
                catch
                {
                    // Fall back to application directory if cannot write to /var/log/tinyopds/
                    Debug.WriteLine("Cannot write to /var/log/tinyopds/, falling back to application directory");
                }
            }

            // For Windows (including Microsoft Store) and Unix fallback:
            // Use Utils.ServiceFilesLocation which handles:
            // - Regular Windows: exe directory
            // - Microsoft Store: LocalApplicationData/TinyOPDS
            // - Unix fallback: exe directory or LocalApplicationData
            return Path.Combine(Utils.ServiceFilesLocation, logFileName);
        }

        /// <summary>
        /// Writes the args to the default logging output using the format provided.
        /// </summary>
        public static void WriteLine(string format, params object[] args)
        {
            WriteLine(LogLevel.Info, format, args);
        }

        /// <summary>
        /// Writes the args to the default logging output using the format provided.
        /// </summary>
        public static void WriteLine(LogLevel level, string format, params object[] args)
        {
            // Authentication verbosity keeps logins, bans and errors only
            bool write = VerbosityLevel == LogLevel.Authentication
                ? level == LogLevel.Authentication || level == LogLevel.Error
                : level >= VerbosityLevel || level == LogLevel.Authentication;

            if (write)
            {
                string caller = "---";

                if (level != LogLevel.Authentication)
                {
                    try
                    {
                        caller = new StackTrace().GetFrame(2).GetMethod().ReflectedType.Name;
                        if (caller.StartsWith("<>")) caller = new StackTrace().GetFrame(1).GetMethod().ReflectedType.Name;
                    }
                    catch { }
                }
                else caller = "HTTPServer";
                string prefix = string.Format("{0}\t{1}\t{2}", (level == LogLevel.Info) ? 'I' : ((level == LogLevel.Warning) ? 'W' : ((level == LogLevel.Authentication) ? 'A' : 'E')), caller, (caller.Length > 7 ? "" : "\t"));

                string message = string.Format(prefix + format, args);
                Debug.WriteLine(message);
                if (SaveToFile) WriteToFile(message);
            }
        }

        private static readonly object fileSyncObject = new object();

        // When the log grows over this size it is renamed to TinyOPDS.log.old and a new one is started
        private const long MaxLogSize = 10 * 1024 * 1024;

        /// <summary>
        /// Writes a line to the current log file.
        /// </summary>
        /// <param name="message"></param>
        private static void WriteToFile(string message)
        {
            lock (fileSyncObject)
            {
                FileStream fileStream = null;
                try
                {
                    // Always get fresh path to ensure correct location
                    string fullLogPath = GetLogPath();

                    // Ensure directory exists (in case Utils.ServiceFilesLocation changed)
                    string directory = Path.GetDirectoryName(fullLogPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (File.Exists(fullLogPath) && new FileInfo(fullLogPath).Length > MaxLogSize)
                    {
                        string oldLogPath = fullLogPath + ".old";
                        if (File.Exists(oldLogPath)) File.Delete(oldLogPath);
                        File.Move(fullLogPath, oldLogPath);
                    }

                    fileStream = new FileStream(fullLogPath, (File.Exists(fullLogPath) ? FileMode.Append : FileMode.Create), FileAccess.Write, FileShare.ReadWrite);
                    using (StreamWriter writer = new StreamWriter(fileStream))
                    {
                        fileStream = null;
                        writer.WriteLine(string.Format("{0:MM/dd/yyyy HH:mm:ss.f}\t{1}", DateTime.Now, message));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to write to log file: {ex.Message}");
                }
                finally
                {
                    fileStream?.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// The type of error to log
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// A message containing information only.
        /// </summary>
        Info = 0,
        /// <summary>
        /// A non-critical warning error message.
        /// </summary>
        Warning = 1,
        /// <summary>
        /// A fatal error message.
        /// </summary>
        Error = 2,
        /// <summary>
        /// Authentication message, MUST be logged. As verbosity level: only authentication messages and errors
        /// </summary>
        Authentication = 3,
    }
}