﻿/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module handles static resources like icons, logos,
 * and OpenSearch descriptors
 * 
 */

using System;
using System.IO;
using System.Reflection;
using TinyOPDS.OPDS;

namespace TinyOPDS.Server
{
    /// <summary>
    /// Handles static resource requests
    /// </summary>
    public class ResourceHandlers
    {
        /// <summary>
        /// Handles icon file requests
        /// </summary>
        public void HandleIconRequest(HttpProcessor processor, string request)
        {
            try
            {
                string iconName = Path.GetFileName(request);
                string resourceName = Assembly.GetExecutingAssembly().GetName().Name + ".Resources." + iconName;

                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream != null && stream.Length > 0)
                    {
                        processor.WriteSuccess("image/x-icon");
                        stream.CopyTo(processor.OutputStream.BaseStream);
                        processor.OutputStream.BaseStream.Flush();
                        return;
                    }
                }

                Log.WriteLine(LogLevel.Warning, "Icon not found: {0}", iconName);
                processor.WriteFailure();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Icon request error: {0}", ex.Message);
                processor.WriteFailure();
            }
        }

        /// <summary>
        /// Handles logo.png request
        /// </summary>
        public void HandleDummyCoverRequest(HttpProcessor processor, string imageName)
        {
            try
            {
                string resourceName = Assembly.GetExecutingAssembly().GetName().Name + $".Resources.{imageName}";

                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream != null && stream.Length > 0)
                    {
                        processor.WriteSuccess(imageName.Contains("png") ? "image/png" : "image/jpeg");
                        stream.CopyTo(processor.OutputStream.BaseStream);
                        processor.OutputStream.BaseStream.Flush();
                        return;
                    }
                }

                Log.WriteLine(LogLevel.Warning, "Image not found in resources");
                processor.WriteFailure();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Image request error: {0}", ex.Message);
                processor.WriteFailure();
            }
        }

        /// <summary>
        /// Handles OpenSearch description document request
        /// </summary>
        public void HandleOpenSearchRequest(HttpProcessor processor, bool isOPDSRequest)
        {
            try
            {
                string xml = new OpenSearch().OpenSearchDescription().ToStringWithDeclaration();
                xml = xml.Insert(xml.IndexOf("<OpenSearchDescription") + 22,
                    " xmlns=\"http://a9.com/-/spec/opensearch/1.1/\"");

                // OpenSearch always needs to be accessible from root
                xml = OPDSUtilities.ApplyUriPrefixes(xml, false);

                processor.WriteSuccess("application/atom+xml;charset=utf-8");
                processor.OutputStream.Write(xml);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "OpenSearch request error: {0}", ex.Message);
                processor.WriteFailure();
            }
        }

        /// <summary>
        /// Handles JavaScript file requests from embedded resources
        /// </summary>
        public void HandleJavaScriptRequest(HttpProcessor processor, string request)
        {
            try
            {
                // Extract script name from request
                string scriptName = Path.GetFileName(request);
                if (string.IsNullOrEmpty(scriptName))
                {
                    processor.WriteFailure();
                    return;
                }

                // Build resource name
                string resourceName = Assembly.GetExecutingAssembly().GetName().Name + ".Resources." + scriptName;

                // Try to load from embedded resources
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream != null && stream.Length > 0)
                    {
                        processor.WriteSuccess("application/javascript");
                        stream.CopyTo(processor.OutputStream.BaseStream);
                        processor.OutputStream.BaseStream.Flush();
                        Log.WriteLine(LogLevel.Info, "Served JavaScript file: {0}", scriptName);
                        return;
                    }
                }

                Log.WriteLine(LogLevel.Warning, "JavaScript file not found in resources: {0}", scriptName);
                processor.WriteFailure();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "JavaScript request error for {0}: {1}", request, ex.Message);
                processor.WriteFailure();
            }
        }
    }
}