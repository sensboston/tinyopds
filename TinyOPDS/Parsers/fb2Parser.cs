/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * FB2 parser implementation using MindTouch SGMLReader
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
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
                FB2File fb2 = new FB2File();

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

                    if (fb2.DocumentInfo != null)
                    {
                        book.ID = Guid.NewGuid().ToString();
                        book.DocumentIDTrusted = false;

                        if (fb2.DocumentInfo.DocumentVersion != null)
                            book.Version = (float)fb2.DocumentInfo.DocumentVersion;

                        if (fb2.DocumentInfo.DocumentDate != null)
                        {
                            book.DocumentDate = DateParser.ParseFB2Date(fb2.DocumentInfo.DocumentDate, fileName);
                        }
                        else
                        {
                            book.DocumentDate = DateParser.GetFileDate(fileName);
                        }
                    }
                    else
                    {
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

                        if (fb2.TitleInfo.BookDate != null)
                        {
                            book.BookDate = DateParser.ParseFB2Date(fb2.TitleInfo.BookDate, fileName);
                        }
                        else
                        {
                            book.BookDate = DateParser.GetFileDate(fileName);
                        }

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

        private string BuildAuthorName(string firstName, string middleName, string lastName)
        {
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

            return string.Join(" ", nameParts);
        }

        private XDocument ParseNonSeekableStream(Stream stream, string fileName)
        {
            try
            {
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

        private XDocument ParseSeekableStream(Stream stream, string fileName)
        {
            XDocument xml = null;

            try
            {
                stream.Position = 0;

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

        private XDocument TryParseBySgml(Stream stream, string fileName)
        {
            try
            {
                using (SgmlReader sgmlReader = new SgmlReader())
                {
                    sgmlReader.CaseFolding = CaseFolding.None;
                    sgmlReader.WhitespaceHandling = WhitespaceHandling.All;
                    sgmlReader.StripDocType = true;

                    StreamReader streamReader = null;
                    try
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        stream.Position = 0;

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
                                        detectedEncoding = Encoding.UTF8;
                                    }
                                }
                            }
                        }

                        streamReader = new StreamReader(stream, detectedEncoding, false, 1024, true);
                    }
                    catch
                    {
                        stream.Position = 0;
                        streamReader = new StreamReader(stream, Encoding.UTF8, true, 1024, true);
                    }

                    using (streamReader)
                    {
                        sgmlReader.InputStream = streamReader;
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

        public override Image GetCoverImage(Stream stream, string fileName)
        {
            Image image = null;
            try
            {
                FB2File fb2 = new FB2File();

                Stream workingStream = stream;
                bool needsDisposal = false;

                if (!stream.CanSeek)
                {
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
    }
}