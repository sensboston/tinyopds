﻿/*
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
        /// Get books catalog for series list by author
        /// </summary>
        public XDocument GetSeriesCatalog(string author, bool acceptFB2, int threshold = 100)
        {
            // Decode URL-encoded author name properly for Cyrillic
            if (!string.IsNullOrEmpty(author))
            {
                try
                {
                    string originalAuthor = author;
                    author = Uri.UnescapeDataString(author).Replace('+', ' ');
                    Log.WriteLine(LogLevel.Info, "GetSeriesCatalog author name decoded: '{0}' -> '{1}'", originalAuthor, author);
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Warning, "Error decoding author name '{0}': {1}", author, ex.Message);
                }
            }

            XDocument doc = new XDocument(
                new XElement("feed",
                    new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc),
                    new XAttribute(XNamespace.Xmlns + "os", Namespaces.os),
                    new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:author-series:" + author),
                    new XElement("title", string.Format(Localizer.Text("Books by series - {0}"), author)),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/series.ico"),
                    Links.opensearch, Links.search, Links.start)
                );

            // Get author's books with series - database now contains canonical names
            List<Book> books = Library.GetBooksByAuthor(author);
            Log.WriteLine(LogLevel.Info, "Found {0} total books for author '{1}'", books.Count, author);

            var seriesGroups = books
                .Where(b => !string.IsNullOrEmpty(b.Sequence))
                .GroupBy(b => b.Sequence)
                .OrderBy(g => g.Key, new OPDSComparer(Properties.Settings.Default.SortOrder > 0));

            Log.WriteLine(LogLevel.Info, "Found {0} series for author '{1}'", seriesGroups.Count(), author);

            foreach (var seriesGroup in seriesGroups)
            {
                var seriesBooks = seriesGroup.ToList();
                doc.Root.Add(
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:author-series:" + author + ":" + seriesGroup.Key),
                        new XElement("title", seriesGroup.Key),
                        new XElement("content",
                            StringUtils.ApplyPluralForm(seriesBooks.Count, Localizer.Language,
                                string.Format(Localizer.Text("{0} books in series"), seriesBooks.Count)),
                            new XAttribute("type", "text")),
                        new XElement("link",
                            // FIX: Include author in URL to maintain filter context
                            new XAttribute("href", "/author-sequence/" + Uri.EscapeDataString(author) + "/" + Uri.EscapeDataString(seriesGroup.Key)),
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
            // Decode URL-encoded author name properly for Cyrillic
            if (!string.IsNullOrEmpty(author))
            {
                try
                {
                    string originalAuthor = author;
                    author = Uri.UnescapeDataString(author).Replace('+', ' ');
                    Log.WriteLine(LogLevel.Info, "GetBooksCatalog author name decoded: '{0}' -> '{1}'", originalAuthor, author);
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Warning, "Error decoding author name '{0}': {1}", author, ex.Message);
                }
            }

            // Extract page number if present
            int pageNumber = 0;
            int slashIndex = author.IndexOf('/');
            if (slashIndex > 0)
            {
                int.TryParse(author.Substring(slashIndex + 1), out pageNumber);
                author = author.Substring(0, slashIndex);
                Log.WriteLine(LogLevel.Info, "Extracted page number {0} for author '{1}'", pageNumber, author);
            }

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
                    new XElement("id", "tag:author-books:" + viewType.ToString().ToLower() + ":" + author),
                    new XElement("title", string.Format("{0} - {1}", titleSuffix, author)),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", iconPath),
                    Links.opensearch, Links.search, Links.start)
                );

            // Get and filter books based on view type - database now contains canonical names
            List<Book> books = Library.GetBooksByAuthor(author);
            Log.WriteLine(LogLevel.Info, "Found {0} total books for author '{1}' view '{2}'", books.Count, author, viewType);

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

            Log.WriteLine(LogLevel.Info, "After filtering: {0} books for author '{1}' view '{2}'", books.Count, author, viewType);

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

            Log.WriteLine(LogLevel.Info, "Pagination for author '{0}': page {1}, showing books {2}-{3} of {4}",
                author, pageNumber, startIndex, endIndex - 1, books.Count);

            // Add next page link if needed
            if ((pageNumber + 1) * threshold < books.Count)
            {
                string nextPageUrl = "";
                switch (viewType)
                {
                    case ViewType.NoSeries:
                        nextPageUrl = "/author-no-series/" + Uri.EscapeDataString(author) + "/" + (pageNumber + 1);
                        break;
                    case ViewType.Alphabetic:
                        nextPageUrl = "/author-alphabetic/" + Uri.EscapeDataString(author) + "/" + (pageNumber + 1);
                        break;
                    case ViewType.ByDate:
                        nextPageUrl = "/author-by-date/" + Uri.EscapeDataString(author) + "/" + (pageNumber + 1);
                        break;
                }

                doc.Root.Add(new XElement("link",
                    new XAttribute("href", nextPageUrl),
                    new XAttribute("rel", "next"),
                    new XAttribute("type", "application/atom+xml;profile=opds-catalog"),
                    new XAttribute("title", string.Format(Localizer.Text("Page {0}"), pageNumber + 2))));
            }

            // Add previous page link if needed
            if (pageNumber > 0)
            {
                string prevPageUrl = "";
                switch (viewType)
                {
                    case ViewType.NoSeries:
                        prevPageUrl = "/author-no-series/" + Uri.EscapeDataString(author) + "/" + (pageNumber - 1);
                        break;
                    case ViewType.Alphabetic:
                        prevPageUrl = "/author-alphabetic/" + Uri.EscapeDataString(author) + "/" + (pageNumber - 1);
                        break;
                    case ViewType.ByDate:
                        prevPageUrl = "/author-by-date/" + Uri.EscapeDataString(author) + "/" + (pageNumber - 1);
                        break;
                }

                doc.Root.Add(new XElement("link",
                    new XAttribute("href", prevPageUrl),
                    new XAttribute("rel", "previous"),
                    new XAttribute("type", "application/atom+xml;profile=opds-catalog"),
                    new XAttribute("title", string.Format(Localizer.Text("Page {0}"), pageNumber))));
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

                // Author names are already canonical in database
                foreach (string authorName in book.Authors)
                {
                    entry.Add(
                        new XElement("author",
                            new XElement("name", authorName),
                            new XElement("uri", "/author-details/" + Uri.EscapeDataString(authorName))
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

                // Build a plain text content entry (translator(s), year, annotation etc.)
                string plainText = "";

                // Add annotation first
                if (!string.IsNullOrEmpty(book.Annotation))
                {
                    plainText = book.Annotation.Trim();
                }

                // Add translators on new line
                if (book.Translators.Count > 0)
                {
                    if (!string.IsNullOrEmpty(plainText)) plainText += "\n";
                    plainText += Localizer.Text("Translation:") + " " + string.Join(", ", book.Translators);
                }

                // Add year on new line
                if (book.BookDate != DateTime.MinValue)
                {
                    if (!string.IsNullOrEmpty(plainText)) plainText += "\n";
                    plainText += Localizer.Text("Year of publication:") + " " + book.BookDate.Year;
                }

                // Add series on new line
                if (!string.IsNullOrEmpty(book.Sequence))
                {
                    if (!string.IsNullOrEmpty(plainText)) plainText += "\n";
                    plainText += Localizer.Text("Series:") + " " + book.Sequence + " #" + book.NumberInSequence;
                }

                entry.Add(
                    new XElement(Namespaces.dc + "language", book.Language),
                    new XElement(Namespaces.dc + "format", book.BookType == BookType.FB2 ? "fb2+zip" : "epub+zip"),
                    new XElement("content", new XAttribute("type", "text"), plainText),
                    new XElement("format", book.BookType == BookType.EPUB ? "epub" : "fb2"),
                    new XElement("size", string.Format("{0} Kb", (int)book.DocumentSize / 1024)));

                // Add cover and thumbnail links
                entry.Add(
                    new XElement("link", new XAttribute("href", "/cover/" + book.ID + ".jpeg"), new XAttribute("rel", "http://opds-spec.org/image"), new XAttribute("type", "image/jpeg")),
                    new XElement("link", new XAttribute("href", "/cover/" + book.ID + ".jpeg"), new XAttribute("rel", "x-stanza-cover-image"), new XAttribute("type", "image/jpeg")),
                    new XElement("link", new XAttribute("href", "/thumbnail/" + book.ID + ".jpeg"), new XAttribute("rel", "http://opds-spec.org/thumbnail"), new XAttribute("type", "image/jpeg")),
                    new XElement("link", new XAttribute("href", "/thumbnail/" + book.ID + ".jpeg"), new XAttribute("rel", "x-stanza-cover-image-thumbnail"), new XAttribute("type", "image/jpeg"))
                );

                // Add download links - NEW FORMAT WITHOUT FILENAME
                if (book.BookType == BookType.EPUB || (book.BookType == BookType.FB2 && !acceptFB2))
                {
                    entry.Add(new XElement("link",
                        new XAttribute("href", "/download/" + book.ID + "/epub"),
                        new XAttribute("rel", "http://opds-spec.org/acquisition/open-access"),
                        new XAttribute("type", "application/epub+zip")));
                }

                if (book.BookType == BookType.FB2)
                {
                    entry.Add(new XElement("link",
                        new XAttribute("href", "/download/" + book.ID + "/fb2"),
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

            Log.WriteLine(LogLevel.Info, "Generated {0} book entries for author '{1}' view '{2}' page {3}",
                endIndex - startIndex, author, viewType, pageNumber);

            return doc;
        }
    }
}
