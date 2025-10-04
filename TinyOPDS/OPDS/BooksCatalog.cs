/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the OPDS BookCatalog class
 * FIXED: Display correct sequence numbers when browsing by series
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using TinyOPDS.Data;

namespace TinyOPDS.OPDS
{
    public class BooksCatalog
    {
        private enum SearchFor
        {
            Sequence,
            Genre,
            Title,
        }

        /// <summary>
        /// Returns books catalog by selected sequence (series)
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public XDocument GetCatalogBySequence(string sequence, bool fb2Only, int threshold = 100)
        {
            return GetCatalog(sequence, SearchFor.Sequence, fb2Only, threshold, false);
        }

        /// <summary>
        /// Returns books catalog by selected genre
        /// </summary>
        /// <param name="author"></param>
        /// <returns></returns>
        public XDocument GetCatalogByGenre(string genre, bool fb2Only, int threshold = 100)
        {
            return GetCatalog(genre, SearchFor.Genre, fb2Only, threshold, false);
        }

        /// <summary>
        /// Returns books catalog by selected title
        /// </summary>
        /// <param name="title">Title to search for</param>
        /// <param name="fb2Only">FB2 only flag</param>
        /// <param name="pageNumber">Page number</param>
        /// <param name="threshold">Items per page</param>
        /// <param name="isOpenSearch">Whether this is OpenSearch (enables Soundex)</param>
        /// <returns></returns>
        public XDocument GetCatalogByTitle(string title, bool fb2Only, int pageNumber = 0, int threshold = 100, bool isOpenSearch = false)
        {
            return GetCatalog(title, SearchFor.Title, fb2Only, threshold, isOpenSearch);
        }

        /// <summary>
        /// Returns books catalog for specific search
        /// </summary>
        /// <param name="searchPattern">Keyword to search</param>
        /// <param name="searchFor">Type of search</param>
        /// <param name="acceptFB2">Client can accept fb2 files</param>
        /// <param name="threshold">Items per page</param>
        /// <param name="isOpenSearch">Whether this is OpenSearch (enables Soundex)</param>
        /// <returns></returns>
        private XDocument GetCatalog(string searchPattern, SearchFor searchFor, bool acceptFB2, int threshold = 100, bool isOpenSearch = false)
        {
            if (!string.IsNullOrEmpty(searchPattern)) searchPattern = Uri.UnescapeDataString(searchPattern).Replace('+', ' ');

            // Create proper title based on search type
            string feedTitle;
            switch (searchFor)
            {
                case SearchFor.Sequence:
                    feedTitle = Localizer.Text("Books in series: ") + searchPattern;
                    break;
                case SearchFor.Genre:
                    bool useRu = Properties.Settings.Default.Language.Equals("ru") || Properties.Settings.Default.Language.Equals("uk");
                    string genreName = searchPattern;
                    foreach (var mainGenre in Library.FB2Genres)
                    {
                        // Check if searchPattern is a subgenre tag
                        var subgenre = mainGenre.Subgenres.FirstOrDefault(sg => sg.Tag.Equals(searchPattern));
                        if (subgenre != null)
                        {
                            genreName = useRu ? subgenre.Translation : subgenre.Name;
                            break;
                        }
                    }
                    feedTitle = Localizer.Text("Books by genre: ") + genreName;
                    break;
                case SearchFor.Title:
                    if (isOpenSearch)
                    {
                        // For OpenSearch, show search results title
                        feedTitle = string.Format(Localizer.Text("Search results for books: «{0}»"), searchPattern);
                    }
                    else
                    {
                        // For navigation, show books starting with
                        feedTitle = string.Format(Localizer.Text("Books starting with «{0}»"), searchPattern);
                    }
                    break;
                default:
                    feedTitle = Localizer.Text("Books catalog");
                    break;
            }

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:books"),
                    new XElement("title", feedTitle),  // Use dynamic title based on search type
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/icons/books.ico"),
                    // Add links
                    Links.opensearch, Links.search, Links.start)
                );

            int pageNumber = 0;
            // Extract and remove page number from the search pattern
            int j = searchPattern.IndexOf('/');
            if (j > 0)
            {
                int.TryParse(searchPattern.Substring(j + 1), out pageNumber);
                searchPattern = searchPattern.Substring(0, j);
            }

            // Get books
            string catalogType = string.Empty;
            List<Book> books = new List<Book>();
            switch (searchFor)
            {
                case SearchFor.Sequence:
                    books = Library.GetBooksBySequence(searchPattern);
                    catalogType = "/sequence/" + Uri.EscapeDataString(searchPattern);
                    break;
                case SearchFor.Genre:
                    books = Library.GetBooksByGenre(searchPattern);
                    catalogType = "/genre/" + Uri.EscapeDataString(searchPattern);
                    break;
                case SearchFor.Title:
                    // Use OpenSearch flag for enhanced search with Soundex
                    books = Library.GetBooksByTitle(searchPattern, isOpenSearch);
                    // For search, also return books by transliterated titles
                    if (threshold > 50)
                    {
                        string translit = Transliteration.Back(searchPattern, TransliterationType.GOST);
                        if (!string.IsNullOrEmpty(translit))
                        {
                            List<Book> transTitles = Library.GetBooksByTitle(translit, isOpenSearch);
                            if (transTitles.Count > 0) books.AddRange(transTitles);
                        }
                    }
                    break;
            }

            // Sort books based on search type
            if (searchFor == SearchFor.Sequence)
            {
                // For sequences, sort by sequence number
                // FIXED: Now uses correct sequence number from first element
                books = books.OrderBy(b => b.NumberInSequence).ToList();
            }
            else if (searchFor == SearchFor.Title && isOpenSearch)
            {
                // For OpenSearch title search, prioritize titles starting with search pattern
                string lowerPattern = searchPattern.ToLower();
                books = books
                    .OrderBy(b =>
                    {
                        string lowerTitle = b.Title.ToLower();
                        // Priority 1: Exact match
                        if (lowerTitle == lowerPattern) return 0;
                        // Priority 2: Starts with search pattern
                        if (lowerTitle.StartsWith(lowerPattern)) return 1;
                        // Priority 3: Word boundary match (space before pattern)
                        if (lowerTitle.Contains(" " + lowerPattern)) return 2;
                        // Priority 4: Contains anywhere
                        return 3;
                    })
                    .ThenBy(b => b.Title, new OPDSComparer(Properties.Settings.Default.SortOrder > 0))
                    .ToList();
            }
            else
            {
                // Default: sort by title
                books = books.OrderBy(b => b.Title, new OPDSComparer(Properties.Settings.Default.SortOrder > 0)).ToList();
            }

            int startIndex = pageNumber * threshold;
            int endIndex = startIndex + ((books.Count / threshold == 0) ? books.Count : Math.Min(threshold, books.Count - startIndex));

            if (searchFor == SearchFor.Title)
            {
                if ((pageNumber + 1) * threshold < books.Count)
                {
                    catalogType = string.Format("/search?searchType=books&searchTerm={0}&pageNumber={1}", Uri.EscapeDataString(searchPattern), pageNumber + 1);
                    doc.Root.Add(new XElement("link",
                                    new XAttribute("href", catalogType),
                                    new XAttribute("rel", "next"),
                                    new XAttribute("type", "application/atom+xml;profile=opds-catalog")));
                }
            }
            else if ((pageNumber + 1) * threshold < books.Count)
            {
                catalogType += "/" + (pageNumber + 1);
                doc.Root.Add(new XElement("link",
                                new XAttribute("href", catalogType),
                                new XAttribute("rel", "next"),
                                new XAttribute("type", "application/atom+xml;profile=opds-catalog")));

            }

            bool useCyrillic = Properties.Settings.Default.SortOrder > 0;

            List<Genre> genres = Library.Genres;

            // Add catalog entries
            for (int i = startIndex; i < endIndex; i++)
            {
                Book book = books.ElementAt(i);

                XElement entry =
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:book:" + book.ID),
                        new XElement("title", book.Title)
                );

                // Author names are already canonical in database
                foreach (string author in book.Authors)
                {
                    entry.Add(
                        new XElement("author",
                            new XElement("name", author),
                            new XElement("uri", "/author-details/" + Uri.EscapeDataString(author)
                    )));
                }

                foreach (string genreStr in book.Genres)
                {
                    Genre genre = genres.Where(g => g.Tag.Equals(genreStr)).FirstOrDefault();
                    if (genre != null)
                        entry.Add(new XElement("category", new XAttribute("term", (useCyrillic ? genre.Translation : genre.Name)), new XAttribute("label", (useCyrillic ? genre.Translation : genre.Name))));
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
                if (book.BookDate.Year > 1800 && book.BookDate.Year <= DateTime.Now.Year)
                {
                    if (!string.IsNullOrEmpty(plainText)) plainText += "\n";
                    plainText += Localizer.Text("Year of publication:") + " " + book.BookDate.Year;
                }

                // Add series on new line
                // FIXED: Display correct sequence number based on search context
                if (book.Sequences != null && book.Sequences.Count > 0)
                {
                    if (!string.IsNullOrEmpty(plainText)) plainText += "\n";

                    // If searching by sequence, show that specific sequence with correct number
                    if (searchFor == SearchFor.Sequence)
                    {
                        // Find the requested sequence in the book's sequence list
                        var requestedSequence = book.Sequences.FirstOrDefault(s => s.Name == searchPattern);
                        if (requestedSequence != null)
                        {
                            plainText += Localizer.Text("Series:") + " " + requestedSequence.Name;
                            if (requestedSequence.NumberInSequence > 0)
                                plainText += " #" + requestedSequence.NumberInSequence;
                        }
                    }
                    else
                    {
                        // For other searches, show the primary sequence (first in list)
                        var primarySequence = book.Sequences.First();
                        plainText += Localizer.Text("Series:") + " " + primarySequence.Name;
                        if (primarySequence.NumberInSequence > 0)
                            plainText += " #" + primarySequence.NumberInSequence;
                    }
                }

                entry.Add(
                    new XElement(Namespaces.dc + "language", book.Language),
                    new XElement(Namespaces.dc + "format", book.BookType == BookType.FB2 ? "fb2+zip" : "epub+zip"),
                    new XElement("content", new XAttribute("type", "text"), plainText),
                    new XElement("format", book.BookType == BookType.EPUB ? "epub" : "fb2"),
                    new XElement("size", string.Format("{0} Kb", (int)book.DocumentSize / 1024))
                );

                entry.Add(
                    // Adding cover page and thumbnail links
                    new XElement("link", new XAttribute("href", "/cover/" + book.ID + ".jpeg"), new XAttribute("rel", "http://opds-spec.org/image"), new XAttribute("type", "image/jpeg")),
                    new XElement("link", new XAttribute("href", "/cover/" + book.ID + ".jpeg"), new XAttribute("rel", "x-stanza-cover-image"), new XAttribute("type", "image/jpeg")),
                    new XElement("link", new XAttribute("href", "/thumbnail/" + book.ID + ".jpeg"), new XAttribute("rel", "http://opds-spec.org/thumbnail"), new XAttribute("type", "image/jpeg")),
                    new XElement("link", new XAttribute("href", "/thumbnail/" + book.ID + ".jpeg"), new XAttribute("rel", "x-stanza-cover-image-thumbnail"), new XAttribute("type", "image/jpeg"))
                // Adding download links
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

                // Add navigation links for author and series - author names already canonical
                foreach (string author in book.Authors)
                {
                    entry.Add(new XElement("link",
                                    new XAttribute("href", "/author-details/" + Uri.EscapeDataString(author)),
                                    new XAttribute("rel", "related"),
                                    new XAttribute("type", "application/atom+xml;profile=opds-catalog"),
                                    new XAttribute("title", string.Format(Localizer.Text("All books by author {0}"), author))));
                }

                // FIXED: Use the new Sequences list for navigation link
                if (searchFor != SearchFor.Sequence && book.Sequences != null && book.Sequences.Count > 0)
                {
                    var primarySequence = book.Sequences.First();
                    entry.Add(new XElement("link",
                                 new XAttribute("href", "/sequence/" + Uri.EscapeDataString(primarySequence.Name)),
                                 new XAttribute("rel", "related"),
                                 new XAttribute("type", "application/atom+xml;profile=opds-catalog"),
                                 new XAttribute("title", string.Format(Localizer.Text("All books by series {0}"), primarySequence.Name))));
                }

                doc.Root.Add(entry);
            }
            return doc;
        }


        /// <summary>
        /// Returns books catalog by selected author and sequence
        /// Filters books to show only those from the specified author in the specified sequence
        /// </summary>
        /// <param name="author">Author name (URL-encoded)</param>
        /// <param name="sequence">Sequence name (URL-encoded)</param>
        /// <param name="acceptFB2">Client can accept FB2 files</param>
        /// <param name="threshold">Items per page</param>
        /// <returns>OPDS catalog document</returns>
        public XDocument GetCatalogByAuthorAndSequence(string author, string sequence, bool acceptFB2, int threshold = 100)
        {
            // Decode parameters
            if (!string.IsNullOrEmpty(author))
                author = Uri.UnescapeDataString(author).Replace('+', ' ');
            if (!string.IsNullOrEmpty(sequence))
                sequence = Uri.UnescapeDataString(sequence).Replace('+', ' ');

            Log.WriteLine(LogLevel.Info, "GetCatalogByAuthorAndSequence: author='{0}', sequence='{1}'", author, sequence);

            // Create feed document with proper title showing both author and sequence
            XDocument doc = new XDocument(
                new XElement("feed",
                    new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc),
                    new XAttribute(XNamespace.Xmlns + "os", Namespaces.os),
                    new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:author-sequence:" + author + ":" + sequence),
                    new XElement("title", string.Format("{0} - {1}", sequence, author)),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/icons/books.ico"),
                    Links.opensearch, Links.search, Links.start)
            );

            // Get all books in the sequence
            List<Book> books = Library.GetBooksBySequence(sequence);

            // Filter by author (case-insensitive comparison)
            books = books.Where(b => b.Authors != null &&
                                    b.Authors.Any(a => a.Equals(author, StringComparison.OrdinalIgnoreCase)))
                         .ToList();

            // Filter by format if needed
            if (!acceptFB2)
            {
                books = books.Where(b => System.IO.Path.GetExtension(b.FileName).ToLower() != ".fb2").ToList();
            }

            // Sort books by sequence number (using the new Sequences structure)
            books = books.OrderBy(b =>
            {
                // Find the sequence number for this specific sequence
                var seq = b.Sequences?.FirstOrDefault(s => s.Name == sequence);
                return seq?.NumberInSequence ?? 0;
            })
                .ThenBy(b => b.Title)
                .ToList();

            Log.WriteLine(LogLevel.Info, "Found {0} books for author '{1}' in sequence '{2}'",
                books.Count, author, sequence);

            // Prepare for entry creation
            bool useCyrillic = Properties.Settings.Default.SortOrder > 0;
            List<Genre> genres = Library.Genres;

            // Add book entries
            foreach (Book book in books)
            {
                XElement entry = new XElement("entry",
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("id", "tag:book:" + book.ID),
                    new XElement("title", book.Title)
                );

                // Add authors
                foreach (string authorName in book.Authors)
                {
                    entry.Add(
                        new XElement("author",
                            new XElement("name", authorName),
                            new XElement("uri", "/author-details/" + Uri.EscapeDataString(authorName))
                    ));
                }

                // Add genres
                foreach (string genreStr in book.Genres)
                {
                    Genre genre = genres.Where(g => g.Tag.Equals(genreStr)).FirstOrDefault();
                    if (genre != null)
                        entry.Add(new XElement("category",
                            new XAttribute("term", (useCyrillic ? genre.Translation : genre.Name)),
                            new XAttribute("label", (useCyrillic ? genre.Translation : genre.Name))));
                }

                // Build content text
                string plainText = "";

                if (!string.IsNullOrEmpty(book.Annotation))
                {
                    plainText = book.Annotation.Trim();
                }

                if (book.Translators.Count > 0)
                {
                    if (!string.IsNullOrEmpty(plainText)) plainText += "\n";
                    plainText += Localizer.Text("Translation:") + " " + string.Join(", ", book.Translators);
                }

                if (book.BookDate.Year > 1800 && book.BookDate.Year <= DateTime.Now.Year)
                {
                    if (!string.IsNullOrEmpty(plainText)) plainText += "\n";
                    plainText += Localizer.Text("Year of publication:") + " " + book.BookDate.Year;
                }

                // Add series info - show the correct number for this specific sequence
                if (book.Sequences != null && book.Sequences.Count > 0)
                {
                    var requestedSequence = book.Sequences.FirstOrDefault(s => s.Name == sequence);
                    if (requestedSequence != null)
                    {
                        if (!string.IsNullOrEmpty(plainText)) plainText += "\n";
                        plainText += Localizer.Text("Series:") + " " + requestedSequence.Name;
                        if (requestedSequence.NumberInSequence > 0)
                            plainText += " #" + requestedSequence.NumberInSequence;
                    }
                }

                entry.Add(
                    new XElement(Namespaces.dc + "language", book.Language),
                    new XElement(Namespaces.dc + "format", book.BookType == BookType.FB2 ? "fb2+zip" : "epub+zip"),
                    new XElement("content", new XAttribute("type", "text"), plainText),
                    new XElement("format", book.BookType == BookType.EPUB ? "epub" : "fb2"),
                    new XElement("size", string.Format("{0} Kb", (int)book.DocumentSize / 1024))
                );

                // Add cover and thumbnail links
                entry.Add(
                    new XElement("link", new XAttribute("href", "/cover/" + book.ID + ".jpeg"),
                        new XAttribute("rel", "http://opds-spec.org/image"), new XAttribute("type", "image/jpeg")),
                    new XElement("link", new XAttribute("href", "/cover/" + book.ID + ".jpeg"),
                        new XAttribute("rel", "x-stanza-cover-image"), new XAttribute("type", "image/jpeg")),
                    new XElement("link", new XAttribute("href", "/thumbnail/" + book.ID + ".jpeg"),
                        new XAttribute("rel", "http://opds-spec.org/thumbnail"), new XAttribute("type", "image/jpeg")),
                    new XElement("link", new XAttribute("href", "/thumbnail/" + book.ID + ".jpeg"),
                        new XAttribute("rel", "x-stanza-cover-image-thumbnail"), new XAttribute("type", "image/jpeg"))
                );

                // Add download links
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

                // Add navigation links for all authors of this book
                foreach (string bookAuthor in book.Authors ?? new List<string>())
                {
                    entry.Add(new XElement("link",
                        new XAttribute("href", "/author-details/" + Uri.EscapeDataString(bookAuthor)),
                        new XAttribute("rel", "related"),
                        new XAttribute("type", "application/atom+xml;profile=opds-catalog"),
                        new XAttribute("title", string.Format(Localizer.Text("All books by author {0}"), bookAuthor))));
                }

                // Add link to view ALL books in this series (without author filter)
                entry.Add(new XElement("link",
                    new XAttribute("href", "/sequence/" + Uri.EscapeDataString(sequence)),
                    new XAttribute("rel", "related"),
                    new XAttribute("type", "application/atom+xml;profile=opds-catalog"),
                    new XAttribute("title", string.Format(Localizer.Text("All books by series {0}"), sequence))));

                doc.Root.Add(entry);
            }

            return doc;
        }

    }
}