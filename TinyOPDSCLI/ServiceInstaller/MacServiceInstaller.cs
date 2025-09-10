/*
* This file is part of TinyOPDS server project
* https://github.com/sensboston/tinyopds
*
* Copyright (c) 2013-2025 SeNSSoFT
* SPDX-License-Identifier: MIT
*
* macOS service installer (launchd: LaunchDaemons / LaunchAgents)
* 
*/

using System;
using System.IO;
using System.Text;
using System.Xml;

namespace TinyOPDS
{
    /// <summary>
    /// macOS launchd installer with support for system daemons (root) and per-user agents (non-root).
    /// </summary>
    public class MacServiceInstaller : ServiceInstallerBase
    {
        private readonly string serviceDomain;
        private readonly string plistPath;
        private readonly bool isRoot;

        public MacServiceInstaller(string serviceName, string displayName, string executablePath, string description)
            : base(serviceName, displayName, executablePath, description)
        {
            // launchd label should be reverse-DNS-like and stable
            // Keep it predictable but avoid collisions with other vendors
            serviceDomain = $"com.senssoft.{ServiceName.ToLower()}";

            isRoot = IsElevated();

            // Choose destination according to privileges:
            // - root -> system-wide LaunchDaemon
            // - non-root -> per-user LaunchAgent
            if (isRoot)
            {
                plistPath = $"/Library/LaunchDaemons/{serviceDomain}.plist";
            }
            else
            {
                var agentsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                    "Library", "LaunchAgents");
                Directory.CreateDirectory(agentsDir);
                plistPath = Path.Combine(agentsDir, $"{serviceDomain}.plist");
            }

            Log.WriteLine("macOS installer: label={0}, plist={1}, isRoot={2}", serviceDomain, plistPath, isRoot);
        }

        public override void Install()
        {
            // For LaunchDaemons root is required; LaunchAgents can be installed without root
            if (!isRoot && plistPath.StartsWith("/Library/LaunchDaemons", StringComparison.Ordinal))
                throw new UnauthorizedAccessException("Root privileges required to install a system daemon");

            if (IsInstalled())
                throw new InvalidOperationException("Service is already installed");

            // Prepare data/log directories suitable for daemon vs agent
            string dataDir, logDir;
            if (isRoot)
            {
                dataDir = $"/usr/local/var/lib/{ServiceName.ToLower()}";
                logDir = $"/usr/local/var/log/{ServiceName.ToLower()}";
            }
            else
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                dataDir = Path.Combine(home, "Library", "Application Support", ServiceName);
                logDir = Path.Combine(home, "Library", "Logs", ServiceName);
            }

            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(logDir);

            // Generate launchd plist
            WritePlist(plistPath, dataDir, logDir);

            // Permissions and ownership
            // LaunchDaemons require root:wheel and 0644; LaunchAgents can be user-owned
            SetFilePermissions(plistPath, "644");
            if (isRoot && plistPath.StartsWith("/Library/LaunchDaemons", StringComparison.Ordinal))
                ExecuteCommand("chown", $"root:wheel {Escape(plistPath)}", true);

            // Load and enable the job
            if (isRoot)
            {
                // System-wide daemon
                // "-w true" permanently enables; "bootstrap" (>=10.13) is recommended, but "load" still works for compatibility
                var ld = ExecuteCommand("launchctl", $"load -w {Escape(plistPath)}", true);
                if (!ld.Success) throw new InvalidOperationException($"launchctl load failed: {ld.Error}");
            }
            else
            {
                // Per-user agent
                var ld = ExecuteCommand("launchctl", $"load -w {Escape(plistPath)}", false);
                if (!ld.Success)
                {
                    // Some shells require explicit domain when not in a GUI session; keep simple here
                    Log.WriteLine("Warning: launchctl load returned error (agent may start on next login): {0}", ld.Error);
                }
            }

            Log.WriteLine("Service {0} installed successfully (macOS)", ServiceName);
        }

        public override void Uninstall()
        {
            if (!IsInstalled())
                throw new InvalidOperationException("Service is not installed");

            // Unload job
            if (isRoot)
            {
                ExecuteCommand("launchctl", $"unload -w {Escape(plistPath)}", true);
            }
            else
            {
                ExecuteCommand("launchctl", $"unload -w {Escape(plistPath)}", false);
            }

            // Remove plist
            if (File.Exists(plistPath))
                File.Delete(plistPath);

            Log.WriteLine("Service {0} uninstalled successfully (macOS)", ServiceName);
        }

        public override void Start()
        {
            if (!IsInstalled())
                throw new InvalidOperationException("Service is not installed");

            ProcessResult res;
            if (isRoot)
            {
                res = ExecuteCommand("launchctl", $"start {serviceDomain}", true);
            }
            else
            {
                res = ExecuteCommand("launchctl", $"start {serviceDomain}", false);
            }

            if (!res.Success)
                throw new InvalidOperationException($"Failed to start service: {res.Error}");

            Log.WriteLine("Service {0} started (macOS)", ServiceName);
        }

        public override void Stop()
        {
            if (!IsInstalled())
                throw new InvalidOperationException("Service is not installed");

            ProcessResult res;
            if (isRoot)
            {
                res = ExecuteCommand("launchctl", $"stop {serviceDomain}", true);
            }
            else
            {
                res = ExecuteCommand("launchctl", $"stop {serviceDomain}", false);
            }

            if (!res.Success)
                throw new InvalidOperationException($"Failed to stop service: {res.Error}");

            Log.WriteLine("Service {0} stopped (macOS)", ServiceName);
        }

        public override bool IsInstalled()
        {
            return File.Exists(plistPath);
        }

        public override bool IsRunning()
        {
            // Basic probe: list services and look for our label
            // On modern macOS: `launchctl print system/<label>` for daemons, or `gui/<uid>/<label>` for agents.
            // Keep it simple and grep the output.
            var res = ExecuteCommand("launchctl", "list");
            if (!res.Success || string.IsNullOrEmpty(res.Output)) return false;
            return res.Output.IndexOf(serviceDomain, StringComparison.Ordinal) >= 0;
        }

        private void WritePlist(string path, string dataDir, string logDir)
        {
            // Build the plist via XmlWriter to avoid formatting issues
            using (var sw = new StreamWriter(path, false, new UTF8Encoding(false)))
            using (var xw = XmlWriter.Create(sw, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false }))
            {
                xw.WriteStartDocument();
                xw.WriteDocType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null);
                xw.WriteStartElement("plist");
                xw.WriteAttributeString("version", "1.0");

                xw.WriteStartElement("dict");

                // Label
                WriteKey(xw, "Label");
                WriteString(xw, serviceDomain);

                // ProgramArguments: use /usr/bin/env mono to avoid PATH problems
                WriteKey(xw, "ProgramArguments");
                xw.WriteStartElement("array");
                WriteString(xw, "/usr/bin/env");
                WriteString(xw, "mono");
                WriteString(xw, ExecutablePath);
                WriteString(xw, "start");
                xw.WriteEndElement(); // array

                // WorkingDirectory
                WriteKey(xw, "WorkingDirectory");
                WriteString(xw, WorkingDirectory);

                // EnvironmentVariables
                WriteKey(xw, "EnvironmentVariables");
                xw.WriteStartElement("dict");
                WriteKey(xw, "TINYOPDS_SERVICE");
                WriteString(xw, "1");
                // Ensure PATH includes common Homebrew prefixes and system paths
                WriteKey(xw, "PATH");
                WriteString(xw, "/opt/homebrew/bin:/usr/local/bin:/usr/bin:/bin");
                xw.WriteEndElement(); // dict

                // StandardOutPath / StandardErrorPath
                WriteKey(xw, "StandardOutPath");
                WriteString(xw, Path.Combine(logDir, "stdout.log"));
                WriteKey(xw, "StandardErrorPath");
                WriteString(xw, Path.Combine(logDir, "stderr.log"));

                // RunAtLoad
                WriteKey(xw, "RunAtLoad");
                xw.WriteElementString("true", string.Empty);

                // KeepAlive: restart on crash/abnormal exit
                WriteKey(xw, "KeepAlive");
                xw.WriteStartElement("dict");
                WriteKey(xw, "SuccessfulExit");
                xw.WriteElementString("false", string.Empty);
                xw.WriteEndElement(); // dict

                // ProcessType (optional): set to Background
                WriteKey(xw, "ProcessType");
                WriteString(xw, "Background");

                // ThrottleInterval (optional, seconds) to avoid crash loops
                WriteKey(xw, "ThrottleInterval");
                WriteInteger(xw, 5);

                xw.WriteEndElement(); // dict
                xw.WriteEndElement(); // plist
                xw.WriteEndDocument();
            }
        }

        private static void WriteKey(XmlWriter xw, string key)
        {
            xw.WriteElementString("key", key);
        }

        private static void WriteString(XmlWriter xw, string value)
        {
            xw.WriteElementString("string", value);
        }

        private static void WriteInteger(XmlWriter xw, int value)
        {
            xw.WriteElementString("integer", value.ToString());
        }

        private static string Escape(string path)
        {
            // Simple shell-escape for whitespace; launchctl tolerates quoted args
            if (string.IsNullOrEmpty(path)) return path;
            if (path.IndexOf(' ') >= 0 || path.IndexOf('(') >= 0 || path.IndexOf(')') >= 0)
                return $"\"{path.Replace("\"", "\\\"")}\"";
            return path;
        }
    }
}
