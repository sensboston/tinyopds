/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * FB2 parser implementation - FIXED for duplicate ID issues and Russian dates
 * Updated to use MindTouch SGMLReader NuGet package
 *
 */

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Drawing;
using System.Drawing.Imaging;

using FB2Library;
using FB2Library.Elements;
using TinyOPDS.Data;
using Sgml;

namespace TinyOPDS.Parsers
{
    public class FB2Parser : BookParser
    {
        private XDocument xml = null;

        /// <summary>
        /// Parse FB2 book from stream - supports both seekable and non-seekable streams
        /// MODIFIED: Always generates unique ID to avoid duplicate ID conflicts
        /// MODIFIED: Uses RussianDateParser for intelligent date handling
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override Book Parse(Stream stream, string fileName)
        {
            Book book = new Book(fileName);

            // Handle document size for different stream types
            try
            {
                if (stream.CanSeek)
                {
                    book.DocumentSize = (UInt32)stream.Length;
                }
                else
                {
                    // For non-seekable streams, we'll estimate or set later
                    book.DocumentSize = 0;
                }
            }
            catch
            {
                book.DocumentSize = 0;
            }

            try
            {
                FB2File fb2 = new FB2File();

                // For non-seekable streams, we need to read content into memory first
                if (!stream.CanSeek)
                {
                    xml = ParseNonSeekableStream(stream, fileName);
                }
                else
                {
                    xml = ParseSeekableStream(stream, fileName);
                }

                if (xml != null)
                {
                    fb2.Load(xml, true);

                    // MODIFIED: Always generate unique ID to avoid conflicts
                    if (fb2.DocumentInfo != null)
                    {
                        // Save original ID for logging/debugging
                        string originalID = fb2.DocumentInfo.ID;

                        // ALWAYS generate new unique ID for our database
                        book.ID = Guid.NewGuid().ToString();

                        // Never trust FB2 IDs as they can be duplicated
                        book.DocumentIDTrusted = false;

                        if (fb2.DocumentInfo.DocumentVersion != null)
                            book.Version = (float)fb2.DocumentInfo.DocumentVersion;

                        // Parse DocumentDate with intelligent Russian date support
                        if (fb2.DocumentInfo.DocumentDate != null)
                        {
                            book.DocumentDate = DateParser.ParseFB2Date(fb2.DocumentInfo.DocumentDate, fileName);
                        }
                        else
                        {
                            // Use file date as fallback
                            book.DocumentDate = DateParser.GetFileDate(fileName);
                        }
                    }
                    else
                    {
                        // No DocumentInfo - generate ID anyway
                        book.ID = Guid.NewGuid().ToString();
                        book.DocumentIDTrusted = false;
                        book.DocumentDate = DateParser.GetFileDate(fileName);
                    }

                    if (fb2.TitleInfo != null)
                    {
                        if (fb2.TitleInfo.BookTitle != null) book.Title = fb2.TitleInfo.BookTitle.Text;
                        if (fb2.TitleInfo.Annotation != null) book.Annotation = fb2.TitleInfo.Annotation.ToString();
                        if (fb2.TitleInfo.Sequences != null && fb2.TitleInfo.Sequences.Count > 0)
                        {
                            book.Sequence = fb2.TitleInfo.Sequences.First().Name.Capitalize(true);
                            if (fb2.TitleInfo.Sequences.First().Number != null)
                            {
                                book.NumberInSequence = (UInt32)(fb2.TitleInfo.Sequences.First().Number);
                            }
                        }
                        if (fb2.TitleInfo.Language != null) book.Language = fb2.TitleInfo.Language;

                        // Parse BookDate with intelligent Russian date support
                        if (fb2.TitleInfo.BookDate != null)
                        {
                            book.BookDate = DateParser.ParseFB2Date(fb2.TitleInfo.BookDate, fileName);
                        }
                        else
                        {
                            // No date specified - use file date as fallback
                            book.BookDate = DateParser.GetFileDate(fileName);
                        }

                        // Process authors using structured FB2 data
                        if (fb2.TitleInfo.BookAuthors != null && fb2.TitleInfo.BookAuthors.Any())
                        {
                            book.Authors = new List<string>();
                            foreach (var ba in fb2.TitleInfo.BookAuthors)
                            {
                                string firstName = ba.FirstName?.Text ?? "";
                                string middleName = ba.MiddleName?.Text ?? "";
                                string lastName = ba.LastName?.Text ?? "";

                                string authorName = BuildAuthorName(firstName, middleName, lastName);
                                if (!string.IsNullOrEmpty(authorName))
                                {
                                    book.Authors.Add(authorName);
                                }
                            }
                        }

                        // Process translators using structured FB2 data
                        if (fb2.TitleInfo.Translators != null && fb2.TitleInfo.Translators.Any())
                        {
                            book.Translators = new List<string>();
                            foreach (var tr in fb2.TitleInfo.Translators)
                            {
                                string firstName = tr.FirstName?.Text ?? "";
                                string middleName = tr.MiddleName?.Text ?? "";
                                string lastName = tr.LastName?.Text ?? "";

                                string translatorName = BuildAuthorName(firstName, middleName, lastName);
                                if (!string.IsNullOrEmpty(translatorName))
                                {
                                    book.Translators.Add(translatorName);
                                }
                            }
                        }

                        if (fb2.TitleInfo.Genres != null && fb2.TitleInfo.Genres.Any())
                        {
                            book.Genres = new List<string>();
                            book.Genres.AddRange((from g in fb2.TitleInfo.Genres select g.Genre).ToList());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Book.Parse() exception {0} on file: {1}", e.Message, fileName);
            }

            return book;
        }

        /// <summary>
        /// Build author name in standard "LastName FirstName MiddleName" format from FB2 structured data
        /// FB2 already provides separate FirstName, MiddleName, LastName fields
        /// </summary>
        /// <param name="firstName">First name from FB2</param>
        /// <param name="middleName">Middle name from FB2</param>
        /// <param name="lastName">Last name from FB2</param>
        /// <returns>Formatted author name</returns>
        private string BuildAuthorName(string firstName, string middleName, string lastName)
        {
            var nameParts = new List<string>();

            // Always start with LastName if available
            if (!string.IsNullOrEmpty(lastName))
            {
                nameParts.Add(lastName.Trim().Capitalize());
            }

            // Add FirstName if available
            if (!string.IsNullOrEmpty(firstName))
            {
                nameParts.Add(firstName.Trim().Capitalize());
            }

            // Add MiddleName if available
            if (!string.IsNullOrEmpty(middleName))
            {
                nameParts.Add(middleName.Trim().Capitalize());
            }

            return string.Join(" ", nameParts);
        }

        /// <summary>
        /// Parse non-seekable stream (like ZipEntry.OpenReader())
        /// </summary>
        private XDocument ParseNonSeekableStream(Stream stream, string fileName)
        {
            try
            {
                // Read entire stream content into memory for parsing
                using (var memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    memoryStream.Position = 0;

                    return ParseSeekableStream(memoryStream, fileName);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Failed to parse non-seekable stream for {0}: {1}", fileName, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Parse seekable stream (original logic)
        /// </summary>
        private XDocument ParseSeekableStream(Stream stream, string fileName)
        {
            XDocument xml = null;

            try
            {
                stream.Position = 0;

                // Project Mono has a bug: Xdocument.Load() can't detect encoding
                string encoding = string.Empty;
                if (Utils.IsLinux)
                {
                    using (StreamReader sr = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
                    {
                        encoding = sr.ReadLine();
                        int idx = encoding.ToLower().IndexOf("encoding=\"");
                        if (idx > 0)
                        {
                            encoding = encoding.Substring(idx + 10);
                            encoding = encoding.Substring(0, encoding.IndexOf('"'));
                            stream.Position = 0;
                            using (StreamReader esr = new StreamReader(stream, Encoding.GetEncoding(encoding), false, 1024, true))
                            {
                                string xmlStr = esr.ReadToEnd();
                                try
                                {
                                    xml = XDocument.Parse(xmlStr, LoadOptions.PreserveWhitespace);
                                }
                                catch
                                {
                                    stream.Position = 0;
                                    xml = TryParseBySgml(stream, fileName);
                                }
                            }
                        }
                    }
                }

                if (xml == null)
                {
                    try
                    {
                        stream.Position = 0;
                        xml = XDocument.Load(stream);
                    }
                    catch
                    {
                        stream.Position = 0;
                        xml = TryParseBySgml(stream, fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error parsing seekable stream for {0}: {1}", fileName, ex.Message);
            }

            return xml;
        }

        /// <summary>
        /// Try parsing using SGML reader for malformed XML
        /// Updated for MindTouch SGMLReader API
        /// </summary>
        private XDocument TryParseBySgml(Stream stream, string fileName)
        {
            try
            {
                // Create SGML reader with proper configuration
                using (SgmlReader sgmlReader = new SgmlReader())
                {
                    // Configure the reader for FB2 parsing
                    sgmlReader.DocType = "FictionBook";
                    sgmlReader.WhitespaceHandling = WhitespaceHandling.All;

                    // Try to detect encoding from the stream
                    StreamReader streamReader = null;
                    try
                    {
                        // First, try to detect encoding
                        byte[] buffer = new byte[1024];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        stream.Position = 0;

                        // Check for BOM or XML declaration
                        string start = Encoding.UTF8.GetString(buffer, 0, Math.Min(bytesRead, 200));
                        Encoding detectedEncoding = Encoding.UTF8;

                        if (start.Contains("encoding="))
                        {
                            int encStart = start.IndexOf("encoding=\"") + 10;
                            if (encStart > 10)
                            {
                                int encEnd = start.IndexOf('"', encStart);
                                if (encEnd > encStart)
                                {
                                    string encodingName = start.Substring(encStart, encEnd - encStart);
                                    try
                                    {
                                        detectedEncoding = Encoding.GetEncoding(encodingName);
                                    }
                                    catch
                                    {
                                        // Fall back to UTF-8 if encoding is not recognized
                                        detectedEncoding = Encoding.UTF8;
                                    }
                                }
                            }
                        }

                        streamReader = new StreamReader(stream, detectedEncoding, false, 1024, true);
                    }
                    catch
                    {
                        // Fall back to default encoding
                        stream.Position = 0;
                        streamReader = new StreamReader(stream, Encoding.UTF8, true, 1024, true);
                    }

                    using (streamReader)
                    {
                        sgmlReader.InputStream = streamReader;

                        // Try to load custom FB2 DTD if available
                        try
                        {
                            sgmlReader.Dtd = LoadFb2Dtd();
                        }
                        catch (Exception dtdEx)
                        {
                            // If DTD loading fails, let SGMLReader use its default behavior
                            // It will still try to parse the document
                            Log.WriteLine(LogLevel.Warning, "Failed to load FB2 DTD for {0}: {1}", fileName, dtdEx.Message);
                        }

                        // Load the document
                        return XDocument.Load(sgmlReader);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "SGML parsing failed for {0}: {1}", fileName, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Load FB2 DTD for SGML parsing
        /// Uses MindTouch SGMLReader static Parse method
        /// </summary>
        private static SgmlDtd LoadFb2Dtd()
        {
            try
            {
                // Load the FB2 DTD from embedded resources
                Assembly assembly = Assembly.GetExecutingAssembly();
                string resourceName = assembly.GetName().Name + ".Resources.fb2.dtd";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            // Use the static Parse method to create DTD
                            // Method signature: Parse(Uri baseUri, string name, TextReader input, string subset, string proxy, XmlNameTable nt)
                            Uri baseUri = new Uri("http://www.gribuser.ru/xml/fictionbook/2.0");

                            // Call the static Parse method with correct parameters
                            return SgmlDtd.Parse(
                                baseUri,        // baseUri
                                "FictionBook",  // name
                                reader,         // input (TextReader)
                                null,           // subset
                                null,           // proxy
                                null            // nameTable
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't fail - SGML reader can work without DTD
                Log.WriteLine(LogLevel.Warning, "Failed to load FB2 DTD: {0}", ex.Message);
            }

            // Return null if DTD resource not found or failed to load
            return null;
        }

        /// <summary>
        /// Get cover image from FB2 file
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override Image GetCoverImage(Stream stream, string fileName)
        {
            Image image = null;
            try
            {
                FB2File fb2 = new FB2File();

                // Handle non-seekable streams
                Stream workingStream = stream;
                bool needsDisposal = false;

                if (!stream.CanSeek)
                {
                    // Copy to memory stream for seeking operations
                    var memStream = new MemoryStream();
                    stream.CopyTo(memStream);
                    memStream.Position = 0;
                    workingStream = memStream;
                    needsDisposal = true;
                }
                else
                {
                    workingStream.Position = 0;
                }

                try
                {
                    xml = XDocument.Load(workingStream);
                    fb2.Load(xml, false);

                    if (fb2.TitleInfo != null && fb2.TitleInfo.Cover != null && fb2.TitleInfo.Cover.HasImages() && fb2.Images.Count > 0)
                    {
                        string coverHRef = fb2.TitleInfo.Cover.CoverpageImages.First().HRef.Substring(1);
                        var binaryObject = fb2.Images.First(item => item.Value.Id == coverHRef);
                        if (binaryObject.Value.BinaryData != null && binaryObject.Value.BinaryData.Length > 0)
                        {
                            using (MemoryStream memStream = new MemoryStream(binaryObject.Value.BinaryData))
                            {
                                image = Image.FromStream(memStream);
                                // Convert image to jpeg
                                ImageFormat fmt = binaryObject.Value.ContentType == ContentTypeEnum.ContentTypePng ? ImageFormat.Png : ImageFormat.Gif;
                                if (binaryObject.Value.ContentType != ContentTypeEnum.ContentTypeJpeg)
                                {
                                    image = Image.FromStream(image.ToStream(fmt));
                                }
                                image = image.Resize(CoverImage.CoverSize);
                            }
                        }
                    }
                }
                finally
                {
                    if (needsDisposal)
                    {
                        workingStream?.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Book.GetCoverImage() exception {0} on file: {1}", e.Message, fileName);
            }
            return image;
        }

        /// <summary>
        /// Remove illegal XML characters from a string.
        /// </summary>
        public string SanitizeXmlString(string xml)
        {
            StringBuilder buffer = new StringBuilder(xml.Length);
            foreach (char c in xml) if (IsLegalXmlChar(c)) buffer.Append(c);
            return buffer.ToString();
        }

        /// <summary>
        /// Whether a given character is allowed by XML 1.0.
        /// </summary>
        public bool IsLegalXmlChar(int character)
        {
            return
            (
                 character == 0x9 /* == '\t' == 9   */          ||
                 character == 0xA /* == '\n' == 10  */          ||
                 character == 0xD /* == '\r' == 13  */          ||
                (character >= 0x20 && character <= 0xD7FF) ||
                (character >= 0xE000 && character <= 0xFFFD) ||
                (character >= 0x10000 && character <= 0x10FFFF)
            );
        }
    }
}