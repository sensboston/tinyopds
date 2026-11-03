/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Installs a new release over the running exe (Windows GUI)
 *
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;

namespace TinyOPDS
{
    /// <summary>
    /// Replaces the running exe with the release asset: the asset is downloaded next to the exe and checked
    /// by the digest GitHub gives, the running exe is renamed to .old (Windows allows renaming a running exe),
    /// the new one takes its name and is started with /updated:pid, so it waits for this process to exit.
    /// Settings and database are not touched
    /// </summary>
    public static class UpdateInstaller
    {
        public const string UpdatedArgument = "/updated:";

        private static string ExePath
        {
            get { return Assembly.GetEntryAssembly().Location; }
        }

        /// <summary>
        /// This copy can replace itself: Windows (not Mono), a release asset with a digest, a writable exe folder
        /// </summary>
        public static bool CanInstall(UpdateCheckEventArgs update)
        {
            if (!Utils.IsWindows || Utils.IsLinux || Utils.IsMacOS) return false;
            if (string.IsNullOrEmpty(update?.AssetUrl) || string.IsNullOrEmpty(update.AssetDigest)) return false;

            try
            {
                string probe = Path.Combine(Path.GetDirectoryName(ExePath), Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Download and check the new exe and put it in place of the running one. On failure nothing is changed
        /// </summary>
        public static bool Install(UpdateCheckEventArgs update)
        {
            string exe = ExePath;
            string download = exe + ".download";
            string old = exe + ".old";

            try
            {
                Log.WriteLine("Downloading update {0} from {1}", update.NewVersion, update.AssetUrl);
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "TinyOPDS Update Checker");
                    client.DownloadFile(update.AssetUrl, download);
                }

                string digest = UpdateChecker.FileDigest(download);
                if (digest != update.AssetDigest)
                {
                    Log.WriteLine(LogLevel.Error, "Update {0} is not installed: downloaded file digest {1} differs from the release digest {2}",
                        update.NewVersion, digest, update.AssetDigest);
                    return false;
                }

                Version version = AssemblyName.GetAssemblyName(download).Version;
                Version expected = UpdateChecker.ParseVersion(update.NewVersion);
                if (expected == null || new Version(version.Major, version.Minor, Math.Max(0, version.Build)) < expected)
                {
                    Log.WriteLine(LogLevel.Error, "Update {0} is not installed: the downloaded exe has version {1}", update.NewVersion, version);
                    return false;
                }

                if (File.Exists(old)) File.Delete(old);
                File.Move(exe, old);
                try
                {
                    File.Move(download, exe);
                }
                catch
                {
                    File.Move(old, exe);
                    throw;
                }

                Log.WriteLine("Update {0} is installed, restarting", update.NewVersion);
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Update {0} is not installed: {1}", update.NewVersion, ex.Message);
                return false;
            }
            finally
            {
                try { if (File.Exists(download)) File.Delete(download); } catch { }
            }
        }

        /// <summary>
        /// Start the installed exe; it waits for this process to exit (single instance)
        /// </summary>
        public static void StartNewVersion()
        {
            Process.Start(new ProcessStartInfo(ExePath, UpdatedArgument + Process.GetCurrentProcess().Id)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(ExePath)
            });
        }

        /// <summary>
        /// Started by the previous version: wait for it to exit and remove its exe. Returns true if started so
        /// </summary>
        public static bool CompleteUpdate(string[] args)
        {
            string arg = Array.Find(args ?? new string[0], a => a.StartsWith(UpdatedArgument, StringComparison.OrdinalIgnoreCase));
            if (arg == null) return false;

            if (int.TryParse(arg.Substring(UpdatedArgument.Length), out int pid))
            {
                try
                {
                    using (var previous = Process.GetProcessById(pid))
                    {
                        previous.WaitForExit(60000);
                    }
                }
                catch (ArgumentException)
                {
                    // Already exited
                }
            }

            try
            {
                string old = ExePath + ".old";
                if (File.Exists(old)) File.Delete(old);
            }
            catch
            {
                // Removed on the next update
            }
            return true;
        }
    }
}
