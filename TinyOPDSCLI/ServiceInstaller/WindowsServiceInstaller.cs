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
using System.Configuration.Install;
using System.ServiceProcess;
using System.Threading;
using TimeoutException = System.TimeoutException;

namespace TinyOPDS
{
    /// <summary>
    /// Windows service installer that uses SC.EXE by default.
    /// </summary>
    public class WindowsServiceInstaller : ServiceInstallerBase
    {
        // Tune these if needed
        private const string DefaultStartMode = "auto"; // auto | demand | disabled
        private const bool UseDelayedAutoStart = true;  // delayed start on boot
        private const int StartStopTimeoutMs = 90000;   // 90s total wait

        public WindowsServiceInstaller(string serviceName, string displayName, string executablePath, string description)
            : base(serviceName, displayName, executablePath, description)
        {
        }

        public override void Install()
        {
            if (!IsElevated())
                throw new UnauthorizedAccessException("Administrator privileges required to install service");

            if (IsInstalled())
                throw new InvalidOperationException("Service is already installed");

            // Create service via SC.EXE
            InstallUsingSC();

            Log.WriteLine("Service {0} installed successfully", ServiceName);
        }

        public override void Uninstall()
        {
            if (!IsElevated())
                throw new UnauthorizedAccessException("Administrator privileges required to uninstall service");

            if (!IsInstalled())
                throw new InvalidOperationException("Service is not installed");

            StopIfRunning();

            var result = ExecuteCommand("sc.exe", $"delete \"{ServiceName}\"", true);
            if (!result.Success)
                throw new InvalidOperationException($"Failed to delete service: {result.Error}");

            Log.WriteLine("Service {0} uninstalled successfully", ServiceName);
        }

        public override void Start()
        {
            if (!IsInstalled())
                throw new InvalidOperationException("Service is not installed");

            using (var sc = new ServiceController(ServiceName))
            {
                sc.Refresh();

                if (sc.Status == ServiceControllerStatus.Running)
                {
                    Log.WriteLine("Service is already running");
                    return;
                }

                if (sc.Status == ServiceControllerStatus.StartPending)
                {
                    WaitForStableState(sc, ServiceControllerStatus.Running, StartStopTimeoutMs);
                    return;
                }

                sc.Start();
                WaitForStableState(sc, ServiceControllerStatus.Running, StartStopTimeoutMs);
            }

            Log.WriteLine("Service {0} started", ServiceName);
        }

        public override void Stop()
        {
            if (!IsInstalled())
                throw new InvalidOperationException("Service is not installed");

            using (var sc = new ServiceController(ServiceName))
            {
                sc.Refresh();

                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    Log.WriteLine("Service is already stopped");
                    return;
                }

                if (sc.Status == ServiceControllerStatus.StopPending)
                {
                    WaitForStableState(sc, ServiceControllerStatus.Stopped, StartStopTimeoutMs);
                    return;
                }

                // Try to stop nicely
                try
                {
                    sc.Stop();
                }
                catch (InvalidOperationException)
                {
                    // Some services don't support Stop; fall back to SC stop
                    var res = ExecuteCommand("sc.exe", $"stop \"{ServiceName}\"", true);
                    if (!res.Success)
                        throw;
                }

                WaitForStableState(sc, ServiceControllerStatus.Stopped, StartStopTimeoutMs);
            }

            Log.WriteLine("Service {0} stopped", ServiceName);
        }

        public override bool IsInstalled()
        {
            try
            {
                using (var sc = new ServiceController(ServiceName))
                {
                    // Touching Status will throw if service does not exist
                    var s = sc.Status;
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        public override bool IsRunning()
        {
            try
            {
                using (var sc = new ServiceController(ServiceName))
                {
                    return sc.Status == ServiceControllerStatus.Running;
                }
            }
            catch
            {
                return false;
            }
        }

        private void InstallUsingSC()
        {
            // Quote path; arguments can be appended if your app needs a special switch.
            // If TinyOPDSCLI.exe contains proper Windows Service entry point, no args are required.
            var binPath = $"\"{ExecutablePath}\"";

            // Create service
            var create = ExecuteCommand(
                "sc.exe",
                $"create \"{ServiceName}\" binPath= {binPath} DisplayName= \"{DisplayName}\" start= {DefaultStartMode} obj= LocalSystem",
                true);

            if (!create.Success)
                throw new InvalidOperationException($"Failed to create service: {create.Error}");

            // Description
            var desc = ExecuteCommand("sc.exe", $"description \"{ServiceName}\" \"{Description}\"", true);
            if (!desc.Success)
                Log.WriteLine(LogLevel.Warning, "Unable to set description: {0}", desc.Error);

            // Delayed Auto Start
            if (UseDelayedAutoStart && DefaultStartMode == "auto")
            {
                var delayed = ExecuteCommand("sc.exe", $"config \"{ServiceName}\" start= delayed-auto", true);
                if (!delayed.Success)
                    Log.WriteLine(LogLevel.Warning, "Unable to set delayed-auto: {0}", delayed.Error);
            }

            // Recovery policy (restart on first/second failure, then longer delay)
            ExecuteCommand("sc.exe", $"failureflag \"{ServiceName}\" 1", true);
            ExecuteCommand(
                "sc.exe",
                $"failure \"{ServiceName}\" reset= 86400 actions= restart/60000/restart/60000/restart/600000",
                true);

            // Optional: set service type to own process explicitly
            ExecuteCommand("sc.exe", $"config \"{ServiceName}\" type= own", true);
        }

        // Optional path if you keep a ProjectInstaller inside the assembly
        private void InstallViaAssemblyInstaller()
        {
            using (var ti = new TransactedInstaller())
            using (var ai = new AssemblyInstaller(ExecutablePath, new string[0]))
            {
                ai.UseNewContext = true;
                ti.Installers.Add(ai);
                var state = new System.Collections.Hashtable();
                ti.Install(state);
            }
        }

        private void StopIfRunning()
        {
            try
            {
                using (var sc = new ServiceController(ServiceName))
                {
                    sc.Refresh();
                    if (sc.Status == ServiceControllerStatus.Running ||
                        sc.Status == ServiceControllerStatus.StartPending ||
                        sc.Status == ServiceControllerStatus.PausePending ||
                        sc.Status == ServiceControllerStatus.ContinuePending)
                    {
                        try { sc.Stop(); } catch { /* ignore */ }
                        WaitForStableState(sc, ServiceControllerStatus.Stopped, StartStopTimeoutMs);
                    }
                }
            }
            catch { /* ignore */ }
        }

        private static void WaitForStableState(ServiceController sc, ServiceControllerStatus desired, int timeoutMs)
        {
            var deadline = Environment.TickCount + timeoutMs;
            while (Environment.TickCount < deadline)
            {
                sc.Refresh();

                if (sc.Status == desired)
                    return;

                // If the service crashed/exited early
                if (desired == ServiceControllerStatus.Running &&
                    (sc.Status == ServiceControllerStatus.Stopped || sc.Status == ServiceControllerStatus.Paused))
                {
                    throw new InvalidOperationException("Service failed to reach Running state");
                }

                Thread.Sleep(500);
            }

            throw new TimeoutException($"Timed out waiting for service to reach {desired}");
        }
    }
}
