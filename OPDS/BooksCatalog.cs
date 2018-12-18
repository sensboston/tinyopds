/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines the OPDS BookCatalog class
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
    public class BooksCatalog
    {
        private enum SearchFor
        {
            Author = 0,
            Sequence,
            Genre,
            Title
        }

        /// <summary>
        /// Returns books catalog by selected author
        /// </summary>
        /// <param name="author"></param>
        /// <returns></returns>
        public XDocument GetCatalogByAuthor(string author, bool fb2Only)
        {
            return GetCatalog(author, SearchFor.Author, fb2Only);
        }

        /// <summary>
        /// Returns books catalog by selected sequence (series)
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public XDocument GetCatalogBySequence(string sequence, bool fb2Only)
        {
            return GetCatalog(sequence, SearchFor.Sequence, fb2Only);
        }

        /// <summary>
        /// Returns books catalog by selected genre
        /// </summary>
        /// <param name="author"></param>
        /// <returns></returns>
        public XDocument GetCatalogByGenre(string genre, bool fb2Only)
        {
            return GetCatalog(genre, SearchFor.Genre, fb2Only);
        }


        /// <summary>
        /// Returns books catalog by selected genre
        /// </summary>
        /// <param name="author"></param>
        /// <returns></returns>
        public XDocument GetCatalogByTitle(string title, bool fb2Only, int pageNumber = 0, int threshold = 50)
        {
            return GetCatalog(title, SearchFor.Title, fb2Only, threshold);
        }

        /// <summary>
        /// Returns books catalog for specific search
        /// </summary>
        /// <param name="searchPattern">Keyword to search</param>
        /// <param name="searchFor">Type of search</param>
        /// <param name="acceptFB2">Client can accept fb2 files</param>
        /// <param name="threshold">Items per page</param>
        /// <returns></returns>
        private XDocument GetCatalog(string searchPattern, SearchFor searchFor, bool acceptFB2, int threshold = 50)
        {
            if (!string.IsNullOrEmpty(searchPattern)) searchPattern = Uri.UnescapeDataString(searchPattern).Replace('+', ' ');

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("title", Localizer.Text("Books by author ") + searchPattern),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/icons/books.ico"),
                    // Add links
                    Links.opensearch, Links.search, Links.start)
                );

            int pageNumber = 0;
            // Extract and remove page number from the search patter
            int j = searchPattern.IndexOf('/');
            if (j > 0)
            {
                int.TryParse(searchPattern.Substring(j + 1), out pageNumber);
                searchPattern = searchPattern.Substring(0, j);
            }

            // Get author's books
            string catalogType = string.Empty;
            List<Book> books = new List<Book>();
            switch (searchFor)
            {
                case SearchFor.Author:
                    books = Library.GetBooksByAuthor(searchPattern);
                    catalogType = "/author/" + Uri.EscapeDataString(searchPattern);
                    break;
                case SearchFor.Sequence:
                    books = Library.GetBooksBySequence(searchPattern);
                    catalogType = "/sequence/" + Uri.EscapeDataString(searchPattern);
                    break;
                case SearchFor.Genre:
                    books = Library.GetBooksByGenre(searchPattern);
                    catalogType = "/genre/" + Uri.EscapeDataString(searchPattern);
                    break;
                case SearchFor.Title:
                    books = Library.GetBooksByTitle(searchPattern);
                    // For search, also return books by 
                    if (threshold > 50)
                    {
                        string translit = Transliteration.Back(searchPattern, TransliterationType.GOST);
                        if (!string.IsNullOrEmpty(translit))
                        {
                            List<Book> transTitles = Library.GetBooksByTitle(translit);
                            if (transTitles.Count > 0) books.AddRange(transTitles);
                        }
                    }
                    break;
            }

            // For sequences, sort books by sequence number
            if (searchFor == SearchFor.Sequence)
            {
                books = books.OrderBy(b => b.NumberInSequence).ToList();
            }
            // else sort by title
            else
            {
                books = books.OrderBy(b => b.Title, new OPDSComparer(Localizer.Language.Equals("ru"))).ToList();
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

            bool useCyrillic = Localizer.Language.Equals("ru");

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
                bookInfo += string.Format("<b>{0}</b> {1}<br/>", Localizer.Text("Format:"), book.BookType == BookType.EPUB ? "epub" : "fb2");
                bookInfo += string.Format("<b>{0}</b> {1} Kb<br/>", Localizer.Text("Size:"), (int) book.DocumentSize / 1024);
                if (!string.IsNullOrEmpty(book.Sequence))
                {
                    bookInfo += string.Format("<b>{0} {1} #{2}</b><br/>", Localizer.Text("Series:"), book.Sequence, book.NumberInSequence);
                }

                entry.Add(
                    new XElement(Namespaces.dc + "language", book.Language),
                    new XElement(Namespaces.dc + "format", book.BookType == BookType.FB2 ? "fb2+zip" : "epub+zip"),
                    new XElement("content", new XAttribute("type", "text/html"), bookInfo));

                if (book.HasCover)
                {
                    entry.Add(
                        // Adding cover page and thumbnail links
                        new XElement("link", new XAttribute("href", "/cover/" + book.ID + ".jpeg"), new XAttribute("rel", "http://opds-spec.org/image"), new XAttribute("type", "image/jpeg")),
                        new XElement("link", new XAttribute("href", "/cover/" + book.ID + ".jpeg"), new XAttribute("rel", "x-stanza-cover-image"), new XAttribute("type", "image/jpeg")),
                        new XElement("link", new XAttribute("href", "/thumbnail/" + book.ID + ".jpeg"), new XAttribute("rel", "http://opds-spec.org/thumbnail"), new XAttribute("type", "image/jpeg")),
                        new XElement("link", new XAttribute("href", "/thumbnail/" + book.ID + ".jpeg"), new XAttribute("rel", "x-stanza-cover-image-thumbnail"), new XAttribute("type", "image/jpeg"))
                        // Adding download links
                    );
                }

                string fileName = Uri.EscapeDataString(Transliteration.Front(string.Format("{0}_{1}", book.Authors.First(), book.Title)).SanitizeFileName());
                string url = "/" + string.Format("{0}/{1}", book.ID, fileName);
                if (book.BookType == BookType.EPUB || (book.BookType == BookType.FB2 && !acceptFB2 && !string.IsNullOrEmpty(Properties.Settings.Default.ConvertorPath)))
                {
                    entry.Add(new XElement("link", new XAttribute("href",  url+".epub"), new XAttribute("rel", "http://opds-spec.org/acquisition/open-access"), new XAttribute("type", "application/epub+zip")));
                }

                if (book.BookType == BookType.FB2)
                {
                    entry.Add(new XElement("link", new XAttribute("href",  url+".fb2.zip"), new XAttribute("rel", "http://opds-spec.org/acquisition/open-access"), new XAttribute("type", "application/fb2+zip")));
                }

                // For search requests, lets add navigation links for author and series (if any)
                if (searchFor != SearchFor.Author)
                {
                    foreach (string author in book.Authors)
                    {
                        entry.Add(new XElement("link",
                                        new XAttribute("href", "/author/" + Uri.EscapeDataString(author)),
                                        new XAttribute("rel", "related"), 
                                        new XAttribute("type", "application/atom+xml;profile=opds-catalog"),
                                        new XAttribute("title", string.Format(Localizer.Text("All books by author {0}"), author))));
                    }
                }

                if (searchFor != SearchFor.Sequence && !string.IsNullOrEmpty(book.Sequence))
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