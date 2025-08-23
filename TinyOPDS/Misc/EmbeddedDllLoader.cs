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
        private static readonly Dictionary<string, Assembly> _loadedAssemblies = new Dictionary<string, Assembly>();
        private static readonly Dictionary<string, string> _extractedNativeDlls = new Dictionary<string, string>();
        private static readonly object _lockObject = new object();
        private static bool _isInitialized = false;

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
            if (_isInitialized) return;

            lock (_lockObject)
            {
                if (_isInitialized) return;

                try
                {
                    // Hook into AssemblyResolve event for managed DLLs
                    AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

                    Log.WriteLine("EmbeddedDllLoader initialized");
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Failed to initialize EmbeddedDllLoader: {0}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Event handler for resolving managed assemblies from embedded resources
        /// </summary>
        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                // Get the assembly name without version info
                string assemblyName = new AssemblyName(args.Name).Name;

                Log.WriteLine("Attempting to resolve assembly: {0}", assemblyName);

                // Check if already loaded
                if (_loadedAssemblies.ContainsKey(assemblyName))
                {
                    Log.WriteLine("Assembly {0} already loaded from cache", assemblyName);
                    return _loadedAssemblies[assemblyName];
                }

                // Try to load from embedded resources
                Assembly assembly = LoadManagedAssembly(assemblyName);
                if (assembly != null)
                {
                    _loadedAssemblies[assemblyName] = assembly;
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
                // Try different resource name patterns
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
                // Check if already loaded
                if (_extractedNativeDlls.ContainsKey(dllName))
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

                // Load the native library
                IntPtr handle = LoadNativeLibrary(extractedPath);
                if (handle == IntPtr.Zero)
                {
                    Log.WriteLine(LogLevel.Error, "Failed to load native DLL: {0}", extractedPath);
                    return false;
                }

                _extractedNativeDlls[dllName] = extractedPath;
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
                // Determine platform-specific resource path
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

                    // Create temp directory for native DLLs
                    string tempDir = Path.Combine(Path.GetTempPath(), "TinyOPDS_Native");
                    if (!Directory.Exists(tempDir))
                    {
                        Directory.CreateDirectory(tempDir);
                    }

                    // Extract to temp file
                    string extractedPath = Path.Combine(tempDir, dllName);

                    // Don't extract if file already exists and has correct size
                    if (File.Exists(extractedPath) && new FileInfo(extractedPath).Length == stream.Length)
                    {
                        Log.WriteLine("Native DLL already extracted: {0}", extractedPath);
                        return extractedPath;
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
                string[] managedDlls = {
                    "eBdb.EpubReader",
                    "FB2Library",
                    "Ionic.Zip.Reduced",
                    "System.Data.SQLite"
                };

                foreach (string dllName in managedDlls)
                {
                    Assembly assembly = LoadManagedAssembly(dllName);
                    if (assembly != null)
                    {
                        _loadedAssemblies[dllName] = assembly;
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
            // Load SQLite native DLL
            LoadNativeDll("SQLite.Interop.dll");
        }

        /// <summary>
        /// Get information about loaded assemblies (for debugging)
        /// </summary>
        public static string GetLoadedAssembliesInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("Loaded managed assemblies:");
            foreach (var kvp in _loadedAssemblies)
            {
                info.AppendLine($"  {kvp.Key} -> {kvp.Value.FullName}");
            }

            info.AppendLine("Extracted native DLLs:");
            foreach (var kvp in _extractedNativeDlls)
            {
                info.AppendLine($"  {kvp.Key} -> {kvp.Value}");
            }

            return info.ToString();
        }
    }
}