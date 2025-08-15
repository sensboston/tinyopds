/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines the OPDS NewBooksCatalog class with SQLite support
 * 
 * 
 ************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Web;

using TinyOPDS.Data;

namespace TinyOPDS.OPDS
{
    public class NewBooksCatalog
    {
        /// <summary>
        /// Returns books catalog for specific search
        /// </summary>
        /// <param name="searchPattern">Keyword to search</param>
        /// <param name="searchFor">Type of search</param>
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
            // Extract and remove page number from the search patter
            int j = searchPattern.IndexOf("?pageNumber=");
            if (j >= 0)
            {
                int.TryParse(searchPattern.Substring(j + 12), out pageNumber);
            }

            // Get list of new books - use SQLite version
            string catalogType = string.Empty;
            List<Book> books = Library.NewBooks;

            if (sortByDate) books = books.OrderBy(b => b.AddedDate).ToList();
            else books = books.OrderBy(b => b.Title, new OPDSComparer(TinyOPDS.Properties.Settings.Default.SortOrder > 0)).ToList();

            int startIndex = pageNumber * threshold;
            int endIndex = startIndex + ((books.Count / threshold == 0) ? books.Count : Math.Min(threshold, books.Count - startIndex));

            if ((pageNumber + 1) * threshold < books.Count)
            {
                catalogType = string.Format("/{0}?pageNumber={1}", (sortByDate ? "newdate" : "newtitle"), pageNumber + 1);
                doc.Root.Add(new XElement("link",
                                new XAttribute("href", catalogType),
                                new XAttribute("rel", "next"),
                                new XAttribute("type", "application/atom+xml;profile=opds-catalog")));
            }

            bool useCyrillic = TinyOPDS.Properties.Settings.Default.SortOrder > 0;

            // Use SQLite version for genres
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

                foreach (string author in book.Authors)
                {
                    entry.Add(
                        new XElement("author",
                            new XElement("name", author),
                            new XElement("uri", "/author/" + Uri.EscapeDataString(author)
                    )));
                }

                foreach (string genreStr in book.Genres)
                {
                    Genre genre = genres.Where(g => g.Tag.Equals(genreStr)).FirstOrDefault();
                    if (genre != null)
                        entry.Add(new XElement("category", new XAttribute("term", (useCyrillic ? genre.Translation : genre.Name)), new XAttribute("label", (useCyrillic ? genre.Translation : genre.Name))));
                }

                // Build a content entry (translator(s), year, size, annotation etc.)
                string bookInfo = string.Empty;

                if (!string.IsNullOrEmpty(book.Annotation))
                {
                    bookInfo += string.Format("<p class=book>{0}</p><br/>", book.Annotation);
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
                bookInfo += string.Format("<b>{0}</b> {1}<br/>", Localizer.Text("Format:"), book.BookType == BookType.EPUB ? "EPUB" : "FB2");
                bookInfo += string.Format("<b>{0}</b> {1} KB", Localizer.Text("Size:"), book.DocumentSize / 1024);
                if (!string.IsNullOrEmpty(book.Sequence)) bookInfo += string.Format("<br/><b>{0}</b> {1}", Localizer.Text("Series:"), book.Sequence);

                entry.Add(new XElement("content", bookInfo, new XAttribute("type", "html")));

                // Add book cover link (if any)
                if (book.HasCover) entry.Add(new XElement("link", new XAttribute("href", "/cover/" + book.ID), new XAttribute("type", "image/jpeg"), new XAttribute("rel", "http://opds-spec.org/image/thumbnail")));

                // For old readers who doesn't support opds links we should add more FB2 books
                string searchAppendix = "";

                if (!acceptFB2)
                {
                    searchAppendix = "?t=epub";
                    if (book.BookType == BookType.FB2) entry.Add(new XElement("link", new XAttribute("href", "/convert/" + book.ID), new XAttribute("type", "application/epub+zip"), new XAttribute("rel", "http://opds-spec.org/acquisition")));
                    else entry.Add(new XElement("link", new XAttribute("href", "/book/" + book.ID + searchAppendix), new XAttribute("type", "application/epub+zip"), new XAttribute("rel", "http://opds-spec.org/acquisition")));
                }
                else
                {
                    if (book.BookType == BookType.FB2) entry.Add(new XElement("link", new XAttribute("href", "/book/" + book.ID + searchAppendix), new XAttribute("type", "application/fb2+zip"), new XAttribute("rel", "http://opds-spec.org/acquisition")));
                    else entry.Add(new XElement("link", new XAttribute("href", "/book/" + book.ID + searchAppendix), new XAttribute("type", "application/epub+zip"), new XAttribute("rel", "http://opds-spec.org/acquisition")));
                }

                doc.Root.Add(entry);
            }

            return doc;
        }
    }
}