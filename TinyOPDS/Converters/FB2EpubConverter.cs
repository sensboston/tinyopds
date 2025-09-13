/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * FB2 to EPUB converter using EpubSharp
 *
 */

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml.Linq;

using EpubSharp;
using FB2Library;
using FB2Library.Elements;
using TinyOPDS.Data;

namespace TinyOPDS
{
    public class FB2EpubConverter
    {
        /// <summary>
        /// Convert FB2 stream to EPUB stream in memory
        /// </summary>
        /// <param name="book">Book metadata</param>
        /// <param name="fb2Stream">Input FB2 stream</param>
        /// <param name="epubStream">Output EPUB stream</param>
        /// <returns>True if successful</returns>
        public bool ConvertToEpubStream(Book book, Stream fb2Stream, Stream epubStream)
        {
            try
            {
                var fb2File = ParseFB2Stream(fb2Stream);
                if (fb2File == null)
                {
                    Log.WriteLine(LogLevel.Error, "Failed to parse FB2 content for {0}", book.FileName);
                    return false;
                }

                var writer = CreateEpubWriter(book, fb2File);
                writer.Write(epubStream);

                Log.WriteLine(LogLevel.Info, "Successfully converted {0} to EPUB in memory", book.FileName);
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error converting {0} to EPUB: {1}", book.FileName, ex.Message);
                return false;
            }
        }

        private FB2File ParseFB2Stream(Stream stream)
        {
            try
            {
                if (!stream.CanSeek)
                {
                    using (var memStream = new MemoryStream())
                    {
                        stream.CopyTo(memStream);
                        memStream.Position = 0;
                        return LoadFB2FromStream(memStream);
                    }
                }
                else
                {
                    stream.Position = 0;
                    return LoadFB2FromStream(stream);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error parsing FB2 stream: {0}", ex.Message);
                return null;
            }
        }

        private FB2File LoadFB2FromStream(Stream stream)
        {
            var fb2File = new FB2File();
            var xml = XDocument.Load(stream);
            fb2File.Load(xml, false);
            return fb2File;
        }

        private EpubWriter CreateEpubWriter(Book book, FB2File fb2File)
        {
            var writer = new EpubWriter();

            SetMetadata(writer, book);
            AddCover(writer, fb2File);
            AddContent(writer, fb2File, book);

            return writer;
        }

        private void SetMetadata(EpubWriter writer, Book book)
        {
            writer.SetTitle(!string.IsNullOrEmpty(book.Title) ? book.Title : "Unknown Title");

            if (book.Authors != null && book.Authors.Any())
            {
                foreach (var author in book.Authors)
                {
                    writer.AddAuthor(author);
                }
            }
            else
            {
                writer.AddAuthor("Unknown Author");
            }
        }

        private void AddCover(EpubWriter writer, FB2File fb2File)
        {
            try
            {
                if (fb2File.TitleInfo?.Cover?.HasImages() == true && fb2File.Images.Count > 0)
                {
                    string coverHRef = fb2File.TitleInfo.Cover.CoverpageImages.First().HRef.Substring(1);
                    var binaryObject = fb2File.Images.FirstOrDefault(item => item.Value.Id == coverHRef);

                    if (binaryObject.Value?.BinaryData != null && binaryObject.Value.BinaryData.Length > 0)
                    {
                        EpubSharp.ImageFormat imageFormat = GetImageFormat(binaryObject.Value.ContentType.ToString());
                        writer.SetCover(binaryObject.Value.BinaryData, imageFormat);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error adding cover: {0}", ex.Message);
            }
        }

        private void AddContent(EpubWriter writer, FB2File fb2File, Book book)
        {
            try
            {
                if (fb2File.Bodies?.Any() == true)
                {
                    var body = fb2File.Bodies.First();
                    if (body.Sections?.Any() == true)
                    {
                        for (int i = 0; i < body.Sections.Count; i++)
                        {
                            var section = body.Sections[i];
                            string title = ExtractSectionTitle(section, i + 1);
                            string content = ConvertSectionToHtml(section);

                            writer.AddChapter(title, content);
                        }
                    }
                    else
                    {
                        string content = ConvertBodyToHtml(body);
                        writer.AddChapter(!string.IsNullOrEmpty(book.Title) ? book.Title : "Content", content);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error adding content: {0}", ex.Message);
            }
        }

        private string ExtractSectionTitle(SectionItem section, int chapterNum)
        {
            try
            {
                if (section.Title?.TitleData?.Any() == true)
                {
                    return string.Join(" ", section.Title.TitleData.Select(t => t.ToString()));
                }
            }
            catch { }

            return $"Chapter {chapterNum}";
        }

        private string ConvertSectionToHtml(SectionItem section)
        {
            var html = new StringBuilder();

            try
            {
                // Use reflection to check if Elements exists as method or property
                try
                {
                    var elementsMethod = section.GetType().GetMethod("Elements");
                    if (elementsMethod != null)
                    {
                        var elements = elementsMethod.Invoke(section, null) as System.Collections.IEnumerable;
                        if (elements != null)
                        {
                            foreach (var element in elements)
                            {
                                html.AppendLine($"<p>{EscapeHtml(element.ToString())}</p>");
                            }
                        }
                    }
                    else
                    {
                        // Try as property
                        var elementsProperty = section.GetType().GetProperty("Elements");
                        if (elementsProperty != null)
                        {
                            var elements = elementsProperty.GetValue(section) as System.Collections.IEnumerable;
                            if (elements != null)
                            {
                                foreach (var element in elements)
                                {
                                    html.AppendLine($"<p>{EscapeHtml(element.ToString())}</p>");
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // If Elements doesn't exist, try other approaches
                    html.AppendLine($"<p>{EscapeHtml(section.ToString())}</p>");
                }

                if (section.SubSections?.Any() == true)
                {
                    foreach (var subSection in section.SubSections)
                    {
                        html.AppendLine($"<h3>{EscapeHtml(ExtractSectionTitle(subSection, 0))}</h3>");
                        html.AppendLine(ConvertSectionToHtml(subSection));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error converting section: {0}", ex.Message);
            }

            return html.ToString();
        }

        private string ConvertBodyToHtml(BodyItem body)
        {
            var html = new StringBuilder();

            try
            {
                if (body.Sections?.Any() == true)
                {
                    foreach (var section in body.Sections)
                    {
                        html.AppendLine(ConvertSectionToHtml(section));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error converting body: {0}", ex.Message);
            }

            return html.ToString();
        }

        private string EscapeHtml(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private EpubSharp.ImageFormat GetImageFormat(string contentType)
        {
            if (string.IsNullOrEmpty(contentType)) return ImageFormat.Jpeg;
            contentType = contentType.ToLower();
            if (contentType.Contains("png")) return ImageFormat.Png;
            if (contentType.Contains("gif")) return ImageFormat.Gif;
            return ImageFormat.Jpeg;
        }
    }
}