/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the OPDS AuthorBooksCatalog class
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
    /// Author books catalog with different sorting and filtering options
    /// </summary>
    public class AuthorBooksCatalog
    {
        public enum ViewType
        {
            Series,
            NoSeries,
            Alphabetic,
            ByDate
        }

        /// <summary>
        /// Get all books for author including books from aliases, avoiding duplicates
        /// </summary>
        /// <param name="author">Author name (can be canonical or alias)</param>
        /// <returns>Combined list of unique books</returns>
        private List<Book> GetAllAuthorBooks(string author)
        {
            var allBooks = new Dictionary<string, Book>(); // Use dictionary to avoid duplicates by ID
            var canonicalAuthor = Library.ApplyAuthorAlias(author);

            // Add books from the requested author name (direct match)
            foreach (var book in Library.GetBooksByAuthor(author))
            {
                allBooks[book.ID] = book;
            }

            // If aliases are enabled and the requested author is canonical, 
            // also add books from all aliases that map to this canonical name
            if (Properties.Settings.Default.UseAuthorsAliases && author.Equals(canonicalAuthor))
            {
                var aliases = GetAliasesForCanonicalName(canonicalAuthor);
                foreach (var alias in aliases)
                {
                    foreach (var book in Library.GetBooksByAuthor(alias))
                    {
                        allBooks[book.ID] = book; // Dictionary handles duplicates
                    }
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
        /// Get books catalog for series list by author
        /// </summary>
        public XDocument GetSeriesCatalog(string author, bool acceptFB2, int threshold = 100)
        {
            if (!string.IsNullOrEmpty(author))
                author = Uri.UnescapeDataString(author).Replace('+', ' ');

            // Apply alias to get canonical name for display
            string displayAuthor = Library.ApplyAuthorAlias(author);

            XDocument doc = new XDocument(
                new XElement("feed",
                    new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc),
                    new XAttribute(XNamespace.Xmlns + "os", Namespaces.os),
                    new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:author-series:" + displayAuthor),
                    new XElement("title", string.Format(Localizer.Text("Books by series - {0}"), displayAuthor)),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/series.ico"),
                    Links.opensearch, Links.search, Links.start)
                );

            // Get author's books with series (including from aliases)
            List<Book> allBooks = GetAllAuthorBooks(author);
            var seriesGroups = allBooks
                .Where(b => !string.IsNullOrEmpty(b.Sequence))
                .GroupBy(b => b.Sequence)
                .OrderBy(g => g.Key, new OPDSComparer(Properties.Settings.Default.SortOrder > 0));

            foreach (var seriesGroup in seriesGroups)
            {
                var seriesBooks = seriesGroup.ToList();
                doc.Root.Add(
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:author-series:" + displayAuthor + ":" + seriesGroup.Key),
                        new XElement("title", seriesGroup.Key),
                        new XElement("content",
                            string.Format(Localizer.Text("{0} books in series"), seriesBooks.Count),
                            new XAttribute("type", "text")),
                        new XElement("link",
                            new XAttribute("href", "/sequence/" + Uri.EscapeDataString(seriesGroup.Key)),
                            new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                    )
                );
            }

            return doc;
        }

        /// <summary>
        /// Get books catalog by specific view type
        /// </summary>
        public XDocument GetBooksCatalog(string author, ViewType viewType, bool acceptFB2, int threshold = 100)
        {
            if (!string.IsNullOrEmpty(author))
                author = Uri.UnescapeDataString(author).Replace('+', ' ');

            // Extract page number if present
            int pageNumber = 0;
            int slashIndex = author.IndexOf('/');
            if (slashIndex > 0)
            {
                int.TryParse(author.Substring(slashIndex + 1), out pageNumber);
                author = author.Substring(0, slashIndex);
            }

            // Apply alias to get canonical name for display
            string displayAuthor = Library.ApplyAuthorAlias(author);

            string titleSuffix = "";
            string iconPath = "/icons/books.ico";

            switch (viewType)
            {
                case ViewType.NoSeries:
                    titleSuffix = Localizer.Text("Books without series");
                    break;
                case ViewType.Alphabetic:
                    titleSuffix = Localizer.Text("Books alphabetically");
                    break;
                case ViewType.ByDate:
                    titleSuffix = Localizer.Text("Books by creation date");
                    break;
            }

            XDocument doc = new XDocument(
                new XElement("feed",
                    new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc),
                    new XAttribute(XNamespace.Xmlns + "os", Namespaces.os),
                    new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:author-books:" + viewType.ToString().ToLower() + ":" + displayAuthor),
                    new XElement("title", string.Format("{0} - {1}", titleSuffix, displayAuthor)),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", iconPath),
                    Links.opensearch, Links.search, Links.start)
                );

            // Get and filter books based on view type (including from aliases)
            List<Book> books = GetAllAuthorBooks(author);

            switch (viewType)
            {
                case ViewType.NoSeries:
                    books = books.Where(b => string.IsNullOrEmpty(b.Sequence)).ToList();
                    break;
                case ViewType.Alphabetic:
                    // Keep all books, will sort alphabetically
                    break;
                case ViewType.ByDate:
                    // Keep all books, will sort by date
                    break;
            }

            // Sort books based on view type
            switch (viewType)
            {
                case ViewType.NoSeries:
                case ViewType.Alphabetic:
                    books = books.OrderBy(b => b.Title, new OPDSComparer(Properties.Settings.Default.SortOrder > 0)).ToList();
                    break;
                case ViewType.ByDate:
                    books = books.OrderByDescending(b => b.BookDate).ThenBy(b => b.Title).ToList();
                    break;
            }

            // Pagination
            int startIndex = pageNumber * threshold;
            int endIndex = startIndex + Math.Min(threshold, books.Count - startIndex);

            // Add next page link if needed
            if ((pageNumber + 1) * threshold < books.Count)
            {
                string nextPageUrl = "";
                switch (viewType)
                {
                    case ViewType.NoSeries:
                        nextPageUrl = "/author-no-series/" + Uri.EscapeDataString(displayAuthor) + "/" + (pageNumber + 1);
                        break;
                    case ViewType.Alphabetic:
                        nextPageUrl = "/author-alphabetic/" + Uri.EscapeDataString(displayAuthor) + "/" + (pageNumber + 1);
                        break;
                    case ViewType.ByDate:
                        nextPageUrl = "/author-by-date/" + Uri.EscapeDataString(displayAuthor) + "/" + (pageNumber + 1);
                        break;
                }

                doc.Root.Add(new XElement("link",
                    new XAttribute("href", nextPageUrl),
                    new XAttribute("rel", "next"),
                    new XAttribute("type", "application/atom+xml;profile=opds-catalog")));
            }

            bool useCyrillic = Properties.Settings.Default.SortOrder > 0;
            List<Genre> genres = Library.Genres;

            // Add book entries
            for (int i = startIndex; i < endIndex; i++)
            {
                Book book = books.ElementAt(i);

                XElement entry = new XElement("entry",
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("id", "tag:book:" + book.ID),
                    new XElement("title", book.Title)
                );

                // Apply aliases to author names in output
                foreach (string authorName in book.Authors)
                {
                    string displayAuthorName = Library.ApplyAuthorAlias(authorName);
                    entry.Add(
                        new XElement("author",
                            new XElement("name", displayAuthorName),
                            new XElement("uri", "/author-details/" + Uri.EscapeDataString(displayAuthorName))
                    ));
                }

                foreach (string genreStr in book.Genres)
                {
                    Genre genre = genres.Where(g => g.Tag.Equals(genreStr)).FirstOrDefault();
                    if (genre != null)
                        entry.Add(new XElement("category",
                            new XAttribute("term", (useCyrillic ? genre.Translation : genre.Name)),
                            new XAttribute("label", (useCyrillic ? genre.Translation : genre.Name))));
                }

                // Build content entry
                string bookInfo = string.Empty;

                if (!string.IsNullOrEmpty(book.Annotation))
                {
                    bookInfo += string.Format(@"<p>{0}<br/></p>", System.Security.SecurityElement.Escape(book.Annotation.Trim()));
                }
                if (book.Translators.Count > 0)
                {
                    bookInfo += string.Format("<b>{0} </b>", Localizer.Text("Translation:"));
                    foreach (string translator in book.Translators) bookInfo += translator + " ";
                    bookInfo += "<br/>";
                }
                if (book.BookDate != DateTime.MinValue)
                {
                    bookInfo += string.Format("<b>{0}</b> {1}<br/>", Localizer.Text("Year of publication:"), book.BookDate.Year);
                }

                if (!string.IsNullOrEmpty(book.Sequence))
                {
                    bookInfo += string.Format("<b>{0} {1} #{2}</b><br/>", Localizer.Text("Series:"), book.Sequence, book.NumberInSequence);
                }

                entry.Add(
                    new XElement(Namespaces.dc + "language", book.Language),
                    new XElement(Namespaces.dc + "format", book.BookType == BookType.FB2 ? "fb2+zip" : "epub+zip"),
                    new XElement("content", new XAttribute("type", "text/html"), XElement.Parse("<div>" + bookInfo + "<br/></div>")),
                    new XElement("format", book.BookType == BookType.EPUB ? "epub" : "fb2"),
                    new XElement("size", string.Format("{0} Kb", (int)book.DocumentSize / 1024)));

                // Add cover and thumbnail links
                entry.Add(
                    new XElement("link", new XAttribute("href", "/cover/" + book.ID + ".jpeg"), new XAttribute("rel", "http://opds-spec.org/image"), new XAttribute("type", "image/jpeg")),
                    new XElement("link", new XAttribute("href", "/cover/" + book.ID + ".jpeg"), new XAttribute("rel", "x-stanza-cover-image"), new XAttribute("type", "image/jpeg")),
                    new XElement("link", new XAttribute("href", "/thumbnail/" + book.ID + ".jpeg"), new XAttribute("rel", "http://opds-spec.org/thumbnail"), new XAttribute("type", "image/jpeg")),
                    new XElement("link", new XAttribute("href", "/thumbnail/" + book.ID + ".jpeg"), new XAttribute("rel", "x-stanza-cover-image-thumbnail"), new XAttribute("type", "image/jpeg"))
                );

                // Add download links - use canonical author name
                string canonicalFirstAuthor = book.Authors.Any() ? Library.ApplyAuthorAlias(book.Authors.First()) : "Unknown";
                string fileName = Uri.EscapeDataString(Transliteration.Front(string.Format("{0}_{1}", canonicalFirstAuthor, book.Title)).SanitizeFileName());
                string url = "/" + string.Format("{0}/{1}", book.ID, fileName);

                if (book.BookType == BookType.EPUB || (book.BookType == BookType.FB2 && !acceptFB2 && !string.IsNullOrEmpty(Properties.Settings.Default.ConvertorPath)))
                {
                    entry.Add(new XElement("link",
                        new XAttribute("href", url + ".epub"),
                        new XAttribute("rel", "http://opds-spec.org/acquisition/open-access"),
                        new XAttribute("type", "application/epub+zip")));
                }

                if (book.BookType == BookType.FB2)
                {
                    entry.Add(new XElement("link",
                        new XAttribute("href", url + ".fb2.zip"),
                        new XAttribute("rel", "http://opds-spec.org/acquisition/open-access"),
                        new XAttribute("type", "application/fb2+zip")));
                }

                // Add navigation links for series if book has series and we're not in series view
                if (!string.IsNullOrEmpty(book.Sequence))
                {
                    entry.Add(new XElement("link",
                        new XAttribute("href", "/sequence/" + Uri.EscapeDataString(book.Sequence)),
                        new XAttribute("rel", "related"),
                        new XAttribute("type", "application/atom+xml;profile=opds-catalog"),
                        new XAttribute("title", string.Format(Localizer.Text("All books by series {0}"), book.Sequence))));
                }

                doc.Root.Add(entry);
            }

            return doc;
        }
    }
}