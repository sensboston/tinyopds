/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the OPDS NewBooksCatalog class
 *
 */

using System;
using System.Linq;
using System.Xml.Linq;

using TinyOPDS.Data;

namespace TinyOPDS.OPDS
{
    public class NewBooksCatalog
    {
        /// <summary>
        /// Returns books catalog for new books with pagination support
        /// </summary>
        /// <param name="searchPattern">Contains page number parameters</param>
        /// <param name="sortByDate">Sort by date (true) or alphabetically by title (false)</param>
        /// <param name="acceptFB2">Client can accept fb2 files</param>
        /// <param name="threshold">Items per page</param>
        /// <returns></returns>
        public XDocument GetCatalog(string searchPattern, bool sortByDate, bool acceptFB2, int threshold = 100)
        {
            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:new"),
                    new XElement("title", string.Format(Localizer.Text("New books sorted by {0}"), (sortByDate ? Localizer.Text("date") : Localizer.Text("titles")))),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/icons/books.ico"),
                    // Add links
                    Links.opensearch, Links.search, Links.start)
                );

            int pageNumber = 0;
            // Extract and remove page number from the search pattern
            int j = searchPattern.IndexOf("?pageNumber=");
            if (j >= 0)
            {
                int.TryParse(searchPattern.Substring(j + 12), out pageNumber);
            }

            // Get paginated new books using the new Library method
            var paginatedResult = Library.GetNewBooksPaginated(sortByDate, pageNumber, threshold);

            // Build catalog type for navigation links
            string catalogType = string.Empty;
            if (paginatedResult.HasNextPage)
            {
                catalogType = string.Format("/{0}?pageNumber={1}",
                    (sortByDate ? "newdate" : "newtitle"),
                    pageNumber + 1);
            }

            // Add pagination links
            if (paginatedResult.HasNextPage)
            {
                doc.Root.Add(new XElement("link",
                    new XAttribute("rel", "next"),
                    new XAttribute("href", catalogType),
                    new XAttribute("type", "application/atom+xml;profile=opds-catalog"),
                    new XAttribute("title", string.Format(Localizer.Text("Page {0}"), pageNumber + 2))));
            }

            if (paginatedResult.HasPreviousPage)
            {
                string prevCatalogType = string.Format("/{0}?pageNumber={1}",
                    (sortByDate ? "newdate" : "newtitle"),
                    pageNumber - 1);

                doc.Root.Add(new XElement("link",
                    new XAttribute("rel", "previous"),
                    new XAttribute("href", prevCatalogType),
                    new XAttribute("type", "application/atom+xml;profile=opds-catalog"),
                    new XAttribute("title", string.Format(Localizer.Text("Page {0}"), pageNumber))));
            }

            // Add first page link if not on first page
            if (pageNumber > 0)
            {
                string firstPageType = string.Format("/{0}", (sortByDate ? "newdate" : "newtitle"));
                doc.Root.Add(new XElement("link",
                    new XAttribute("rel", "first"),
                    new XAttribute("href", firstPageType),
                    new XAttribute("type", "application/atom+xml;profile=opds-catalog"),
                    new XAttribute("title", Localizer.Text("First page"))));
            }

            // Add page info to title
            if (paginatedResult.TotalPages > 1)
            {
                string pageInfo = StringUtils.ApplyPluralForm(paginatedResult.TotalBooks, Localizer.Language,
                    string.Format(" - {0} {1}/{2} ({3} {4})",
                        Localizer.Text("Page"),
                        pageNumber + 1,
                        paginatedResult.TotalPages,
                        paginatedResult.TotalBooks,
                        Localizer.Text("books")));

                doc.Root.Element("title").Value += pageInfo;
            }

            bool useCyrillic = Properties.Settings.Default.SortOrder > 0;

            // Add book entries
            foreach (Book book in paginatedResult.Books)
            {
                string id = "tag:book:" + book.ID;
                // Construct book title, with volume/title
                string bookTitle = book.Title;
                if (!string.IsNullOrEmpty(book.Sequence))
                    bookTitle = string.Format(Localizer.Text("({0} #{1}) {2}"), book.Sequence, book.NumberInSequence, book.Title);

                XElement entry = new XElement("entry",
                    new XElement("updated", book.DocumentDate != DateTime.MinValue ? book.DocumentDate.ToUniversalTime() : book.AddedDate.ToUniversalTime()),
                    new XElement("id", id),
                    new XElement("title", bookTitle),
                    new XElement(Namespaces.dc + "issued", book.BookDate != DateTime.MinValue ? book.BookDate.Year.ToString() : book.AddedDate.Year.ToString()));

                // Add authors with links
                foreach (string authorName in book.Authors)
                {
                    entry.Add(
                        new XElement("author",
                            new XElement("name", authorName),
                            new XElement("uri", "/author-details/" + Uri.EscapeDataString(authorName))
                    ));
                }

                // Add genres
                if (book.Genres != null && book.Genres.Count > 0)
                {
                    foreach (string genre in book.Genres)
                    {
                        Genre g = Library.FB2Genres.FirstOrDefault(x => x.Subgenres.Exists(y => y.Tag.Equals(genre)));
                        if (g != null)
                        {
                            Genre sg = g.Subgenres.First(x => x.Tag.Equals(genre));
                            entry.Add(new XElement("category", new XAttribute("term", sg.Tag),
                                new XAttribute("label", (useCyrillic ? sg.Translation : sg.Name))));
                        }
                    }
                }

                // Build a plain text content entry (translator(s), year, annotation etc.)
                string plainText = "";

                // Add annotation first
                if (!string.IsNullOrEmpty(book.Annotation))
                {
                    plainText = book.Annotation.Trim();
                }

                // Add translators on new line
                if (book.Translators != null && book.Translators.Count > 0)
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

                entry.Add(
                    // Adding cover page and thumbnail links
                    new XElement("link", new XAttribute("href", "/cover/" + book.ID + ".jpeg"), new XAttribute("rel", "http://opds-spec.org/image"), new XAttribute("type", "image/jpeg")),
                    new XElement("link", new XAttribute("href", "/cover/" + book.ID + ".jpeg"), new XAttribute("rel", "x-stanza-cover-image"), new XAttribute("type", "image/jpeg")),
                    new XElement("link", new XAttribute("href", "/thumbnail/" + book.ID + ".jpeg"), new XAttribute("rel", "http://opds-spec.org/thumbnail"), new XAttribute("type", "image/jpeg")),
                    new XElement("link", new XAttribute("href", "/thumbnail/" + book.ID + ".jpeg"), new XAttribute("rel", "x-stanza-cover-image-thumbnail"), new XAttribute("type", "image/jpeg"))
                // Adding download links
                );

                // Add download links - NEW FORMAT WITHOUT FILENAME
                if (book.BookType == BookType.FB2)
                {
                    entry.Add(new XElement("link",
                        new XAttribute("href", "/download/" + book.ID + "/fb2"),
                        new XAttribute("rel", "http://opds-spec.org/acquisition"),
                        new XAttribute("type", "application/fb2+zip")));
                }
                else
                {
                    entry.Add(new XElement("link",
                        new XAttribute("href", "/download/" + book.ID + "/epub"),
                        new XAttribute("rel", "http://opds-spec.org/acquisition"),
                        new XAttribute("type", "application/epub+zip")));
                }

                doc.Root.Add(entry);
            }

            return doc;
        }
    }
}