/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Checks for new releases on GitHub
 *
 */

using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace TinyOPDS
{
    /// <summary>
    /// Handles checking for TinyOPDS updates from GitHub
    /// </summary>
    public class UpdateChecker
    {
        #region Events

        /// <summary>
        /// Raised when update check is completed
        /// </summary>
        public event EventHandler<UpdateCheckEventArgs> CheckCompleted;

        #endregion

        #region Private fields

        private readonly string githubReleasesApi = "https://api.github.com/repos/sensboston/tinyopds/releases/latest";
        private readonly string githubTagsApi = "https://api.github.com/repos/sensboston/tinyopds/tags";
        private readonly string githubReleasesPage = "https://github.com/sensboston/tinyopds/releases";

        private bool isChecking = false;
        private WebClient webClient;

        #endregion

        #region Public properties

        /// <summary>
        /// Indicates if update check is in progress
        /// </summary>
        public bool IsChecking
        {
            get { return isChecking; }
        }

        /// <summary>
        /// Check intervals in minutes: Never, Weekly, Monthly
        /// </summary>
        public static readonly int[] CheckIntervals = new int[] { 0, 60 * 24 * 7, 60 * 24 * 30 };

        /// <summary>
        /// While a found update is not installed, it is checked (and reminded) once a day
        /// </summary>
        public const int ReminderInterval = 60 * 24;

        /// <summary>
        /// Version as release tags give it: major.minor.build, the revision is not used
        /// </summary>
        public static Version CurrentVersion
        {
            get
            {
                Version v = Utils.Version;
                return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
            }
        }

        /// <summary>
        /// The latest release found by a check is newer than this copy
        /// </summary>
        public static bool UpdatePending
        {
            get
            {
                string release = Properties.Settings.Default.LatestRelease ?? "";
                Version latest = ParseVersion(release.Split(' ')[0]);
                return latest != null && latest > CurrentVersion;
            }
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Check if it's time to check for updates
        /// </summary>
        public static bool ShouldCheckForUpdates(int updateCheckSetting, DateTime lastCheck, bool updatePending = false)
        {
            if (updateCheckSetting <= 0 || updateCheckSetting >= CheckIntervals.Length)
                return false;

            int interval = CheckIntervals[updateCheckSetting];
            if (updatePending) interval = Math.Min(interval, ReminderInterval);
            return DateTime.Now.Subtract(lastCheck).TotalMinutes >= interval;
        }

        /// <summary>
        /// Parse version from tag string: "v3.18" -> 3.18.0, "v3.18.1" -> 3.18.1
        /// </summary>
        public static Version ParseVersion(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;

            string[] parts = tag.Trim().TrimStart('v', 'V').Split('.');
            int[] numbers = new int[3];
            if (parts.Length > 3) return null;
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], out numbers[i]) || numbers[i] < 0) return null;
            }
            return new Version(numbers[0], numbers[1], numbers[2]);
        }

        /// <summary>
        /// Start asynchronous update check
        /// </summary>
        public void CheckAsync()
        {
            if (isChecking) return;

            isChecking = true;

            try
            {
                Log.WriteLine(LogLevel.Info, "Starting update check from GitHub");

                webClient = new WebClient();
                webClient.Headers.Add("User-Agent", "TinyOPDS Update Checker");
                webClient.DownloadStringCompleted += OnReleasesApiCompleted;
                webClient.DownloadStringAsync(new Uri(githubReleasesApi));
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Failed to start update check: {0}", ex.Message);
                OnCheckCompleted(new UpdateCheckEventArgs(false, null, null));
            }
        }

        /// <summary>
        /// Cancel ongoing update check
        /// </summary>
        public void Cancel()
        {
            if (webClient != null && isChecking)
            {
                webClient.CancelAsync();
                webClient.Dispose();
                webClient = null;
                isChecking = false;
            }
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Handle GitHub releases API response
        /// </summary>
        private void OnReleasesApiCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            webClient.DownloadStringCompleted -= OnReleasesApiCompleted;

            if (e.Cancelled)
            {
                OnCheckCompleted(new UpdateCheckEventArgs(false, null, null));
                return;
            }

            if (e.Error == null && !string.IsNullOrEmpty(e.Result))
            {
                ProcessReleasesResponse(e.Result);
            }
            else
            {
                // Fallback to tags API
                Log.WriteLine(LogLevel.Info, "GitHub releases API failed, trying tags API");

                try
                {
                    webClient.DownloadStringCompleted += OnTagsApiCompleted;
                    webClient.DownloadStringAsync(new Uri(githubTagsApi));
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Warning, "Failed to query tags API: {0}", ex.Message);
                    OnCheckCompleted(new UpdateCheckEventArgs(false, null, null));
                }
            }
        }

        /// <summary>
        /// Handle GitHub tags API response
        /// </summary>
        private void OnTagsApiCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            webClient.DownloadStringCompleted -= OnTagsApiCompleted;

            if (e.Cancelled)
            {
                OnCheckCompleted(new UpdateCheckEventArgs(false, null, null));
                return;
            }

            if (e.Error == null && !string.IsNullOrEmpty(e.Result))
            {
                ProcessTagsResponse(e.Result);
            }
            else
            {
                Log.WriteLine(LogLevel.Warning, "Failed to check for updates from GitHub");
                OnCheckCompleted(new UpdateCheckEventArgs(false, null, null));
            }
        }

        /// <summary>
        /// Process GitHub releases API response
        /// </summary>
        private void ProcessReleasesResponse(string jsonResponse)
        {
            try
            {
                // Extract tag_name from JSON
                var match = Regex.Match(jsonResponse, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");

                if (match.Success)
                {
                    string latestTag = match.Groups[1].Value;
                    FindAsset(jsonResponse, out string assetUrl, out string assetDigest);
                    CheckVersion(latestTag, assetUrl, assetDigest);
                }
                else
                {
                    OnCheckCompleted(new UpdateCheckEventArgs(false, null, null));
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error parsing releases response: {0}", ex.Message);
                OnCheckCompleted(new UpdateCheckEventArgs(false, null, null));
            }
        }

        /// <summary>
        /// Download URL and digest ("sha256:...") of the release asset with the name of this exe
        /// </summary>
        private static void FindAsset(string jsonResponse, out string url, out string digest)
        {
            url = digest = null;

            string exeName = Path.GetFileName(Assembly.GetEntryAssembly()?.Location ?? "TinyOPDS.exe");
            var name = Regex.Match(jsonResponse, "\"name\"\\s*:\\s*\"" + Regex.Escape(exeName) + "\"", RegexOptions.IgnoreCase);
            if (!name.Success) return;

            // Asset fields follow its name, browser_download_url is the last one
            var urlMatch = new Regex("\"browser_download_url\"\\s*:\\s*\"([^\"]+)\"").Match(jsonResponse, name.Index);
            if (!urlMatch.Success) return;
            url = urlMatch.Groups[1].Value;

            string fields = jsonResponse.Substring(name.Index, urlMatch.Index - name.Index);
            var digestMatch = Regex.Match(fields, "\"digest\"\\s*:\\s*\"(sha256:[0-9a-fA-F]{64})\"");
            if (digestMatch.Success) digest = digestMatch.Groups[1].Value.ToLowerInvariant();
        }

        /// <summary>
        /// Process GitHub tags API response
        /// </summary>
        private void ProcessTagsResponse(string jsonResponse)
        {
            try
            {
                // Extract all version tags
                var matches = Regex.Matches(jsonResponse, "\"name\"\\s*:\\s*\"(v[0-9]+\\.[0-9]+(?:\\.[0-9]+)?)\"");

                Version highestVersion = new Version(0, 0, 0);
                string highestTag = "";

                foreach (Match match in matches)
                {
                    string tag = match.Groups[1].Value;
                    Version tagVersion = ParseVersion(tag);

                    if (tagVersion != null && tagVersion > highestVersion)
                    {
                        highestVersion = tagVersion;
                        highestTag = tag;
                    }
                }

                if (!string.IsNullOrEmpty(highestTag))
                {
                    CheckVersion(highestTag, null, null);
                }
                else
                {
                    OnCheckCompleted(new UpdateCheckEventArgs(false, null, null));
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error parsing tags response: {0}", ex.Message);
                OnCheckCompleted(new UpdateCheckEventArgs(false, null, null));
            }
        }

        /// <summary>
        /// Check if new version is available. A release of this version is an update too when its exe
        /// was replaced (rebuilt) after the previous check and differs from this one
        /// </summary>
        private void CheckVersion(string latestTag, string assetUrl, string assetDigest)
        {
            try
            {
                Version latestVersion = ParseVersion(latestTag);
                Version currentVersion = CurrentVersion;
                if (latestVersion == null)
                {
                    OnCheckCompleted(new UpdateCheckEventArgs(false, null, null));
                    return;
                }

                bool updateAvailable = latestVersion > currentVersion;
                if (!updateAvailable && latestVersion == currentVersion && assetDigest != null)
                {
                    string seen = Properties.Settings.Default.LatestRelease ?? "";
                    updateAvailable = seen.StartsWith(latestTag + " ") && seen != latestTag + " " + assetDigest &&
                                      assetDigest != OwnDigest();
                }

                // The check is done only when GitHub answered: a failed check is repeated on the next timer tick
                Properties.Settings.Default.LatestRelease = (latestTag + " " + assetDigest).Trim();
                Properties.Settings.Default.LastCheck = DateTime.Now;
                Properties.Settings.Default.Save();

                if (updateAvailable)
                {
                    Log.WriteLine(LogLevel.Info, "New version available: {0} (current: {1})", latestTag, currentVersion);
                    OnCheckCompleted(new UpdateCheckEventArgs(true, latestTag, githubReleasesPage, assetUrl, assetDigest));
                }
                else
                {
                    Log.WriteLine(LogLevel.Info, "No updates available. Current version {0} is up to date", currentVersion);
                    OnCheckCompleted(new UpdateCheckEventArgs(false, null, null));
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error checking version: {0}", ex.Message);
                OnCheckCompleted(new UpdateCheckEventArgs(false, null, null));
            }
        }

        /// <summary>
        /// Digest of the running exe in the form GitHub gives it ("sha256:...")
        /// </summary>
        internal static string OwnDigest()
        {
            try
            {
                return FileDigest(Assembly.GetEntryAssembly().Location);
            }
            catch
            {
                return null;
            }
        }

        internal static string FileDigest(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                return "sha256:" + BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Raise CheckCompleted event
        /// </summary>
        private void OnCheckCompleted(UpdateCheckEventArgs args)
        {
            CleanupWebClient();
            isChecking = false;

            CheckCompleted?.Invoke(this, args);
        }

        /// <summary>
        /// Clean up WebClient resources
        /// </summary>
        private void CleanupWebClient()
        {
            if (webClient != null)
            {
                webClient.Dispose();
                webClient = null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Event arguments for update check completion
    /// </summary>
    public class UpdateCheckEventArgs : EventArgs
    {
        public bool UpdateAvailable { get; private set; }
        public string NewVersion { get; private set; }
        public string DownloadUrl { get; private set; }

        /// <summary>
        /// Release asset of this exe and its digest ("sha256:..."), null when GitHub didn't give them
        /// </summary>
        public string AssetUrl { get; private set; }
        public string AssetDigest { get; private set; }

        public UpdateCheckEventArgs(bool updateAvailable, string newVersion, string downloadUrl,
                                    string assetUrl = null, string assetDigest = null)
        {
            UpdateAvailable = updateAvailable;
            NewVersion = newVersion;
            DownloadUrl = downloadUrl;
            AssetUrl = assetUrl;
            AssetDigest = assetDigest;
        }
    }
}
