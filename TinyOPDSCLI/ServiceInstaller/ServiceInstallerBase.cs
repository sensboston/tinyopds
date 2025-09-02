/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Cross-platform service installer base class
 *
 */

using System;
using System.IO;
using System.Diagnostics;

namespace TinyOPDS
{
    /// <summary>
    /// Base class for platform-specific service installers
    /// </summary>
    public abstract class ServiceInstallerBase
    {
        protected string ServiceName { get; set; }
        protected string DisplayName { get; set; }
        protected string Description { get; set; }
        protected string ExecutablePath { get; set; }
        protected string WorkingDirectory { get; set; }

        // Common service user for Unix systems
        protected const string SERVICE_USER = "tinyopds";

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="serviceName">Internal service name</param>
        /// <param name="displayName">Display name for the service</param>
        /// <param name="executablePath">Path to the executable</param>
        /// <param name="description">Service description</param>
        protected ServiceInstallerBase(string serviceName, string displayName, string executablePath, string description)
        {
            ServiceName = serviceName;
            DisplayName = displayName;
            ExecutablePath = executablePath;
            Description = description;
            WorkingDirectory = Path.GetDirectoryName(executablePath);
        }

        /// <summary>
        /// Install the service
        /// </summary>
        public abstract void Install();

        /// <summary>
        /// Uninstall the service
        /// </summary>
        public abstract void Uninstall();

        /// <summary>
        /// Start the service
        /// </summary>
        public abstract void Start();

        /// <summary>
        /// Stop the service
        /// </summary>
        public abstract void Stop();

        /// <summary>
        /// Check if service is installed
        /// </summary>
        public abstract bool IsInstalled();

        /// <summary>
        /// Check if service is running
        /// </summary>
        public abstract bool IsRunning();

        /// <summary>
        /// Execute a shell command and return the result
        /// </summary>
        protected ProcessResult ExecuteCommand(string command, string arguments, bool requiresElevation = false)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                if (requiresElevation && !IsElevated())
                {
                    // For Unix systems, prepend sudo
                    if (Environment.OSVersion.Platform == PlatformID.Unix ||
                        Environment.OSVersion.Platform == PlatformID.MacOSX)
                    {
                        startInfo.FileName = "sudo";
                        startInfo.Arguments = $"{command} {arguments}";
                    }
                }

                using (var process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    return new ProcessResult
                    {
                        ExitCode = process.ExitCode,
                        Output = output,
                        Error = error,
                        Success = process.ExitCode == 0
                    };
                }
            }
            catch (Exception ex)
            {
                return new ProcessResult
                {
                    ExitCode = -1,
                    Error = ex.Message,
                    Success = false
                };
            }
        }

        /// <summary>
        /// Check if running with elevated privileges
        /// </summary>
        protected bool IsElevated()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return Utils.IsElevated;
            }
            else
            {
                // Check if running as root
                return Environment.UserName == "root";
            }
        }

        /// <summary>
        /// Create service user on Unix systems
        /// </summary>
        protected void CreateServiceUser()
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                // Check if user exists
                var checkUser = ExecuteCommand("id", SERVICE_USER);
                if (!checkUser.Success)
                {
                    // Create user without home directory and without shell
                    Log.WriteLine("Creating service user: {0}", SERVICE_USER);
                    var createUser = ExecuteCommand("useradd",
                        $"-r -s /bin/false -d /nonexistent -c \"TinyOPDS Service User\" {SERVICE_USER}",
                        true);

                    if (!createUser.Success)
                    {
                        Log.WriteLine(LogLevel.Warning, "Failed to create service user: {0}", createUser.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Set proper permissions for service files
        /// </summary>
        protected void SetFilePermissions(string filePath, string permissions = "755")
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix ||
                Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                ExecuteCommand("chmod", $"{permissions} {filePath}", true);
            }
        }

        /// <summary>
        /// Create directory with proper permissions
        /// </summary>
        protected void CreateDirectoryWithPermissions(string path, string owner = null)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            if (!string.IsNullOrEmpty(owner) &&
                (Environment.OSVersion.Platform == PlatformID.Unix ||
                 Environment.OSVersion.Platform == PlatformID.MacOSX))
            {
                ExecuteCommand("chown", $"-R {owner}:{owner} {path}", true);
            }
        }

        /// <summary>
        /// Result of a process execution
        /// </summary>
        protected class ProcessResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; }
            public string Error { get; set; }
            public bool Success { get; set; }
        }

        /// <summary>
        /// Factory method to create appropriate installer for current platform
        /// </summary>
        public static ServiceInstallerBase CreateInstaller(string serviceName, string displayName,
            string executablePath, string description)
        {
            PlatformID platform = Environment.OSVersion.Platform;

            switch (platform)
            {
                case PlatformID.Win32NT:
                    return new WindowsServiceInstaller(serviceName, displayName, executablePath, description);

                case PlatformID.Unix:
                case (PlatformID)128: // Old Mono detection for Unix
                    return new LinuxServiceInstaller(serviceName, displayName, executablePath, description);

                case PlatformID.MacOSX:
                    // Check if really macOS
                    if (Directory.Exists("/System/Library/CoreServices"))
                    {
                        return new MacServiceInstaller(serviceName, displayName, executablePath, description);
                    }
                    else
                    {
                        // Probably Linux with incorrect platform ID
                        return new LinuxServiceInstaller(serviceName, displayName, executablePath, description);
                    }

                default:
                    throw new PlatformNotSupportedException($"Platform {platform} is not supported");
            }
        }
    }
}