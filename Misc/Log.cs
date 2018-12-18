/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines the Log class
 * 
 * TODO: add threading for performance reason (may be, should check)
 * 
 ************************************************************/

using System;
using System.IO;
using System.Diagnostics;

namespace TinyOPDS
{
    /// <summary>
    /// A lightweight logging class for Silverlight.
    /// </summary>
    internal static class Log
    {
        private static string _logFileName = "TinyOPDS.log";

        /// <summary>
        /// 
        /// </summary>
        internal static LogLevel VerbosityLevel = LogLevel.Info;

        /// <summary>
        /// Enable or disable logging to file
        /// </summary>
        private static bool _saveToFile = false;
        internal static bool SaveToFile
        {
            get { return _saveToFile; }
            set 
            {
                _logFileName = Path.Combine(Utils.ServiceFilesLocation, "TinyOPDS.log");
                _saveToFile = value;
            }
        }

        /// <summary>
        /// Writes the args to the default logging output using the format provided.
        /// </summary>
        internal static void WriteLine(string format, params object[] args)
        {
            WriteLine(LogLevel.Info, format, args);
        }

        /// <summary>
        /// Writes the args to the default logging output using the format provided.
        /// </summary>
        internal static void WriteLine(LogLevel level, string format, params object[] args)
        {
            if (level >= VerbosityLevel)
            {
                string caller = new StackTrace().GetFrame(2).GetMethod().ReflectedType.Name;
                if (caller.StartsWith("<>")) caller = new StackTrace().GetFrame(1).GetMethod().ReflectedType.Name;
                string prefix = string.Format("{0}\t{1}\t{2}", (level == LogLevel.Info) ? 'I' : ((level == LogLevel.Warning) ? 'W' : 'E'), caller, (caller.Length > 7 ? "" : "\t"));

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
                    fileStream = new FileStream(_logFileName, (File.Exists(_logFileName) ? FileMode.Append : FileMode.Create), FileAccess.Write, FileShare.ReadWrite);
                    using (StreamWriter writer = new StreamWriter(fileStream))
                    {
                        fileStream = null;
                        writer.WriteLine(string.Format("{0:MM/dd/yyyy HH:mm:ss.f}\t{1}", DateTime.Now, message), _logFileName);
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
        Error = 2
    }
}