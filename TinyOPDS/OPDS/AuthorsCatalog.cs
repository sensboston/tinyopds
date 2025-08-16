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
        public XDocument GetCatalog(string searchPattern, bool isOpenSearch = false, int threshold = 100)
        {
            if (!string.IsNullOrEmpty(searchPattern)) searchPattern = Uri.UnescapeDataString(searchPattern).Replace('+', ' ').ToLower();

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:authors"),
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

            // if there are more authors then threshold, try to collapse them into groups
            // and render these groups first and authors after them
            if (Authors.Count > threshold)
            {
                Dictionary<string, int> catalogGroups = null;
                do
                {
                    catalogGroups = (from a in Authors
                           group a by (a.Length > searchPattern.Length ? a.Substring(0, searchPattern.Length + 1).Capitalize(true)
                                                                       : a.Capitalize(true)) into g
                           where g.Count() > 1
                           select new { Name = g, Count = g.Count() }).ToDictionary(x => x.Name.Key, y => y.Count);

                    if (catalogGroups.Count == 1) searchPattern = catalogGroups.First().Key;
                    else break;
                } while (true);

                // remove entry that exactly matches search pattern to avoid recursion 
                catalogGroups.Remove(searchPattern.Capitalize(true));
                // remove entries that are groupped ( if any )
                foreach (var kv in catalogGroups)
                {
                    Authors.RemoveAll(a => a.StartsWith(kv.Key, StringComparison.InvariantCultureIgnoreCase));
                }

                // Add catalog groups
                foreach (KeyValuePair<string, int> cg in catalogGroups)
                {
                    doc.Root.Add(
                        new XElement("entry",
                            new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                            new XElement("id", "tag:authors:" + cg.Key),
                            new XElement("title", cg.Key),
                            new XElement("content", string.Format(Localizer.Text("Total authors on {0}: {1}"), cg.Key, cg.Value),
                                         new XAttribute("type", "text")),
                            new XElement("link", new XAttribute("href", "/authorsindex/" + Uri.EscapeDataString(cg.Key)),
                                         new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                        )
                    );
                }
            }

            // Add catalog entries
            foreach (string author in Authors)
            {
                var booksCount = Library.GetBooksByAuthorCount(author);

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
            return doc;
        }
    }
}
