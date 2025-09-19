/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Native FB2 parser without external dependencies - optimized version
 *
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Drawing;
using System.Globalization;

using TinyOPDS.Data;
using System.Text.RegularExpressions;

namespace TinyOPDS.Parsers
{
    public class FB2Parser : BookParser
    {
        private XNamespace fb2Ns = "http://www.gribuser.ru/xml/fictionbook/2.0";

        public override Book Parse(Stream stream, string fileName)
        {
            Book book = new Book(fileName);

            try
            {
                if (stream.CanSeek)
                {
                    book.DocumentSize = (UInt32)stream.Length;
                }
                else
                {
                    book.DocumentSize = 0;
                }
            }
            catch
            {
                book.DocumentSize = 0;
            }

            try
            {
                // Read only description block
                var description = ReadDescriptionBlock(stream, fileName);
                if (description != null)
                {
                    // Always generate unique ID
                    book.ID = Guid.NewGuid().ToString();
                    book.DocumentIDTrusted = false;

                    // Parse description content
                    ParseDescriptionContent(description, book, fileName);
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Book.Parse() exception {0} on file: {1}", e.Message, fileName);
            }

            return book;
        }

        private XElement ReadDescriptionBlock(Stream stream, string fileName)
        {
            try
            {
                const int chunkSize = 1024 * 8;
                var buffer = new byte[chunkSize];
                var sb = new StringBuilder();
                bool foundDescription = false;

                // Reset stream position
                if (stream.CanSeek)
                {
                    stream.Position = 0;
                }

                // Detect encoding from first few bytes
                Encoding encoding = Encoding.UTF8;
                byte[] encodingBuffer = new byte[1024];
                int encBytes = stream.Read(encodingBuffer, 0, Math.Min(1024, (int)stream.Length));
                if (encBytes > 0)
                {
                    string encodingTest = Encoding.UTF8.GetString(encodingBuffer, 0, encBytes);
                    encoding = DetectEncoding(encodingTest);
                }

                // Reset to beginning
                stream.Position = 0;

                string content = string.Empty;

                // Read chunks with correct encoding
                while (true)
                {
                    int bytesRead = stream.Read(buffer, 0, chunkSize);
                    if (bytesRead == 0) break;

                    string chunk = encoding.GetString(buffer, 0, bytesRead);
                    sb.Append(chunk);

                    // Check if we have complete description
                    content = sb.ToString();
                    int descEnd = content.IndexOf("</description>", StringComparison.OrdinalIgnoreCase);
                    if (descEnd >= 0)
                    {
                        foundDescription = true;
                        // Extract only up to </description>
                        content = content.Substring(0, descEnd + 14);
                        break;
                    }

                    // Safety check - don't read more than 64KB for description
                    if (sb.Length > 1024 * 64)
                    {
                        Log.WriteLine(LogLevel.Warning, "Description block too large in file: {0}", fileName);
                        break;
                    }
                }

                if (!foundDescription)
                {
                    Log.WriteLine(LogLevel.Warning, "No description block found in file: {0}", fileName);
                    return null;
                }

                // Fix XML encoding issues
                string xmlContent = FixXmlEncoding(content);
                debugxml = xmlContent + "</FictionBook>";
                var doc = XDocument.Parse(xmlContent + "</FictionBook>");
                return doc.Root?.Element(fb2Ns + "description");
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Failed to read description block for {0}: {1}", fileName, ex.Message);
                return null;
            }
        }

        string debugxml = "";

        private string FixXmlEncoding(string xmlText)
        {
            // Remove BOM if present
            if (xmlText.Length > 0 && xmlText[0] == '\ufeff')
            {
                xmlText = xmlText.Substring(1);
            }

            // First decode HTML entities (like &#1041; -> Б)
            if (xmlText.Contains("&#"))
            {
                xmlText = Regex.Replace(xmlText, @"&#(\d+);",
                    match => ((char)int.Parse(match.Groups[1].Value)).ToString());
            }

            // Fix broken paragraph tags in annotations
            xmlText = FixBrokenParagraphs(xmlText);

            // Then escape unescaped ampersands (but not already escaped ones)
            xmlText = Regex.Replace(xmlText, @"&(?!(amp|lt|gt|quot|apos);)", "&amp;");

            return xmlText;
        }

        private string FixBrokenParagraphs(string xmlText)
        {
            // Find annotations and fix them
            int annotationStart = xmlText.IndexOf("<annotation>");
            if (annotationStart >= 0)
            {
                int annotationEnd = xmlText.IndexOf("</annotation>");
                if (annotationEnd > annotationStart)
                {
                    string before = xmlText.Substring(0, annotationStart);
                    string annotation = xmlText.Substring(annotationStart, annotationEnd - annotationStart + 13);
                    string after = xmlText.Substring(annotationEnd + 13);

                    // Fix broken paragraphs in annotation
                    annotation = Regex.Replace(annotation, @"<p>([^<]*(?:<(?!/?p|empty-line)[^>]*>[^<]*)*)<(?=p>|empty-line)", "<p>$1</p><");

                    xmlText = before + annotation + after;
                }
            }

            return xmlText;
        }

        private Encoding DetectEncoding(string xmlStart)
        {
            try
            {
                if (xmlStart.Contains("encoding="))
                {
                    int encStart = xmlStart.IndexOf("encoding=\"") + 10;
                    if (encStart > 10)
                    {
                        int encEnd = xmlStart.IndexOf('"', encStart);
                        if (encEnd > encStart)
                        {
                            string encodingName = xmlStart.Substring(encStart, encEnd - encStart);
                            return Encoding.GetEncoding(encodingName);
                        }
                    }
                }
            }
            catch
            {
                // Fallback to UTF-8
            }

            return Encoding.UTF8;
        }

        private void ParseDescriptionContent(XElement description, Book book, string fileName)
        {
            // Parse title-info
            var titleInfo = description.Element(fb2Ns + "title-info");
            if (titleInfo != null)
            {
                ParseTitleInfo(titleInfo, book, fileName);
            }

            // Parse document-info
            var documentInfo = description.Element(fb2Ns + "document-info");
            if (documentInfo != null)
            {
                ParseDocumentInfo(documentInfo, book, fileName);
            }

            // Parse publish-info (optional)
            var publishInfo = description.Element(fb2Ns + "publish-info");
            if (publishInfo != null)
            {
                ParsePublishInfo(publishInfo, book);
            }
        }

        private void ParseTitleInfo(XElement titleInfo, Book book, string fileName)
        {
            // Genres
            var genres = titleInfo.Elements(fb2Ns + "genre").Select(g => g.Value).ToList();
            if (genres.Any())
            {
                book.Genres = genres;
            }

            // Authors
            var authors = titleInfo.Elements(fb2Ns + "author");
            if (authors.Any())
            {
                book.Authors = new List<string>();
                foreach (var author in authors)
                {
                    string authorName = ParsePersonName(author);
                    if (!string.IsNullOrEmpty(authorName))
                    {
                        book.Authors.Add(authorName);
                    }
                }
            }

            // Book title
            var bookTitle = titleInfo.Element(fb2Ns + "book-title")?.Value;
            if (!string.IsNullOrEmpty(bookTitle))
            {
                book.Title = bookTitle;
            }

            // Annotation
            var annotation = titleInfo.Element(fb2Ns + "annotation");
            if (annotation != null)
            {
                book.Annotation = ExtractTextFromAnnotation(annotation);
            }

            // Date
            var date = titleInfo.Element(fb2Ns + "date");
            if (date != null)
            {
                book.BookDate = ParseDate(date, fileName);
            }
            else
            {
                book.BookDate = DateParser.GetFileDate(fileName);
            }

            // Language
            var lang = titleInfo.Element(fb2Ns + "lang")?.Value;
            if (!string.IsNullOrEmpty(lang))
            {
                book.Language = lang;
            }

            // Translators
            var translators = titleInfo.Elements(fb2Ns + "translator");
            if (translators.Any())
            {
                book.Translators = new List<string>();
                foreach (var translator in translators)
                {
                    string translatorName = ParsePersonName(translator);
                    if (!string.IsNullOrEmpty(translatorName))
                    {
                        book.Translators.Add(translatorName);
                    }
                }
            }

            // Sequences (series)
            var sequences = titleInfo.Elements(fb2Ns + "sequence");
            if (sequences.Any())
            {
                var firstSequence = sequences.First();
                var seqName = firstSequence.Attribute("name")?.Value;
                if (!string.IsNullOrEmpty(seqName))
                {
                    book.Sequence = seqName.Capitalize(true);

                    var seqNumber = firstSequence.Attribute("number")?.Value;
                    if (!string.IsNullOrEmpty(seqNumber) && uint.TryParse(seqNumber, out uint num))
                    {
                        book.NumberInSequence = num;
                    }
                }
            }

            var coverpage = titleInfo.Element(fb2Ns + "coverpage");
        }

        private void ParseDocumentInfo(XElement documentInfo, Book book, string fileName)
        {
            // Document date
            var date = documentInfo.Element(fb2Ns + "date");
            if (date != null)
            {
                book.DocumentDate = ParseDate(date, fileName);
            }
            else
            {
                book.DocumentDate = DateParser.GetFileDate(fileName);
            }

            // Version
            var version = documentInfo.Element(fb2Ns + "version")?.Value;
            if (!string.IsNullOrEmpty(version) && float.TryParse(version, NumberStyles.Any, CultureInfo.InvariantCulture, out float ver))
            {
                book.Version = ver;
            }
        }

        private void ParsePublishInfo(XElement publishInfo, Book book)
        {
            // Publisher
            var publisher = publishInfo.Element(fb2Ns + "publisher")?.Value;

            // Year
            var year = publishInfo.Element(fb2Ns + "year")?.Value;
            if (!string.IsNullOrEmpty(year) && int.TryParse(year, out int yearNum))
            {
                // Can override book date if needed
                // book.BookDate = new DateTime(yearNum, 1, 1);
            }

            // ISBN
            var isbn = publishInfo.Element(fb2Ns + "isbn")?.Value;
        }

        private string ParsePersonName(XElement person)
        {
            var firstName = person.Element(fb2Ns + "first-name")?.Value ?? "";
            var middleName = person.Element(fb2Ns + "middle-name")?.Value ?? "";
            var lastName = person.Element(fb2Ns + "last-name")?.Value ?? "";
            var nickName = person.Element(fb2Ns + "nickname")?.Value ?? "";

            // Build name in format: LastName FirstName MiddleName
            var nameParts = new List<string>();

            if (!string.IsNullOrEmpty(lastName))
            {
                nameParts.Add(lastName.Trim().Capitalize());
            }

            if (!string.IsNullOrEmpty(firstName))
            {
                nameParts.Add(firstName.Trim().Capitalize());
            }

            if (!string.IsNullOrEmpty(middleName))
            {
                nameParts.Add(middleName.Trim().Capitalize());
            }

            // Use nickname if no other name parts
            if (nameParts.Count == 0 && !string.IsNullOrEmpty(nickName))
            {
                nameParts.Add(nickName.Trim().Capitalize());
            }

            return string.Join(" ", nameParts);
        }

        private DateTime ParseDate(XElement dateElement, string fileName)
        {
            // Try value attribute first
            var dateValue = dateElement.Attribute("value")?.Value;
            if (!string.IsNullOrEmpty(dateValue))
            {
                if (DateTime.TryParse(dateValue, out DateTime parsedDate))
                {
                    return ValidateDate(parsedDate);
                }
            }

            // Try element text
            var dateText = dateElement.Value;
            if (!string.IsNullOrEmpty(dateText))
            {
                // Try full date
                if (DateTime.TryParse(dateText, out DateTime parsedDate))
                {
                    return ValidateDate(parsedDate);
                }

                // Try year only
                if (int.TryParse(dateText.Substring(0, Math.Min(4, dateText.Length)), out int year))
                {
                    if (year >= 1 && year <= 9999)
                    {
                        return new DateTime(year, 1, 1);
                    }
                }
            }

            return DateParser.GetFileDate(fileName);
        }

        private DateTime ValidateDate(DateTime date)
        {
            if (date.Year < 1 || date.Year > 9999)
                return DateTime.Now;

            if (date > DateTime.Now.AddYears(10))
                return DateTime.Now;

            return date;
        }

        private string ExtractTextFromAnnotation(XElement annotation)
        {
            var sb = new StringBuilder();

            foreach (var node in annotation.DescendantNodes())
            {
                if (node is XText textNode)
                {
                    sb.Append(textNode.Value);
                }
            }

            return sb.ToString().Trim();
        }

        public override Image GetCoverImage(Stream stream, string fileName)
        {
            Image image = null;
            try
            {
                XDocument xml = null;

                // Parse full file for cover extraction
                if (!stream.CanSeek)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        memoryStream.Position = 0;
                        xml = ParseFullDocument(memoryStream, fileName);
                    }
                }
                else
                {
                    stream.Position = 0;
                    xml = ParseFullDocument(stream, fileName);
                }

                if (xml == null) return null;

                // Get namespaces from document
                XNamespace fb2Namespace = xml.Root?.Name.Namespace ?? XNamespace.None;
                XNamespace xlinkNs = "http://www.w3.org/1999/xlink";

                // Find coverpage in title-info
                var titleInfo = xml.Root?.Element(fb2Namespace + "description")?.Element(fb2Namespace + "title-info");
                if (titleInfo == null) return null;

                var coverpage = titleInfo.Element(fb2Namespace + "coverpage");
                if (coverpage == null) return null;

                // Get image href - FB2 uses xlink:href
                var imageElement = coverpage.Element(fb2Namespace + "image");
                if (imageElement == null) return null;

                string href = imageElement.Attribute(xlinkNs + "href")?.Value ??
                             imageElement.Attribute("href")?.Value ?? // fallback for non-standard FB2
                             imageElement.Attribute("l:href")?.Value; // sometimes used in broken files

                if (string.IsNullOrEmpty(href)) return null;

                // Remove # prefix if present
                if (href.StartsWith("#"))
                    href = href.Substring(1);

                // Find binary element with this id
                var binaries = xml.Root.Elements(fb2Namespace + "binary");
                foreach (var binary in binaries)
                {
                    var id = binary.Attribute("id")?.Value;
                    if (id == href)
                    {
                        // Get content type if available
                        var contentType = binary.Attribute("content-type")?.Value ?? "image/jpeg";

                        // Decode base64 image
                        string base64Data = binary.Value.Replace("\r", "").Replace("\n", "").Replace(" ", "");

                        try
                        {
                            byte[] imageData = Convert.FromBase64String(base64Data);

                            using (var memStream = new MemoryStream(imageData))
                            {
                                image = Image.FromStream(memStream);

                                // Resize to standard cover size
                                image = image.Resize(CoverImage.CoverSize);
                            }
                        }
                        catch (FormatException)
                        {
                            Log.WriteLine(LogLevel.Warning, "Invalid base64 image data for {0}", fileName);
                        }
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "GetCoverImage() exception {0} on file: {1}", e.Message, fileName);
            }
            return image;
        }

        private XDocument ParseFullDocument(Stream stream, string fileName)
        {
            try
            {
                // Simple full document parsing for cover extraction
                stream.Position = 0;
                return XDocument.Load(stream);
            }
            catch
            {
                // Try with encoding detection
                try
                {
                    stream.Position = 0;
                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, Math.Min(buffer.Length, (int)stream.Length));
                    string start = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    Encoding encoding = DetectEncoding(start);

                    stream.Position = 0;
                    using (var reader = new StreamReader(stream, encoding, false, 1024, true))
                    {
                        string xmlStr = reader.ReadToEnd();
                        return XDocument.Parse(xmlStr, LoadOptions.PreserveWhitespace);
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Warning, "Error parsing full document for {0}: {1}", fileName, ex.Message);
                    return null;
                }
            }
        }
    }
}