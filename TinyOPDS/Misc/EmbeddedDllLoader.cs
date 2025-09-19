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
    /// Works on Windows and Linux Mono without Costura.Fody
    /// </summary>
    public static class EmbeddedDllLoader
    {
        private static readonly Dictionary<string, Assembly> loadedAssemblies = new Dictionary<string, Assembly>();
        private static readonly Dictionary<string, string> extractedNativeDlls = new Dictionary<string, string>();
        private static readonly object lockObject = new object();
        private static bool isInitialized = false;

        private static Assembly linuxSqliteAssembly;

        // P/Invoke declarations for LoadLibrary
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        // Linux equivalent
        [DllImport("libdl.so.2", SetLastError = true)]
        private static extern IntPtr dlopen(string filename, int flags);

        [DllImport("libdl.so.2", SetLastError = true)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport("libdl.so.2", SetLastError = true)]
        private static extern int dlclose(IntPtr handle);

        private const int RTLD_NOW = 2;

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
                    AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

                    if (Utils.IsLinux)
                    {
                        LoadLinuxSqlite();
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
        /// Load Mono.Data.Sqlite assembly on Linux
        /// </summary>
        private static void LoadLinuxSqlite()
        {
            try
            {
                try
                {
                    linuxSqliteAssembly = Assembly.Load("Mono.Data.Sqlite");
                    Log.WriteLine("Loaded Mono.Data.Sqlite from GAC");
                    return;
                }
                catch { }

                string[] possiblePaths = {
                    "/usr/lib/mono/4.5/Mono.Data.Sqlite.dll",
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mono.Data.Sqlite.dll")
                };

                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        linuxSqliteAssembly = Assembly.LoadFrom(path);
                        Log.WriteLine("Loaded Mono.Data.Sqlite from: {0}", path);
                        return;
                    }
                }

                Log.WriteLine(LogLevel.Warning, "Mono.Data.Sqlite not found, will fall back to System.Data.SQLite");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error loading Mono.Data.Sqlite: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Get Linux SQLite assembly for factory
        /// </summary>
        public static Assembly GetLinuxSqliteAssembly()
        {
            return linuxSqliteAssembly;
        }

        /// <summary>
        /// Event handler for resolving managed assemblies from embedded resources
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

                // Skip system assemblies but allow System.Data.SQLite
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
                    $"TinyOPDS.Libs.{assemblyName}.dll",
                    $"TinyOPDSConsole.Libs.{assemblyName}.dll",
                    $"{Assembly.GetExecutingAssembly().GetName().Name}.Libs.{assemblyName}.dll"
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
        /// </summary>
        private static string ExtractNativeDll(string dllName)
        {
            try
            {
                string architecture = Environment.Is64BitProcess ? "x64" : "x86";
                string resourceName = $"{Assembly.GetExecutingAssembly().GetName().Name}.Libs.{architecture}.{dllName}";

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

                    // Always extract fresh copy to avoid version conflicts
                    if (File.Exists(extractedPath))
                    {
                        try
                        {
                            File.Delete(extractedPath);
                        }
                        catch (Exception deleteEx)
                        {
                            Log.WriteLine(LogLevel.Warning, "Could not delete existing native DLL: {0}", deleteEx.Message);
                        }
                    }

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
        /// </summary>
        private static IntPtr LoadNativeLibrary(string libraryPath)
        {
            if (Utils.IsLinux)
            {
                return dlopen(libraryPath, RTLD_NOW);
            }
            else
            {
                return LoadLibrary(libraryPath);
            }
        }

        /// <summary>
        /// Preload all embedded managed DLLs (optional - use if you want to load all at startup)
        /// </summary>
        public static void PreloadManagedAssemblies()
        {
            try
            {
                string[] managedDlls = {"System.Data.SQLite"};

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
        /// </summary>
        public static void PreloadNativeDlls()
        {
            if (!Utils.IsLinux)
            {
                LoadNativeDll("SQLite.Interop.dll");
            }
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
                info.AppendLine($"  {kvp.Key} -> {kvp.Value.FullName}");
            }

            info.AppendLine("Extracted native DLLs:");
            foreach (var kvp in extractedNativeDlls)
            {
                info.AppendLine($"  {kvp.Key} -> {kvp.Value}");
            }

            return info.ToString();
        }
    }
}