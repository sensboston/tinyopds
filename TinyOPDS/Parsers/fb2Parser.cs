/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * FB2 parser implementation
 * 
 ************************************************************/

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;

using FB2Library;
using FB2Library.Elements;
using TinyOPDS.Data;
using TinyOPDS.Sgml;

namespace TinyOPDS.Parsers
{
    public class FB2Parser : BookParser
    {
        private XDocument xml = null;

        private static SgmlDtd LoadFb2Dtd(SgmlReader sgml)
        {
            Contract.Requires(sgml != null);
            Contract.Ensures(Contract.Result<SgmlDtd>() != null);

            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(assembly.GetName().Name + ".Resources.fb2.dtd"))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    return SgmlDtd.Parse(new Uri("http://localhost"), sgml.DocType, null, reader, null, sgml.WebProxy, sgml.NameTable);
                }
            }
        }

        /// <summary>
        /// Parse FB2 book from stream
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override Book Parse(Stream stream, string fileName)
        {
            Book book = new Book(fileName);
            book.DocumentSize = (UInt32)stream.Length;

            try
            {
                FB2File fb2 = new FB2File();
                stream.Position = 0;

                xml = LoadXmlFromStream(stream, fileName);

                if (xml != null)
                {
                    fb2.Load(xml, true);
                    FillBookFromFB2(book, fb2);
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Book.Parse() exception {0} on file: {1}", e.Message, fileName);
            }
            finally
            {
                stream?.Dispose();
            }

            return book;
        }

        /// <summary>
        /// Load and parse XML from stream with fallback methods
        /// </summary>
        private XDocument LoadXmlFromStream(Stream stream, string fileName)
        {
            // Try standard XML loading first
            try
            {
                stream.Position = 0;
                return XDocument.Load(stream);
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Standard XML parsing failed for {0}: {1}, trying alternatives", fileName, ex.Message);
            }

            // Try with encoding detection for Linux
            if (Utils.IsLinux)
            {
                var xmlDoc = TryParseWithEncodingDetection(stream, fileName);
                if (xmlDoc != null) return xmlDoc;
            }

            // Try with XML sanitization
            var sanitizedXmlDoc = TryParseWithSanitization(stream, fileName);
            if (sanitizedXmlDoc != null) return sanitizedXmlDoc;

            // Try with SGML reader as last resort
            return TryParseWithSgmlReader(stream, fileName);
        }

        /// <summary>
        /// Try parsing with encoding detection (Linux specific)
        /// </summary>
        private XDocument TryParseWithEncodingDetection(Stream stream, string fileName)
        {
            try
            {
                stream.Position = 0;
                using (StreamReader sr = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
                {
                    string firstLine = sr.ReadLine();
                    if (string.IsNullOrEmpty(firstLine)) return null;

                    string encoding = ExtractEncodingFromXmlDeclaration(firstLine);
                    if (!string.IsNullOrEmpty(encoding))
                    {
                        stream.Position = 0;
                        using (StreamReader esr = new StreamReader(stream, Encoding.GetEncoding(encoding), false, 1024, true))
                        {
                            string xmlContent = esr.ReadToEnd();
                            xmlContent = SanitizeXmlContent(xmlContent);
                            return XDocument.Parse(xmlContent, LoadOptions.PreserveWhitespace);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Encoding detection parsing failed for {0}: {1}", fileName, ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Try parsing with XML content sanitization
        /// </summary>
        private XDocument TryParseWithSanitization(Stream stream, string fileName)
        {
            try
            {
                stream.Position = 0;
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
                {
                    string xmlContent = reader.ReadToEnd();
                    xmlContent = SanitizeXmlContent(xmlContent);
                    return XDocument.Parse(xmlContent, LoadOptions.PreserveWhitespace);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Sanitized XML parsing failed for {0}: {1}", fileName, ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Try parsing with SGML reader
        /// </summary>
        private XDocument TryParseWithSgmlReader(Stream stream, string fileName)
        {
            try
            {
                stream.Position = 0;
                using (HtmlStream reader = new HtmlStream(stream, Encoding.Default))
                {
                    using (SgmlReader sgmlReader = new SgmlReader())
                    {
                        sgmlReader.InputStream = reader;
                        sgmlReader.Dtd = LoadFb2Dtd(sgmlReader);
                        return XDocument.Load(sgmlReader);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "SGML parsing failed for {0}: {1}", fileName, ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Extract encoding from XML declaration
        /// </summary>
        private string ExtractEncodingFromXmlDeclaration(string xmlDeclaration)
        {
            if (string.IsNullOrEmpty(xmlDeclaration)) return null;

            int encodingIndex = xmlDeclaration.ToLower().IndexOf("encoding=\"");
            if (encodingIndex > 0)
            {
                string encodingPart = xmlDeclaration.Substring(encodingIndex + 10);
                int endQuoteIndex = encodingPart.IndexOf('"');
                if (endQuoteIndex > 0)
                {
                    return encodingPart.Substring(0, endQuoteIndex);
                }
            }
            return null;
        }

        /// <summary>
        /// Sanitize XML content to fix common issues
        /// </summary>
        private string SanitizeXmlContent(string xmlContent)
        {
            if (string.IsNullOrEmpty(xmlContent)) return xmlContent;

            // Remove illegal XML characters
            StringBuilder sanitized = new StringBuilder(xmlContent.Length);
            foreach (char c in xmlContent)
            {
                if (IsLegalXmlChar(c))
                    sanitized.Append(c);
            }

            string result = sanitized.ToString();

            // Fix invalid XML names that start with '.' or other invalid characters
            result = FixInvalidXmlNames(result);

            // Fix other common XML issues
            result = FixCommonXmlIssues(result);

            return result;
        }

        /// <summary>
        /// Fix invalid XML element and attribute names
        /// </summary>
        private string FixInvalidXmlNames(string xmlContent)
        {
            // Pattern to match XML tags and attributes that start with invalid characters
            string tagPattern = @"<(/?)\s*\.([^>\s]+)";
            xmlContent = Regex.Replace(xmlContent, tagPattern, "<$1_dot_$2", RegexOptions.IgnoreCase);

            // Pattern to match attribute names that start with invalid characters
            string attrPattern = @"\s+\.([^=\s]+)=";
            xmlContent = Regex.Replace(xmlContent, attrPattern, " _dot_$1=", RegexOptions.IgnoreCase);

            // Fix other invalid starting characters for XML names
            string invalidNamePattern = @"<(/?)\s*([0-9\-])([^>\s]*)";
            xmlContent = Regex.Replace(xmlContent, invalidNamePattern, "<$1_$2$3", RegexOptions.IgnoreCase);

            string invalidAttrPattern = @"\s+([0-9\-])([^=\s]*)=";
            xmlContent = Regex.Replace(xmlContent, invalidAttrPattern, " _$1$2=", RegexOptions.IgnoreCase);

            return xmlContent;
        }

        /// <summary>
        /// Fix other common XML issues
        /// </summary>
        private string FixCommonXmlIssues(string xmlContent)
        {
            // Fix unescaped ampersands (but not already escaped ones)
            xmlContent = Regex.Replace(xmlContent, @"&(?![a-zA-Z0-9]+;)", "&amp;");

            // Fix unclosed CDATA sections
            xmlContent = Regex.Replace(xmlContent, @"<!\[CDATA\[([^\]]*)\](?!\]>)", "<![CDATA[$1]]>");

            // Remove or fix invalid characters in attribute values
            xmlContent = Regex.Replace(xmlContent, @"=""([^""]*[\x00-\x08\x0B\x0C\x0E-\x1F])([^""]*)""",
                match => "=\"" + SanitizeAttributeValue(match.Groups[1].Value + match.Groups[2].Value) + "\"");

            // Fix mismatched tags
            xmlContent = FixMismatchedTags(xmlContent);

            // Fix nested tags issues
            xmlContent = FixNestedTagsIssues(xmlContent);

            return xmlContent;
        }

        /// <summary>
        /// Fix mismatched opening and closing tags
        /// </summary>
        private string FixMismatchedTags(string xmlContent)
        {
            try
            {
                // Stack to track open tags
                var tagStack = new Stack<string>();
                var lines = xmlContent.Split('\n');
                var result = new StringBuilder(xmlContent.Length);

                foreach (string line in lines)
                {
                    string processedLine = FixLineTagIssues(line, tagStack);
                    result.AppendLine(processedLine);
                }

                // Close any remaining open tags
                while (tagStack.Count > 0)
                {
                    string unclosedTag = tagStack.Pop();
                    if (!IsSelfClosingTag(unclosedTag))
                    {
                        result.AppendLine($"</{unclosedTag}>");
                    }
                }

                return result.ToString();
            }
            catch
            {
                // If tag fixing fails, return original content
                return xmlContent;
            }
        }

        /// <summary>
        /// Fix tag issues in a single line
        /// </summary>
        private string FixLineTagIssues(string line, Stack<string> tagStack)
        {
            if (string.IsNullOrEmpty(line)) return line;

            var result = new StringBuilder(line.Length);
            int pos = 0;

            while (pos < line.Length)
            {
                int tagStart = line.IndexOf('<', pos);
                if (tagStart == -1)
                {
                    // No more tags, add remaining content
                    result.Append(line.Substring(pos));
                    break;
                }

                // Add content before tag
                result.Append(line.Substring(pos, tagStart - pos));

                int tagEnd = line.IndexOf('>', tagStart);
                if (tagEnd == -1)
                {
                    // Malformed tag, treat as text
                    result.Append(line.Substring(tagStart));
                    break;
                }

                string tagContent = line.Substring(tagStart + 1, tagEnd - tagStart - 1).Trim();

                if (string.IsNullOrEmpty(tagContent))
                {
                    // Empty tag, skip
                    pos = tagEnd + 1;
                    continue;
                }

                if (tagContent.StartsWith("/"))
                {
                    // Closing tag
                    string closingTagName = ExtractTagName(tagContent.Substring(1));
                    ProcessClosingTag(result, tagStack, closingTagName, tagStart, tagEnd, line);
                }
                else if (tagContent.StartsWith("!") || tagContent.StartsWith("?"))
                {
                    // Comment or processing instruction, keep as is
                    result.Append(line.Substring(tagStart, tagEnd - tagStart + 1));
                }
                else
                {
                    // Opening tag
                    string openingTagName = ExtractTagName(tagContent);
                    ProcessOpeningTag(result, tagStack, openingTagName, tagStart, tagEnd, line, tagContent);
                }

                pos = tagEnd + 1;
            }

            return result.ToString();
        }

        /// <summary>
        /// Process closing tag and handle mismatches
        /// </summary>
        private void ProcessClosingTag(StringBuilder result, Stack<string> tagStack, string closingTagName,
            int tagStart, int tagEnd, string line)
        {
            if (tagStack.Count > 0 && tagStack.Peek().Equals(closingTagName, StringComparison.OrdinalIgnoreCase))
            {
                // Perfect match
                tagStack.Pop();
                result.Append(line.Substring(tagStart, tagEnd - tagStart + 1));
            }
            else if (tagStack.Count > 0)
            {
                // Mismatch - try to find matching opening tag in stack
                var tempStack = new Stack<string>();
                bool found = false;

                while (tagStack.Count > 0)
                {
                    string currentTag = tagStack.Pop();
                    if (currentTag.Equals(closingTagName, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                    tempStack.Push(currentTag);
                }

                if (found)
                {
                    // Close intermediate tags that were left open
                    while (tempStack.Count > 0)
                    {
                        string tagToClose = tempStack.Pop();
                        if (!IsSelfClosingTag(tagToClose))
                        {
                            result.Append($"</{tagToClose}>");
                        }
                    }
                    result.Append(line.Substring(tagStart, tagEnd - tagStart + 1));
                }
                else
                {
                    // No matching opening tag found, restore stack and convert to text
                    while (tempStack.Count > 0)
                    {
                        tagStack.Push(tempStack.Pop());
                    }
                    result.Append(EscapeXmlContent(line.Substring(tagStart, tagEnd - tagStart + 1)));
                }
            }
            else
            {
                // No opening tags in stack, convert to text
                result.Append(EscapeXmlContent(line.Substring(tagStart, tagEnd - tagStart + 1)));
            }
        }

        /// <summary>
        /// Process opening tag
        /// </summary>
        private void ProcessOpeningTag(StringBuilder result, Stack<string> tagStack, string openingTagName,
            int tagStart, int tagEnd, string line, string tagContent)
        {
            if (IsValidXmlName(openingTagName))
            {
                result.Append(line.Substring(tagStart, tagEnd - tagStart + 1));

                if (!IsSelfClosingTag(openingTagName) && !tagContent.EndsWith("/"))
                {
                    tagStack.Push(openingTagName);
                }
            }
            else
            {
                // Invalid tag name, convert to text
                result.Append(EscapeXmlContent(line.Substring(tagStart, tagEnd - tagStart + 1)));
            }
        }

        /// <summary>
        /// Fix nested tags issues like overlapping tags
        /// </summary>
        private string FixNestedTagsIssues(string xmlContent)
        {
            // Fix common nested tag problems in FB2 files
            // Remove invalid nesting of block elements inside inline elements
            xmlContent = Regex.Replace(xmlContent, @"<(em|strong|i|b|u)([^>]*)>([^<]*)<(p|div)([^>]*)>",
                "<$1$2>$3</$1><$4$5>", RegexOptions.IgnoreCase);

            // Fix unclosed inline tags before block tags
            xmlContent = Regex.Replace(xmlContent, @"<(em|strong|i|b|u)([^>]*)>([^<]*)</(p|div)>",
                "<$1$2>$3</$1></$4>", RegexOptions.IgnoreCase);

            return xmlContent;
        }

        /// <summary>
        /// Extract tag name from tag content
        /// </summary>
        private string ExtractTagName(string tagContent)
        {
            if (string.IsNullOrEmpty(tagContent)) return string.Empty;

            int spaceIndex = tagContent.IndexOf(' ');
            if (spaceIndex > 0)
            {
                return tagContent.Substring(0, spaceIndex).Trim();
            }
            return tagContent.Trim();
        }

        /// <summary>
        /// Check if tag name is valid XML name
        /// </summary>
        private bool IsValidXmlName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            // XML name must start with letter, underscore, or colon
            char firstChar = name[0];
            if (!char.IsLetter(firstChar) && firstChar != '_' && firstChar != ':')
                return false;

            // Rest of characters must be letters, digits, hyphens, periods, underscores, or colons
            for (int i = 1; i < name.Length; i++)
            {
                char c = name[i];
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '.' && c != '_' && c != ':')
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Check if tag is self-closing
        /// </summary>
        private bool IsSelfClosingTag(string tagName)
        {
            if (string.IsNullOrEmpty(tagName)) return false;

            string[] selfClosingTags = { "img", "br", "hr", "input", "meta", "link", "area", "base", "col", "embed", "source", "track", "wbr" };
            return Array.Exists(selfClosingTags, t => t.Equals(tagName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Escape XML content to make it safe as text
        /// </summary>
        private string EscapeXmlContent(string content)
        {
            if (string.IsNullOrEmpty(content)) return content;

            return content
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        /// <summary>
        /// Sanitize attribute values
        /// </summary>
        private string SanitizeAttributeValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            StringBuilder result = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (IsLegalXmlChar(c) && c != '<' && c != '>')
                    result.Append(c);
                else
                    result.Append(' '); // Replace invalid chars with space
            }
            return result.ToString();
        }

        /// <summary>
        /// Fill book object from FB2 data
        /// </summary>
        private void FillBookFromFB2(Book book, FB2File fb2)
        {
            if (fb2.DocumentInfo != null)
            {
                book.ID = fb2.DocumentInfo.ID;
                if (fb2.DocumentInfo.DocumentVersion != null)
                    book.Version = (float)fb2.DocumentInfo.DocumentVersion;
                if (fb2.DocumentInfo.DocumentDate != null)
                    book.DocumentDate = fb2.DocumentInfo.DocumentDate.DateValue;
            }

            if (fb2.TitleInfo != null)
            {
                if (fb2.TitleInfo.BookTitle != null)
                    book.Title = fb2.TitleInfo.BookTitle.Text;
                if (fb2.TitleInfo.Annotation != null)
                    book.Annotation = fb2.TitleInfo.Annotation.ToString();

                if (fb2.TitleInfo.Sequences != null && fb2.TitleInfo.Sequences.Count > 0)
                {
                    book.Sequence = fb2.TitleInfo.Sequences.First().Name.Capitalize(true);
                    if (fb2.TitleInfo.Sequences.First().Number != null)
                    {
                        book.NumberInSequence = (UInt32)(fb2.TitleInfo.Sequences.First().Number);
                    }
                }

                if (fb2.TitleInfo.Language != null)
                    book.Language = fb2.TitleInfo.Language;
                if (fb2.TitleInfo.BookDate != null)
                    book.BookDate = fb2.TitleInfo.BookDate.DateValue;

                if (fb2.TitleInfo.BookAuthors != null && fb2.TitleInfo.BookAuthors.Any())
                {
                    book.Authors = new List<string>();
                    book.Authors.AddRange(from ba in fb2.TitleInfo.BookAuthors
                                          select string.Concat(ba.LastName, " ", ba.FirstName, " ", ba.MiddleName)
                                          .Replace("  ", " ").Capitalize());
                }

                if (fb2.TitleInfo.Translators != null && fb2.TitleInfo.Translators.Any())
                {
                    book.Translators = new List<string>();
                    book.Translators.AddRange(from ba in fb2.TitleInfo.Translators
                                              select string.Concat(ba.LastName, " ", ba.FirstName, " ", ba.MiddleName)
                                              .Replace("  ", " ").Capitalize());
                }

                if (fb2.TitleInfo.Genres != null && fb2.TitleInfo.Genres.Any())
                {
                    book.Genres = new List<string>();
                    book.Genres.AddRange((from g in fb2.TitleInfo.Genres select g.Genre).ToList());
                }
            }
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
                stream.Position = 0;

                xml = LoadXmlFromStream(stream, fileName);
                if (xml == null) return null;

                fb2.Load(xml, false);

                if (fb2.TitleInfo?.Cover?.HasImages() == true && fb2.Images.Count > 0)
                {
                    string coverHRef = fb2.TitleInfo.Cover.CoverpageImages.First().HRef.Substring(1);
                    var binaryObject = fb2.Images.FirstOrDefault(item => item.Value.Id == coverHRef);

                    if (binaryObject.Value?.BinaryData != null && binaryObject.Value.BinaryData.Length > 0)
                    {
                        using (MemoryStream memStream = new MemoryStream(binaryObject.Value.BinaryData))
                        {
                            image = Image.FromStream(memStream);

                            ImageFormat fmt = binaryObject.Value.ContentType == ContentTypeEnum.ContentTypePng ?
                                ImageFormat.Png : ImageFormat.Gif;

                            if (binaryObject.Value.ContentType != ContentTypeEnum.ContentTypeJpeg)
                            {
                                using (var tempImage = image)
                                {
                                    image = Image.FromStream(tempImage.ToStream(fmt));
                                }
                            }
                            image = image.Resize(CoverImage.CoverSize);
                        }
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
            if (string.IsNullOrEmpty(xml)) return xml;

            StringBuilder buffer = new StringBuilder(xml.Length);
            foreach (char c in xml)
            {
                if (IsLegalXmlChar(c))
                    buffer.Append(c);
            }
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