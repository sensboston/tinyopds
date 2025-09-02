/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Simple logging solution, w/o third party
 *
 */

using System;
using System.IO;
using System.Diagnostics;

namespace TinyOPDS
{
    /// <summary>
    /// A lightweight logging class for Silverlight.
    /// </summary>
    public static class Log
    {
        public static string LogFileName = "TinyOPDS.log";

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
                LogFileName = GetLogPath();
                saveToFile = value;
            }
        }

        /// <summary>
        /// Determine log file path based on execution context
        /// </summary>
        /// <returns>Full path to log file</returns>
        private static string GetLogPath()
        {
            string logFileName = "TinyOPDS.log";

            // If running as service on Unix/Linux, try system log directory
            if (IsRunningAsService)
            {
                bool isRunningOnMono = Type.GetType("Mono.Runtime") != null;
                if (isRunningOnMono)
                {
                    PlatformID platform = Environment.OSVersion.Platform;
                    if (platform == PlatformID.Unix || platform == PlatformID.MacOSX || (int)platform == 128)
                    {
                        // Try /var/log/tinyopds/ for service mode
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
                            // Fall back to application directory
                            Debug.WriteLine("Cannot write to /var/log/tinyopds/, falling back to application directory");
                        }
                    }
                }
            }

            // Default: use application directory (portable mode)
            // This covers: Windows, GUI mode, CLI user mode, and fallback for service mode
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
            if (level >= VerbosityLevel || level == LogLevel.Authentication)
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

        private static object fileSyncObject = new object();

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
                    fileStream = new FileStream(LogFileName, (File.Exists(LogFileName) ? FileMode.Append : FileMode.Create), FileAccess.Write, FileShare.ReadWrite);
                    using (StreamWriter writer = new StreamWriter(fileStream))
                    {
                        fileStream = null;
                        writer.WriteLine(string.Format("{0:MM/dd/yyyy HH:mm:ss.f}\t{1}", DateTime.Now, message), LogFileName);
                    }
                }
                finally
                {
                    if (fileStream != null) fileStream.Dispose();
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
        /// Authentication message, MUST be logged.
        /// </summary>
        Authentication = 3,
    }
}