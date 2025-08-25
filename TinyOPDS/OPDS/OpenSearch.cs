/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module contains OPDS OpenSearch implementation
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
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

            // Search only authors - simple prefix search, no Soundex
            List<string> authors = new List<string>();

            if (string.IsNullOrEmpty(searchType))
            {
                // OpenSearch mode - smart search without Soundex for now
                authors = Library.GetAuthorsByName(searchPattern, true);
                Log.WriteLine(LogLevel.Info, "OpenSearch found {0} authors", authors.Count);
            }

            // Always delegate to AuthorsCatalog if we have authors or searchType is "authors"
            if (searchType.Equals("authors") || authors.Count > 0)
            {
                Log.WriteLine(LogLevel.Info, "OpenSearch: showing authors catalog");
                return new AuthorsCatalog().GetCatalog(searchPattern, true);
            }

            // If no authors found, return empty results
            XDocument doc = new XDocument(
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
            return doc;
        }
    }
}