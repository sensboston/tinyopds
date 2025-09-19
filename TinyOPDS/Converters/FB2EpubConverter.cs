/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Native FB2 to EPUB converter - creates valid EPUB 3.0 without external libraries
 *
 */

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;

using TinyOPDS.Data;

namespace TinyOPDS
{
    public class FB2EpubConverter
    {
        private XDocument fb2Xml = null;
        private XNamespace fb2Ns;
        private readonly XNamespace opfNs = "http://www.idpf.org/2007/opf";
        private readonly XNamespace dcNs = "http://purl.org/dc/elements/1.1/";

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
                // Parse FB2 XML
                if (!LoadFB2Xml(fb2Stream))
                {
                    Log.WriteLine(LogLevel.Error, "Failed to parse FB2 content for {0}", book.FileName);
                    return false;
                }

                // Create EPUB
                CreateEpubArchive(epubStream, book);

                Log.WriteLine(LogLevel.Info, "Successfully converted {0} to EPUB", book.FileName);
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error converting {0} to EPUB: {1}", book.FileName, ex.Message);
                return false;
            }
        }

        private bool LoadFB2Xml(Stream stream)
        {
            try
            {
                if (!stream.CanSeek)
                {
                    using (var memStream = new MemoryStream())
                    {
                        stream.CopyTo(memStream);
                        memStream.Position = 0;
                        fb2Xml = XDocument.Load(memStream);
                    }
                }
                else
                {
                    stream.Position = 0;
                    fb2Xml = XDocument.Load(stream);
                }

                // Get FB2 namespace
                fb2Ns = fb2Xml.Root?.Name.Namespace ?? XNamespace.None;
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error loading FB2 XML: {0}", ex.Message);
                return false;
            }
        }

        private void CreateEpubArchive(Stream outputStream, Book book)
        {
            // Create ZIP manually with proper mimetype handling
            using (var tempStream = new MemoryStream())
            {
                // Write raw ZIP structure for mimetype (uncompressed)
                WriteRawMimetype(tempStream);

                // Now add other files using ZipArchive
                using (var archive = new ZipArchive(tempStream, ZipArchiveMode.Update, true))
                {
                    // Add container.xml
                    AddContainer(archive);

                    // Extract chapters and images
                    var chapters = ExtractChapters();
                    var images = ExtractImages();

                    // Add package.opf
                    AddPackage(archive, book, chapters, images);

                    // Add navigation
                    AddNavigation(archive, chapters);

                    // Add content chapters
                    foreach (var chapter in chapters)
                    {
                        AddChapterFile(archive, chapter);
                    }

                    // Add images
                    foreach (var image in images)
                    {
                        AddImageFile(archive, image);
                    }

                    // Add cover if exists
                    AddCoverImage(archive);
                }

                // Copy to output stream
                tempStream.Position = 0;
                tempStream.CopyTo(outputStream);
            }
        }

        private void WriteRawMimetype(Stream stream)
        {
            var writer = new BinaryWriter(stream);

            // Local file header for "mimetype"
            writer.Write(0x04034b50);        // Local file header signature
            writer.Write((ushort)0x000a);    // Version needed to extract (1.0)
            writer.Write((ushort)0x0000);    // General purpose bit flag
            writer.Write((ushort)0x0000);    // Compression method (0 = stored)
            writer.Write((ushort)0x0000);    // File last modification time
            writer.Write((ushort)0x0000);    // File last modification date
            writer.Write(0x2cab616f);        // CRC-32 of "application/epub+zip"
            writer.Write(0x00000014);        // Compressed size (20 bytes)
            writer.Write(0x00000014);        // Uncompressed size (20 bytes)
            writer.Write((ushort)0x0008);    // File name length (8 bytes)
            writer.Write((ushort)0x0000);    // Extra field length (0 bytes)

            // File name
            writer.Write(Encoding.ASCII.GetBytes("mimetype"));

            // File data
            writer.Write(Encoding.ASCII.GetBytes("application/epub+zip"));

            // Now write Central Directory structure
            long centralDirOffset = stream.Position;

            // Central directory file header
            writer.Write(0x02014b50);        // Central file header signature
            writer.Write((ushort)0x0000);    // Version made by
            writer.Write((ushort)0x000a);    // Version needed to extract
            writer.Write((ushort)0x0000);    // General purpose bit flag
            writer.Write((ushort)0x0000);    // Compression method
            writer.Write((ushort)0x0000);    // File last modification time
            writer.Write((ushort)0x0000);    // File last modification date
            writer.Write(0x2cab616f);        // CRC-32
            writer.Write(0x00000014);        // Compressed size
            writer.Write(0x00000014);        // Uncompressed size
            writer.Write((ushort)0x0008);    // File name length
            writer.Write((ushort)0x0000);    // Extra field length
            writer.Write((ushort)0x0000);    // File comment length
            writer.Write((ushort)0x0000);    // Disk number start
            writer.Write((ushort)0x0000);    // Internal file attributes
            writer.Write(0x00000000);        // External file attributes
            writer.Write(0x00000000);        // Relative offset of local header

            // File name in central directory
            writer.Write(Encoding.ASCII.GetBytes("mimetype"));

            // End of central directory record
            long endCentralDirOffset = stream.Position;
            writer.Write(0x06054b50);        // End of central dir signature
            writer.Write((ushort)0x0000);    // Number of this disk
            writer.Write((ushort)0x0000);    // Number of disk with start of central dir
            writer.Write((ushort)0x0001);    // Total number of entries in central dir on this disk
            writer.Write((ushort)0x0001);    // Total number of entries in central dir
            writer.Write((int)(endCentralDirOffset - centralDirOffset)); // Size of central directory
            writer.Write((int)centralDirOffset); // Offset of start of central directory
            writer.Write((ushort)0x0000);    // ZIP file comment length

            writer.Flush();
        }

        private void AddContainer(ZipArchive archive)
        {
            var entry = archive.CreateEntry("META-INF/container.xml");
            using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
            {
                writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<container version=""1.0"" xmlns=""urn:oasis:names:tc:opendocument:xmlns:container"">
  <rootfiles>
    <rootfile full-path=""EPUB/package.opf"" media-type=""application/oebps-package+xml""/>
  </rootfiles>
</container>");
            }
        }

        private void AddPackage(ZipArchive archive, Book book, List<ChapterInfo> chapters, List<ImageInfo> images)
        {
            var entry = archive.CreateEntry("EPUB/package.opf");
            using (var stream = entry.Open())
            {
                var package = new XDocument(
                    new XDeclaration("1.0", "utf-8", null),
                    new XElement(opfNs + "package",
                        new XAttribute("version", "3.0"),
                        new XAttribute("unique-identifier", "book-id"),
                        new XAttribute(XNamespace.Xml + "lang", GetLanguage(book)),
                        CreateMetadata(book),
                        CreateManifest(chapters, images),
                        CreateSpine(chapters)
                    )
                );

                package.Save(stream);
            }
        }

        private XElement CreateMetadata(Book book)
        {
            var metadata = new XElement(opfNs + "metadata",
                new XAttribute(XNamespace.Xmlns + "dc", dcNs));

            // Identifier (required)
            string bookId = !string.IsNullOrEmpty(book.ID) ? book.ID : Guid.NewGuid().ToString();
            metadata.Add(new XElement(dcNs + "identifier",
                new XAttribute("id", "book-id"),
                "urn:uuid:" + bookId));

            // Title (required)
            string title = GetTitle(book);
            metadata.Add(new XElement(dcNs + "title", title));

            // Language (required)
            metadata.Add(new XElement(dcNs + "language", GetLanguage(book)));

            // Modified date (required for EPUB 3.0)
            metadata.Add(new XElement(opfNs + "meta",
                new XAttribute("property", "dcterms:modified"),
                DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")));

            // Authors
            AddAuthors(metadata, book);

            // Publication date
            if (book.BookDate != DateTime.MinValue && book.BookDate.Year > 1)
            {
                metadata.Add(new XElement(dcNs + "date", book.BookDate.Year.ToString()));
            }

            return metadata;
        }

        private void AddAuthors(XElement metadata, Book book)
        {
            // Try book authors first
            if (book.Authors != null && book.Authors.Any())
            {
                foreach (var author in book.Authors)
                {
                    metadata.Add(new XElement(dcNs + "creator", author));
                }
                return;
            }

            // Try to get from FB2 XML
            var titleInfo = fb2Xml?.Root?.Element(fb2Ns + "description")?.Element(fb2Ns + "title-info");
            if (titleInfo != null)
            {
                var authors = titleInfo.Elements(fb2Ns + "author");
                foreach (var author in authors)
                {
                    string name = ExtractAuthorName(author);
                    if (!string.IsNullOrEmpty(name))
                        metadata.Add(new XElement(dcNs + "creator", name));
                }
            }

            // Fallback to unknown
            if (!metadata.Elements(dcNs + "creator").Any())
            {
                metadata.Add(new XElement(dcNs + "creator", "Unknown Author"));
            }
        }

        private string ExtractAuthorName(XElement author)
        {
            var parts = new List<string>();

            var lastName = author.Element(fb2Ns + "last-name")?.Value;
            var firstName = author.Element(fb2Ns + "first-name")?.Value;
            var middleName = author.Element(fb2Ns + "middle-name")?.Value;
            var nickName = author.Element(fb2Ns + "nickname")?.Value;

            if (!string.IsNullOrEmpty(lastName))
                parts.Add(lastName);
            if (!string.IsNullOrEmpty(firstName))
                parts.Add(firstName);
            if (!string.IsNullOrEmpty(middleName))
                parts.Add(middleName);

            if (parts.Count == 0 && !string.IsNullOrEmpty(nickName))
                parts.Add(nickName);

            return string.Join(" ", parts);
        }

        private XElement CreateManifest(List<ChapterInfo> chapters, List<ImageInfo> images)
        {
            var manifest = new XElement(opfNs + "manifest");

            // Navigation
            manifest.Add(new XElement(opfNs + "item",
                new XAttribute("id", "nav"),
                new XAttribute("href", "nav.xhtml"),
                new XAttribute("media-type", "application/xhtml+xml"),
                new XAttribute("properties", "nav")));

            // Cover
            if (HasCover())
            {
                manifest.Add(new XElement(opfNs + "item",
                    new XAttribute("id", "cover-image"),
                    new XAttribute("href", "cover.jpg"),
                    new XAttribute("media-type", "image/jpeg"),
                    new XAttribute("properties", "cover-image")));
            }

            // Chapters
            for (int i = 0; i < chapters.Count; i++)
            {
                manifest.Add(new XElement(opfNs + "item",
                    new XAttribute("id", $"chapter{i + 1}"),
                    new XAttribute("href", $"chapter{i + 1}.xhtml"),
                    new XAttribute("media-type", "application/xhtml+xml")));
            }

            // Images
            foreach (var image in images)
            {
                manifest.Add(new XElement(opfNs + "item",
                    new XAttribute("id", "img-" + image.Id),
                    new XAttribute("href", image.FileName),
                    new XAttribute("media-type", image.MimeType)));
            }

            return manifest;
        }

        private XElement CreateSpine(List<ChapterInfo> chapters)
        {
            var spine = new XElement(opfNs + "spine");

            for (int i = 0; i < chapters.Count; i++)
            {
                spine.Add(new XElement(opfNs + "itemref",
                    new XAttribute("idref", $"chapter{i + 1}")));
            }

            return spine;
        }

        private void AddNavigation(ZipArchive archive, List<ChapterInfo> chapters)
        {
            var entry = archive.CreateEntry("EPUB/nav.xhtml");
            using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
            {
                writer.WriteLine(@"<?xml version=""1.0"" encoding=""utf-8""?>
<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"" xmlns:epub=""http://www.idpf.org/2007/ops"">
<head>
  <title>Navigation</title>
</head>
<body>
  <nav epub:type=""toc"">
    <h1>Table of Contents</h1>
    <ol>");

                for (int i = 0; i < chapters.Count; i++)
                {
                    writer.WriteLine($"      <li><a href=\"chapter{i + 1}.xhtml\">{EscapeXml(chapters[i].Title)}</a></li>");
                }

                writer.WriteLine(@"    </ol>
  </nav>
</body>
</html>");
            }
        }

        private void AddChapterFile(ZipArchive archive, ChapterInfo chapter)
        {
            var entry = archive.CreateEntry($"EPUB/{chapter.FileName}");
            using (var writer = new StreamWriter(entry.Open(), Encoding.UTF8))
            {
                writer.Write(@"<?xml version=""1.0"" encoding=""utf-8""?>
<!DOCTYPE html>
<html xmlns=""http://www.w3.org/1999/xhtml"">
<head>
  <title>" + EscapeXml(chapter.Title) + @"</title>
  <style>
    body { font-family: serif; margin: 1em; }
    p { text-indent: 1.5em; margin: 0.5em 0; text-align: justify; }
    h1, h2, h3 { text-align: center; }
    .poem { margin: 1em 0; padding-left: 2em; }
    .stanza { margin: 1em 0; }
    .verse { margin: 0; padding-left: 1em; }
    .epigraph { font-style: italic; margin: 2em; }
    blockquote { margin: 1em 2em; padding-left: 1em; border-left: 3px solid #ccc; }
  </style>
</head>
<body>
");
                writer.Write(chapter.Content);
                writer.Write(@"
</body>
</html>");
            }
        }

        private void AddImageFile(ZipArchive archive, ImageInfo image)
        {
            var entry = archive.CreateEntry($"EPUB/{image.FileName}");
            using (var stream = entry.Open())
            {
                stream.Write(image.Data, 0, image.Data.Length);
            }
        }

        private void AddCoverImage(ZipArchive archive)
        {
            if (!HasCover()) return;

            try
            {
                var coverData = ExtractCoverData();
                if (coverData != null && coverData.Length > 0)
                {
                    var entry = archive.CreateEntry("EPUB/cover.jpg");
                    using (var stream = entry.Open())
                    {
                        stream.Write(coverData, 0, coverData.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error adding cover: {0}", ex.Message);
            }
        }

        private List<ChapterInfo> ExtractChapters()
        {
            var chapters = new List<ChapterInfo>();

            if (fb2Xml == null) return chapters;

            var bodies = fb2Xml.Descendants(fb2Ns + "body").ToList();
            if (!bodies.Any()) return chapters;

            var mainBody = bodies.FirstOrDefault(b =>
                b.Attribute("name") == null ||
                b.Attribute("name").Value == "main" ||
                b.Attribute("name").Value == "") ?? bodies.First();

            var sections = mainBody.Elements(fb2Ns + "section").ToList();

            if (sections.Any())
            {
                int chapterNum = 1;
                foreach (var section in sections)
                {
                    ProcessFB2Section(chapters, section, ref chapterNum);
                }
            }
            else
            {
                // Single chapter from body
                chapters.Add(new ChapterInfo
                {
                    Title = "Content",
                    FileName = "chapter1.xhtml",
                    Content = ConvertToHtml(mainBody)
                });
            }

            return chapters;
        }

        private void ProcessFB2Section(List<ChapterInfo> chapters, XElement section, ref int chapterNum)
        {
            string title = ExtractTitle(section);
            if (string.IsNullOrEmpty(title))
                title = $"Chapter {chapterNum}";

            var subsections = section.Elements(fb2Ns + "section").ToList();

            if (!subsections.Any())
            {
                // Leaf section - add as chapter
                string content = ConvertToHtml(section);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    chapters.Add(new ChapterInfo
                    {
                        Title = title,
                        FileName = $"chapter{chapterNum}.xhtml",
                        Content = content
                    });
                    chapterNum++;
                }
            }
            else
            {
                // Process subsections recursively
                foreach (var subsection in subsections)
                {
                    ProcessFB2Section(chapters, subsection, ref chapterNum);
                }
            }
        }

        private List<ImageInfo> ExtractImages()
        {
            var images = new List<ImageInfo>();

            if (fb2Xml == null) return images;

            var binaries = fb2Xml.Root?.Elements(fb2Ns + "binary");
            if (binaries == null) return images;

            foreach (var binary in binaries)
            {
                var id = binary.Attribute("id")?.Value;
                var contentType = binary.Attribute("content-type")?.Value ?? "image/jpeg";

                if (!string.IsNullOrEmpty(id))
                {
                    try
                    {
                        byte[] data = Convert.FromBase64String(binary.Value);
                        if (data.Length > 0)
                        {
                            images.Add(new ImageInfo
                            {
                                Id = id,
                                FileName = $"{id}.{GetImageExtension(contentType)}",
                                MimeType = GetImageMimeType(contentType),
                                Data = data
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.WriteLine(LogLevel.Warning, "Error extracting image {0}: {1}", id, ex.Message);
                    }
                }
            }

            return images;
        }

        private string ExtractTitle(XElement element)
        {
            var titleElement = element.Element(fb2Ns + "title");
            if (titleElement == null) return "";

            var sb = new StringBuilder();
            foreach (var child in titleElement.Elements())
            {
                string text = GetText(child);
                if (!string.IsNullOrEmpty(text))
                {
                    if (sb.Length > 0) sb.Append(" ");
                    sb.Append(text);
                }
            }

            return sb.ToString().Trim();
        }

        private string ConvertToHtml(XElement element)
        {
            var html = new StringBuilder();

            foreach (var child in element.Elements())
            {
                string tagName = child.Name.LocalName.ToLower();

                switch (tagName)
                {
                    case "title":
                        html.AppendLine($"<h2>{GetText(child)}</h2>");
                        break;

                    case "subtitle":
                        html.AppendLine($"<h3>{GetText(child)}</h3>");
                        break;

                    case "p":
                        html.AppendLine($"<p>{ProcessInline(child)}</p>");
                        break;

                    case "empty-line":
                        html.AppendLine("<br/>");
                        break;

                    case "poem":
                        html.AppendLine("<div class=\"poem\">");
                        foreach (var stanza in child.Elements(fb2Ns + "stanza"))
                        {
                            html.AppendLine("<div class=\"stanza\">");
                            foreach (var verse in stanza.Elements(fb2Ns + "v"))
                            {
                                html.AppendLine($"<p class=\"verse\">{ProcessInline(verse)}</p>");
                            }
                            html.AppendLine("</div>");
                        }
                        html.AppendLine("</div>");
                        break;

                    case "cite":
                        html.AppendLine("<blockquote>");
                        html.AppendLine(ConvertToHtml(child));
                        html.AppendLine("</blockquote>");
                        break;

                    case "epigraph":
                        html.AppendLine("<div class=\"epigraph\">");
                        html.AppendLine(ConvertToHtml(child));
                        html.AppendLine("</div>");
                        break;

                    case "image":
                        string href = child.Attribute(XNamespace.Xml + "href")?.Value ??
                                     child.Attribute("href")?.Value ?? "";
                        if (!string.IsNullOrEmpty(href))
                        {
                            href = href.TrimStart('#');
                            string ext = GetImageExtension("image/jpeg");
                            html.AppendLine($"<img src=\"{href}.{ext}\" alt=\"\"/>");
                        }
                        break;

                    case "text-author":
                        html.AppendLine($"<cite>{ProcessInline(child)}</cite>");
                        break;

                    case "section":
                        html.AppendLine(ConvertToHtml(child));
                        break;
                }
            }

            return html.ToString();
        }

        private string ProcessInline(XElement element)
        {
            var sb = new StringBuilder();

            foreach (var node in element.Nodes())
            {
                if (node is XText textNode)
                {
                    sb.Append(EscapeXml(textNode.Value));
                }
                else if (node is XElement elem)
                {
                    string tagName = elem.Name.LocalName.ToLower();

                    switch (tagName)
                    {
                        case "strong":
                            sb.Append($"<strong>{ProcessInline(elem)}</strong>");
                            break;

                        case "emphasis":
                            sb.Append($"<em>{ProcessInline(elem)}</em>");
                            break;

                        case "style":
                            sb.Append($"<span>{ProcessInline(elem)}</span>");
                            break;

                        case "a":
                            string href = elem.Attribute(XNamespace.Xml + "href")?.Value ??
                                         elem.Attribute("href")?.Value ?? "#";
                            sb.Append($"<a href=\"{EscapeXml(href)}\">{ProcessInline(elem)}</a>");
                            break;

                        case "strikethrough":
                            sb.Append($"<s>{ProcessInline(elem)}</s>");
                            break;

                        case "sub":
                            sb.Append($"<sub>{ProcessInline(elem)}</sub>");
                            break;

                        case "sup":
                            sb.Append($"<sup>{ProcessInline(elem)}</sup>");
                            break;

                        case "code":
                            sb.Append($"<code>{ProcessInline(elem)}</code>");
                            break;

                        case "image":
                            string imgHref = elem.Attribute(XNamespace.Xml + "href")?.Value ??
                                           elem.Attribute("href")?.Value ?? "";
                            if (!string.IsNullOrEmpty(imgHref))
                            {
                                imgHref = imgHref.TrimStart('#');
                                string ext = GetImageExtension("image/jpeg");
                                sb.Append($"<img src=\"{imgHref}.{ext}\" alt=\"\" style=\"display:inline;\"/>");
                            }
                            break;

                        default:
                            sb.Append(GetText(elem));
                            break;
                    }
                }
            }

            return sb.ToString();
        }

        private string GetText(XElement element)
        {
            if (element == null) return "";

            var sb = new StringBuilder();
            foreach (var node in element.Nodes())
            {
                if (node is XText textNode)
                {
                    sb.Append(textNode.Value);
                }
                else if (node is XElement elem)
                {
                    sb.Append(GetText(elem));
                }
            }
            return sb.ToString().Trim();
        }

        private string GetTitle(Book book)
        {
            // From book object
            if (!string.IsNullOrEmpty(book.Title))
                return book.Title;

            // From FB2 XML
            var titleInfo = fb2Xml?.Root?.Element(fb2Ns + "description")?.Element(fb2Ns + "title-info");
            var bookTitle = titleInfo?.Element(fb2Ns + "book-title")?.Value;

            return !string.IsNullOrEmpty(bookTitle) ? bookTitle : "Unknown Title";
        }

        private string GetLanguage(Book book)
        {
            // From book object
            if (!string.IsNullOrEmpty(book.Language))
                return book.Language;

            // From FB2 XML
            var titleInfo = fb2Xml?.Root?.Element(fb2Ns + "description")?.Element(fb2Ns + "title-info");
            var lang = titleInfo?.Element(fb2Ns + "lang")?.Value;

            return !string.IsNullOrEmpty(lang) ? lang : "en";
        }

        private bool HasCover()
        {
            if (fb2Xml == null) return false;

            var titleInfo = fb2Xml.Root?.Element(fb2Ns + "description")?.Element(fb2Ns + "title-info");
            var coverpage = titleInfo?.Element(fb2Ns + "coverpage");
            if (coverpage == null) return false;

            var imageElement = coverpage.Element(fb2Ns + "image");
            if (imageElement == null) return false;

            string href = imageElement.Attribute(XNamespace.Xml + "href")?.Value ??
                         imageElement.Attribute("href")?.Value;

            if (string.IsNullOrEmpty(href)) return false;

            // Check if binary with this ID exists
            href = href.TrimStart('#');
            var binary = fb2Xml.Root?.Elements(fb2Ns + "binary")
                .FirstOrDefault(b => b.Attribute("id")?.Value == href);

            return binary != null;
        }

        private byte[] ExtractCoverData()
        {
            if (!HasCover()) return null;

            var titleInfo = fb2Xml.Root?.Element(fb2Ns + "description")?.Element(fb2Ns + "title-info");
            var coverpage = titleInfo?.Element(fb2Ns + "coverpage");
            var imageElement = coverpage?.Element(fb2Ns + "image");

            string href = imageElement?.Attribute(XNamespace.Xml + "href")?.Value ??
                         imageElement?.Attribute("href")?.Value;

            if (string.IsNullOrEmpty(href)) return null;

            href = href.TrimStart('#');
            var binary = fb2Xml.Root?.Elements(fb2Ns + "binary")
                .FirstOrDefault(b => b.Attribute("id")?.Value == href);

            if (binary != null)
            {
                try
                {
                    return Convert.FromBase64String(binary.Value);
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Warning, "Error decoding cover image: {0}", ex.Message);
                }
            }

            return null;
        }

        private string GetImageMimeType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType)) return "image/jpeg";
            contentType = contentType.ToLower();
            if (contentType.Contains("png")) return "image/png";
            if (contentType.Contains("gif")) return "image/gif";
            return "image/jpeg";
        }

        private string GetImageExtension(string contentType)
        {
            if (string.IsNullOrEmpty(contentType)) return "jpg";
            contentType = contentType.ToLower();
            if (contentType.Contains("png")) return "png";
            if (contentType.Contains("gif")) return "gif";
            return "jpg";
        }

        private string EscapeXml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        // Helper classes
        private class ChapterInfo
        {
            public string Title { get; set; }
            public string FileName { get; set; }
            public string Content { get; set; }
        }

        private class ImageInfo
        {
            public string Id { get; set; }
            public string FileName { get; set; }
            public string MimeType { get; set; }
            public byte[] Data { get; set; }
        }
    }
}