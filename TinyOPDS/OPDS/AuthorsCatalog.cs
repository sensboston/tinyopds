/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the OPDS AuthorsCatalog class
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using TinyOPDS.Data;

namespace TinyOPDS.OPDS
{
    /// <summary>
    /// Authors acquisition feed class
    /// </summary>
    public class AuthorsCatalog
    {
        /// <summary>
        /// Determine the best route for author based on OPDS settings and author's books
        /// </summary>
        /// <param name="author">Author name</param>
        /// <returns>Best route URL for the author</returns>
        private string GetAuthorRoute(string author)
        {
            // Get OPDS structure settings
            var opdsSettings = GetOPDSStructureSettings();

            // If author-details is not enabled, skip intermediate page
            if (!opdsSettings.ContainsKey("author-details") || !opdsSettings["author-details"])
            {
                return GetDirectAuthorRoute(author, opdsSettings);
            }

            // Get author's books to analyze structure - database now contains canonical names
            var authorBooks = Library.GetBooksByAuthor(author);
            var booksWithSeries = authorBooks.Where(b => !string.IsNullOrEmpty(b.Sequence)).ToList();
            var booksWithoutSeries = authorBooks.Where(b => string.IsNullOrEmpty(b.Sequence)).ToList();

            // If author has both series and non-series books, show intermediate page
            if (booksWithSeries.Count > 0 && booksWithoutSeries.Count > 0)
            {
                return "/author-details/" + Uri.EscapeDataString(author);
            }

            // If author has only one type of books, go directly to appropriate view
            return GetDirectAuthorRoute(author, opdsSettings);
        }

        /// <summary>
        /// Get direct route to author's books based on OPDS settings
        /// </summary>
        /// <param name="author">Author name</param>
        /// <param name="opdsSettings">OPDS structure settings</param>
        /// <returns>Direct route URL</returns>
        private string GetDirectAuthorRoute(string author, Dictionary<string, bool> opdsSettings)
        {
            // Priority order: by date -> alphabetic (as fallback)

            if (opdsSettings.ContainsKey("author-by-date") && opdsSettings["author-by-date"])
            {
                return "/author-by-date/" + Uri.EscapeDataString(author);
            }

            // Default to alphabetic view (should always be available)
            return "/author-alphabetic/" + Uri.EscapeDataString(author);
        }

        /// <summary>
        /// Get OPDS structure settings from application settings
        /// </summary>
        /// <returns>Dictionary of OPDS structure settings</returns>
        private Dictionary<string, bool> GetOPDSStructureSettings()
        {
            var settings = new Dictionary<string, bool>();

            try
            {
                string settingsString = Properties.Settings.Default.OPDSStructure ??
                    "newdate:1;newtitle:1;authorsindex:1;author-details:1;author-series:1;author-no-series:1;author-alphabetic:1;author-by-date:1;sequencesindex:1;genres:1";

                string[] parts = settingsString.Split(';');
                foreach (string part in parts)
                {
                    string[] keyValue = part.Split(':');
                    if (keyValue.Length == 2)
                    {
                        settings[keyValue[0]] = keyValue[1] == "1";
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error parsing OPDS structure settings: {0}", ex.Message);
                // Default fallback - enable alphabetic view
                settings["author-alphabetic"] = true;
            }

            return settings;
        }

        /// <summary>
        /// Get authors catalog
        /// </summary>
        /// <param name="searchPattern">Search pattern</param>
        /// <param name="isOpenSearch">Is this an open search</param>
        /// <param name="threshold">Items per page</param>
        /// <returns>OPDS catalog</returns>
        public XDocument GetCatalog(string searchPattern, bool isOpenSearch = false, int threshold = 100)
        {
            // Decode URL-encoded search pattern properly for Cyrillic
            if (!string.IsNullOrEmpty(searchPattern))
            {
                try
                {
                    searchPattern = Uri.UnescapeDataString(searchPattern).Replace('+', ' ');
                    Log.WriteLine(LogLevel.Info, "AuthorsCatalog search pattern decoded: '{0}'", searchPattern);
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Warning, "Error decoding search pattern '{0}': {1}", searchPattern, ex.Message);
                }
            }

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:authors"),
                    new XElement("title", string.IsNullOrEmpty(searchPattern) ?
                        Localizer.Text("Books by authors") :
                        string.Format(Localizer.Text("Authors starting with '{0}'"), searchPattern)),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/authors.ico"),
                    // Add links
                    Links.opensearch, Links.search, Links.start)
                );

            // Get authors names based on search pattern
            List<string> authors;

            if (string.IsNullOrEmpty(searchPattern))
            {
                // Get all authors for initial navigation
                authors = Library.GetAuthorsByName("", false);
            }
            else
            {
                // Get authors matching the pattern - use prefix search for navigation
                authors = Library.GetAuthorsByName(searchPattern, isOpenSearch);
            }

            // Remove duplicates and sort - authors are already canonical from database
            authors = authors.Distinct().OrderBy(a => a, new OPDSComparer(Properties.Settings.Default.SortOrder > 0)).ToList();

            Log.WriteLine(LogLevel.Info, "Found {0} authors for pattern '{1}'", authors.Count, searchPattern ?? "");

            // If no search pattern (root level) or too many results, create navigation groups
            if (string.IsNullOrEmpty(searchPattern) || authors.Count > threshold)
            {
                // Create alphabetical groups for navigation
                var navigationGroups = CreateNavigationGroups(authors, searchPattern, threshold);

                // Add navigation entries
                foreach (var group in navigationGroups)
                {
                    doc.Root.Add(
                        new XElement("entry",
                            new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                            new XElement("id", "tag:authors:" + group.Key),
                            new XElement("title", group.Key),
                            new XElement("content", string.Format(Localizer.Text("Total authors on {0}: {1}"), group.Key, group.Value),
                                         new XAttribute("type", "text")),
                            new XElement("link", new XAttribute("href", "/authorsindex/" + Uri.EscapeDataString(group.Key)),
                                         new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                        )
                    );
                }

                // Filter out authors that are now in groups
                var groupPrefixes = navigationGroups.Keys.ToList();
                authors = authors.Where(a => !groupPrefixes.Any(prefix => a.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase) && a.Length > prefix.Length)).ToList();
            }

            // Add individual author entries
            foreach (string author in authors.Take(threshold))
            {
                // Get books count directly
                var booksCount = Library.GetBooksByAuthorCount(author);

                // Use smart routing based on OPDS settings and author's book structure
                string authorRoute = GetAuthorRoute(author);

                doc.Root.Add(
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:authors:" + author),
                        new XElement("title", author), // Already canonical name
                        new XElement("content", string.Format(Localizer.Text("Books: {0}"), booksCount), new XAttribute("type", "text")),
                        // Smart routing based on OPDS settings and author's book structure
                        new XElement("link", new XAttribute("href", authorRoute), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                    )
                );
            }

            int totalEntries = doc.Root.Elements("entry").Count();
            int groupEntries = doc.Root.Elements("entry").Count(e => e.Element("link")?.Attribute("href")?.Value?.Contains("/authorsindex/") == true);

            Log.WriteLine(LogLevel.Info, "Generated authors catalog with {0} author entries and {1} navigation groups",
                totalEntries - groupEntries, groupEntries);

            return doc;
        }

        /// <summary>
        /// Create navigation groups for authors based on first letters
        /// </summary>
        /// <param name="authors">List of all authors</param>
        /// <param name="currentPattern">Current search pattern</param>
        /// <param name="threshold">Threshold for grouping</param>
        /// <returns>Dictionary of group name to author count</returns>
        private Dictionary<string, int> CreateNavigationGroups(List<string> authors, string currentPattern, int threshold)
        {
            var groups = new Dictionary<string, int>();

            if (string.IsNullOrEmpty(currentPattern))
            {
                // Root level - group by first letter
                var firstLetterGroups = authors
                    .GroupBy(a => a.Substring(0, 1).ToUpperInvariant())
                    .Where(g => g.Count() > 0)
                    .ToDictionary(g => g.Key, g => g.Count());

                return firstLetterGroups;
            }
            else
            {
                // Deeper level - extend current pattern by one character
                int nextCharPos = currentPattern.Length;
                var extendedGroups = authors
                    .Where(a => a.Length > nextCharPos)
                    .GroupBy(a => a.Substring(0, nextCharPos + 1).Capitalize(true))
                    .Where(g => g.Count() > 1) // Only create groups if there are multiple authors
                    .ToDictionary(g => g.Key, g => g.Count());

                return extendedGroups;
            }
        }
    }
}