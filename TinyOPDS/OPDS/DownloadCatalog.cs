/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the OPDS DownloadCatalog class
 * Provides access to downloaded/read books history
 * Uses /downstat path to avoid confusion with /download
 *
 */

using System;
using System.Linq;
using System.Xml.Linq;

using TinyOPDS.Data;

namespace TinyOPDS.OPDS
{
    /// <summary>
    /// Downloaded books catalog class - provides download statistics
    /// </summary>
    public class DownloadCatalog
    {
        /// <summary>
        /// Get root catalog with download history options
        /// </summary>
        public XDocument GetRootCatalog()
        {
            // Get count of unique downloaded books from library
            int downloadsCount = Library.GetUniqueDownloadsCount();

            XDocument doc = new XDocument(
                new XElement("feed",
                    new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc),
                    new XAttribute(XNamespace.Xmlns + "os", Namespaces.os),
                    new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:downstat"),
                    new XElement("title", Localizer.Text("Downloaded books")),
                    new XElement("subtitle", string.Format(Localizer.Text("{0} downloaded books"), downloadsCount)),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/library.ico"),
                    Links.opensearch, Links.search, Links.start)
                );

            // Only show entries if we have downloaded books
            if (downloadsCount > 0)
            {
                // Entry for books sorted by download date
                doc.Root.Add(new XElement("entry",
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("id", "tag:downstat:bydate"),
                    new XElement("title", Localizer.Text("By download date"), new XAttribute("type", "text")),
                    new XElement("content", string.Format(Localizer.Text("{0} books sorted by download date"), downloadsCount),
                        new XAttribute("type", "text")),
                    new XElement("link",
                        new XAttribute("href", "/downstat/date"),
                        new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                ));

                // Entry for books sorted alphabetically
                doc.Root.Add(new XElement("entry",
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("id", "tag:downstat:alphabetic"),
                    new XElement("title", Localizer.Text("Alphabetically"), new XAttribute("type", "text")),
                    new XElement("content", string.Format(Localizer.Text("{0} books sorted alphabetically"), downloadsCount),
                        new XAttribute("type", "text")),
                    new XElement("link",
                        new XAttribute("href", "/downstat/alpha"),
                        new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                ));
            }
            else
            {
                // Show message when no downloads
                doc.Root.Add(new XElement("entry",
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("id", "tag:downstat:empty"),
                    new XElement("title", Localizer.Text("No downloaded books"), new XAttribute("type", "text")),
                    new XElement("content", Localizer.Text("Your download history is empty"),
                        new XAttribute("type", "text"))
                ));
            }

            return doc;
        }

        /// <summary>
        /// Get catalog of downloaded books with pagination
        /// </summary>
        /// <param name="sortByDate">Sort by date (true) or alphabetically (false)</param>
        /// <param name="pageNumber">Page number (0-based)</param>
        /// <param name="threshold">Items per page</param>
        /// <param name="acceptFB2">Client accepts FB2 format</param>
        public XDocument GetCatalog(bool sortByDate, int pageNumber, int threshold, bool acceptFB2)
        {
            string catalogId = sortByDate ? "tag:downstat:date" : "tag:downstat:alpha";
            string catalogTitle = sortByDate
                ? Localizer.Text("Downloaded books by date")
                : Localizer.Text("Downloaded books alphabetically");

            XDocument doc = new XDocument(
                new XElement("feed",
                    new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc),
                    new XAttribute(XNamespace.Xmlns + "os", Namespaces.os),
                    new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", catalogId),
                    new XElement("title", catalogTitle),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/icons/books.ico"),  // Use standard books icon like BooksCatalog
                    Links.opensearch, Links.search, Links.start)
                );

            // Get books from library
            int offset = pageNumber * threshold;
            System.Collections.Generic.List<Book> books;

            if (sortByDate)
            {
                books = Library.GetRecentDownloads(threshold + 1, offset); // Get one extra to check for next page
            }
            else
            {
                books = Library.GetDownloadsAlphabetic(threshold + 1, offset);
            }

            // Check if there's a next page
            bool hasNextPage = books.Count > threshold;
            if (hasNextPage)
            {
                books = books.Take(threshold).ToList(); // Remove the extra item
            }

            // Add navigation link for next page
            if (hasNextPage)
            {
                string nextUrl = string.Format("/downstat/{0}?pageNumber={1}",
                    sortByDate ? "date" : "alpha",
                    pageNumber + 1);

                doc.Root.Add(new XElement("link",
                    new XAttribute("href", nextUrl),
                    new XAttribute("rel", "next"),
                    new XAttribute("type", "application/atom+xml;profile=opds-catalog")));
            }

            // Add self link with current page
            string selfUrl = string.Format("/downstat/{0}{1}",
                sortByDate ? "date" : "alpha",
                pageNumber > 0 ? "?pageNumber=" + pageNumber : "");

            doc.Root.Add(new XElement("link",
                new XAttribute("href", selfUrl),
                new XAttribute("rel", "self"),
                new XAttribute("type", "application/atom+xml;profile=opds-catalog")));

            bool useCyrillic = Properties.Settings.Default.SortOrder > 0;
            var genres = Library.Genres;

            // Add book entries - EXACT COPY FROM BooksCatalog
            foreach (var book in books)
            {
                // Load full book data including authors, genres, sequences
                Book fullBook = Library.GetBook(book.ID);
                if (fullBook == null) continue;

                // Transfer LastDownloadDate from the book returned by GetRecentDownloads/GetDownloadsAlphabetic
                // to the fullBook object (which doesn't have this info)
                fullBook.LastDownloadDate = book.LastDownloadDate;

                XElement entry = new XElement("entry",
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("id", "tag:book:" + fullBook.ID),
                    new XElement("title", fullBook.Title)
                );

                // Add authors
                foreach (string author in fullBook.Authors)
                {
                    entry.Add(
                        new XElement("author",
                            new XElement("name", author),
                            new XElement("uri", "/author-details/" + Uri.EscapeDataString(author))
                    ));
                }

                // Add genres as categories
                foreach (string genreStr in fullBook.Genres)
                {
                    Genre genre = genres.Where(g => g.Tag.Equals(genreStr)).FirstOrDefault();
                    if (genre != null)
                        entry.Add(new XElement("category",
                            new XAttribute("term", (useCyrillic ? genre.Translation : genre.Name)),
                            new XAttribute("label", (useCyrillic ? genre.Translation : genre.Name))));
                }

                // Build content entry - REMOVED DOWNLOAD DATE FROM HERE
                string bookInfo = string.Empty;

                if (!string.IsNullOrEmpty(fullBook.Annotation))
                {
                    bookInfo += string.Format(@"<p>{0}<br/></p>", System.Security.SecurityElement.Escape(fullBook.Annotation.Trim()));
                }
                if (fullBook.Translators != null && fullBook.Translators.Count > 0)
                {
                    bookInfo += string.Format("<b>{0} </b>", Localizer.Text("Translation:"));
                    foreach (string translator in fullBook.Translators) bookInfo += translator + " ";
                    bookInfo += "<br/>";
                }
                if (fullBook.BookDate != DateTime.MinValue)
                {
                    bookInfo += string.Format("<b>{0}</b> {1}<br/>", Localizer.Text("Year of publication:"), fullBook.BookDate.Year);
                }

                if (!string.IsNullOrEmpty(fullBook.Sequence))
                {
                    bookInfo += string.Format("<b>{0} {1} #{2}</b><br/>", Localizer.Text("Series:"), fullBook.Sequence, fullBook.NumberInSequence);
                }

                // Add all metadata elements - EXACT FORMAT FROM BooksCatalog
                entry.Add(
                    new XElement(Namespaces.dc + "language", fullBook.Language),
                    new XElement(Namespaces.dc + "format", fullBook.BookType == BookType.FB2 ? "fb2+zip" : "epub+zip"),
                    new XElement("content", new XAttribute("type", "text/html"), XElement.Parse("<div>" + bookInfo + "<br/></div>")),
                    new XElement("format", fullBook.BookType == BookType.EPUB ? "epub" : "fb2"),
                    new XElement("size", string.Format("{0} Kb", (int)fullBook.DocumentSize / 1024)));

                // Add download date as a separate element for XSL processing
                if (fullBook.LastDownloadDate.HasValue)
                {
                    string dateFormat = "d/M/yyyy HH:mm";
                    entry.Add(new XElement("lastDownload",
                        fullBook.LastDownloadDate.Value.ToLocalTime().ToString(dateFormat)));
                }

                // Add cover and thumbnail links
                entry.Add(
                    new XElement("link",
                        new XAttribute("href", "/cover/" + fullBook.ID + ".jpeg"),
                        new XAttribute("rel", "http://opds-spec.org/image"),
                        new XAttribute("type", "image/jpeg")),
                    new XElement("link",
                        new XAttribute("href", "/cover/" + fullBook.ID + ".jpeg"),
                        new XAttribute("rel", "x-stanza-cover-image"),
                        new XAttribute("type", "image/jpeg")),
                    new XElement("link",
                        new XAttribute("href", "/thumbnail/" + fullBook.ID + ".jpeg"),
                        new XAttribute("rel", "http://opds-spec.org/thumbnail"),
                        new XAttribute("type", "image/jpeg")),
                    new XElement("link",
                        new XAttribute("href", "/thumbnail/" + fullBook.ID + ".jpeg"),
                        new XAttribute("rel", "x-stanza-cover-image-thumbnail"),
                        new XAttribute("type", "image/jpeg"))
                );

                // Add download links - EXACT FORMAT FROM BooksCatalog
                if (fullBook.BookType == BookType.EPUB || (fullBook.BookType == BookType.FB2 && !acceptFB2))
                {
                    entry.Add(new XElement("link",
                        new XAttribute("href", "/download/" + fullBook.ID + "/epub"),
                        new XAttribute("rel", "http://opds-spec.org/acquisition/open-access"),
                        new XAttribute("type", "application/epub+zip")));
                }

                if (fullBook.BookType == BookType.FB2)
                {
                    entry.Add(new XElement("link",
                        new XAttribute("href", "/download/" + fullBook.ID + "/fb2"),
                        new XAttribute("rel", "http://opds-spec.org/acquisition/open-access"),
                        new XAttribute("type", "application/fb2+zip")));
                }

                // Add navigation links for author and series
                foreach (string author in fullBook.Authors)
                {
                    entry.Add(new XElement("link",
                        new XAttribute("href", "/author-details/" + Uri.EscapeDataString(author)),
                        new XAttribute("rel", "related"),
                        new XAttribute("type", "application/atom+xml;profile=opds-catalog"),
                        new XAttribute("title", string.Format(Localizer.Text("All books by author {0}"), author))));
                }

                if (!string.IsNullOrEmpty(fullBook.Sequence))
                {
                    entry.Add(new XElement("link",
                        new XAttribute("href", "/sequence/" + Uri.EscapeDataString(fullBook.Sequence)),
                        new XAttribute("rel", "related"),
                        new XAttribute("type", "application/atom+xml;profile=opds-catalog"),
                        new XAttribute("title", string.Format(Localizer.Text("All books by series {0}"), fullBook.Sequence))));
                }

                doc.Root.Add(entry);
            }

            Log.WriteLine(LogLevel.Info, "Generated download statistics catalog: {0} books, page {1}, sorted by {2}",
                books.Count, pageNumber, sortByDate ? "date" : "alphabet");

            return doc;
        }
    }
}