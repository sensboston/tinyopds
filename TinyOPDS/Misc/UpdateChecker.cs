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
using System.Net;
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

        #endregion

        #region Public methods

        /// <summary>
        /// Check if it's time to check for updates
        /// </summary>
        public static bool ShouldCheckForUpdates(int updateCheckSetting, DateTime lastCheck)
        {
            if (updateCheckSetting <= 0 || updateCheckSetting >= CheckIntervals.Length)
                return false;

            int minutesFromLastCheck = (int)Math.Round(DateTime.Now.Subtract(lastCheck).TotalMinutes);
            return minutesFromLastCheck >= CheckIntervals[updateCheckSetting];
        }

        /// <summary>
        /// Start asynchronous update check
        /// </summary>
        public void CheckAsync()
        {
            if (isChecking) return;

            // Update last check time immediately to prevent repeated checks
            Properties.Settings.Default.LastCheck = DateTime.Now;
            Properties.Settings.Default.Save();

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
                OnCheckCompleted(false, null, null);
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
                OnCheckCompleted(false, null, null);
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
                    OnCheckCompleted(false, null, null);
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
                OnCheckCompleted(false, null, null);
                return;
            }

            if (e.Error == null && !string.IsNullOrEmpty(e.Result))
            {
                ProcessTagsResponse(e.Result);
            }
            else
            {
                Log.WriteLine(LogLevel.Warning, "Failed to check for updates from GitHub");
                OnCheckCompleted(false, null, null);
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
                    CheckVersion(latestTag);
                }
                else
                {
                    OnCheckCompleted(false, null, null);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error parsing releases response: {0}", ex.Message);
                OnCheckCompleted(false, null, null);
            }
        }

        /// <summary>
        /// Process GitHub tags API response
        /// </summary>
        private void ProcessTagsResponse(string jsonResponse)
        {
            try
            {
                // Extract all version tags
                var matches = Regex.Matches(jsonResponse, "\"name\"\\s*:\\s*\"(v[0-9]+\\.[0-9]+)\"");

                if (matches.Count > 0)
                {
                    Version highestVersion = new Version(0, 0);
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
                        CheckVersion(highestTag);
                    }
                    else
                    {
                        OnCheckCompleted(false, null, null);
                    }
                }
                else
                {
                    OnCheckCompleted(false, null, null);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error parsing tags response: {0}", ex.Message);
                OnCheckCompleted(false, null, null);
            }
        }

        /// <summary>
        /// Parse version from tag string (v2.0, v2.1, etc.)
        /// </summary>
        private Version ParseVersion(string tag)
        {
            try
            {
                string versionString = tag.TrimStart('v', 'V');
                string[] parts = versionString.Split('.');

                if (parts.Length >= 2)
                {
                    if (int.TryParse(parts[0], out int major) &&
                        int.TryParse(parts[1], out int minor))
                    {
                        return new Version(major, minor);
                    }
                }
                else if (parts.Length == 1)
                {
                    if (int.TryParse(parts[0], out int major))
                    {
                        return new Version(major, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error parsing version '{0}': {1}", tag, ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Check if new version is available
        /// </summary>
        private void CheckVersion(string latestTag)
        {
            try
            {
                Version latestVersion = ParseVersion(latestTag);
                Version currentVersion = Utils.Version;

                if (latestVersion != null && latestVersion > currentVersion)
                {
                    Log.WriteLine(LogLevel.Info, "New version available: {0} (current: {1}.{2})",
                        latestTag, currentVersion.Major, currentVersion.Minor);

                    OnCheckCompleted(true, latestTag, githubReleasesPage);
                }
                else
                {
                    Log.WriteLine(LogLevel.Info, "No updates available. Current version {0}.{1} is up to date",
                        currentVersion.Major, currentVersion.Minor);

                    OnCheckCompleted(false, null, null);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error checking version: {0}", ex.Message);
                OnCheckCompleted(false, null, null);
            }
        }

        /// <summary>
        /// Raise CheckCompleted event
        /// </summary>
        private void OnCheckCompleted(bool updateAvailable, string newVersion, string downloadUrl)
        {
            CleanupWebClient();
            isChecking = false;

            var handler = CheckCompleted;
            if (handler != null)
            {
                handler(this, new UpdateCheckEventArgs(updateAvailable, newVersion, downloadUrl));
            }
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

        public UpdateCheckEventArgs(bool updateAvailable, string newVersion, string downloadUrl)
        {
            UpdateAvailable = updateAvailable;
            NewVersion = newVersion;
            DownloadUrl = downloadUrl;
        }
    }
}