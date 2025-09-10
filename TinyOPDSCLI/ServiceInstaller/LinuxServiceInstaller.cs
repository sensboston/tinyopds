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
        // Effective user to run the service under (SUDO_USER or Environment.UserName)
        private readonly string effectiveUser;

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

            effectiveUser = GetEffectiveUser();
            Log.WriteLine("Detected init system: {0}", initSystem);
            Log.WriteLine("Service will be installed for user: {0}", effectiveUser);
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

        /// <summary>
        /// Determine the non-root login user who invoked the installer.
        /// If running under sudo, prefer SUDO_USER; otherwise Environment.UserName.
        /// </summary>
        private static string GetEffectiveUser()
        {
            var sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");
            if (!string.IsNullOrEmpty(sudoUser))
                return sudoUser;
            return Environment.UserName;
        }

        public override void Install()
        {
            if (!IsElevated())
            {
                throw new UnauthorizedAccessException("Root privileges required to install service");
            }

            if (initSystem == InitSystem.Unknown)
            {
                throw new NotSupportedException("Unsupported Linux init system");
            }

            if (IsInstalled())
            {
                throw new InvalidOperationException("Service is already installed");
            }

            // Create service user only if using the dedicated service account
            if (effectiveUser == SERVICE_USER)
                CreateServiceUser();

            // Create necessary directories
            string dataDir = "/var/lib/tinyopds";
            string logDir = "/var/log/tinyopds";
            CreateDirectoryWithPermissions(dataDir, effectiveUser);
            CreateDirectoryWithPermissions(logDir, effectiveUser);

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
            unitFile.AppendLine($"User={effectiveUser}");
            unitFile.AppendLine($"Group={effectiveUser}");
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

            // Reload systemd daemon and enable the service
            ExecuteCommand("systemctl", "daemon-reload", true);
            ExecuteCommand("systemctl", $"enable {ServiceName.ToLower()}.service", true);
        }

        private void InstallInitDService()
        {
            // Create init.d script
            var script = new StringBuilder();
            script.AppendLine("#!/bin/sh");
            script.AppendLine($"### BEGIN INIT INFO");
            script.AppendLine($"# Provides:          {ServiceName.ToLower()}");
            script.AppendLine($"# Required-Start:    $remote_fs $syslog $network");
            script.AppendLine($"# Required-Stop:     $remote_fs $syslog $network");
            script.AppendLine($"# Default-Start:     2 3 4 5");
            script.AppendLine($"# Default-Stop:      0 1 6");
            script.AppendLine($"# Short-Description: {Description}");
            script.AppendLine($"### END INIT INFO");
            script.AppendLine();
            script.AppendLine($"NAME={ServiceName.ToLower()}");
            script.AppendLine($"DESC=\"{Description}\"");
            script.AppendLine($"WORKDIR=\"{WorkingDirectory}\"");
            script.AppendLine($"DAEMON=/usr/bin/mono");
            script.AppendLine($"DAEMON_ARGS=\"{ExecutablePath} start\"");
            script.AppendLine($"PIDFILE=/var/run/$NAME.pid");
            script.AppendLine($"USER={effectiveUser}");
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
            script.AppendLine("exit 0");

            // Write init.d script
            File.WriteAllText(servicePath, script.ToString());

            // Make executable and enable on boot
            ExecuteCommand("chmod", $"+x {servicePath}", true);
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

            if (initSystem == InitSystem.Systemd)
            {
                ExecuteCommand("systemctl", $"disable {ServiceName.ToLower()}.service", true);
                if (File.Exists(servicePath))
                {
                    File.Delete(servicePath);
                }
                ExecuteCommand("systemctl", "daemon-reload", true);
            }
            else
            {
                if (File.Exists(servicePath))
                {
                    ExecuteCommand("update-rc.d", $"{ServiceName.ToLower()} remove", true);
                    File.Delete(servicePath);
                }
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
                Log.WriteLine("Service is not running");
                return;
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
            if (initSystem == InitSystem.Systemd)
            {
                return File.Exists(servicePath);
            }
            else
            {
                return File.Exists(servicePath);
            }
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
