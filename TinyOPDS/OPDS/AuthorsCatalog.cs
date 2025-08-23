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

            // Get author's books to analyze structure (including books from aliases)
            var authorBooks = GetAllAuthorBooks(author);
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
        /// Get all books for author including books from aliases
        /// </summary>
        /// <param name="author">Canonical author name</param>
        /// <returns>Combined list of books</returns>
        private List<Book> GetAllAuthorBooks(string author)
        {
            var allBooks = new Dictionary<string, Book>(); // Use dictionary to avoid duplicates by ID

            // Add books from canonical name
            foreach (var book in Library.GetBooksByAuthor(author))
            {
                allBooks[book.ID] = book;
            }

            // Add books from all aliases that map to this canonical name
            var aliases = GetAliasesForCanonicalName(author);
            foreach (var alias in aliases)
            {
                foreach (var book in Library.GetBooksByAuthor(alias))
                {
                    allBooks[book.ID] = book; // Dictionary will handle duplicates
                }
            }

            return allBooks.Values.ToList();
        }

        /// <summary>
        /// Get all alias names that map to the canonical name
        /// </summary>
        /// <param name="canonicalName">Canonical author name</param>
        /// <returns>List of alias names</returns>
        private List<string> GetAliasesForCanonicalName(string canonicalName)
        {
            var aliases = new List<string>();

            // This is a simple approach - in a real implementation you might want to cache this
            var allAuthors = Library.Authors;
            foreach (var author in allAuthors)
            {
                var canonical = Library.ApplyAuthorAlias(author);
                if (canonical.Equals(canonicalName, StringComparison.OrdinalIgnoreCase) &&
                    !author.Equals(canonicalName, StringComparison.OrdinalIgnoreCase))
                {
                    aliases.Add(author);
                }
            }

            return aliases;
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
        /// Get total books count for author including aliases
        /// </summary>
        /// <param name="canonicalAuthor">Canonical author name</param>
        /// <returns>Total books count</returns>
        private int GetTotalAuthorBooksCount(string canonicalAuthor)
        {
            return GetAllAuthorBooks(canonicalAuthor).Count;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="searchPattern"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public XDocument GetCatalog(string searchPattern, bool isOpenSearch = false, int threshold = 100)
        {
            if (!string.IsNullOrEmpty(searchPattern)) searchPattern = Uri.UnescapeDataString(searchPattern).Replace('+', ' ').ToLower();

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:authors"),
                    new XElement("title", Localizer.Text("Books by authors")),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/authors.ico"),
                    // Add links
                    Links.opensearch, Links.search, Links.start)
                );

            // Get all authors names starting with searchPattern
            List<string> authors = Library.GetAuthorsByName(searchPattern, isOpenSearch);

            // For search, also check transliterated names
            if (isOpenSearch)
            {
                // Try transliteration
                string translit = Transliteration.Back(searchPattern, TransliterationType.GOST);
                if (!string.IsNullOrEmpty(translit))
                {
                    List<string> transAuthors = Library.GetAuthorsByName(translit, isOpenSearch);
                    if (transAuthors.Count > 0) authors.AddRange(transAuthors);
                }
            }

            // Apply aliases to get canonical names and remove duplicates
            var canonicalAuthors = new HashSet<string>();
            foreach (var author in authors)
            {
                string canonical = Library.ApplyAuthorAlias(author);
                canonicalAuthors.Add(canonical);
            }
            authors = canonicalAuthors.OrderBy(a => a, new OPDSComparer(Properties.Settings.Default.SortOrder > 0)).ToList();

            // if there are more authors then threshold, try to collapse them into groups
            // and render these groups first and authors after them
            if (authors.Count > threshold)
            {
                Dictionary<string, int> catalogGroups = null;
                do
                {
                    catalogGroups = (from a in authors
                                     group a by (a.Length > searchPattern.Length ? a.Substring(0, searchPattern.Length + 1).Capitalize(true)
                                                                                 : a.Capitalize(true)) into g
                                     where g.Count() > 1
                                     select new { Name = g, Count = g.Count() }).ToDictionary(x => x.Name.Key, y => y.Count);

                    if (catalogGroups.Count == 1) searchPattern = catalogGroups.First().Key;
                    else break;
                } while (true);

                // remove entry that exactly matches search pattern to avoid recursion 
                catalogGroups.Remove(searchPattern.Capitalize(true));
                // remove entries that are groupped ( if any )
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

            // Add catalog entries with canonical author names
            foreach (string author in authors)
            {
                // Use total count including books from aliases
                var booksCount = GetTotalAuthorBooksCount(author);

                // Use smart routing instead of always going to author-details
                string authorRoute = GetAuthorRoute(author);

                doc.Root.Add(
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:authors:" + author),
                        new XElement("title", author), // This is now canonical name
                        new XElement("content", string.Format(Localizer.Text("Books: {0}"), booksCount), new XAttribute("type", "text")),
                        // Smart routing based on OPDS settings and author's book structure
                        new XElement("link", new XAttribute("href", authorRoute), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                    )
                );
            }
            return doc;
        }
    }
}