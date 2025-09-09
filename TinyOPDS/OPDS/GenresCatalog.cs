/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the OPDS GenresCatalog class
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
    /// Genres acquisition feed class
    /// </summary>
    public class GenresCatalog
    {
        public XDocument GetCatalog(string searchPattern, int threshold = 100)
        {
            // Decode URL-encoded search pattern properly for any special characters
            if (!string.IsNullOrEmpty(searchPattern))
            {
                try
                {
                    searchPattern = Uri.UnescapeDataString(searchPattern).Replace('+', ' ');
                    Log.WriteLine(LogLevel.Info, "GenresCatalog search pattern decoded: '{0}'", searchPattern);
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Warning, "Error decoding search pattern '{0}': {1}", searchPattern, ex.Message);
                }
            }

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                new XElement("id", "tag:genres"),
                new XElement("title", string.IsNullOrEmpty(searchPattern) ?
                    Localizer.Text("Books by genres") :
                    string.Format(Localizer.Text("Genres: {0}"), searchPattern)),
                new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                new XElement("icon", "icons/genres.ico"),
                // Add links
                Links.opensearch, Links.search, Links.start)
            );

            bool topLevel = true;
            bool useCyrillic = Properties.Settings.Default.SortOrder > 0;

            // Get all genre statistics with single fast query
            Dictionary<string, int> genreStatistics;
            try
            {
                genreStatistics = Library.GetBookRepository()?.GetAllGenreStatistics() ?? new Dictionary<string, int>();
                Log.WriteLine(LogLevel.Info, "Loaded statistics for {0} genres from database", genreStatistics.Count);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error getting genre statistics from database: {0}", ex.Message);
                return doc; // Return empty catalog on error
            }

            List<Genre> genres = null;

            // Is it top level (main genres)?
            if (string.IsNullOrEmpty(searchPattern))
            {
                // Find all main genres that have at least one subgenre used in database
                genres = new List<Genre>();

                foreach (var mainGenre in Library.FB2Genres)
                {
                    // Check if this main genre has any subgenres that have books in database
                    bool hasUsedSubgenres = mainGenre.Subgenres.Any(sg => genreStatistics.ContainsKey(sg.Tag) && genreStatistics[sg.Tag] > 0);

                    if (hasUsedSubgenres)
                    {
                        genres.Add(mainGenre);
                    }
                }

                Log.WriteLine(LogLevel.Info, "Found {0} main genres with books", genres.Count);
            }
            // Is it a second level (subgenres)?
            else
            {
                Genre mainGenre = Library.FB2Genres.Where(g => g.Name.Equals(searchPattern) || g.Translation.Equals(searchPattern)).FirstOrDefault();
                if (mainGenre != null)
                {
                    // Filter subgenres to show only those that have books in database
                    genres = mainGenre.Subgenres.Where(sg => genreStatistics.ContainsKey(sg.Tag) && genreStatistics[sg.Tag] > 0).ToList();
                    topLevel = false;

                    Log.WriteLine(LogLevel.Info, "Found {0} subgenres with books for main genre '{1}'", genres.Count, searchPattern);
                }
                else
                {
                    Log.WriteLine(LogLevel.Warning, "Main genre not found: '{0}'", searchPattern);
                }
            }

            if (genres != null && genres.Count > 0)
            {
                // Sort genres
                genres.Sort((g1, g2) =>
                {
                    var comparer = new OPDSComparer(useCyrillic);
                    string name1 = useCyrillic ? g1.Translation : g1.Name;
                    string name2 = useCyrillic ? g2.Translation : g2.Name;
                    return comparer.Compare(name1, name2);
                });

                // Add catalog entries
                foreach (Genre genre in genres)
                {
                    string genreName = useCyrillic ? genre.Translation : genre.Name;
                    string genreId = topLevel ? genreName : genre.Tag;

                    // Calculate books count using preloaded statistics
                    int booksCount = 0;
                    if (topLevel)
                    {
                        // Count books in all subgenres of this main genre
                        foreach (var subgenre in genre.Subgenres)
                        {
                            if (genreStatistics.ContainsKey(subgenre.Tag))
                            {
                                booksCount += genreStatistics[subgenre.Tag];
                            }
                        }
                    }
                    else
                    {
                        // Get books count for this specific subgenre
                        if (genreStatistics.ContainsKey(genre.Tag))
                        {
                            booksCount = genreStatistics[genre.Tag];
                        }
                    }

                    // Only add entry if there are books (should always be true due to filtering above)
                    if (booksCount > 0)
                    {
                        doc.Root.Add(
                            new XElement("entry",
                                new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                                new XElement("id", "tag:root:genre:" + genreName),
                                new XElement("title", genreName),
                                new XElement("content", string.Format(Localizer.Text("Books in genre «{0}»: {1}"), genreName, booksCount), new XAttribute("type", "text")),
                                new XElement("link",
                                    new XAttribute("href", "/" + (topLevel ? "genres/" : "genre/") + Uri.EscapeDataString(genreId)),
                                    new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                            )
                        );
                    }
                }

                Log.WriteLine(LogLevel.Info, "Generated {0} genre entries for level '{1}' in fast mode",
                    doc.Root.Elements("entry").Count(), topLevel ? "main" : "sub");
            }
            else
            {
                Log.WriteLine(LogLevel.Info, "No genres found for pattern '{0}'", searchPattern ?? "root");
            }

            return doc;
        }
    }
}