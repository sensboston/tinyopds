/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Linux service installer (systemd and init.d support)
 *
 */

using System;
using System.IO;
using System.Text;

namespace TinyOPDS
{
    /// <summary>
    /// Linux service installer with automatic detection of init system
    /// </summary>
    public class LinuxServiceInstaller : ServiceInstallerBase
    {
        private enum InitSystem
        {
            Systemd,
            InitD,
            Unknown
        }

        private readonly InitSystem initSystem;
        private readonly string servicePath;

        public LinuxServiceInstaller(string serviceName, string displayName, string executablePath, string description)
            : base(serviceName, displayName, executablePath, description)
        {
            // Detect init system
            initSystem = DetectInitSystem();

            // Set service file path based on init system
            switch (initSystem)
            {
                case InitSystem.Systemd:
                    servicePath = $"/etc/systemd/system/{ServiceName.ToLower()}.service";
                    break;
                case InitSystem.InitD:
                    servicePath = $"/etc/init.d/{ServiceName.ToLower()}";
                    break;
                default:
                    throw new NotSupportedException("Unable to detect Linux init system (systemd or init.d)");
            }

            Log.WriteLine("Detected init system: {0}", initSystem);
        }

        /// <summary>
        /// Detect which init system is available
        /// </summary>
        private InitSystem DetectInitSystem()
        {
            // Check for systemd first (most modern systems)
            var systemctl = ExecuteCommand("which", "systemctl");
            if (systemctl.Success && !string.IsNullOrEmpty(systemctl.Output))
            {
                // Double-check that systemd is actually running
                var systemdCheck = ExecuteCommand("systemctl", "is-system-running");
                if (systemdCheck.ExitCode != 127) // 127 = command not found
                {
                    return InitSystem.Systemd;
                }
            }

            // Check for init.d
            if (Directory.Exists("/etc/init.d"))
            {
                return InitSystem.InitD;
            }

            return InitSystem.Unknown;
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

            // Create service user
            CreateServiceUser();

            // Create necessary directories
            string dataDir = "/var/lib/tinyopds";
            string logDir = "/var/log/tinyopds";
            CreateDirectoryWithPermissions(dataDir, SERVICE_USER);
            CreateDirectoryWithPermissions(logDir, SERVICE_USER);

            // Install based on init system
            if (initSystem == InitSystem.Systemd)
            {
                InstallSystemdService();
            }
            else
            {
                InstallInitDService();
            }

            Log.WriteLine("Service {0} installed successfully", ServiceName);
        }

        private void InstallSystemdService()
        {
            // Create systemd unit file
            var unitFile = new StringBuilder();
            unitFile.AppendLine("[Unit]");
            unitFile.AppendLine($"Description={Description}");
            unitFile.AppendLine("After=network.target");
            unitFile.AppendLine();
            unitFile.AppendLine("[Service]");
            unitFile.AppendLine("Type=simple");
            unitFile.AppendLine($"User={SERVICE_USER}");
            unitFile.AppendLine($"Group={SERVICE_USER}");
            unitFile.AppendLine($"WorkingDirectory={WorkingDirectory}");

            // Set environment to indicate running as service
            unitFile.AppendLine("Environment=\"TINYOPDS_SERVICE=1\"");

            // Use mono to run the executable
            unitFile.AppendLine($"ExecStart=/usr/bin/mono \"{ExecutablePath}\" start");
            unitFile.AppendLine("Restart=always");
            unitFile.AppendLine("RestartSec=10");

            // Logging
            unitFile.AppendLine("StandardOutput=journal");
            unitFile.AppendLine("StandardError=journal");
            unitFile.AppendLine("SyslogIdentifier=tinyopds");

            unitFile.AppendLine();
            unitFile.AppendLine("[Install]");
            unitFile.AppendLine("WantedBy=multi-user.target");

            // Write unit file
            File.WriteAllText(servicePath, unitFile.ToString());
            SetFilePermissions(servicePath, "644");

            // Reload systemd daemon
            ExecuteCommand("systemctl", "daemon-reload", true);

            // Enable service for auto-start
            ExecuteCommand("systemctl", $"enable {ServiceName.ToLower()}.service", true);
        }

        private void InstallInitDService()
        {
            // Create init.d script
            var script = new StringBuilder();
            script.AppendLine("#!/bin/sh");
            script.AppendLine("### BEGIN INIT INFO");
            script.AppendLine($"# Provides:          {ServiceName.ToLower()}");
            script.AppendLine("# Required-Start:    $network $local_fs $remote_fs");
            script.AppendLine("# Required-Stop:     $network $local_fs $remote_fs");
            script.AppendLine("# Default-Start:     2 3 4 5");
            script.AppendLine("# Default-Stop:      0 1 6");
            script.AppendLine($"# Short-Description: {DisplayName}");
            script.AppendLine($"# Description:       {Description}");
            script.AppendLine("### END INIT INFO");
            script.AppendLine();

            script.AppendLine($"NAME={ServiceName.ToLower()}");
            script.AppendLine($"DESC=\"{DisplayName}\"");
            script.AppendLine($"DAEMON=/usr/bin/mono");
            script.AppendLine($"DAEMON_ARGS=\"{ExecutablePath} start\"");
            script.AppendLine($"PIDFILE=/var/run/$NAME.pid");
            script.AppendLine($"USER={SERVICE_USER}");
            script.AppendLine($"export TINYOPDS_SERVICE=1");
            script.AppendLine();

            script.AppendLine(". /lib/lsb/init-functions");
            script.AppendLine();

            script.AppendLine("case \"$1\" in");
            script.AppendLine("  start)");
            script.AppendLine("    log_daemon_msg \"Starting $DESC\" \"$NAME\"");
            script.AppendLine("    start-stop-daemon --start --quiet --background --make-pidfile \\");
            script.AppendLine("        --pidfile $PIDFILE --chuid $USER --exec $DAEMON -- $DAEMON_ARGS");
            script.AppendLine("    log_end_msg $?");
            script.AppendLine("    ;;");

            script.AppendLine("  stop)");
            script.AppendLine("    log_daemon_msg \"Stopping $DESC\" \"$NAME\"");
            script.AppendLine("    start-stop-daemon --stop --quiet --pidfile $PIDFILE --retry=TERM/30/KILL/5");
            script.AppendLine("    rm -f $PIDFILE");
            script.AppendLine("    log_end_msg $?");
            script.AppendLine("    ;;");

            script.AppendLine("  restart)");
            script.AppendLine("    $0 stop");
            script.AppendLine("    $0 start");
            script.AppendLine("    ;;");

            script.AppendLine("  status)");
            script.AppendLine("    status_of_proc -p $PIDFILE $DAEMON $NAME && exit 0 || exit $?");
            script.AppendLine("    ;;");

            script.AppendLine("  *)");
            script.AppendLine("    echo \"Usage: $0 {start|stop|restart|status}\"");
            script.AppendLine("    exit 1");
            script.AppendLine("    ;;");
            script.AppendLine("esac");
            script.AppendLine();
            script.AppendLine("exit 0");

            // Write init script
            File.WriteAllText(servicePath, script.ToString());
            SetFilePermissions(servicePath, "755");

            // Enable service for auto-start
            ExecuteCommand("update-rc.d", $"{ServiceName.ToLower()} defaults", true);
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

            if (initSystem == InitSystem.Systemd)
            {
                // Disable and remove systemd service
                ExecuteCommand("systemctl", $"disable {ServiceName.ToLower()}.service", true);
                File.Delete(servicePath);
                ExecuteCommand("systemctl", "daemon-reload", true);
            }
            else
            {
                // Remove init.d service
                ExecuteCommand("update-rc.d", $"-f {ServiceName.ToLower()} remove", true);
                File.Delete(servicePath);
            }

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

            ProcessResult result;
            if (initSystem == InitSystem.Systemd)
            {
                result = ExecuteCommand("systemctl", $"start {ServiceName.ToLower()}.service", true);
            }
            else
            {
                result = ExecuteCommand("service", $"{ServiceName.ToLower()} start", true);
            }

            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to start service: {result.Error}");
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

            ProcessResult result;
            if (initSystem == InitSystem.Systemd)
            {
                result = ExecuteCommand("systemctl", $"stop {ServiceName.ToLower()}.service", true);
            }
            else
            {
                result = ExecuteCommand("service", $"{ServiceName.ToLower()} stop", true);
            }

            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to stop service: {result.Error}");
            }

            Log.WriteLine("Service {0} stopped", ServiceName);
        }

        public override bool IsInstalled()
        {
            return File.Exists(servicePath);
        }

        public override bool IsRunning()
        {
            ProcessResult result;

            if (initSystem == InitSystem.Systemd)
            {
                result = ExecuteCommand("systemctl", $"is-active {ServiceName.ToLower()}.service");
                return result.Success && result.Output.Trim() == "active";
            }
            else
            {
                result = ExecuteCommand("service", $"{ServiceName.ToLower()} status");
                return result.Success;
            }
        }
    }
}