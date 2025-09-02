/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * macOS service installer (launchd support)
 *
 */

using System;
using System.IO;
using System.Text;
using System.Xml;

namespace TinyOPDS
{
    /// <summary>
    /// macOS service installer using launchd
    /// </summary>
    public class MacServiceInstaller : ServiceInstallerBase
    {
        private readonly string plistPath;
        private readonly string serviceDomain;

        public MacServiceInstaller(string serviceName, string displayName, string executablePath, string description)
            : base(serviceName, displayName, executablePath, description)
        {
            // Use reverse domain notation for macOS
            serviceDomain = $"com.senssoft.{ServiceName.ToLower()}";

            // LaunchDaemons for system-wide service (runs without user login)
            plistPath = $"/Library/LaunchDaemons/{serviceDomain}.plist";
        }

        public override void Install()
        {
            if (!IsElevated())
            {
                throw new UnauthorizedAccessException("Root privileges required to install service");
            }

            if (IsInstalled())
            {
                throw new InvalidOperationException("Service is already installed");
            }

            // Create necessary directories
            string dataDir = $"/usr/local/var/lib/{ServiceName.ToLower()}";
            string logDir = $"/usr/local/var/log/{ServiceName.ToLower()}";
            CreateDirectoryWithPermissions(dataDir);
            CreateDirectoryWithPermissions(logDir);

            // Create launchd plist file
            CreatePlistFile();

            // Load the service
            var result = ExecuteCommand("launchctl", $"load -w {plistPath}", true);
            if (!result.Success)
            {
                // Try to clean up if load failed
                File.Delete(plistPath);
                throw new InvalidOperationException($"Failed to load service: {result.Error}");
            }

            Log.WriteLine("Service {0} installed successfully", ServiceName);
        }

        private void CreatePlistFile()
        {
            // Create plist XML document
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "\t",
                Encoding = Encoding.UTF8
            };

            using (var writer = XmlWriter.Create(plistPath, settings))
            {
                writer.WriteStartDocument();
                writer.WriteDocType("plist", "-//Apple//DTD PLIST 1.0//EN",
                    "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null);

                writer.WriteStartElement("plist");
                writer.WriteAttributeString("version", "1.0");

                writer.WriteStartElement("dict");

                // Label (service identifier)
                writer.WriteElementString("key", "Label");
                writer.WriteElementString("string", serviceDomain);

                // Program arguments
                writer.WriteElementString("key", "ProgramArguments");
                writer.WriteStartElement("array");
                writer.WriteElementString("string", "/usr/local/bin/mono");
                writer.WriteElementString("string", ExecutablePath);
                writer.WriteElementString("string", "start");
                writer.WriteEndElement(); // array

                // Working directory
                writer.WriteElementString("key", "WorkingDirectory");
                writer.WriteElementString("string", WorkingDirectory);

                // Environment variables
                writer.WriteElementString("key", "EnvironmentVariables");
                writer.WriteStartElement("dict");
                writer.WriteElementString("key", "TINYOPDS_SERVICE");
                writer.WriteElementString("string", "1");
                writer.WriteEndElement(); // dict

                // Run at load (auto-start)
                writer.WriteElementString("key", "RunAtLoad");
                writer.WriteElementString("true", string.Empty);

                // Keep alive (restart if crashes)
                writer.WriteElementString("key", "KeepAlive");
                writer.WriteElementString("true", string.Empty);

                // Standard output path
                writer.WriteElementString("key", "StandardOutPath");
                writer.WriteElementString("string", $"/usr/local/var/log/{ServiceName.ToLower()}/stdout.log");

                // Standard error path
                writer.WriteElementString("key", "StandardErrorPath");
                writer.WriteElementString("string", $"/usr/local/var/log/{ServiceName.ToLower()}/stderr.log");

                // Throttle interval (wait 10 seconds before restart)
                writer.WriteElementString("key", "ThrottleInterval");
                writer.WriteElementString("integer", "10");

                writer.WriteEndElement(); // dict
                writer.WriteEndElement(); // plist
                writer.WriteEndDocument();
            }

            // Set proper permissions
            SetFilePermissions(plistPath, "644");
        }

        public override void Uninstall()
        {
            if (!IsElevated())
            {
                throw new UnauthorizedAccessException("Root privileges required to uninstall service");
            }

            if (!IsInstalled())
            {
                throw new InvalidOperationException("Service is not installed");
            }

            // Stop service if running
            if (IsRunning())
            {
                Stop();
            }

            // Unload the service
            ExecuteCommand("launchctl", $"unload -w {plistPath}", true);

            // Remove from launchd (for older macOS versions)
            ExecuteCommand("launchctl", $"remove {serviceDomain}", true);

            // Delete plist file
            File.Delete(plistPath);

            Log.WriteLine("Service {0} uninstalled successfully", ServiceName);
        }

        public override void Start()
        {
            if (!IsInstalled())
            {
                throw new InvalidOperationException("Service is not installed");
            }

            if (IsRunning())
            {
                throw new InvalidOperationException("Service is already running");
            }

            // Try modern launchctl command first (macOS 10.10+)
            var result = ExecuteCommand("launchctl", $"start {serviceDomain}", true);

            if (!result.Success)
            {
                // Fallback to load command
                result = ExecuteCommand("launchctl", $"load {plistPath}", true);

                if (!result.Success)
                {
                    throw new InvalidOperationException($"Failed to start service: {result.Error}");
                }
            }

            Log.WriteLine("Service {0} started", ServiceName);
        }

        public override void Stop()
        {
            if (!IsInstalled())
            {
                throw new InvalidOperationException("Service is not installed");
            }

            if (!IsRunning())
            {
                throw new InvalidOperationException("Service is not running");
            }

            // Try modern launchctl command first (macOS 10.10+)
            var result = ExecuteCommand("launchctl", $"stop {serviceDomain}", true);

            if (!result.Success)
            {
                // Fallback to unload command
                result = ExecuteCommand("launchctl", $"unload {plistPath}", true);

                if (!result.Success)
                {
                    throw new InvalidOperationException($"Failed to stop service: {result.Error}");
                }
            }

            Log.WriteLine("Service {0} stopped", ServiceName);
        }

        public override bool IsInstalled()
        {
            return File.Exists(plistPath);
        }

        public override bool IsRunning()
        {
            // Check service status with launchctl list
            var result = ExecuteCommand("launchctl", $"list {serviceDomain}");

            if (!result.Success)
            {
                return false;
            }

            // Parse output to check if running
            // Format: "PID Status Label"
            // If PID is "-" then service is not running
            string[] parts = result.Output.Trim().Split('\t');
            if (parts.Length > 0)
            {
                return parts[0] != "-" && !string.IsNullOrEmpty(parts[0]);
            }

            return false;
        }
    }
}