/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Enhanced OPDS OpenSearch implementation with FTS5 books search
 *
 */

using System;
using System.Collections.Generic;
using System.Xml.Linq;

using TinyOPDS.Data;

namespace TinyOPDS.OPDS
{
    public class OpenSearch
    {
        public XDocument OpenSearchDescription()
        {
            XDocument doc = new XDocument(
                new XElement("OpenSearchDescription",
                    new XElement("ShortName", "TinyOPDS"),
                    new XElement("LongName", "TinyOPDS"),
                    new XElement("Url", new XAttribute("type", "application/atom+xml"), new XAttribute("template", "/search?searchTerm={searchTerms}")),
                    new XElement("Image", "/favicon.ico", new XAttribute("width", "16"), new XAttribute("height", "16")),
                    new XElement("Tags"),
                    new XElement("Contact"),
                    new XElement("Developer"),
                    new XElement("Attribution"),
                    new XElement("SyndicationRight", "open"),
                    new XElement("AdultContent", "false"),
                    new XElement("Language", "*"),
                    new XElement("OutputEncoding", "UTF-8"),
                    new XElement("InputEncoding", "UTF-8")));

            return doc;
        }

        public XDocument Search(string searchPattern, string searchType = "", bool fb2Only = false, int pageNumber = 0, int threshold = 100)
        {
            if (!string.IsNullOrEmpty(searchPattern))
                searchPattern = Uri.UnescapeDataString(searchPattern).Replace('+', ' ').ToLower();

            Log.WriteLine(LogLevel.Info, "OpenSearch.Search: pattern='{0}', searchType='{1}'", searchPattern, searchType);

            List<string> authors = new List<string>();
            List<Book> titles = new List<Book>();

            if (string.IsNullOrEmpty(searchType))
            {
                // Search both authors and books
                authors = Library.GetAuthorsByName(searchPattern, true);
                titles = Library.GetBooksByTitle(searchPattern, true);

                Log.WriteLine(LogLevel.Info, "OpenSearch found {0} authors and {1} books", authors.Count, titles.Count);
            }

            if (string.IsNullOrEmpty(searchType) && authors.Count > 0 && titles.Count > 0)
            {
                // Add two navigation entries: search by authors name and book title
                XDocument doc = new XDocument(
                    new XElement("feed",
                        new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc),
                        new XAttribute(XNamespace.Xmlns + "os", Namespaces.os),
                        new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                        new XElement("id", "tag:search:" + searchPattern),
                        new XElement("title", Localizer.Text("Search results")),
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("icon", "/series.ico"),
                        Links.opensearch, Links.search, Links.start, Links.self)
                    );

                doc.Root.Add(
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:search:author"),
                        new XElement("title", Localizer.Text("Search authors")),
                        new XElement("content", Localizer.Text("Search authors by name"), new XAttribute("type", "text")),
                        new XElement("link", new XAttribute("href", "/search?searchType=authors&searchTerm=" + Uri.EscapeDataString(searchPattern)), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))),
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:search:title"),
                        new XElement("title", Localizer.Text("Search books")),
                        new XElement("content", Localizer.Text("Search books by title"), new XAttribute("type", "text")),
                        new XElement("link", new XAttribute("href", "/search?searchType=books&searchTerm=" + Uri.EscapeDataString(searchPattern)), new XAttribute("type", "application/atom+xml;profile=opds-catalog")))
                    );

                return doc;
            }
            else if (searchType.Equals("authors") || (authors.Count > 0 && titles.Count == 0))
            {
                // Delegate to AuthorsCatalog
                Log.WriteLine(LogLevel.Info, "OpenSearch: showing authors catalog");
                return new AuthorsCatalog().GetCatalog(searchPattern, true);
            }
            else if (searchType.Equals("books") || (titles.Count > 0 && authors.Count == 0))
            {
                // Delegate to BooksCatalog
                Log.WriteLine(LogLevel.Info, "OpenSearch: showing books catalog");
                if (pageNumber > 0) searchPattern += "/" + pageNumber;
                return new BooksCatalog().GetCatalogByTitle(searchPattern, fb2Only, 0, 1000, true);
            }

            // If no results found, return empty results
            XDocument emptyDoc = new XDocument(
                new XElement("feed",
                    new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc),
                    new XAttribute(XNamespace.Xmlns + "os", Namespaces.os),
                    new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:search:" + searchPattern),
                    new XElement("title", Localizer.Text("Search results")),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/authors.ico"),
                    Links.opensearch, Links.search, Links.start, Links.self)
                );

            Log.WriteLine(LogLevel.Warning, "OpenSearch: no results found for pattern '{0}'", searchPattern);
            return emptyDoc;
        }
    }
}