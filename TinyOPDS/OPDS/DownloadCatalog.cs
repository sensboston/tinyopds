/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the OPDS DownloadCatalog class
 * Provides access to downloaded/read books history
 *
 */

using System;
using System.Linq;
using System.Xml.Linq;

using TinyOPDS.Data;

namespace TinyOPDS.OPDS
{
    /// <summary>
    /// Downloaded books catalog class
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
                    new XElement("id", "tag:downloads"),
                    new XElement("title", Localizer.Text("Downloaded books")),
                    new XElement("subtitle", string.Format(Localizer.Text("{0} downloaded books"), downloadsCount)),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/downloads.ico"),
                    Links.opensearch, Links.search, Links.start)
                );

            // Only show entries if we have downloaded books
            if (downloadsCount > 0)
            {
                // Entry for books sorted by download date
                doc.Root.Add(new XElement("entry",
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("id", "tag:downloads:bydate"),
                    new XElement("title", Localizer.Text("By download date"), new XAttribute("type", "text")),
                    new XElement("content", string.Format(Localizer.Text("{0} books sorted by download date"), downloadsCount),
                        new XAttribute("type", "text")),
                    new XElement("link",
                        new XAttribute("href", "/downloads/date"),
                        new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                ));

                // Entry for books sorted alphabetically
                doc.Root.Add(new XElement("entry",
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("id", "tag:downloads:alphabetic"),
                    new XElement("title", Localizer.Text("Alphabetically"), new XAttribute("type", "text")),
                    new XElement("content", string.Format(Localizer.Text("{0} books sorted alphabetically"), downloadsCount),
                        new XAttribute("type", "text")),
                    new XElement("link",
                        new XAttribute("href", "/downloads/alpha"),
                        new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                ));
            }
            else
            {
                // Show message when no downloads
                doc.Root.Add(new XElement("entry",
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("id", "tag:downloads:empty"),
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
            string catalogId = sortByDate ? "tag:downloads:date" : "tag:downloads:alpha";
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
                    new XElement("icon", "/downloads.ico"),
                    Links.opensearch, Links.search, Links.start)
                );

            // Get books from library
            int offset = pageNumber * threshold;
            System.Collections.Generic.List<Book> books;
            int totalCount;

            if (sortByDate)
            {
                books = Library.GetRecentDownloads(threshold + 1, offset); // Get one extra to check for next page
            }
            else
            {
                books = Library.GetDownloadsAlphabetic(threshold + 1, offset);
            }
            totalCount = Library.GetUniqueDownloadsCount();

            // Check if there's a next page
            bool hasNextPage = books.Count > threshold;
            if (hasNextPage)
            {
                books = books.Take(threshold).ToList(); // Remove the extra item
            }

            // Add navigation link for next page
            if (hasNextPage)
            {
                string nextUrl = string.Format("/downloads/{0}?pageNumber={1}",
                    sortByDate ? "date" : "alpha",
                    pageNumber + 1);

                doc.Root.Add(new XElement("link",
                    new XAttribute("href", nextUrl),
                    new XAttribute("rel", "next"),
                    new XAttribute("type", "application/atom+xml;profile=opds-catalog")));
            }

            // Add self link with current page
            string selfUrl = string.Format("/downloads/{0}{1}",
                sortByDate ? "date" : "alpha",
                pageNumber > 0 ? "?pageNumber=" + pageNumber : "");

            doc.Root.Add(new XElement("link",
                new XAttribute("href", selfUrl),
                new XAttribute("rel", "self"),
                new XAttribute("type", "application/atom+xml;profile=opds-catalog")));

            // Add book entries
            foreach (var book in books)
            {
                // Load full book data including authors, genres, sequences
                Book fullBook = Library.GetBook(book.ID);
                if (fullBook == null) continue;

                XElement entry = new XElement("entry",
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("id", "tag:book:" + fullBook.ID),
                    new XElement("title", fullBook.Title)
                );

                // Add authors
                foreach (string author in fullBook.Authors)
                {
                    entry.Add(new XElement("author",
                        new XElement("name", author),
                        new XElement("uri", "/author-details/" + Uri.EscapeDataString(author))
                    ));
                }

                // Add genres as categories
                bool useCyrillic = Properties.Settings.Default.SortOrder > 0;
                var genres = Library.Genres;
                foreach (string genreStr in fullBook.Genres)
                {
                    var genre = genres.Where(g => g.Tag.Equals(genreStr)).FirstOrDefault();
                    if (genre != null)
                    {
                        entry.Add(new XElement("category",
                            new XAttribute("term", useCyrillic ? genre.Translation : genre.Name),
                            new XAttribute("label", useCyrillic ? genre.Translation : genre.Name)));
                    }
                }

                // Build content with book info
                string bookInfo = string.Empty;
                if (!string.IsNullOrEmpty(fullBook.Sequence))
                {
                    bookInfo = string.Format("{0} - {1}", fullBook.Sequence, fullBook.NumberInSequence);
                }
                if (fullBook.BookDate != DateTime.MinValue && fullBook.BookDate.Year > 1900)
                {
                    if (!string.IsNullOrEmpty(bookInfo)) bookInfo += "<br/>";
                    bookInfo += fullBook.BookDate.Year.ToString();
                }
                if (!string.IsNullOrEmpty(fullBook.Language))
                {
                    if (!string.IsNullOrEmpty(bookInfo)) bookInfo += "<br/>";
                    bookInfo += string.Format(Localizer.Text("Language: {0}"), fullBook.Language);
                }
                if (fullBook.DocumentSize > 0)
                {
                    if (!string.IsNullOrEmpty(bookInfo)) bookInfo += "<br/>";
                    bookInfo += string.Format("{0} KB", (int)fullBook.DocumentSize / 1024);
                }
                if (!string.IsNullOrEmpty(fullBook.Annotation))
                {
                    if (!string.IsNullOrEmpty(bookInfo)) bookInfo += "<br/><br/>";
                    string annotation = fullBook.Annotation;
                    if (annotation.Length > 500)
                    {
                        annotation = annotation.Substring(0, 500) + "...";
                    }
                    bookInfo += annotation;
                }

                entry.Add(new XElement("content",
                    new XAttribute("type", "text/html"),
                    XElement.Parse("<div>" + bookInfo + "</div>")));

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

                // Add download links
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

            Log.WriteLine(LogLevel.Info, "Generated download catalog: {0} books, page {1}, sorted by {2}",
                books.Count, pageNumber, sortByDate ? "date" : "alphabet");

            return doc;
        }
    }
}