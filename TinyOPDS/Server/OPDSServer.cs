/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Enhanced OPDS HTTP server - main coordinator class
 * Delegates actual work to specialized handlers
 * 
 */

using System;
using System.IO;
using System.Net;

namespace TinyOPDS.Server
{
    /// <summary>
    /// Main OPDS HTTP server class that coordinates request handling
    /// </summary>
    public class OPDSServer : HttpServer
    {
        // Handler instances
        private readonly OPDSRequestRouter requestRouter;
        private readonly ReaderHandler readerHandler;
        private readonly BookDownloadHandler downloadHandler;
        private readonly ImageRequestHandler imageHandler;
        private readonly XslTransformHandler xslHandler;
        private readonly ResourceHandlers resourceHandlers;
        private readonly OPDSUtilities utilities;

        public OPDSServer(IPAddress interfaceIP, int port, int timeout = 5000)
            : base(interfaceIP, port, timeout)
        {
            // Initialize all handlers
            utilities = new OPDSUtilities();
            xslHandler = new XslTransformHandler();
            requestRouter = new OPDSRequestRouter(xslHandler);
            readerHandler = new ReaderHandler();
            downloadHandler = new BookDownloadHandler();
            imageHandler = new ImageRequestHandler();
            resourceHandlers = new ResourceHandlers();
        }

        /// <summary>
        /// Handles POST requests (not implemented for OPDS)
        /// </summary>
        public override void HandlePOSTRequest(HttpProcessor processor, StreamReader inputData)
        {
            Log.WriteLine(LogLevel.Warning, "HTTP POST request from {0}: {1} : NOT IMPLEMENTED",
                utilities.GetClientIP(processor), processor.HttpUrl);
            processor.WriteMethodNotAllowed();
        }

        /// <summary>
        /// Main GET request handler - routes to appropriate handler
        /// </summary>
        public override void HandleGETRequest(HttpProcessor processor)
        {
            string clientIP = utilities.GetClientIP(processor);
            string clientHash = processor.ClientHash; // Get client hash for request cancellation

            Log.WriteLine("HTTP GET request from {0}: {1}", clientIP, processor.HttpUrl);

            try
            {
                string request = utilities.NormalizeRequest(processor.HttpUrl);
                string ext = utilities.GetFileExtension(request);

                if (!utilities.IsValidRequest(request))
                {
                    processor.WriteBadRequest();
                    return;
                }

                // Determine request type for cancellation management
                bool isImageRequest = utilities.IsImageRequest(request, ext);
                bool isNavigationRequest = utilities.IsNavigationRequest(request, ext);

                // Determine if this is OPDS (XML) or Web (HTML) request
                bool isOPDSRequest = utilities.IsOPDSRequest(processor);

                // Detect client capabilities
                string userAgent = processor.HttpHeaders.ContainsKey("User-Agent") ?
                    processor.HttpHeaders["User-Agent"] : "";
                bool acceptFB2 = utilities.DetectFB2Support(userAgent) || !isOPDSRequest;

                // Get pagination threshold
                int threshold = isOPDSRequest ?
                    Properties.Settings.Default.ItemsPerOPDSPage :
                    Properties.Settings.Default.ItemsPerWebPage;

                // Route to appropriate handler based on request type
                RouteRequest(processor, request, ext, isOPDSRequest, acceptFB2, threshold, clientHash);
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "HandleGETRequest() exception: {0}", e.Message);
                processor.WriteFailure();
            }
        }

        /// <summary>
        /// Routes request to appropriate handler
        /// </summary>
        private void RouteRequest(HttpProcessor processor, string request, string ext,
            bool isOPDSRequest, bool acceptFB2, int threshold, string clientHash)
        {
            // Handle reader requests
            if (request.StartsWith("/reader/"))
            {
                readerHandler.HandleReaderRequest(processor, request);
                return;
            }

            // Handle book download requests
            if (request.StartsWith("/download/"))
            {
                string downloadExt = DetermineDownloadFormat(request);
                if (!string.IsNullOrEmpty(downloadExt))
                {
                    downloadHandler.HandleBookDownloadRequest(processor, request, downloadExt, acceptFB2);
                    return;
                }
            }

            // Handle logo request
            if (request.Equals("/logo.png"))
            {
                resourceHandlers.HandleLogoRequest(processor);
                return;
            }

            // Handle smart header script
            if (request.Equals("/smart-header.js"))
            {
                resourceHandlers.HandleSmartHeaderScript(processor);
                return;
            }

            // Handle OPDS catalog requests (no extension)
            if (string.IsNullOrEmpty(ext))
            {
                requestRouter.HandleOPDSRequest(processor, request, isOPDSRequest, acceptFB2, threshold);
            }
            // Handle OpenSearch descriptor
            else if (request.Contains("opds-opensearch.xml"))
            {
                resourceHandlers.HandleOpenSearchRequest(processor, isOPDSRequest);
            }
            // Handle legacy book download URLs
            else if ((request.Contains(".fb2.zip") && ext.Equals(".zip")) || ext.Equals(".epub"))
            {
                downloadHandler.HandleBookDownloadRequest(processor, request, ext, acceptFB2);
            }
            // Handle image requests (covers and thumbnails)
            else if (ext.Equals(".jpeg") || ext.Equals(".png"))
            {
                // Use cancellation-aware handler if client hash is available
                if (!string.IsNullOrEmpty(clientHash))
                {
                    imageHandler.HandleImageRequest(processor, request, clientHash);
                }
                else
                {
                    imageHandler.HandleImageRequest(processor, request);
                }
            }
            // Handle icon requests
            else if (ext.Equals(".ico"))
            {
                resourceHandlers.HandleIconRequest(processor, request);
            }
            else
            {
                processor.WriteFailure();
            }
        }

        /// <summary>
        /// Determines download format from request path
        /// </summary>
        private string DetermineDownloadFormat(string request)
        {
            // Determine format from path structure: /download/{guid}/fb2 or /download/{guid}/epub
            if (request.Contains("/fb2"))
            {
                return ".zip"; // FB2 always comes as ZIP
            }
            else if (request.Contains("/epub"))
            {
                return ".epub";
            }
            return "";
        }
    }
}