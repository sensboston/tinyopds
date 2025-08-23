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

            // Get authors names - Library handles Soundex fallback and transliteration internally
            List<string> authors = Library.GetAuthorsByName(searchPattern ?? "", isOpenSearch);

            // For search, also check transliterated names
            if (isOpenSearch && !string.IsNullOrEmpty(searchPattern))
            {
                // Try transliteration
                string translit = Transliteration.Back(searchPattern, TransliterationType.GOST);
                if (!string.IsNullOrEmpty(translit) && !translit.Equals(searchPattern))
                {
                    List<string> transAuthors = Library.GetAuthorsByName(translit, isOpenSearch);
                    if (transAuthors.Count > 0)
                    {
                        Log.WriteLine(LogLevel.Info, "Found {0} additional authors via transliteration", transAuthors.Count);
                        authors.AddRange(transAuthors);
                    }
                }
            }

            // Remove duplicates and sort - authors are already canonical from database
            authors = authors.Distinct().OrderBy(a => a, new OPDSComparer(Properties.Settings.Default.SortOrder > 0)).ToList();

            Log.WriteLine(LogLevel.Info, "Found {0} authors for pattern '{1}'", authors.Count, searchPattern ?? "");

            // if there are more authors then threshold, try to collapse them into groups
            // and render these groups first and authors after them
            if (authors.Count > threshold)
            {
                Dictionary<string, int> catalogGroups = null;
                string currentPattern = searchPattern ?? "";

                do
                {
                    catalogGroups = (from a in authors
                                     group a by (a.Length > currentPattern.Length ?
                                         a.Substring(0, currentPattern.Length + 1).Capitalize(true) :
                                         a.Capitalize(true)) into g
                                     where g.Count() > 1
                                     select new { Name = g, Count = g.Count() }).ToDictionary(x => x.Name.Key, y => y.Count);

                    if (catalogGroups.Count == 1)
                    {
                        currentPattern = catalogGroups.First().Key;
                    }
                    else
                    {
                        break;
                    }
                } while (true);

                // remove entry that exactly matches search pattern to avoid recursion 
                catalogGroups.Remove(currentPattern.Capitalize(true));

                // remove entries that are grouped ( if any )
                foreach (var kv in catalogGroups)
                {
                    authors.RemoveAll(a => a.StartsWith(kv.Key, StringComparison.InvariantCultureIgnoreCase));
                }

                // Add catalog groups
                foreach (KeyValuePair<string, int> cg in catalogGroups)
                {
                    doc.Root.Add(
                        new XElement("entry",
                            new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                            new XElement("id", "tag:authors:" + cg.Key),
                            new XElement("title", cg.Key),
                            new XElement("content", string.Format(Localizer.Text("Total authors on {0}: {1}"), cg.Key, cg.Value),
                                         new XAttribute("type", "text")),
                            new XElement("link", new XAttribute("href", "/authorsindex/" + Uri.EscapeDataString(cg.Key)),
                                         new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                        )
                    );
                }
            }

            // Add catalog entries - authors are already canonical names from database
            foreach (string author in authors)
            {
                // Get books count directly - no need for alias handling
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

            Log.WriteLine(LogLevel.Info, "Generated authors catalog with {0} entries and {1} groups",
                authors.Count,
                doc.Root.Elements("entry").Count(e => e.Element("link")?.Attribute("href")?.Value?.Contains("/authorsindex/") == true));

            return doc;
        }
    }
}
