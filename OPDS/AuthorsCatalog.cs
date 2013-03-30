﻿/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 * All rights reserved.
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
        public XDocument GetCatalog(string searchPattern, int threshold = 50)
        {
            if (!string.IsNullOrEmpty(searchPattern)) searchPattern = HttpUtility.UrlDecode(searchPattern).ToLower();

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("title", Localizer.Text("Books by authors")),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "http://{$HOST}/authors.ico"),
                    // Add links
                    Links.opensearch, Links.search, Links.start, Links.self)
                );

            // Get all authors names starting with searchPattern
            List<string> Authors = (from a in Library.Authors where a.ToLower().StartsWith(searchPattern) && a.Length > searchPattern.Length + 1 select a).ToList();

            if (Authors.Count > threshold)
            {
                Dictionary<string, int> authors = null;
                do
                {
                    authors = (from a in Authors
                               group a by a.Substring(0, searchPattern.Length + 1) into g
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
                            new XElement("link", new XAttribute("href", "http://{$HOST}/authorsindex/" + HttpUtility.UrlEncode(author.Key)), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
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
                            new XElement("link", new XAttribute("href", "http://{$HOST}/author/" + HttpUtility.UrlEncode(author)), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                        )
                    );
                }
            }
            return doc;
        }
    }
}
