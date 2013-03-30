/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 * All rights reserved.
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
        public XDocument GetCatalogByTitle(string title, bool fb2Only)
        {
            return GetCatalog(title, SearchFor.Title, fb2Only);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="searchPattern"></param>
        /// <param name="searchFor"></param>
        /// <returns></returns>
        private XDocument GetCatalog(string searchPattern, SearchFor searchFor, bool acceptFB2)
        {
            if (!string.IsNullOrEmpty(searchPattern)) searchPattern = HttpUtility.UrlDecode(searchPattern);

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("title", Localizer.Text("Books by author ") + searchPattern),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "http://{$HOST}/icons/books.ico"),
                    // Add links
                    Links.opensearch, Links.search, Links.start, Links.self)
                );

            // Get author's books
            List<Book> books = new List<Book>();
            switch (searchFor)
            {
                case SearchFor.Author:
                    books = Library.GetBooksByAuthor(searchPattern);
                    break;
                case SearchFor.Sequence:
                    books = Library.GetBooksBySequence(searchPattern);
                    break;
                case SearchFor.Genre:
                    books = Library.GetBooksByGenre(searchPattern);
                    break;
                case SearchFor.Title:
                    books = Library.GetBooksByTitle(searchPattern);
                    break;
            }

            bool useCyrillic = Localizer.Language.Equals("ru");

            // Add catalog entries
            for (int i = 0; i < books.Count; i++)
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
                            new XElement("uri", "http://{$HOST}/author/" + HttpUtility.UrlEncode(author)
                    )));
                }

                foreach (string genreStr in book.Genres)
                {
                    Genre genre = Library.Genres.Where(g => g.Tag.Equals(genreStr)).FirstOrDefault();
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
                    bookInfo += string.Format("<b>{0}</b>", Localizer.Text("Translation: "));
                    foreach (string translator in book.Translators) bookInfo += translator + " ";
                    bookInfo += "<br/>";
                }
                if (book.BookDate != DateTime.MinValue)
                {
                    bookInfo += string.Format("<b>{0}</b> {1}<br/>", Localizer.Text("Year of publication: "), book.BookDate.Year);
                }
                bookInfo += string.Format("<b>{0}</b> {1}<br/>", Localizer.Text("Book format:"), book.BookType == BookType.EPUB ? "epub" : "fb2");
                bookInfo += string.Format("<b>{0}</b> {1} Kb<br/>", Localizer.Text("Book size:"), (int) book.DocumentSize / 1024);
                if (!string.IsNullOrEmpty(book.Sequence))
                {
                    bookInfo += string.Format("<b>{0}{1}</b><br/>", Localizer.Text("Book series: "), book.Sequence);
                }

                entry.Add(
                    new XElement(Namespaces.dc + "language", book.Language),
                    new XElement(Namespaces.dc + "format", book.BookType == BookType.FB2 ? "fb2+zip" : "epub+zip"),
                    new XElement("content", new XAttribute("type", "text/html"), bookInfo));

                if (book.HasCover)
                {
                    entry.Add(
                        // Adding cover page and thumbnail links
                        new XElement("link", new XAttribute("href", "http://{$HOST}/cover/" + book.ID + ".jpeg"), new XAttribute("rel", "http://opds-spec.org/image"), new XAttribute("type", "image/jpeg")),
                        new XElement("link", new XAttribute("href", "http://{$HOST}/cover/" + book.ID + ".jpeg"), new XAttribute("rel", "x-stanza-cover-image"), new XAttribute("type", "image/jpeg")),
                        new XElement("link", new XAttribute("href", "http://{$HOST}/thumbnail/" + book.ID + ".jpeg"), new XAttribute("rel", "http://opds-spec.org/thumbnail"), new XAttribute("type", "image/jpeg")),
                        new XElement("link", new XAttribute("href", "http://{$HOST}/thumbnail/" + book.ID + ".jpeg"), new XAttribute("rel", "x-stanza-cover-image-thumbnail"), new XAttribute("type", "image/jpeg"))
                        // Adding download links
                    );
                }

                string url = "http://{$HOST}/" + Transliteration.Front(string.Format("{0}/{1}_{2}", book.ID, book.Authors.First(), book.Title));
                if (book.BookType == BookType.EPUB || !(acceptFB2 && string.IsNullOrEmpty(Properties.Settings.Default.ConvertorPath)))
                {
                    entry.Add(new XElement("link", new XAttribute("href", url+".epub"), new XAttribute("rel", "http://opds-spec.org/acquisition/open-access"), new XAttribute("type", "application/epub+zip")));
                }

                if (book.BookType == BookType.FB2)
                {
                    entry.Add(new XElement("link", new XAttribute("href", url+".fb2.zip"), new XAttribute("rel", "http://opds-spec.org/acquisition/open-access"), new XAttribute("type", "application/fb2+zip")));
                }

                doc.Root.Add(entry);
            }
            return doc;
        }
    }
}