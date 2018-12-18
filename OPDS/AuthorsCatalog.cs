/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines the OPDS AuthorsCatalog class
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
    /// <summary>
    /// Authors acquisition feed class
    /// </summary>
    public class AuthorsCatalog
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="searchPattern"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public XDocument GetCatalog(string searchPattern, bool isOpenSearch = false, int threshold = 50)
        {
            if (!string.IsNullOrEmpty(searchPattern)) searchPattern = Uri.UnescapeDataString(searchPattern).Replace('+', ' ').ToLower();

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("title", Localizer.Text("Books by authors")),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/authors.ico"),
                    // Add links
                    Links.opensearch, Links.search, Links.start)
                );

            // Get all authors names starting with searchPattern
            List<string> Authors = Library.GetAuthorsByName(searchPattern, isOpenSearch);

            // For search, also check transliterated names
            if (isOpenSearch)
            {
                // Try transliteration
                string translit = Transliteration.Back(searchPattern, TransliterationType.GOST);
                if (!string.IsNullOrEmpty(translit))
                {
                    List<string> transAuthors = Library.GetAuthorsByName(translit, isOpenSearch);
                    if (transAuthors.Count > 0) Authors.AddRange(transAuthors);
                }
            }

            if (Authors.Count > threshold)
            {
                Dictionary<string, int> authors = null;
                do
                {
                    authors = (from a in Authors
                               group a by (a.Length > searchPattern.Length ? a.Substring(0, searchPattern.Length + 1) : a) into g
                               where g.Count() > 1
                               select new { Name = g, Count = g.Count() }).ToDictionary(x => x.Name.Key, y => y.Count);
                    if (authors.Count == 1) searchPattern = authors.First().Key;
                } while (authors.Count <= 1);

                // Add catalog entries
                foreach (KeyValuePair<string, int> author in authors)
                {
                    doc.Root.Add(
                        new XElement("entry",
                            new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                            new XElement("id", "tag:authors:" + author.Key),
                            new XElement("title", author.Key),
                            new XElement("content", string.Format(Localizer.Text("Total authors on {0}: {1}"), author.Key, author.Value), new XAttribute("type", "text")),
                            new XElement("link", new XAttribute("href", "/authorsindex/" + Uri.EscapeDataString(author.Key)), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                        )
                    );
                }
            }
            // 
            else
            {
                // Add catalog entries
                foreach (string author in Authors)
                {
                    var booksCount = Library.GetBooksByAuthor(author).Count;

                    doc.Root.Add(
                        new XElement("entry",
                            new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                            new XElement("id", "tag:authors:" + author),
                            new XElement("title", author),
                            new XElement("content", string.Format(Localizer.Text("Books: {0}"), booksCount), new XAttribute("type", "text")),
                            new XElement("link", new XAttribute("href", "/author/" + Uri.EscapeDataString(author)), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                        )
                    );
                }
            }
            return doc;
        }
    }
}
