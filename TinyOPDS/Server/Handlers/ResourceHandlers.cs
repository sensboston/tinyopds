/*
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
                string resourceName = Assembly.GetExecutingAssembly().GetName().Name + ".Icons." + iconName;

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
                xml = ApplyUriPrefixes(xml, false);

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
        /// Handles smart-header.js request
        /// </summary>
        public void HandleSmartHeaderScript(HttpProcessor processor)
        {
            try
            {
                string resourceName = Assembly.GetExecutingAssembly().GetName().Name + ".Resources.smart-header.js";

                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream != null && stream.Length > 0)
                    {
                        processor.WriteSuccess("application/javascript");
                        stream.CopyTo(processor.OutputStream.BaseStream);
                        processor.OutputStream.BaseStream.Flush();
                        Log.WriteLine(LogLevel.Info, "Served smart-header.js");
                        return;
                    }
                }

                Log.WriteLine(LogLevel.Warning, "smart-header.js not found in resources");
                processor.WriteFailure();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "smart-header.js request error: {0}", ex.Message);
                processor.WriteFailure();
            }
        }

        /// <summary>
        /// Applies URI prefixes to XML content
        /// </summary>
        private string ApplyUriPrefixes(string xml, bool isOPDSRequest)
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
    }
}