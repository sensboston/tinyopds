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
        private string readerHtml = null;
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
                string bookId = request.Substring(8);
                bookId = bookId.Replace("%7B", "{").Replace("%7D", "}");

                Log.WriteLine(LogLevel.Info, "Reader request for book: {0}", bookId);

                Book book = Library.GetBook(bookId);
                if (book == null)
                {
                    Log.WriteLine(LogLevel.Warning, "Book {0} not found for reader", bookId);
                    HandleBookNotFoundForReader(processor, bookId);
                    return;
                }

                using (var memStream = new MemoryStream())
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                    {
                        if (!downloadHandler.ExtractBookContentWithCancellation(book, memStream, cts.Token))
                        {
                            HandleBookFileNotFoundForReader(processor, book);
                            return;
                        }
                    }

                    memStream.Position = 0;
                    byte[] bookData = memStream.ToArray();
                    string base64Data = Convert.ToBase64String(bookData);

                    string mimeType = book.BookType == BookType.FB2 ? "application/x-fictionbook+xml" : "application/epub+zip";

                    string fileName = Transliteration.Front(
                        string.Format("{0}_{1}.{2}",
                            book.Authors.FirstOrDefault() ?? "Unknown",
                            book.Title,
                            book.BookType == BookType.FB2 ? "fb2" : "epub"));

                    string html = PrepareReaderHtml(base64Data, mimeType, fileName, book.Title, book.Authors.FirstOrDefault());

                    if (!string.IsNullOrEmpty(html))
                    {
                        processor.WriteSuccess("text/html; charset=utf-8");
                        processor.OutputStream.Write(html);

                        RecordReadEvent(bookId, book.BookType.ToString().ToLower(), processor);
                        HttpServer.ServerStatistics.IncrementBooksSent();
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
        /// Records book read event to database through Library
        /// </summary>
        private void RecordReadEvent(string bookId, string format, HttpProcessor processor)
        {
            try
            {
                string clientInfo = null;
                if (processor != null)
                {
                    string userAgent = processor.HttpHeaders.ContainsKey("User-Agent") ?
                        processor.HttpHeaders["User-Agent"] : null;
                    string clientIp = processor.RealClientIP; // Use centralized property

                    if (!string.IsNullOrEmpty(userAgent) || !string.IsNullOrEmpty(clientIp))
                    {
                        clientInfo = string.Format("{0}|{1}",
                            userAgent ?? "Unknown",
                            clientIp ?? "Unknown");

                        if (clientInfo.Length > 255)
                        {
                            clientInfo = clientInfo.Substring(0, 255);
                        }
                    }
                }

                Library.RecordDownload(bookId, "read", format, clientInfo);

                Log.WriteLine(LogLevel.Info, "Recorded read event for book {0}, format: {1}", bookId, format);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Failed to record read event for book {0}: {1}", bookId, ex.Message);
            }
        }

        /// <summary>
        /// Loads reader HTML template from file system or embedded resources
        /// </summary>
        private void LoadReaderHtml()
        {
            try
            {
                string resourceBase = Assembly.GetExecutingAssembly().GetName().Name + ".Resources.reader.";

                using (Stream htmlStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceBase + "reader.html"))
                {
                    if (htmlStream != null)
                    {
                        using (StreamReader reader = new StreamReader(htmlStream, Encoding.UTF8))
                        {
                            string html = reader.ReadToEnd();

                            string mainCss = LoadResourceText(resourceBase + "reader.css");
                            string themesCss = LoadResourceText(resourceBase + "reader-themes.css");

                            string formatsJs = LoadResourceText(resourceBase + "reader-formats.js");
                            string mainJs = LoadResourceText(resourceBase + "reader-main.js");

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

                string errorMessage = "Book not found";
                string errorHtml = html.Replace(
                    "<div class=\"book-content light font-serif\" id=\"bookContent\"></div>",
                    string.Format("<div class=\"book-content light font-serif\" id=\"bookContent\"><div class=\"error\">{0}</div></div>", errorMessage)
                );

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

                string errorMessage = "Book file not found";
                string bookInfo = string.Format("<h1>{0}</h1><p class=\"author\">{1}</p>",
                    book.Title,
                    book.Authors.FirstOrDefault() ?? "Unknown Author");

                string errorHtml = html.Replace(
                    "<div class=\"book-content light font-serif\" id=\"bookContent\"></div>",
                    string.Format("<div class=\"book-content light font-serif\" id=\"bookContent\">{0}<div class=\"error\">{1}<br><small>File: {2}</small></div></div>",
                        bookInfo, errorMessage, book.FilePath)
                );

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

                string html = readerHtml;

                var locStrings = new StringBuilder();
                locStrings.Append("{");
                locStrings.AppendFormat("'tableOfContents':'{0}',", OPDSUtilities.EscapeJsString(Localizer.Text("Table of Contents")));
                locStrings.AppendFormat("'openBook':'{0}',", OPDSUtilities.EscapeJsString(Localizer.Text("Open Book")));
                locStrings.AppendFormat("'decreaseFont':'{0}',", OPDSUtilities.EscapeJsString(Localizer.Text("Decrease Font")));
                locStrings.AppendFormat("'increaseFont':'{0}',", OPDSUtilities.EscapeJsString(Localizer.Text("Increase Font")));
                locStrings.AppendFormat("'changeFont':'{0}',", OPDSUtilities.EscapeJsString(Localizer.Text("Change Font")));
                locStrings.AppendFormat("'changeTheme':'{0}',", OPDSUtilities.EscapeJsString(Localizer.Text("Change Theme")));
                locStrings.AppendFormat("'decreaseMargins':'{0}',", OPDSUtilities.EscapeJsString(Localizer.Text("Decrease Margins")));
                locStrings.AppendFormat("'increaseMargins':'{0}',", OPDSUtilities.EscapeJsString(Localizer.Text("Increase Margins")));
                locStrings.AppendFormat("'standardWidth':'{0}',", OPDSUtilities.EscapeJsString(Localizer.Text("Standard Width")));
                locStrings.AppendFormat("'fullWidth':'{0}',", OPDSUtilities.EscapeJsString(Localizer.Text("Full Width")));
                locStrings.AppendFormat("'fullscreen':'{0}',", OPDSUtilities.EscapeJsString(Localizer.Text("Fullscreen")));
                locStrings.AppendFormat("'loading':'{0}',", OPDSUtilities.EscapeJsString(Localizer.Text("Loading...")));
                locStrings.AppendFormat("'errorLoading':'{0}',", OPDSUtilities.EscapeJsString(Localizer.Text("Error loading file")));
                locStrings.AppendFormat("'noTitle':'{0}',", OPDSUtilities.EscapeJsString(Localizer.Text("Untitled")));
                locStrings.AppendFormat("'unknownAuthor':'{0}',", OPDSUtilities.EscapeJsString(Localizer.Text("Unknown Author")));
                locStrings.AppendFormat("'noChapters':'{0}'", OPDSUtilities.EscapeJsString(Localizer.Text("No chapters available")));
                locStrings.Append("}");

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
        var dataUrl = window.tinyOPDSBook.data;
        fetch(dataUrl)
            .then(res => res.blob())
            .then(blob => {{
                var file = new File([blob], window.tinyOPDSBook.fileName, {{
                    type: blob.type
                }});
                
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
                    OPDSUtilities.EscapeJsString(fileName),
                    OPDSUtilities.EscapeJsString(bookTitle),
                    OPDSUtilities.EscapeJsString(author ?? ""),
                    locStrings.ToString()
                );

                html = html.Replace("</head>", scriptInjection);

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
    }
}