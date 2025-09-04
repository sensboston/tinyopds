/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the OPDS SequencesCatalog class (book series)
 * OPTIMIZED: Using database queries instead of in-memory LINQ operations
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
    /// Sequences acquisition feed class
    /// </summary>
    public class SequencesCatalog
    {
        public XDocument GetCatalog(string searchPattern, int threshold = 100)
        {
            if (!string.IsNullOrEmpty(searchPattern))
                searchPattern = Uri.UnescapeDataString(searchPattern).Replace('+', ' ');

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed",
                    new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc),
                    new XAttribute(XNamespace.Xmlns + "os", Namespaces.os),
                    new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:sequences"),
                    new XElement("title", Localizer.Text("Book series")),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/series.ico"),
                    // Add links
                    Links.opensearch, Links.search, Links.start)
                );

            // OPTIMIZED: Get sequences from database with proper filtering
            var bookRepository = Library.GetBookRepository();
            if (bookRepository == null)
            {
                Log.WriteLine(LogLevel.Error, "BookRepository is null in SequencesCatalog");
                return doc;
            }

            try
            {
                // Get sequences with book count from database - already filtered by prefix
                var sequencesWithCount = bookRepository.GetSequencesWithCount(searchPattern);

                // Filter out sequences that don't have enough length for navigation
                if (!string.IsNullOrEmpty(searchPattern))
                {
                    sequencesWithCount = sequencesWithCount
                        .Where(s => s.Name.Length > searchPattern.Length + 1)
                        .ToList();
                }

                // Apply sorting
                var comparer = new OPDSComparer(Properties.Settings.Default.SortOrder > 0);
                sequencesWithCount = sequencesWithCount
                    .OrderBy(s => s.Name, comparer)
                    .ToList();

                // If too many sequences, create navigation groups
                if (sequencesWithCount.Count > threshold)
                {
                    // Group sequences by next character for navigation
                    var groups = CreateNavigationGroups(sequencesWithCount, searchPattern);

                    // Add navigation entries
                    foreach (var group in groups)
                    {
                        doc.Root.Add(
                            new XElement("entry",
                                new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                                new XElement("id", "tag:sequences:" + group.Key),
                                new XElement("title", group.Key),
                                new XElement("content",
                                    string.Format(Localizer.Text("Total series on {0}: {1}"),
                                        group.Key, group.Value),
                                    new XAttribute("type", "text")),
                                new XElement("link",
                                    new XAttribute("href", "/sequencesindex/" + Uri.EscapeDataString(group.Key)),
                                    new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                            )
                        );
                    }
                }
                else
                {
                    // Add individual sequence entries with book counts
                    foreach (var sequence in sequencesWithCount)
                    {
                        doc.Root.Add(
                            new XElement("entry",
                                new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                                new XElement("id", "tag:sequences:" + sequence.Name),
                                new XElement("title", sequence.Name),
                                new XElement("content",
                                    string.Format(Localizer.Text("{0} books in {1}"),
                                        sequence.BookCount, sequence.Name),
                                    new XAttribute("type", "text")),
                                new XElement("link",
                                    new XAttribute("href", "/sequence/" + Uri.EscapeDataString(sequence.Name)),
                                    new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                            )
                        );
                    }
                }

                Log.WriteLine(LogLevel.Info, "Generated sequences catalog with {0} entries for pattern '{1}'",
                    doc.Root.Elements("entry").Count(), searchPattern ?? "");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error generating sequences catalog: {0}", ex.Message);
            }

            return doc;
        }

        /// <summary>
        /// Create navigation groups for sequences based on first letters
        /// </summary>
        private Dictionary<string, int> CreateNavigationGroups(List<(string Name, int BookCount)> sequences, string currentPattern)
        {
            var groups = new Dictionary<string, int>();

            if (string.IsNullOrEmpty(currentPattern))
            {
                // Root level - group by first letter
                foreach (var sequence in sequences)
                {
                    if (sequence.Name.Length > 0)
                    {
                        string firstLetter = sequence.Name.Substring(0, 1).ToUpperInvariant();
                        if (!groups.ContainsKey(firstLetter))
                            groups[firstLetter] = 0;
                        groups[firstLetter]++;
                    }
                }
            }
            else
            {
                // Deeper level - extend current pattern by one character
                int nextCharPos = currentPattern.Length;

                foreach (var sequence in sequences)
                {
                    if (sequence.Name.Length > nextCharPos)
                    {
                        string groupKey = sequence.Name.Substring(0, nextCharPos + 1);

                        // Only create group if it's different from current pattern
                        if (groupKey.Length > currentPattern.Length)
                        {
                            // Capitalize group key properly
                            if (Properties.Settings.Default.SortOrder > 0)
                                groupKey = groupKey.Capitalize(true); // Cyrillic mode
                            else
                                groupKey = groupKey.Capitalize(false); // Latin mode

                            if (!groups.ContainsKey(groupKey))
                                groups[groupKey] = 0;
                            groups[groupKey]++;
                        }
                    }
                }
            }

            // Filter out single-item groups (no need for navigation)
            return groups.Where(g => g.Value > 1).ToDictionary(g => g.Key, g => g.Value);
        }
    }
}