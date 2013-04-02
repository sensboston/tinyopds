/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 * All rights reserved.
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module contains OPDS OpenSearch implementation
 * 
 * TODO: implement SOUNDEX search
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
    public class OpenSearch
    {
        public XDocument Search(string searchPattern, string searchType = "", bool fb2Only = false, int pageNumber = 0, int threshold = 50)
        {
            if (!string.IsNullOrEmpty(searchPattern)) searchPattern = HttpUtility.UrlDecode(searchPattern).ToLower();

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:search:"+searchPattern),
                    new XElement("title", Localizer.Text("Search results")),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "http://{$HOST}/series.ico"),
                    // Add links
                    Links.opensearch, Links.search, Links.start, Links.self)
                );

            List<string> authors = (from a in Library.Authors where a.ToLower().StartsWith(searchPattern) select a).ToList();
            List<string> titles = (from t in Library.Titles where t.ToLower().StartsWith(searchPattern) select t).ToList();

            if (string.IsNullOrEmpty(searchType) && authors.Count > 0 && titles.Count > 0)
            {
                // Add two navigation entries: search by authors name and book title
                doc.Root.Add(
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:search:author"),
                        new XElement("title", Localizer.Text("Search authors")),
                        new XElement("content", Localizer.Text("Search authors by name"), new XAttribute("type", "text")),
                        new XElement("link", new XAttribute("href", "http://{$HOST}/search?searchType=authors&searchTerm=" + HttpUtility.UrlEncode(searchPattern)), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))),
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:search:title"),
                        new XElement("title", Localizer.Text("Search books")),
                        new XElement("content", Localizer.Text("Search books by title"), new XAttribute("type", "text")),
                        new XElement("link", new XAttribute("href", "http://{$HOST}/search?searchType=books&searchTerm=" + HttpUtility.UrlEncode(searchPattern)), new XAttribute("type", "application/atom+xml;profile=opds-catalog")))
                    );
            }
            else if (searchType.Equals("authors") || (authors.Count > 0 && titles.Count == 0))
            {
                return new AuthorsCatalog().GetCatalog(searchPattern);
            }
            else if (searchType.Equals("books") || (titles.Count > 0 && authors.Count == 0))
            {
                if (pageNumber > 0) searchPattern += "/" + pageNumber;
                return new BooksCatalog().GetCatalogByTitle(searchPattern, fb2Only);
            }
            return doc;
        }
    }
}
