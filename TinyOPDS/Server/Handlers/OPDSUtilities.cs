/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module contains utility methods for OPDS request processing
 * 
 */

using System;
using System.IO;
using System.Linq;
using System.Net;

namespace TinyOPDS.Server
{
    /// <summary>
    /// Utility methods for OPDS server operations
    /// </summary>
    public class OPDSUtilities
    {
        private readonly string[] extensions = { ".zip", ".epub", ".jpeg", ".ico", ".xml" };

        /// <summary>
        /// Gets client IP address from processor
        /// </summary>
        public string GetClientIP(HttpProcessor processor)
        {
            try
            {
                return ((IPEndPoint)processor.Socket.Client.RemoteEndPoint).Address.ToString();
            }
            catch
            {
                return "unknown";
            }
        }

        /// <summary>
        /// Normalizes request URL by removing prefixes and handling parameters
        /// </summary>
        public string NormalizeRequest(string httpUrl)
        {
            string request = httpUrl;
            Log.WriteLine(LogLevel.Info, "Original request: {0}", request);

            // Special handling for opensearch which must be at root
            if (!request.Contains("opds-opensearch.xml"))
            {
                // Remove OPDS prefix if present to normalize the request path
                if (!string.IsNullOrEmpty(Properties.Settings.Default.RootPrefix) &&
                    request.StartsWith("/" + Properties.Settings.Default.RootPrefix))
                {
                    request = request.Substring(Properties.Settings.Default.RootPrefix.Length + 1);
                    if (!request.StartsWith("/"))
                        request = "/" + request;
                }
            }

            while (request.Contains("//"))
                request = request.Replace("//", "/");

            if (!request.StartsWith("/"))
                request = "/" + request;

            // Handle parameters specially - don't decode them yet as they may contain encoded data
            int paramPos = request.IndexOf('?');
            if (paramPos >= 0)
            {
                if (request.Contains("pageNumber") || request.Contains("searchTerm"))
                {
                    int endParam = request.IndexOf('&', paramPos + 1);
                    if (endParam > 0 && !request.Substring(endParam).Contains("pageNumber") && !request.Substring(endParam).Contains("searchTerm"))
                    {
                        request = request.Substring(0, endParam);
                    }
                }
                else
                {
                    request = request.Substring(0, paramPos);
                }
            }

            Log.WriteLine(LogLevel.Info, "Normalized request: {0}", request);
            return request;
        }

        /// <summary>
        /// Gets file extension from request if it's a known type
        /// </summary>
        public string GetFileExtension(string request)
        {
            string ext = Path.GetExtension(request).ToLower();
            return extensions.Contains(ext) ? ext : string.Empty;
        }

        /// <summary>
        /// Validates if request is within acceptable parameters
        /// </summary>
        public bool IsValidRequest(string request)
        {
            return !string.IsNullOrEmpty(request) && request.Length <= 2048;
        }

        /// <summary>
        /// Determines if the request is for OPDS (XML) or web (HTML) format
        /// </summary>
        public bool IsOPDSRequest(string httpUrl)
        {
            // Simple logic: if URL contains /opds prefix, it's an OPDS request
            if (!string.IsNullOrEmpty(Properties.Settings.Default.RootPrefix) &&
                httpUrl.StartsWith("/" + Properties.Settings.Default.RootPrefix))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Detects if client supports FB2 format based on User-Agent
        /// </summary>
        public bool DetectFB2Support(string userAgent)
        {
            return Utils.DetectFB2Reader(userAgent);
        }

        /// <summary>
        /// Fixes missing Atom namespace in XML if needed
        /// </summary>
        public string FixNamespace(string xml)
        {
            if (xml.Contains("<feed ") && !xml.Contains("xmlns=\"http://www.w3.org/2005/Atom\""))
            {
                int feedPos = xml.IndexOf("<feed ");
                if (feedPos >= 0)
                {
                    xml = xml.Insert(feedPos + 5, " xmlns=\"http://www.w3.org/2005/Atom\"");
                }
            }
            return xml;
        }

        /// <summary>
        /// Applies URI prefixes to XML content for OPDS/Web routing
        /// </summary>
        public string ApplyUriPrefixes(string xml, bool isOPDSRequest)
        {
            try
            {
                // Always use relative paths for maximum compatibility and simplicity
                // For OPDS requests, add the /opds prefix
                // For web requests, no prefix needed
                if (isOPDSRequest && !string.IsNullOrEmpty(Properties.Settings.Default.RootPrefix))
                {
                    string prefix = "/" + Properties.Settings.Default.RootPrefix;
                    xml = xml.Replace("href=\"", "href=\"" + prefix);

                    // Special case: opensearch.xml must always be at root
                    xml = xml.Replace(prefix + "/opds-opensearch.xml", "/opds-opensearch.xml");
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error applying URI prefixes: {0}", ex.Message);
            }
            return xml;
        }

        /// <summary>
        /// Escapes JavaScript string for safe embedding in HTML
        /// </summary>
        public string EscapeJsString(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";

            return str.Replace("\\", "\\\\")
                     .Replace("'", "\\'")
                     .Replace("\"", "\\\"")
                     .Replace("\r", "\\r")
                     .Replace("\n", "\\n");
        }

        /// <summary>
        /// Determines request type for cancellation management
        /// </summary>
        public bool IsImageRequest(string request, string ext)
        {
            return ext.Equals(".jpeg") || ext.Equals(".png") ||
                   request.Contains("/cover/") || request.Contains("/thumbnail/");
        }

        /// <summary>
        /// Determines if request is a navigation request (non-image)
        /// </summary>
        public bool IsNavigationRequest(string request, string ext)
        {
            return string.IsNullOrEmpty(ext) ||
                   request.StartsWith("/reader/") ||
                   request.Contains("opds-opensearch.xml") ||
                   request.StartsWith("/search") ||
                   request.StartsWith("/author") ||
                   request.StartsWith("/sequence") ||
                   request.StartsWith("/genre");
        }
    }
}