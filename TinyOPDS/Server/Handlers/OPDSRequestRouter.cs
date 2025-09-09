/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module handles OPDS catalog request routing and
 * generation based on configurable structure
 * ENHANCED: Added download statistics catalog routing (/downstat)
 * 
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TinyOPDS.OPDS;

namespace TinyOPDS.Server
{
    /// <summary>
    /// Routes OPDS catalog requests and manages catalog structure
    /// </summary>
    public class OPDSRequestRouter
    {
        private Dictionary<string, bool> opdsStructure;
        private readonly XslTransformHandler xslHandler;
        private readonly OPDSUtilities utilities;

        public OPDSRequestRouter(XslTransformHandler xslHandler)
        {
            this.xslHandler = xslHandler;
            this.utilities = new OPDSUtilities();
            LoadOPDSStructure();
        }

        /// <summary>
        /// Handles OPDS catalog request
        /// </summary>
        public void HandleOPDSRequest(HttpProcessor processor, string request, bool isOPDSRequest, bool acceptFB2, int threshold)
        {
            try
            {
                LoadOPDSStructure();

                string xml = GenerateOPDSResponse(request, acceptFB2, threshold);

                if (string.IsNullOrEmpty(xml))
                {
                    processor.WriteFailure();
                    return;
                }

                xml = utilities.FixNamespace(xml);
                xml = utilities.ApplyUriPrefixes(xml, isOPDSRequest);

                if (isOPDSRequest)
                {
                    // OPDS request - always return XML
                    processor.WriteSuccess("application/atom+xml;charset=utf-8");
                    processor.OutputStream.Write(xml);
                }
                else
                {
                    // Web request - transform to HTML
                    string html = xslHandler.TransformToHtml(xml);
                    if (!string.IsNullOrEmpty(html))
                    {
                        processor.WriteSuccess("text/html");
                        processor.OutputStream.Write(html);
                    }
                    else
                    {
                        processor.WriteFailure();
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "OPDS catalog exception: {0}", e.Message);
                processor.WriteFailure();
            }
        }

        /// <summary>
        /// Generates OPDS XML response based on request
        /// </summary>
        public string GenerateOPDSResponse(string request, bool acceptFB2, int threshold)
        {
            Log.WriteLine(LogLevel.Info, "Generating OPDS response for: {0}", request);

            string[] pathParts = request.Split(new char[] { '?', '=', '&' }, StringSplitOptions.RemoveEmptyEntries);

            if (request.Equals("/"))
            {
                return new RootCatalogWithStructure(opdsStructure).GetCatalog().ToStringWithDeclaration();
            }
            else if (request.StartsWith("/newdate") && IsRouteEnabled("newdate"))
            {
                return new NewBooksCatalog().GetCatalog(request.Substring(8), true, acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/newtitle") && IsRouteEnabled("newtitle"))
            {
                return new NewBooksCatalog().GetCatalog(request.Substring(9), false, acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/downstat") && IsRouteEnabled("downloads"))  // Note: config key stays as "downloads" for compatibility
            {
                return HandleDownloadStatisticsRequest(request, acceptFB2, threshold);
            }
            else if (request.StartsWith("/authorsindex") && IsRouteEnabled("authorsindex"))
            {
                int numChars = request.StartsWith("/authorsindex/") ? 14 : 13;
                string searchPattern = request.Substring(numChars);

                // Important: Pass the URL-encoded pattern to catalog - let it handle decoding
                Log.WriteLine(LogLevel.Info, "AuthorsIndex search pattern (raw): '{0}'", searchPattern);

                return new AuthorsCatalog().GetCatalog(searchPattern, false, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/author-details/") && IsRouteEnabled("author-details"))
            {
                string authorParam = request.Substring(16);
                Log.WriteLine(LogLevel.Info, "Author details parameter (raw): '{0}'", authorParam);

                return new AuthorDetailsCatalogWithStructure(opdsStructure).GetCatalog(authorParam).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/author-series/") && IsRouteEnabled("author-series"))
            {
                string authorParam = request.Substring(15);
                Log.WriteLine(LogLevel.Info, "Author series parameter (raw): '{0}'", authorParam);

                return new AuthorBooksCatalog().GetSeriesCatalog(authorParam, acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/author-no-series/") && IsRouteEnabled("author-no-series"))
            {
                string authorParam = request.Substring(18);
                Log.WriteLine(LogLevel.Info, "Author no-series parameter (raw): '{0}'", authorParam);

                return new AuthorBooksCatalog().GetBooksCatalog(authorParam, AuthorBooksCatalog.ViewType.NoSeries, acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/author-alphabetic/") && IsRouteEnabled("author-alphabetic"))
            {
                string authorParam = request.Substring(19);
                Log.WriteLine(LogLevel.Info, "Author alphabetic parameter (raw): '{0}'", authorParam);

                return new AuthorBooksCatalog().GetBooksCatalog(authorParam, AuthorBooksCatalog.ViewType.Alphabetic, acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/author-by-date/") && IsRouteEnabled("author-by-date"))
            {
                string authorParam = request.Substring(16);
                Log.WriteLine(LogLevel.Info, "Author by-date parameter (raw): '{0}'", authorParam);

                return new AuthorBooksCatalog().GetBooksCatalog(authorParam, AuthorBooksCatalog.ViewType.ByDate, acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/author/"))
            {
                string authorParam = request.Substring(8);
                Log.WriteLine(LogLevel.Info, "Author parameter (raw): '{0}'", authorParam);

                if (IsRouteEnabled("author-details"))
                {
                    return new AuthorDetailsCatalogWithStructure(opdsStructure).GetCatalog(authorParam).ToStringWithDeclaration();
                }
                else
                {
                    return new AuthorBooksCatalog().GetBooksCatalog(authorParam, AuthorBooksCatalog.ViewType.Alphabetic, acceptFB2, threshold).ToStringWithDeclaration();
                }
            }
            else if (request.StartsWith("/sequencesindex") && IsRouteEnabled("sequencesindex"))
            {
                int numChars = request.StartsWith("/sequencesindex/") ? 16 : 15;
                string searchPattern = request.Substring(numChars);
                Log.WriteLine(LogLevel.Info, "SequencesIndex search pattern (raw): '{0}'", searchPattern);

                return new SequencesCatalog().GetCatalog(searchPattern, threshold).ToStringWithDeclaration();
            }
            else if (request.Contains("/sequence/"))
            {
                string sequenceParam = request.Substring(request.IndexOf("/sequence/") + 10);
                Log.WriteLine(LogLevel.Info, "Sequence parameter (raw): '{0}'", sequenceParam);

                return new BooksCatalog().GetCatalogBySequence(sequenceParam, acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/genres") && IsRouteEnabled("genres"))
            {
                int numChars = request.Contains("/genres/") ? 8 : 7;
                string genreParam = request.Substring(numChars);
                Log.WriteLine(LogLevel.Info, "Genres parameter (raw): '{0}'", genreParam);

                return new GenresCatalog().GetCatalog(genreParam).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/genre/"))
            {
                string genreParam = request.Substring(7);
                Log.WriteLine(LogLevel.Info, "Genre parameter (raw): '{0}'", genreParam);

                return new BooksCatalog().GetCatalogByGenre(genreParam, acceptFB2, threshold).ToStringWithDeclaration();
            }
            else if (request.StartsWith("/search"))
            {
                return HandleSearchRequest(pathParts, acceptFB2);
            }

            Log.WriteLine(LogLevel.Warning, "No matching route found for request: {0}", request);
            return null;
        }

        /// <summary>
        /// Handles download statistics catalog requests (/downstat path)
        /// </summary>
        private string HandleDownloadStatisticsRequest(string request, bool acceptFB2, int threshold)
        {
            // Extract page number if present
            int pageNumber = 0;
            if (request.Contains("?pageNumber="))
            {
                int idx = request.IndexOf("?pageNumber=");
                string pageStr = request.Substring(idx + 12);
                int.TryParse(pageStr, out pageNumber);
                request = request.Substring(0, idx); // Remove query string for route matching
            }

            Log.WriteLine(LogLevel.Info, "Download statistics request: '{0}', page: {1}", request, pageNumber);

            if (request.Equals("/downstat"))
            {
                // Root download statistics catalog
                return new DownloadCatalog().GetRootCatalog().ToStringWithDeclaration();
            }
            else if (request.Equals("/downstat/date"))
            {
                // Downloads sorted by date
                return new DownloadCatalog().GetCatalog(true, pageNumber, threshold, acceptFB2).ToStringWithDeclaration();
            }
            else if (request.Equals("/downstat/alpha"))
            {
                // Downloads sorted alphabetically
                return new DownloadCatalog().GetCatalog(false, pageNumber, threshold, acceptFB2).ToStringWithDeclaration();
            }

            return null;
        }

        /// <summary>
        /// Handles search requests
        /// </summary>
        private string HandleSearchRequest(string[] pathParts, bool acceptFB2)
        {
            Log.WriteLine(LogLevel.Info, "Handling search request with {0} parts", pathParts.Length);

            if (pathParts.Length > 1)
            {
                if (pathParts[1].Equals("searchTerm") && pathParts.Length > 2)
                {
                    string searchTerm = pathParts[2];
                    Log.WriteLine(LogLevel.Info, "Simple search term: '{0}'", searchTerm);
                    return new OpenSearch().Search(searchTerm, "", acceptFB2).ToStringWithDeclaration();
                }
                else if (pathParts[1].Equals("searchType") && pathParts.Length > 4)
                {
                    int pageNumber = 0;
                    if (pathParts.Length > 6 && pathParts[5].Equals("pageNumber"))
                    {
                        int.TryParse(pathParts[6], out pageNumber);
                    }
                    string searchType = pathParts[2];
                    string searchTerm = pathParts[4];
                    Log.WriteLine(LogLevel.Info, "Advanced search - type: '{0}', term: '{1}', page: {2}", searchType, searchTerm, pageNumber);
                    return new OpenSearch().Search(searchTerm, searchType, acceptFB2, pageNumber).ToStringWithDeclaration();
                }
            }
            return null;
        }

        /// <summary>
        /// Loads OPDS structure configuration
        /// </summary>
        private void LoadOPDSStructure()
        {
            try
            {
                string structureString = Properties.Settings.Default.OPDSStructure;
                opdsStructure = new Dictionary<string, bool>();

                if (string.IsNullOrEmpty(structureString))
                {
                    InitializeDefaultOPDSStructure();
                }
                else
                {
                    ParseOPDSStructure(structureString);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error loading OPDS structure: {0}", ex.Message);
                InitializeDefaultOPDSStructure();
            }
        }

        /// <summary>
        /// Initializes default OPDS structure
        /// </summary>
        private void InitializeDefaultOPDSStructure()
        {
            opdsStructure = new Dictionary<string, bool>
            {
                {"newdate", true},
                {"newtitle", true},
                {"authorsindex", true},
                {"author-details", true},
                {"author-series", true},
                {"author-no-series", true},
                {"author-alphabetic", true},
                {"author-by-date", true},
                {"sequencesindex", true},
                {"genres", true},
                {"downloads", true}  // Keep as "downloads" for settings compatibility
            };
        }

        /// <summary>
        /// Parses OPDS structure from settings
        /// </summary>
        private void ParseOPDSStructure(string structure)
        {
            InitializeDefaultOPDSStructure();

            string[] parts = structure.Split(';');
            foreach (string part in parts)
            {
                string[] keyValue = part.Split(':');
                if (keyValue.Length == 2 && opdsStructure.ContainsKey(keyValue[0]))
                {
                    opdsStructure[keyValue[0]] = keyValue[1] == "1";
                }
            }
        }

        /// <summary>
        /// Checks if route is enabled in structure
        /// </summary>
        private bool IsRouteEnabled(string route)
        {
            return opdsStructure.ContainsKey(route) && opdsStructure[route];
        }
    }

    // Helper classes for structure-aware catalogs
    internal class RootCatalogWithStructure
    {
        private readonly Dictionary<string, bool> _opdsStructure;

        public RootCatalogWithStructure(Dictionary<string, bool> opdsStructure)
        {
            _opdsStructure = opdsStructure;
        }

        public XDocument GetCatalog()
        {
            var rootCatalog = new RootCatalog().GetCatalog();

            // Remove disabled entries from root catalog
            var entries = rootCatalog.Root.Elements("entry").ToList();
            foreach (var entry in entries)
            {
                var link = entry.Element("link");
                if (link != null)
                {
                    string href = link.Attribute("href")?.Value;
                    if (!string.IsNullOrEmpty(href))
                    {
                        bool shouldRemove = false;

                        if (href.Contains("/newdate") && !IsEnabled("newdate")) shouldRemove = true;
                        else if (href.Contains("/newtitle") && !IsEnabled("newtitle")) shouldRemove = true;
                        else if (href.Contains("/authorsindex") && !IsEnabled("authorsindex")) shouldRemove = true;
                        else if (href.Contains("/sequencesindex") && !IsEnabled("sequencesindex")) shouldRemove = true;
                        else if (href.Contains("/genres") && !IsEnabled("genres")) shouldRemove = true;
                        else if (href.Contains("/downstat") && !IsEnabled("downloads")) shouldRemove = true;  // Check /downstat path but use "downloads" key

                        if (shouldRemove)
                        {
                            entry.Remove();
                        }
                    }
                }
            }

            return rootCatalog;
        }

        private bool IsEnabled(string route)
        {
            return _opdsStructure.ContainsKey(route) && _opdsStructure[route];
        }
    }

    internal class AuthorDetailsCatalogWithStructure
    {
        private readonly Dictionary<string, bool> _opdsStructure;

        public AuthorDetailsCatalogWithStructure(Dictionary<string, bool> opdsStructure)
        {
            _opdsStructure = opdsStructure;
        }

        public XDocument GetCatalog(string author)
        {
            var authorDetails = new AuthorDetailsCatalog().GetCatalog(author);

            // Remove disabled entries
            var entries = authorDetails.Root.Elements("entry").ToList();
            foreach (var entry in entries)
            {
                var link = entry.Element("link");
                if (link != null)
                {
                    string href = link.Attribute("href")?.Value;
                    if (!string.IsNullOrEmpty(href))
                    {
                        bool shouldRemove = false;

                        if (href.Contains("/author-series/") && !IsEnabled("author-series")) shouldRemove = true;
                        else if (href.Contains("/author-no-series/") && !IsEnabled("author-no-series")) shouldRemove = true;
                        else if (href.Contains("/author-alphabetic/") && !IsEnabled("author-alphabetic")) shouldRemove = true;
                        else if (href.Contains("/author-by-date/") && !IsEnabled("author-by-date")) shouldRemove = true;

                        if (shouldRemove)
                        {
                            entry.Remove();
                        }
                    }
                }
            }

            return authorDetails;
        }

        private bool IsEnabled(string route)
        {
            return _opdsStructure.ContainsKey(route) && _opdsStructure[route];
        }
    }
}