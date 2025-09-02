/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Windows service installer
 *
 */

using System;
using System.ServiceProcess;
using System.Collections;
using System.Configuration.Install;

namespace TinyOPDS
{
    /// <summary>
    /// Windows service installer using ServiceController
    /// </summary>
    public class WindowsServiceInstaller : ServiceInstallerBase
    {
        public WindowsServiceInstaller(string serviceName, string displayName, string executablePath, string description)
            : base(serviceName, displayName, executablePath, description)
        {
        }

        public override void Install()
        {
            if (!IsElevated())
            {
                throw new UnauthorizedAccessException("Administrator privileges required to install service");
            }

            if (IsInstalled())
            {
                throw new InvalidOperationException("Service is already installed");
            }

            // Use ManagedInstallerClass to install the service
            IDictionary state = new Hashtable();
            try
            {
                using (AssemblyInstaller installer = new AssemblyInstaller(ExecutablePath, new string[] { }))
                {
                    installer.UseNewContext = true;
                    installer.Install(state);
                    installer.Commit(state);
                }

                // Set service description
                using (ServiceController sc = new ServiceController(ServiceName))
                {
                    SetServiceDescription(ServiceName, Description);
                }

                Log.WriteLine("Service {0} installed successfully", ServiceName);
            }
            catch (Exception ex)
            {
                // Try to rollback on error
                try
                {
                    using (AssemblyInstaller installer = new AssemblyInstaller(ExecutablePath, new string[] { }))
                    {
                        installer.UseNewContext = true;
                        installer.Rollback(state);
                    }
                }
                catch { }

                throw new InvalidOperationException($"Failed to install service: {ex.Message}", ex);
            }
        }

        public override void Uninstall()
        {
            if (!IsElevated())
            {
                throw new UnauthorizedAccessException("Administrator privileges required to uninstall service");
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

            // Use ManagedInstallerClass to uninstall the service
            try
            {
                using (AssemblyInstaller installer = new AssemblyInstaller(ExecutablePath, new string[] { }))
                {
                    installer.UseNewContext = true;
                    installer.Uninstall(null);
                }

                Log.WriteLine("Service {0} uninstalled successfully", ServiceName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to uninstall service: {ex.Message}", ex);
            }
        }

        public override void Start()
        {
            if (!IsInstalled())
            {
                throw new InvalidOperationException("Service is not installed");
            }

            using (ServiceController sc = new ServiceController(ServiceName))
            {
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    throw new InvalidOperationException("Service is already running");
                }

                try
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    Log.WriteLine("Service {0} started", ServiceName);
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                    throw new InvalidOperationException("Service failed to start within timeout period");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to start service: {ex.Message}", ex);
                }
            }
        }

        public override void Stop()
        {
            if (!IsInstalled())
            {
                throw new InvalidOperationException("Service is not installed");
            }

            using (ServiceController sc = new ServiceController(ServiceName))
            {
                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    throw new InvalidOperationException("Service is not running");
                }

                try
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    Log.WriteLine("Service {0} stopped", ServiceName);
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                    throw new InvalidOperationException("Service failed to stop within timeout period");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to stop service: {ex.Message}", ex);
                }
            }
        }

        public override bool IsInstalled()
        {
            ServiceController[] services = ServiceController.GetServices();
            foreach (ServiceController service in services)
            {
                if (service.ServiceName.Equals(ServiceName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public override bool IsRunning()
        {
            if (!IsInstalled())
            {
                return false;
            }

            using (ServiceController sc = new ServiceController(ServiceName))
            {
                return sc.Status == ServiceControllerStatus.Running;
            }
        }

        /// <summary>
        /// Set service description using sc.exe command
        /// </summary>
        private void SetServiceDescription(string serviceName, string description)
        {
            try
            {
                ExecuteCommand("sc.exe", $"description \"{serviceName}\" \"{description}\"", true);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Failed to set service description: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Alternative install method using sc.exe (fallback)
        /// </summary>
        public void InstallUsingSC()
        {
            if (!IsElevated())
            {
                throw new UnauthorizedAccessException("Administrator privileges required to install service");
            }

            if (IsInstalled())
            {
                throw new InvalidOperationException("Service is already installed");
            }

            // Create service using sc.exe
            string createCmd = $"create \"{ServiceName}\" binPath= \"\\\"{ExecutablePath}\\\"\" " +
                             $"DisplayName= \"{DisplayName}\" start= auto";

            var result = ExecuteCommand("sc.exe", createCmd, true);

            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to create service: {result.Error}");
            }

            // Set description
            SetServiceDescription(ServiceName, Description);

            // Set failure actions (restart on failure)
            string failureCmd = $"failure \"{ServiceName}\" reset= 86400 actions= restart/60000/restart/60000//";
            ExecuteCommand("sc.exe", failureCmd, true);

            Log.WriteLine("Service {0} installed successfully using SC", ServiceName);
        }

        /// <summary>
        /// Alternative uninstall method using sc.exe (fallback)
        /// </summary>
        public void UninstallUsingSC()
        {
            if (!IsElevated())
            {
                throw new UnauthorizedAccessException("Administrator privileges required to uninstall service");
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

            // Delete service using sc.exe
            var result = ExecuteCommand("sc.exe", $"delete \"{ServiceName}\"", true);

            if (!result.Success)
            {
                throw new InvalidOperationException($"Failed to delete service: {result.Error}");
            }

            Log.WriteLine("Service {0} uninstalled successfully using SC", ServiceName);
        }
    }
}