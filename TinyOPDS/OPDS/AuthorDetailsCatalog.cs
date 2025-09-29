/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the OPDS AuthorDetailsCatalog class
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
    /// Author details intermediate catalog class
    /// </summary>
    public class AuthorDetailsCatalog
    {
        /// <summary>
        /// Get intermediate catalog for selected author with various view options
        /// </summary>
        /// <param name="author">Author name</param>
        /// <returns>OPDS catalog with view options</returns>
        public XDocument GetCatalog(string author)
        {
            // Decode URL-encoded author name properly for Cyrillic
            if (!string.IsNullOrEmpty(author))
            {
                try
                {
                    string originalAuthor = author;
                    author = Uri.UnescapeDataString(author).Replace('+', ' ');
                    Log.WriteLine(LogLevel.Info, "AuthorDetailsCatalog author name decoded: '{0}' -> '{1}'", originalAuthor, author);
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Warning, "Error decoding author name '{0}': {1}", author, ex.Message);
                }
            }

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed",
                    new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc),
                    new XAttribute(XNamespace.Xmlns + "os", Namespaces.os),
                    new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:author-details:" + author),
                    new XElement("title", string.Format(Localizer.Text("Books by author {0}"), author)),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/authors.ico"),
                    // Add links
                    Links.opensearch, Links.search, Links.start)
                );

            // Get author's books to calculate statistics - database now contains canonical names
            List<Book> books = Library.GetBooksByAuthor(author);

            Log.WriteLine(LogLevel.Info, "Found {0} books for author '{1}'", books.Count, author);

            if (books.Count == 0)
            {
                Log.WriteLine(LogLevel.Warning, "No books found for author '{0}'", author);
                // Return empty catalog but with proper structure
                return doc;
            }

            // Group books by series to count series books and non-series books
            var booksWithSeries = books.Where(b => !string.IsNullOrEmpty(b.Sequence)).ToList();
            var booksWithoutSeries = books.Where(b => string.IsNullOrEmpty(b.Sequence)).ToList();
            var seriesCount = booksWithSeries.GroupBy(b => b.Sequence).Count();

            Log.WriteLine(LogLevel.Info, "Author '{0}': {1} books with series ({2} series), {3} books without series",
                author, booksWithSeries.Count, seriesCount, booksWithoutSeries.Count);

            // Add "Books by series" entry only if author has books with series
            if (booksWithSeries.Count > 0)
            {
                // Build content text with proper pluralization for two numbers
                string booksInSeriesContent;
                if (Pluralizer.IsLanguageSupported(Localizer.Language))
                {
                    // For Slavic languages - use pluralizer with special preposition handling
                    booksInSeriesContent = StringUtils.ApplyPluralForm(0, Localizer.Language,
                        string.Format(Localizer.Text("{0} books in {1} series"),
                            booksWithSeries.Count, seriesCount));
                }
                else
                {
                    // For non-Slavic languages - choose correct key based on series count
                    string locKey = seriesCount == 1 ? "{0} books in 1 series" : "{0} books in {1} series";
                    booksInSeriesContent = string.Format(Localizer.Text(locKey),
                        booksWithSeries.Count, seriesCount);
                }

                doc.Root.Add(
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:author-details:series:" + author),
                        new XElement("title", Localizer.Text("Books by series")),
                        new XElement("content", booksInSeriesContent, new XAttribute("type", "text")),
                        new XElement("link",
                            new XAttribute("href", "/author-series/" + Uri.EscapeDataString(author)),
                            new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                    )
                );
            }

            // Add "Books without series" entry
            if (booksWithoutSeries.Count > 0)
            {
                doc.Root.Add(
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:author-details:no-series:" + author),
                        new XElement("title", Localizer.Text("Books without series")),
                        new XElement("content",
                            StringUtils.ApplyPluralForm(booksWithoutSeries.Count, Localizer.Language,
                                string.Format(Localizer.Text("{0} books without series"),
                                    booksWithoutSeries.Count)),
                            new XAttribute("type", "text")),
                        new XElement("link",
                            new XAttribute("href", "/author-no-series/" + Uri.EscapeDataString(author)),
                            new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                    )
                );
            }

            // Add "Books alphabetically" entry
            doc.Root.Add(
                new XElement("entry",
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("id", "tag:author-details:alphabetic:" + author),
                    new XElement("title", Localizer.Text("Books alphabetically")),
                    new XElement("content",
                        StringUtils.ApplyPluralForm(books.Count, Localizer.Language,
                            string.Format(Localizer.Text("{0} books sorted alphabetically"),
                                books.Count)),
                        new XAttribute("type", "text")),
                    new XElement("link",
                        new XAttribute("href", "/author-alphabetic/" + Uri.EscapeDataString(author)),
                        new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                )
            );

            // Add "Books by creation date" entry
            doc.Root.Add(
                new XElement("entry",
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("id", "tag:author-details:by-date:" + author),
                    new XElement("title", Localizer.Text("Books by creation date")),
                    new XElement("content",
                        StringUtils.ApplyPluralForm(books.Count, Localizer.Language,
                            string.Format(Localizer.Text("{0} books sorted by creation date"),
                                books.Count)),
                        new XAttribute("type", "text")),
                    new XElement("link",
                        new XAttribute("href", "/author-by-date/" + Uri.EscapeDataString(author)),
                        new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                )
            );

            Log.WriteLine(LogLevel.Info, "Generated author details catalog with {0} view options for author '{1}'",
                doc.Root.Elements("entry").Count(), author);

            return doc;
        }
    }
}