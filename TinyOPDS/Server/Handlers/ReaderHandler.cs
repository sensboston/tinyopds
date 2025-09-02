/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module handles embedded reader functionality
 * for FB2 and EPUB books
 * 
 */

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using TinyOPDS.Data;

namespace TinyOPDS.Server
{
    /// <summary>
    /// Handles embedded reader functionality
    /// </summary>
    public class ReaderHandler
    {
        private string readerHtml = null; // Cache for reader HTML
        private readonly BookDownloadHandler downloadHandler;

        public ReaderHandler()
        {
            downloadHandler = new BookDownloadHandler();
            LoadReaderHtml();
        }

        /// <summary>
        /// Main entry point for handling reader requests
        /// </summary>
        public void HandleReaderRequest(HttpProcessor processor, string request)
        {
            try
            {
                // Extract book ID from /reader/{bookId}
                string bookId = request.Substring(8); // Remove "/reader/"

                // Clean up GUID encoding
                bookId = bookId.Replace("%7B", "{").Replace("%7D", "}");

                Log.WriteLine(LogLevel.Info, "Reader request for book: {0}", bookId);

                Book book = Library.GetBook(bookId);
                if (book == null)
                {
                    Log.WriteLine(LogLevel.Warning, "Book {0} not found for reader", bookId);
                    HandleBookNotFoundForReader(processor, bookId);
                    return;
                }

                // Get book content with cancellation support for better performance
                using (var memStream = new MemoryStream())
                {
                    // Create a cancellation token with 30 second timeout for reader requests
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                    {
                        if (!downloadHandler.ExtractBookContentWithCancellation(book, memStream, cts.Token))
                        {
                            HandleBookFileNotFoundForReader(processor, book);
                            return;
                        }
                    }

                    // Prepare book data as base64
                    memStream.Position = 0;
                    byte[] bookData = memStream.ToArray();
                    string base64Data = Convert.ToBase64String(bookData);

                    // Determine MIME type
                    string mimeType = book.BookType == BookType.FB2 ? "application/x-fictionbook+xml" : "application/epub+zip";

                    // Prepare file name
                    string fileName = Transliteration.Front(
                        string.Format("{0}_{1}.{2}",
                            book.Authors.FirstOrDefault() ?? "Unknown",
                            book.Title,
                            book.BookType == BookType.FB2 ? "fb2" : "epub"));

                    // Create modified HTML with embedded book
                    string html = PrepareReaderHtml(base64Data, mimeType, fileName, book.Title, book.Authors.FirstOrDefault());

                    if (!string.IsNullOrEmpty(html))
                    {
                        processor.WriteSuccess("text/html; charset=utf-8");
                        processor.OutputStream.Write(html);

                        Log.WriteLine(LogLevel.Info, "Successfully served reader for book: {0}", book.Title);
                    }
                    else
                    {
                        processor.WriteFailure();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Reader request error: {0}", ex.Message);
                processor.WriteFailure();
            }
        }

        /// <summary>
        /// Loads reader HTML template from file system or embedded resources
        /// </summary>
        private void LoadReaderHtml()
        {
            try
            {
                // Load main HTML
                string readerPath = Path.Combine(Utils.ServiceFilesLocation, "reader.html");
                if (File.Exists(readerPath))
                {
                    readerHtml = File.ReadAllText(readerPath, Encoding.UTF8);
                    Log.WriteLine(LogLevel.Info, "Loaded reader.html from file system");
                    return;
                }

                // Load from embedded resources
                string resourceBase = Assembly.GetExecutingAssembly().GetName().Name + ".Resources.reader.";

                // Load HTML template
                using (Stream htmlStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceBase + "reader.html"))
                {
                    if (htmlStream != null)
                    {
                        using (StreamReader reader = new StreamReader(htmlStream, Encoding.UTF8))
                        {
                            string html = reader.ReadToEnd();

                            // Load CSS files
                            string mainCss = LoadResourceText(resourceBase + "reader.css");
                            string themesCss = LoadResourceText(resourceBase + "reader-themes.css");

                            // Load JS files
                            string formatsJs = LoadResourceText(resourceBase + "reader-formats.js");
                            string mainJs = LoadResourceText(resourceBase + "reader-main.js");

                            // Replace link and script tags with inline content
                            if (!string.IsNullOrEmpty(mainCss) && !string.IsNullOrEmpty(themesCss))
                            {
                                string cssBlock = string.Format("<style>\n{0}\n</style>\n<style>\n{1}\n</style>", mainCss, themesCss);
                                html = html.Replace("<link rel=\"stylesheet\" href=\"reader.css\">", "")
                                           .Replace("<link rel=\"stylesheet\" href=\"reader-themes.css\">", "");
                                html = html.Replace("</head>", cssBlock + "\n</head>");
                            }

                            if (!string.IsNullOrEmpty(formatsJs) && !string.IsNullOrEmpty(mainJs))
                            {
                                string jsBlock = string.Format("<script>\n{0}\n</script>\n<script>\n{1}\n</script>", formatsJs, mainJs);
                                html = html.Replace("<script src=\"reader-formats.js\"></script>", "")
                                           .Replace("<script src=\"reader-main.js\"></script>", jsBlock);
                            }

                            readerHtml = html;
                            Log.WriteLine(LogLevel.Info, "Loaded reader components from embedded resources");
                        }
                    }
                    else
                    {
                        Log.WriteLine(LogLevel.Warning, "reader.html not found in resources");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error loading reader.html: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Loads resource text from embedded resources
        /// </summary>
        private string LoadResourceText(string resourceName)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error loading resource {0}: {1}", resourceName, ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Handles case when book is not found in library
        /// </summary>
        private void HandleBookNotFoundForReader(HttpProcessor processor, string bookId)
        {
            try
            {
                if (string.IsNullOrEmpty(readerHtml))
                {
                    LoadReaderHtml();
                }

                string html = readerHtml ?? GetFallbackReaderHtml();

                // Create error message in reader
                string errorMessage = "Book not found";
                string errorHtml = html.Replace(
                    "<div class=\"book-content light font-serif\" id=\"bookContent\"></div>",
                    string.Format("<div class=\"book-content light font-serif\" id=\"bookContent\"><div class=\"error\">{0}</div></div>", errorMessage)
                );

                // Set error title
                errorHtml = errorHtml.Replace("Reader", string.Format("Reader - {0}", errorMessage));

                processor.WriteSuccess("text/html; charset=utf-8");
                processor.OutputStream.Write(errorHtml);

                Log.WriteLine(LogLevel.Info, "Served book not found error for book ID: {0}", bookId);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error handling book not found: {0}", ex.Message);
                processor.WriteFailure();
            }
        }

        /// <summary>
        /// Handles case when book file is not found on disk
        /// </summary>
        private void HandleBookFileNotFoundForReader(HttpProcessor processor, Book book)
        {
            try
            {
                if (string.IsNullOrEmpty(readerHtml))
                {
                    LoadReaderHtml();
                }

                string html = readerHtml ?? GetFallbackReaderHtml();

                // Create error message in reader with book info
                string errorMessage = "Book file not found";
                string bookInfo = string.Format("<h1>{0}</h1><p class=\"author\">{1}</p>",
                    book.Title,
                    book.Authors.FirstOrDefault() ?? "Unknown Author");

                string errorHtml = html.Replace(
                    "<div class=\"book-content light font-serif\" id=\"bookContent\"></div>",
                    string.Format("<div class=\"book-content light font-serif\" id=\"bookContent\">{0}<div class=\"error\">{1}<br><small>File: {2}</small></div></div>",
                        bookInfo, errorMessage, book.FilePath)
                );

                // Set error title with book title
                errorHtml = errorHtml.Replace("Reader", string.Format("Reader - {0}", book.Title));

                processor.WriteSuccess("text/html; charset=utf-8");
                processor.OutputStream.Write(errorHtml);

                Log.WriteLine(LogLevel.Info, "Served book file not found error for book: {0} (file: {1})", book.Title, book.FilePath);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error handling book file not found: {0}", ex.Message);
                processor.WriteFailure();
            }
        }

        /// <summary>
        /// Returns fallback HTML when reader template is not available
        /// </summary>
        private string GetFallbackReaderHtml()
        {
            return @"<!DOCTYPE html>
<html><head><title>Reader - Error</title><meta charset=""UTF-8""></head>
<body><div class=""error"">Error loading reader interface</div></body></html>";
        }

        /// <summary>
        /// Prepares reader HTML with embedded book data
        /// </summary>
        private string PrepareReaderHtml(string base64Data, string mimeType, string fileName, string bookTitle, string author)
        {
            try
            {
                if (string.IsNullOrEmpty(readerHtml))
                {
                    LoadReaderHtml();
                    if (string.IsNullOrEmpty(readerHtml))
                    {
                        Log.WriteLine(LogLevel.Error, "Reader HTML template not available");
                        return null;
                    }
                }

                // Create a copy of the reader HTML
                string html = readerHtml;

                // Prepare localization strings as JSON
                var locStrings = new StringBuilder();
                locStrings.Append("{");
                locStrings.AppendFormat("'tableOfContents':'{0}',", EscapeJsString(Localizer.Text("Table of Contents")));
                locStrings.AppendFormat("'openBook':'{0}',", EscapeJsString(Localizer.Text("Open Book")));
                locStrings.AppendFormat("'decreaseFont':'{0}',", EscapeJsString(Localizer.Text("Decrease Font")));
                locStrings.AppendFormat("'increaseFont':'{0}',", EscapeJsString(Localizer.Text("Increase Font")));
                locStrings.AppendFormat("'changeFont':'{0}',", EscapeJsString(Localizer.Text("Change Font")));
                locStrings.AppendFormat("'changeTheme':'{0}',", EscapeJsString(Localizer.Text("Change Theme")));
                locStrings.AppendFormat("'decreaseMargins':'{0}',", EscapeJsString(Localizer.Text("Decrease Margins")));
                locStrings.AppendFormat("'increaseMargins':'{0}',", EscapeJsString(Localizer.Text("Increase Margins")));
                locStrings.AppendFormat("'standardWidth':'{0}',", EscapeJsString(Localizer.Text("Standard Width")));
                locStrings.AppendFormat("'fullWidth':'{0}',", EscapeJsString(Localizer.Text("Full Width")));
                locStrings.AppendFormat("'fullscreen':'{0}',", EscapeJsString(Localizer.Text("Fullscreen")));
                locStrings.AppendFormat("'loading':'{0}',", EscapeJsString(Localizer.Text("Loading...")));
                locStrings.AppendFormat("'errorLoading':'{0}',", EscapeJsString(Localizer.Text("Error loading file")));
                locStrings.AppendFormat("'noTitle':'{0}',", EscapeJsString(Localizer.Text("Untitled")));
                locStrings.AppendFormat("'unknownAuthor':'{0}',", EscapeJsString(Localizer.Text("Unknown Author")));
                locStrings.AppendFormat("'noChapters':'{0}'", EscapeJsString(Localizer.Text("No chapters available")));
                locStrings.Append("}");

                // Inject book data and localization into the HTML
                string scriptInjection = string.Format(@"
<script>
// Injected book data
window.tinyOPDSBook = {{
    data: 'data:{0};base64,{1}',
    fileName: '{2}',
    title: '{3}',
    author: '{4}'
}};

// Injected localization
localStorage.setItem('tinyopds-localization', JSON.stringify({5}));

// Auto-load the book after page loads
document.addEventListener('DOMContentLoaded', function() {{
    setTimeout(function() {{
        // Create a blob from the data URL
        var dataUrl = window.tinyOPDSBook.data;
        fetch(dataUrl)
            .then(res => res.blob())
            .then(blob => {{
                // Create a File object
                var file = new File([blob], window.tinyOPDSBook.fileName, {{
                    type: blob.type
                }});
                
                // Find the reader instance and load the file
                if (window.universalReader) {{
                    window.universalReader.handleFileSelect(file);
                }}
            }});
    }}, 500);
}});
</script>
</head>",
                    mimeType,
                    base64Data,
                    EscapeJsString(fileName),
                    EscapeJsString(bookTitle),
                    EscapeJsString(author ?? ""),
                    locStrings.ToString()
                );

                // Replace </head> with our script injection
                html = html.Replace("</head>", scriptInjection);

                // Modify the reader initialization to expose the instance globally
                html = html.Replace(
                    "new UniversalReader();",
                    "window.universalReader = new UniversalReader();"
                );

                return html;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error preparing reader HTML: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Escapes JavaScript string for safe embedding
        /// </summary>
        private string EscapeJsString(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";

            return str.Replace("\\", "\\\\")
                     .Replace("'", "\\'")
                     .Replace("\"", "\\\"")
                     .Replace("\r", "\\r")
                     .Replace("\n", "\\n");
        }
    }
}