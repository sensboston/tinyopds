/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Simple implementation of UPnP controller. Works fine with 
 * some D-Link and NetGear router models (need more tests)
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
            if (!string.IsNullOrEmpty(author))
                author = Uri.UnescapeDataString(author).Replace('+', ' ');

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

            // Get author's books to calculate statistics
            List<Book> allBooks = Library.GetBooksByAuthor(author);

            // Group books by series to count series books and non-series books
            var booksWithSeries = allBooks.Where(b => !string.IsNullOrEmpty(b.Sequence)).ToList();
            var booksWithoutSeries = allBooks.Where(b => string.IsNullOrEmpty(b.Sequence)).ToList();
            var seriesCount = booksWithSeries.GroupBy(b => b.Sequence).Count();

            // Add "Books by series" entry only if author has books with series
            if (booksWithSeries.Count > 0)
            {
                doc.Root.Add(
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:author-details:series:" + author),
                        new XElement("title", Localizer.Text("Books by series")),
                        new XElement("content",
                            string.Format(Localizer.Text("{0} books in {1} series"),
                                booksWithSeries.Count, seriesCount),
                            new XAttribute("type", "text")),
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
                            string.Format(Localizer.Text("{0} books without series"),
                                booksWithoutSeries.Count),
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
                        string.Format(Localizer.Text("{0} books sorted alphabetically"),
                            allBooks.Count),
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
                        string.Format(Localizer.Text("{0} books sorted by creation date"),
                            allBooks.Count),
                        new XAttribute("type", "text")),
                    new XElement("link",
                        new XAttribute("href", "/author-by-date/" + Uri.EscapeDataString(author)),
                        new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                )
            );

            return doc;
        }
    }
}