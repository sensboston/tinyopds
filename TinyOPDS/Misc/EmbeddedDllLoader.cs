/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Embedded DLL loader for portable applications
 *
 */

using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TinyOPDS
{
    /// <summary>
    /// Loads managed and native DLLs from embedded resources for portable applications
    /// </summary>
    public static class EmbeddedDllLoader
    {
        private static readonly Dictionary<string, Assembly> loadedAssemblies = new Dictionary<string, Assembly>();
        private static readonly Dictionary<string, string> extractedNativeDlls = new Dictionary<string, string>();
        private static readonly object lockObject = new object();
        private static bool isInitialized = false;

        private static Assembly linuxSqliteAssembly;

        // P/Invoke declarations for LoadLibrary (Windows)
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        // Linux equivalent
        [DllImport("libdl.so.2", SetLastError = true)]
        private static extern IntPtr dlopen_linux(string filename, int flags);

        [DllImport("libdl.so.2", SetLastError = true)]
        private static extern IntPtr dlsym_linux(IntPtr handle, string symbol);

        [DllImport("libdl.so.2", SetLastError = true)]
        private static extern int dlclose_linux(IntPtr handle);

        // macOS equivalent
        [DllImport("libdl.dylib", EntryPoint = "dlopen", SetLastError = true)]
        private static extern IntPtr dlopen_macos(string filename, int flags);

        [DllImport("libdl.dylib", EntryPoint = "dlsym", SetLastError = true)]
        private static extern IntPtr dlsym_macos(IntPtr handle, string symbol);

        [DllImport("libdl.dylib", EntryPoint = "dlclose", SetLastError = true)]
        private static extern int dlclose_macos(IntPtr handle);

        private const int RTLD_NOW = 2;
        private const int RTLD_GLOBAL = 8;  // Make symbols available globally

        /// <summary>
        /// Initialize the embedded DLL loader. Call this once at application startup.
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized) return;

            lock (lockObject)
            {
                if (isInitialized) return;

                try
                {
                    // Preload Homebrew SQLite on macOS before loading Mono.Data.Sqlite
                    // This prevents crashes with system SQLite
                    if (Utils.IsMacOS)
                    {
                        PreloadHomebrewSqlite();
                    }

                    // Set up assembly resolver for Windows only
                    // Unix systems will use Mono.Data.Sqlite directly
                    if (!Utils.IsLinux && !Utils.IsMacOS)
                    {
                        // Load native DLLs first
                        PreloadNativeDlls();

                        // Then set up assembly resolver
                        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
                    }

                    // Load Mono.Data.Sqlite on Unix systems (Linux and macOS)
                    if (Utils.IsLinux || Utils.IsMacOS)
                    {
                        LoadUnixSqlite();
                    }

                    Log.WriteLine("EmbeddedDllLoader initialized");
                    isInitialized = true;
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Failed to initialize EmbeddedDllLoader: {0}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Preload Homebrew SQLite on macOS to avoid crashes with system SQLite
        /// </summary>
        private static void PreloadHomebrewSqlite()
        {
            string[] possiblePaths = new string[] {
                "/usr/local/opt/sqlite/lib/libsqlite3.dylib",      // Intel Homebrew
                "/opt/homebrew/opt/sqlite/lib/libsqlite3.dylib",   // ARM Homebrew (M1/M2)
                "/usr/local/lib/libsqlite3.dylib"                  // Alternative location
            };

            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        // Use RTLD_GLOBAL to make symbols available to subsequently loaded libraries
                        IntPtr handle = dlopen_macos(path, RTLD_NOW | RTLD_GLOBAL);
                        if (handle != IntPtr.Zero)
                        {
                            Log.WriteLine("Preloaded Homebrew SQLite from: {0}", path);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine(LogLevel.Warning, "Failed to preload SQLite from {0}: {1}", path, ex.Message);
                    }
                }
            }

            Log.WriteLine(LogLevel.Warning, "Homebrew SQLite not found, using system SQLite (may cause crashes)");
        }

        /// <summary>
        /// Load Mono.Data.Sqlite assembly on Unix systems (Linux/macOS)
        /// </summary>
        private static void LoadUnixSqlite()
        {
            try
            {
                // First try to load from GAC
                try
                {
                    linuxSqliteAssembly = Assembly.Load("Mono.Data.Sqlite");
                    Log.WriteLine("Loaded Mono.Data.Sqlite from GAC");
                    return;
                }
                catch
                {
                    // GAC load failed, try file paths
                }

                // Platform-specific paths
                string[] possiblePaths;

                if (Utils.IsMacOS)
                {
                    possiblePaths = new string[] {
                        "/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/4.5/Mono.Data.Sqlite.dll",
                        "/usr/local/lib/mono/4.5/Mono.Data.Sqlite.dll",
                        "/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/gac/Mono.Data.Sqlite/4.0.0.0__0738eb9f132ed756/Mono.Data.Sqlite.dll",
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mono.Data.Sqlite.dll")
                    };
                }
                else // Linux
                {
                    possiblePaths = new string[] {
                        "/usr/lib/mono/4.5/Mono.Data.Sqlite.dll",
                        "/usr/lib/mono/gac/Mono.Data.Sqlite/4.0.0.0__0738eb9f132ed756/Mono.Data.Sqlite.dll",
                        "/usr/share/dotnet/shared/Mono.Data.Sqlite/*/Mono.Data.Sqlite.dll",
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mono.Data.Sqlite.dll")
                    };
                }

                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        linuxSqliteAssembly = Assembly.LoadFrom(path);
                        Log.WriteLine("Loaded Mono.Data.Sqlite from: {0}", path);
                        return;
                    }
                }

                Log.WriteLine(LogLevel.Warning, "Mono.Data.Sqlite not found in standard locations");

                // Try to find it using wildcard for Linux dotnet paths
                if (!Utils.IsMacOS)
                {
                    string dotnetPath = "/usr/share/dotnet/shared/Mono.Data.Sqlite";
                    if (Directory.Exists(dotnetPath))
                    {
                        var dirs = Directory.GetDirectories(dotnetPath);
                        foreach (var dir in dirs)
                        {
                            string dllPath = Path.Combine(dir, "Mono.Data.Sqlite.dll");
                            if (File.Exists(dllPath))
                            {
                                linuxSqliteAssembly = Assembly.LoadFrom(dllPath);
                                Log.WriteLine("Loaded Mono.Data.Sqlite from: {0}", dllPath);
                                return;
                            }
                        }
                    }
                }

                Log.WriteLine(LogLevel.Warning, "Mono.Data.Sqlite not found, SQLite operations may fail");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error loading Mono.Data.Sqlite: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Get Unix SQLite assembly for factory
        /// </summary>
        public static Assembly GetLinuxSqliteAssembly()
        {
            return linuxSqliteAssembly;
        }

        /// <summary>
        /// Event handler for resolving managed assemblies from embedded resources
        /// Windows only - Unix systems use Mono.Data.Sqlite
        /// </summary>
        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string assemblyName = new AssemblyName(args.Name).Name;

                // Skip resource assemblies to avoid loading localization files
                if (assemblyName.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                // Skip system assemblies but allow System.Data.SQLite on Windows only
                if ((assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) &&
                     !assemblyName.Equals("System.Data.SQLite", StringComparison.OrdinalIgnoreCase)) ||
                    assemblyName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                    assemblyName.Equals("mscorlib", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                Log.WriteLine("Attempting to resolve assembly: {0}", assemblyName);

                if (loadedAssemblies.ContainsKey(assemblyName))
                {
                    Log.WriteLine("Assembly {0} already loaded from cache", assemblyName);
                    return loadedAssemblies[assemblyName];
                }

                Assembly assembly = LoadManagedAssembly(assemblyName);
                if (assembly != null)
                {
                    loadedAssemblies[assemblyName] = assembly;
                    Log.WriteLine("Successfully loaded assembly {0} from embedded resources", assemblyName);
                    return assembly;
                }

                Log.WriteLine("Could not resolve assembly: {0}", assemblyName);
                return null;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error resolving assembly {0}: {1}", args.Name, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Load a managed assembly from embedded resources
        /// </summary>
        private static Assembly LoadManagedAssembly(string assemblyName)
        {
            try
            {
                string[] resourcePatterns = {
                    "TinyOPDS.Libs." + assemblyName + ".dll",
                    "TinyOPDSCLI.Libs." + assemblyName + ".dll",
                    Assembly.GetExecutingAssembly().GetName().Name + ".Libs." + assemblyName + ".dll"
                };

                Assembly executingAssembly = Assembly.GetExecutingAssembly();

                foreach (string resourceName in resourcePatterns)
                {
                    using (Stream stream = executingAssembly.GetManifestResourceStream(resourceName))
                    {
                        if (stream != null && stream.Length > 0)
                        {
                            Log.WriteLine("Found embedded resource: {0}", resourceName);

                            byte[] assemblyData = new byte[stream.Length];
                            stream.Read(assemblyData, 0, assemblyData.Length);

                            return Assembly.Load(assemblyData);
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error loading managed assembly {0}: {1}", assemblyName, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Extract and load a native DLL for the current platform
        /// Call this manually for native DLLs like SQLite.Interop.dll
        /// </summary>
        /// <param name="dllName">Name of the DLL (e.g., "SQLite.Interop.dll")</param>
        /// <returns>True if successfully loaded</returns>
        public static bool LoadNativeDll(string dllName)
        {
            try
            {
                // Skip native DLL loading on Unix systems completely
                // Unix systems (Linux/macOS) use system SQLite through Mono.Data.Sqlite
                if (Utils.IsLinux || Utils.IsMacOS)
                {
                    Log.WriteLine("Skipping native DLL loading on Unix system");
                    return true;
                }

                if (extractedNativeDlls.ContainsKey(dllName))
                {
                    Log.WriteLine("Native DLL {0} already loaded", dllName);
                    return true;
                }

                string extractedPath = ExtractNativeDll(dllName);
                if (string.IsNullOrEmpty(extractedPath))
                {
                    Log.WriteLine(LogLevel.Warning, "Could not extract native DLL: {0}", dllName);
                    return false;
                }

                IntPtr handle = LoadNativeLibrary(extractedPath);
                if (handle == IntPtr.Zero)
                {
                    Log.WriteLine(LogLevel.Error, "Failed to load native DLL: {0}", extractedPath);
                    return false;
                }

                extractedNativeDlls[dllName] = extractedPath;
                Log.WriteLine("Successfully loaded native DLL: {0}", dllName);
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error loading native DLL {0}: {1}", dllName, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Extract native DLL from embedded resources to temporary location
        /// Windows only - FIXED to handle multi-instance scenarios
        /// </summary>
        private static string ExtractNativeDll(string dllName)
        {
            try
            {
                string architecture = Environment.Is64BitProcess ? "x64" : "x86";
                string resourceName = Assembly.GetExecutingAssembly().GetName().Name + ".Libs." + architecture + "." + dllName;

                Assembly executingAssembly = Assembly.GetExecutingAssembly();

                using (Stream stream = executingAssembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null || stream.Length == 0)
                    {
                        Log.WriteLine(LogLevel.Warning, "Native DLL resource not found: {0}", resourceName);
                        return null;
                    }

                    string tempDir = Path.Combine(Path.GetTempPath(), "TinyOPDS_Native");
                    if (!Directory.Exists(tempDir))
                    {
                        Directory.CreateDirectory(tempDir);
                    }

                    string extractedPath = Path.Combine(tempDir, dllName);

                    // Check if file already exists (from another instance)
                    if (File.Exists(extractedPath))
                    {
                        try
                        {
                            // Try to load the existing file to check if it's valid
                            IntPtr testHandle = LoadLibrary(extractedPath);
                            if (testHandle != IntPtr.Zero)
                            {
                                FreeLibrary(testHandle);
                                Log.WriteLine("Using existing native DLL: {0}", extractedPath);
                                return extractedPath;
                            }
                        }
                        catch
                        {
                            // If loading failed, try to delete and re-extract
                        }

                        // Try to delete the old file
                        try
                        {
                            File.Delete(extractedPath);
                        }
                        catch
                        {
                            Log.WriteLine("Native DLL is locked by another process, using existing: {0}", extractedPath);
                            return extractedPath;
                        }
                    }

                    // Extract new file
                    using (FileStream fileStream = File.Create(extractedPath))
                    {
                        stream.CopyTo(fileStream);
                    }

                    Log.WriteLine("Extracted native DLL to: {0}", extractedPath);
                    return extractedPath;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error extracting native DLL {0}: {1}", dllName, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Load native library using platform-specific API
        /// Windows only - Unix systems use system libraries
        /// </summary>
        private static IntPtr LoadNativeLibrary(string libraryPath)
        {
            if (Utils.IsMacOS)
            {
                // Should not reach here, but handle gracefully
                return IntPtr.Zero;
            }
            else if (Utils.IsLinux)
            {
                // Should not reach here, but handle gracefully
                return IntPtr.Zero;
            }
            else
            {
                // Windows
                return LoadLibrary(libraryPath);
            }
        }

        /// <summary>
        /// Preload all embedded managed DLLs (optional - use if you want to load all at startup)
        /// Windows only - Unix systems use Mono.Data.Sqlite
        /// </summary>
        public static void PreloadManagedAssemblies()
        {
            try
            {
                // Don't preload System.Data.SQLite on Unix systems - they use Mono.Data.Sqlite
                if (Utils.IsLinux || Utils.IsMacOS)
                {
                    Log.WriteLine("Skipping managed assembly preload on Unix system");
                    return;
                }

                string[] managedDlls = { "System.Data.SQLite" };

                foreach (string dllName in managedDlls)
                {
                    Assembly assembly = LoadManagedAssembly(dllName);
                    if (assembly != null)
                    {
                        loadedAssemblies[dllName] = assembly;
                        Log.WriteLine("Preloaded managed assembly: {0}", dllName);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error preloading managed assemblies: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Preload native DLLs (call this before using SQLite)
        /// Windows only - Unix systems use system SQLite
        /// </summary>
        public static void PreloadNativeDlls()
        {
            // Only load native DLLs on Windows
            // Unix systems (Linux/macOS) use system SQLite through Mono.Data.Sqlite
            if (Utils.IsLinux || Utils.IsMacOS)
            {
                Log.WriteLine("Skipping native DLL preload on Unix system");
                return;
            }

            LoadNativeDll("SQLite.Interop.dll");
        }

        /// <summary>
        /// Get information about loaded assemblies (for debugging)
        /// </summary>
        public static string GetLoadedAssembliesInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("Loaded managed assemblies:");
            foreach (var kvp in loadedAssemblies)
            {
                info.AppendLine("  " + kvp.Key + " -> " + kvp.Value.FullName);
            }

            info.AppendLine("Extracted native DLLs:");
            foreach (var kvp in extractedNativeDlls)
            {
                info.AppendLine("  " + kvp.Key + " -> " + kvp.Value);
            }

            if (linuxSqliteAssembly != null)
            {
                info.AppendLine("Unix SQLite assembly:");
                info.AppendLine("  " + linuxSqliteAssembly.FullName);
            }

            return info.ToString();
        }
    }
}