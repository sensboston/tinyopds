/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Enhanced OPDS OpenSearch implementation
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
                    new XElement("Image", "/library.ico", new XAttribute("width", "32"), new XAttribute("height", "32")),
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

        public XDocument Search(string searchPattern, string searchType = "", bool fb2Only = false, int pageNumber = 0)
        {
            if (!string.IsNullOrEmpty(searchPattern))
                searchPattern = Uri.UnescapeDataString(searchPattern).Replace('+', ' ').Trim().ToLower();

            Log.WriteLine(LogLevel.Info, "OpenSearch.Search: pattern='{0}', searchType='{1}'", searchPattern, searchType);

            List<string> authors = new List<string>();
            List<Book> titles = new List<Book>();
            AuthorSearchMethod authorMethod = AuthorSearchMethod.NotFound;

            // If no search type specified, search both books and authors
            if (string.IsNullOrEmpty(searchType))
            {
                // STEP 1: Search books (without Soundex)
                titles = Library.GetBooksByTitle(searchPattern, true);
                Log.WriteLine(LogLevel.Info, "OpenSearch found {0} books", titles.Count);

                // STEP 2: Search authors with method tracking
                var authorResult = Library.GetAuthorsByNameWithMethod(searchPattern, true);
                authors = authorResult.authors;
                authorMethod = authorResult.method;

                Log.WriteLine(LogLevel.Info, "OpenSearch found {0} authors using method: {1}", authors.Count, authorMethod);
            }

            // If both authors and books found, show selection menu
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
                        new XElement("icon", "/library.ico"),
                        Links.opensearch, Links.search, Links.start, Links.self)
                    );

                // Create informative descriptions
                string authorDescription = string.Format(Localizer.Text("Found {0} author(s)"), authors.Count);
                string bookDescription = string.Format(Localizer.Text("Found {0} book(s)"), titles.Count);

                // Add indication of search method used for authors
                switch (authorMethod)
                {
                    case AuthorSearchMethod.Transliteration:
                        authorDescription += " " + Localizer.Text("(via transliteration)");
                        break;
                    case AuthorSearchMethod.PartialMatch:
                        authorDescription += " " + Localizer.Text("(partial match)");
                        break;
                    case AuthorSearchMethod.Soundex:
                        authorDescription += " " + Localizer.Text("(phonetic match)");
                        break;
                        // ExactMatch - no additional text needed
                }

                doc.Root.Add(
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:search:author"),
                        new XElement("title", Localizer.Text("Search in authors")),
                        new XElement("content", authorDescription, new XAttribute("type", "text")),
                        new XElement("link",
                            new XAttribute("href", "/search?searchType=authors&searchTerm=" + Uri.EscapeDataString(searchPattern)),
                            new XAttribute("type", "application/atom+xml;profile=opds-catalog"))),
                    new XElement("entry",
                        new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                        new XElement("id", "tag:search:title"),
                        new XElement("title", Localizer.Text("Search in book titles")),
                        new XElement("content", bookDescription, new XAttribute("type", "text")),
                        new XElement("link",
                            new XAttribute("href", "/search?searchType=books&searchTerm=" + Uri.EscapeDataString(searchPattern)),
                            new XAttribute("type", "application/atom+xml;profile=opds-catalog")))
                    );

                return doc;
            }
            // If only authors found or explicitly searching for authors
            else if (searchType.Equals("authors") || (authors.Count > 0 && titles.Count == 0))
            {
                // Delegate to AuthorsCatalog
                Log.WriteLine(LogLevel.Info, "OpenSearch: showing authors catalog");
                return new AuthorsCatalog().GetCatalog(searchPattern, true);
            }
            // If only books found or explicitly searching for books
            else if (searchType.Equals("books") || (titles.Count > 0 && authors.Count == 0))
            {
                // Delegate to BooksCatalog
                Log.WriteLine(LogLevel.Info, "OpenSearch: showing books catalog");
                if (pageNumber > 0) searchPattern += "/" + pageNumber;
                return new BooksCatalog().GetCatalogByTitle(searchPattern, fb2Only, 0, 1000, true);
            }

            // If no results found at all, return empty results with helpful message
            XDocument emptyDoc = new XDocument(
                new XElement("feed",
                    new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc),
                    new XAttribute(XNamespace.Xmlns + "os", Namespaces.os),
                    new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("id", "tag:search:" + searchPattern),
                    new XElement("title", Localizer.Text("Search results")),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/library.ico"),
                    new XElement("content",
                        string.Format(Localizer.Text("No results found for '{0}'"), searchPattern),
                        new XAttribute("type", "text")),
                    Links.opensearch, Links.search, Links.start, Links.self)
                );

            Log.WriteLine(LogLevel.Warning, "OpenSearch: no results found for pattern '{0}'", searchPattern);
            return emptyDoc;
        }
    }
}