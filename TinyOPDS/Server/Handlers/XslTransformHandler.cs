/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module handles XSL transformations for converting
 * OPDS XML feeds to HTML for web browsers
 * 
 */

using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.XPath;
using TinyOPDS.Data;

namespace TinyOPDS.Server
{
    /// <summary>
    /// Handles XSL transformations for OPDS to HTML conversion
    /// </summary>
    public class XslTransformHandler
    {
        private readonly XslCompiledTransform xslTransform = new XslCompiledTransform();
        private readonly object xslLock = new object();

        // Added to the script URLs: mobile Chrome kept an old script despite no-cache, a new build gives new URLs
        private static readonly string scriptsVersion = GetScriptsVersion();

        private static string GetScriptsVersion()
        {
            try
            {
                return File.GetLastWriteTimeUtc(Assembly.GetExecutingAssembly().Location).Ticks.ToString("x");
            }
            catch
            {
                return Utils.ServerVersionName.GetHashCode().ToString("x");
            }
        }

        public XslTransformHandler()
        {
            InitializeXslTransform();
        }

        /// <summary>
        /// Initializes XSL transform from file or embedded resource
        /// </summary>
        private void InitializeXslTransform()
        {
            try
            {
                string xslFileName = Path.Combine(Utils.ServiceFilesLocation, "xml2html.xsl");

                if (File.Exists(xslFileName))
                {
                    xslTransform.Load(xslFileName);
                    Log.WriteLine(LogLevel.Info, "Loaded external XSL template: {0}", xslFileName);
                }
                else
                {
                    using (Stream resStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                        Assembly.GetExecutingAssembly().GetName().Name + ".Resources.xml2html.xsl"))
                    {
                        if (resStream != null)
                        {
                            using (XmlReader reader = XmlReader.Create(resStream))
                                xslTransform.Load(reader);
                            Log.WriteLine(LogLevel.Info, "Loaded embedded XSL template");
                        }
                        else
                        {
                            Log.WriteLine(LogLevel.Warning, "XSL template not found");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error loading XSL template: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Reloads XSL transform (useful for development)
        /// </summary>
        public void ReloadTransform()
        {
            InitializeXslTransform();
        }

        /// <summary>
        /// Transforms OPDS XML to HTML using XSL template
        /// </summary>
        public string TransformToHtml(string xml)
        {
            try
            {
                lock (xslLock)
                {
#if DEBUG
                    // Reload XSL in debug mode for easier development
                    InitializeXslTransform();
#endif
                    using (var htmlStream = new MemoryStream())
                    using (var stringReader = new StringReader(xml))
                    {
                        var xPathDoc = new XPathDocument(stringReader);
                        var writer = new XmlTextWriter(htmlStream, null);

                        // Create XSL arguments with all parameters
                        XsltArgumentList args = CreateXsltArguments();

                        xslTransform.Transform(xPathDoc, args, writer);

                        htmlStream.Position = 0;

                        using (var streamReader = new StreamReader(htmlStream))
                        {
                            return streamReader.ReadToEnd();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "XSL transformation error: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Creates XSLT argument list with all necessary parameters
        /// </summary>
        private XsltArgumentList CreateXsltArguments()
        {
            XsltArgumentList args = new XsltArgumentList();

            // Server and library information
            args.AddParam("serverVersion", "", Utils.ServerVersionName.Replace("running on ", ""));

            // Library name with book count and proper plural form
            var booksCount = Library.Count;
            var books = StringUtils.ApplyPluralForm(booksCount, Localizer.Language, string.Format(Localizer.Text("{0} books"), booksCount));
            string libName = string.Format("{0}: {1}", Properties.Settings.Default.ServerName, books);
            args.AddParam("libName", "", libName);
            args.AddParam("scriptsVersion", "", scriptsVersion);
            if (Properties.Settings.Default.DarkThemeOnWeb)
            {
                args.AddParam("darkTheme", "", "dark");
            }

            // Book uploads (double click on the logo), the script is added only when they are enabled
            if (UploadHandler.IsEnabled)
            {
                args.AddParam("uploadMaxMB", "", Properties.Settings.Default.MaxUploadSizeMB.ToString());
                args.AddParam("uploadTooLarge", "", string.Format(Localizer.Text("The file is too large, maximum is {0} MB"), Properties.Settings.Default.MaxUploadSizeMB));
                args.AddParam("uploadingText", "", Localizer.Text("Uploading"));
                args.AddParam("uploadFailedText", "", Localizer.Text("Upload error"));

                // Uploaded books get a delete button: ",id1,id2," for a contains() test in the stylesheet
                args.AddParam("uploadedIds", "", "," + string.Join(",", Library.GetUploadedBookIds()) + ",");
                args.AddParam("deleteBookText", "", Localizer.Text("Delete the book"));
                args.AddParam("deleteQuestionText", "", Localizer.Text("Delete the book from the library?"));
                args.AddParam("deleteText", "", Localizer.Text("Delete"));
                args.AddParam("cancelText", "", Localizer.Text("Cancel"));
                args.AddParam("deleteFailedText", "", Localizer.Text("Delete error"));
            }

            // Web interface localization
            AddWebInterfaceParameters(args);

            // Reader localization
            AddReaderParameters(args);

            return args;
        }

        /// <summary>
        /// Adds web interface localization parameters
        /// </summary>
        private void AddWebInterfaceParameters(XsltArgumentList args)
        {
            args.AddParam("uiLanguage", "", Localizer.Language);
            args.AddParam("searchPlaceholder", "", Localizer.Text("Search authors or books..."));
            args.AddParam("searchButtonText", "", Localizer.Text("Search"));
            args.AddParam("formatText", "", Localizer.Text("Format:"));
            args.AddParam("sizeText", "", Localizer.Text("Size:"));
            args.AddParam("downloadText", "", Localizer.Text("Download"));
            args.AddParam("downloadEpubText", "", Localizer.Text("Download EPUB"));
            args.AddParam("readText", "", Localizer.Text("Read"));
        }

        /// <summary>
        /// Adds reader localization parameters
        /// </summary>
        private void AddReaderParameters(XsltArgumentList args)
        {
            args.AddParam("readerTableOfContents", "", Localizer.Text("Table of Contents"));
            args.AddParam("readerOpenBook", "", Localizer.Text("Open Book"));
            args.AddParam("readerDecreaseFont", "", Localizer.Text("Decrease Font"));
            args.AddParam("readerIncreaseFont", "", Localizer.Text("Increase Font"));
            args.AddParam("readerChangeFont", "", Localizer.Text("Change Font"));
            args.AddParam("readerChangeTheme", "", Localizer.Text("Change Theme"));
            args.AddParam("readerDecreaseMargins", "", Localizer.Text("Decrease Margins"));
            args.AddParam("readerIncreaseMargins", "", Localizer.Text("Increase Margins"));
            args.AddParam("readerStandardWidth", "", Localizer.Text("Standard Width"));
            args.AddParam("readerFullWidth", "", Localizer.Text("Full Width"));
            args.AddParam("readerFullscreen", "", Localizer.Text("Fullscreen"));
            args.AddParam("readerLoading", "", Localizer.Text("Loading..."));
            args.AddParam("readerErrorLoading", "", Localizer.Text("Error loading file"));
            args.AddParam("readerNoTitle", "", Localizer.Text("Untitled"));
            args.AddParam("readerUnknownAuthor", "", Localizer.Text("Unknown Author"));
            args.AddParam("readerNoChapters", "", Localizer.Text("No chapters available"));
        }
    }
}