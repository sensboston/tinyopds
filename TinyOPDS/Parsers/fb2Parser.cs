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
using System.Xml;
using System.Xml.Linq;
using System.Drawing;
using System.Drawing.Imaging;

using FB2Library;
using FB2Library.HeaderItems;
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
        /// Parse FB2 book from stream - supports both seekable and non-seekable streams
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

                    if (fb2.DocumentInfo != null)
                    {
                        book.ID = fb2.DocumentInfo.ID;
                        if (fb2.DocumentInfo.DocumentVersion != null) book.Version = (float)fb2.DocumentInfo.DocumentVersion;
                        if (fb2.DocumentInfo.DocumentDate != null) book.DocumentDate = fb2.DocumentInfo.DocumentDate.DateValue;
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
                        if (fb2.TitleInfo.BookDate != null) book.BookDate = fb2.TitleInfo.BookDate.DateValue;
                        if (fb2.TitleInfo.BookAuthors != null && fb2.TitleInfo.BookAuthors.Any())
                        {
                            book.Authors = new List<string>();
                            book.Authors.AddRange(from ba in fb2.TitleInfo.BookAuthors select string.Concat(ba.LastName, " ", ba.FirstName, " ", ba.MiddleName).Replace("  ", " ").Capitalize());
                        }
                        if (fb2.TitleInfo.Translators != null && fb2.TitleInfo.Translators.Any())
                        {
                            book.Translators = new List<string>();
                            book.Translators.AddRange(from ba in fb2.TitleInfo.Translators select string.Concat(ba.LastName, " ", ba.FirstName, " ", ba.MiddleName).Replace("  ", " ").Capitalize());
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
            // Note: Don't dispose the stream here - it's managed by the caller

            return book;
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

                    // Update document size if we have it now
                    if (memoryStream.Length > 0)
                    {
                        // This is a rough estimate since we loaded into memory
                    }

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
                                    xml = TryParseBySgml(stream);
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
                        xml = TryParseBySgml(stream);
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
        /// </summary>
        private XDocument TryParseBySgml(Stream stream)
        {
            try
            {
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
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get cover image from FB2 file
        /// </summary>
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