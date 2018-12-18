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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Drawing;
using System.Drawing.Imaging;

using FB2Library;
using FB2Library.HeaderItems;
using FB2Library.Elements;
using TinyOPDS.Data;

namespace TinyOPDS.Parsers
{
    public class FB2Parser : BookParser
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override Book Parse(Stream stream, string fileName)
        {
            XDocument xml = null;
            Book book = new Book(fileName);
            book.DocumentSize = (UInt32)stream.Length;

            try
            {
                FB2File fb2 = new FB2File();
                // Load header only
                stream.Position = 0;

                // Project Mono has a bug: Xdocument.Load() can't detect encoding
                string encoding = string.Empty;
                if (Utils.IsLinux)
                {
                    using (StreamReader sr = new StreamReader(stream))
                    {
                        encoding = sr.ReadLine();
                        int idx = encoding.ToLower().IndexOf("encoding=\"");
                        if (idx > 0)
                        {
                            encoding = encoding.Substring(idx + 10);
                            encoding = encoding.Substring(0, encoding.IndexOf('"'));
                            stream.Position = 0;
                            using (StreamReader esr = new StreamReader(stream, Encoding.GetEncoding(encoding)))
                            {
                                string xmlStr = esr.ReadToEnd();
                                try
                                {
                                    xml = XDocument.Parse(xmlStr, LoadOptions.PreserveWhitespace);
                                }
                                catch (Exception e)
                                {
                                    if (e.Message.IndexOf("nbsp", StringComparison.InvariantCultureIgnoreCase) > 0)
                                    {
                                        xmlStr = xmlStr.Replace("&nbsp;", "&#160;").Replace("&NBSP;", "&#160;");
                                        xml = XDocument.Parse(xmlStr, LoadOptions.PreserveWhitespace);
                                    }
                                    else if (e.Message.IndexOf("is an invalid character", StringComparison.InvariantCultureIgnoreCase) > 0)
                                    {
                                        xml = XDocument.Parse(this.SanitizeXmlString(xmlStr), LoadOptions.PreserveWhitespace);
                                    }
                                    else throw e;
                                }
                            }
                        }
                    }
                }

                if (xml == null)
                {
                    try
                    {
                        xml = XDocument.Load(stream);
                    }
                    // This code will try to improve xml loading
                    catch (Exception e)
                    {
                        stream.Position = 0;
                        if (e.Message.IndexOf("nbsp", StringComparison.InvariantCultureIgnoreCase) > 0)
                        {
                            using (StreamReader sr = new StreamReader(stream))
                            {
                                string xmlStr = sr.ReadToEnd();
                                xmlStr = xmlStr.Replace("&nbsp;", "&#160;").Replace("&NBSP;", "&#160;");
                                xml = XDocument.Parse(xmlStr, LoadOptions.PreserveWhitespace);
                            }
                        }
                        else if (e.Message.IndexOf("is an invalid character", StringComparison.InvariantCultureIgnoreCase) > 0)
                        {
                            using (StreamReader sr = new StreamReader(stream))
                            {
                                string xmlStr = sr.ReadToEnd();
                                xml = XDocument.Parse(this.SanitizeXmlString(xmlStr), LoadOptions.PreserveWhitespace);
                            }
                        }
                        else throw e;
                    }
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
                        if (fb2.TitleInfo.Cover != null && fb2.TitleInfo.Cover.HasImages()) book.HasCover = true;
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
                        if (fb2.TitleInfo.BookAuthors != null && fb2.TitleInfo.BookAuthors.Count() > 0)
                        {
                            book.Authors = new List<string>();
                            book.Authors.AddRange(from ba in fb2.TitleInfo.BookAuthors select string.Concat(ba.LastName, " ", ba.FirstName, " ", ba.MiddleName).Replace("  ", " ").Capitalize());
                        }
                        if (fb2.TitleInfo.Translators != null && fb2.TitleInfo.Translators.Count() > 0)
                        {
                            book.Translators = new List<string>();
                            book.Translators.AddRange(from ba in fb2.TitleInfo.Translators select string.Concat(ba.LastName, " ", ba.FirstName, " ", ba.MiddleName).Replace("  ", " ").Capitalize());
                        }
                        if (fb2.TitleInfo.Genres != null && fb2.TitleInfo.Genres.Count() > 0)
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
            finally
            {
                // Dispose xml document
                xml = null;
            }

            return book;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override Image GetCoverImage(Stream stream, string fileName)
        {
            Image image = null;
            XDocument xml = null;
            try
            {
                FB2File fb2 = new FB2File();
                stream.Position = 0;
                xml = XDocument.Load(stream);
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
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Book.GetCoverImage() exception {0} on file: {1}", e.Message, fileName);
            }
            // Dispose xml document
            xml = null;
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
