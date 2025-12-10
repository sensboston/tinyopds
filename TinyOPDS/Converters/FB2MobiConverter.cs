/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Native FB2 to MOBI converter - creates valid mobi files (MOBI 6)
 * for old Amazon Kindle readers without external software.
 * 
 * Optimized single-pass algorithm for filepos resolution.
 *
 * TODO: implement INDX records for TOC (system "Go to" menu)
 *
 */

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

using TinyOPDS.Data;

namespace TinyOPDS
{
    public class FB2MobiConverter
    {
        // Placeholder length must be fixed (10 digits for filepos values)
        private const int FILEPOS_PLACEHOLDER_LENGTH = 10;
        private const string FILEPOS_PLACEHOLDER = "0000000000";

        private XDocument _fb2Xml = null;
        private XNamespace _fb2Ns;
        private readonly XNamespace _xlinkNs = "http://www.w3.org/1999/xlink";

        // Image data and indices
        private Dictionary<string, byte[]> _images = new Dictionary<string, byte[]>();
        private Dictionary<string, int> _imageIndices = new Dictionary<string, int>();
        private string _coverImageId = null;
        private int _coverRecindex = 0;

        // Footnotes: id -> text (without title)
        private Dictionary<string, string> _footnotes = new Dictionary<string, string>();

        // Chapters for TOC: (title, anchorId, depth)
        private List<Tuple<string, string, int>> _chapters = new List<Tuple<string, string, int>>();
        private int _chapterCounter = 0;

        // Single-pass optimization structures
        private List<FilePosPatch> _patches = new List<FilePosPatch>();
        private Dictionary<string, int> _anchors = new Dictionary<string, int>();

        // HTML generation stream
        private MemoryStream _htmlStream;
        private StreamWriter _htmlWriter;

        // Footnote output state
        private bool _isFirstFootnote;

        /// <summary>
        /// Patch record for deferred filepos resolution
        /// </summary>
        private struct FilePosPatch
        {
            public int PlaceholderPosition;  // Byte position of placeholder in HTML
            public string TargetAnchorId;    // ID of the target anchor

            public FilePosPatch(int position, string targetId)
            {
                PlaceholderPosition = position;
                TargetAnchorId = targetId;
            }
        }

        public bool ConvertToMobiStream(Book book, Stream fb2Stream, Stream mobiStream)
        {
            try
            {
                if (!LoadFB2Xml(fb2Stream))
                {
                    Log.WriteLine(LogLevel.Error, "Failed to parse FB2: {0}", book.FileName);
                    return false;
                }

                ExtractCoverImage();
                ExtractImages();
                BuildImageIndices();
                ExtractFootnotes();

                // Generate HTML with single-pass optimization
                byte[] htmlBytes = GenerateHtmlOptimized(book);

                // Apply filepos patches
                ApplyFileposPatches(htmlBytes);

                // Write MOBI file using helper
                var imageList = _images.Values.ToList();
                var mobiHelper = new MobiHelper(book, imageList, _coverRecindex);
                mobiHelper.WriteMobiFile(mobiStream, htmlBytes);

                Log.WriteLine(LogLevel.Info, "Created MOBI: {0} ({1} images, {2} footnotes, cover: {3})",
                    book.FileName, _images.Count, _footnotes.Count, _coverImageId != null ? "yes" : "no");
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "MOBI conversion error: {0}", ex.Message);
                return false;
            }
        }

        #region FB2 Parsing

        private bool LoadFB2Xml(Stream stream)
        {
            try
            {
                stream.Position = 0;
                _fb2Xml = XDocument.Load(stream);
                _fb2Ns = _fb2Xml.Root?.GetDefaultNamespace() ??
                        XNamespace.Get("http://www.gribuser.ru/xml/fictionbook/2.0");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ExtractCoverImage()
        {
            _coverImageId = null;
            var coverpage = _fb2Xml.Root?.Descendants(_fb2Ns + "coverpage").FirstOrDefault();
            if (coverpage == null) return;

            var imageElem = coverpage.Element(_fb2Ns + "image");
            if (imageElem == null) return;

            var href = imageElem.Attribute(_xlinkNs + "href")?.Value;
            if (string.IsNullOrEmpty(href)) return;

            _coverImageId = href.TrimStart('#');
        }

        private void ExtractImages()
        {
            _images.Clear();
            var binaries = _fb2Xml.Root?.Descendants(_fb2Ns + "binary");
            if (binaries == null) return;

            foreach (var binary in binaries)
            {
                try
                {
                    string id = binary.Attribute("id")?.Value;
                    if (string.IsNullOrEmpty(id)) continue;

                    string contentType = binary.Attribute("content-type")?.Value ?? "";
                    string base64Data = binary.Value.Trim();
                    byte[] imageData = Convert.FromBase64String(base64Data);

                    if (imageData.Length > 0)
                    {
                        // Convert PNG to GIF (Kindle doesn't support PNG transparency)
                        // GIF is better for line art - no JPEG compression artifacts
                        if (contentType.Contains("png") || IsPngImage(imageData))
                        {
                            imageData = ConvertPngToGif(imageData);
                        }

                        _images[id] = imageData;
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Checks if image data is PNG format by magic bytes
        /// </summary>
        private bool IsPngImage(byte[] data)
        {
            if (data.Length < 8) return false;
            // PNG magic: 89 50 4E 47 0D 0A 1A 0A
            return data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47;
        }

        /// <summary>
        /// Converts PNG image to GIF (better for line art, no JPEG artifacts)
        /// Kindle supports GIF natively and it works well with 16 grayscale levels of E-Ink
        /// </summary>
        private byte[] ConvertPngToGif(byte[] pngData)
        {
            try
            {
                using (var inputStream = new MemoryStream(pngData))
                using (var originalImage = Image.FromStream(inputStream))
                using (var bitmap = new Bitmap(originalImage.Width, originalImage.Height))
                {
                    // Preserve original resolution
                    bitmap.SetResolution(originalImage.HorizontalResolution, originalImage.VerticalResolution);

                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        // Fill with white background (replaces transparency)
                        graphics.Clear(Color.White);
                        // Draw original image without scaling
                        graphics.DrawImageUnscaled(originalImage, 0, 0);
                    }

                    // Save as GIF
                    using (var outputStream = new MemoryStream())
                    {
                        bitmap.Save(outputStream, ImageFormat.Gif);
                        return outputStream.ToArray();
                    }
                }
            }
            catch
            {
                // If conversion fails, return original data
                return pngData;
            }
        }

        private void BuildImageIndices()
        {
            _imageIndices.Clear();
            _coverRecindex = 0;

            int index = 1;
            foreach (var key in _images.Keys)
            {
                _imageIndices[key] = index;
                if (key == _coverImageId)
                {
                    _coverRecindex = index;
                }
                index++;
            }
        }

        private void ExtractFootnotes()
        {
            _footnotes.Clear();

            var notesBody = _fb2Xml.Root?.Descendants(_fb2Ns + "body")
                .FirstOrDefault(b => b.Attribute("name")?.Value == "notes");

            if (notesBody == null) return;

            // Process all sections with id attribute (can be nested in multiple parent sections)
            foreach (var section in notesBody.Descendants(_fb2Ns + "section"))
            {
                string id = section.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(id)) continue;

                // Extract text only from direct <p> children, EXCLUDING <title> content
                var sb = new StringBuilder();
                foreach (var p in section.Elements(_fb2Ns + "p"))
                {
                    var text = ExtractText(p);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.Append(text);
                        sb.Append(" ");
                    }
                }

                if (sb.Length > 0)
                {
                    _footnotes[id] = sb.ToString().Trim();
                }
            }
        }

        #endregion

        #region Optimized HTML Generation

        /// <summary>
        /// Generates HTML with single-pass collection of anchor positions and patch locations
        /// </summary>
        private byte[] GenerateHtmlOptimized(Book book)
        {
            _patches.Clear();
            _anchors.Clear();
            _chapters.Clear();
            _chapterCounter = 0;

            _htmlStream = new MemoryStream();
            _htmlWriter = new StreamWriter(_htmlStream, new UTF8Encoding(false));

            // HTML header
            Write("<html>\r\n");
            Write("<head>\r\n");
            Write("<guide>\r\n");
            Write("<reference title=\"Starts here\" type=\"text\" filepos=0000000000 />\r\n");
            Write("</guide>\r\n");
            Write("</head>\r\n");
            Write("<body>\r\n");

            // Title page
            Write("<p width=\"0\" align=\"center\">\r\n");
            Write("<font size=\"+3\">\r\n");
            Write("<b>");
            Write(EscapeHtml(book.Title));
            Write("</b>\r\n");
            Write("</font>\r\n");
            Write("</p>\r\n");

            if (book.Authors?.Count > 0)
            {
                Write("<p width=\"0\" align=\"center\">");
                Write("<font size=\"+2\">\r\n");
                Write(EscapeHtml(string.Join(", ", book.Authors)));
                Write("</font>\r\n");
                Write("</p>\r\n");
            }

            Write("<br />\r\n");
            Write("<mbp:pagebreak />\r\n");

            // Main body
            var mainBody = _fb2Xml.Root?.Elements(_fb2Ns + "body")
                .FirstOrDefault(b => b.Attribute("name")?.Value != "notes");

            if (mainBody != null)
            {
                foreach (var section in mainBody.Elements(_fb2Ns + "section"))
                {
                    ProcessSection(section, 0);
                }
            }

            // Footnotes section
            GenerateFootnotesSection();

            // TOC section
            Write("<mbp:pagebreak />\r\n");
            WriteAnchorTag("toc_section");
            Write("\r\n");
            Write("<div>\r\n");
            Write("<div height=\"1em\"></div>\r\n");

            foreach (var ch in _chapters)
            {
                Write("<div align=\"left\">\r\n");

                // Indentation via blockquotes
                for (int i = 0; i <= ch.Item3; i++)
                {
                    Write("<blockquote>");
                }

                Write("<a ");
                WriteFileposPlaceholder(ch.Item2);
                Write(">");
                Write(EscapeHtml(ch.Item1));
                Write("</a>");

                for (int i = 0; i <= ch.Item3; i++)
                {
                    Write("</blockquote>");
                }

                Write("\r\n</div>\r\n");
            }

            Write("</div>\r\n");
            Write("<mbp:pagebreak />\r\n");

            Write("</body>\r\n");
            Write("</html>\r\n");

            // Flush and get bytes
            _htmlWriter.Flush();
            byte[] result = _htmlStream.ToArray();

            _htmlWriter.Dispose();
            _htmlStream.Dispose();

            return result;
        }

        /// <summary>
        /// Applies collected patches to resolve filepos values
        /// </summary>
        private void ApplyFileposPatches(byte[] htmlBytes)
        {
            foreach (var patch in _patches)
            {
                if (_anchors.TryGetValue(patch.TargetAnchorId, out int targetPosition))
                {
                    // Format position as 10-digit decimal string
                    string posStr = targetPosition.ToString("D10");
                    byte[] posBytes = Encoding.UTF8.GetBytes(posStr);

                    // Direct write to byte array at placeholder position
                    Array.Copy(posBytes, 0, htmlBytes, patch.PlaceholderPosition, FILEPOS_PLACEHOLDER_LENGTH);
                }
            }
        }

        /// <summary>
        /// Generates footnotes section - handles both flat and nested FB2 structures
        /// </summary>
        private void GenerateFootnotesSection()
        {
            var notesBody = _fb2Xml.Root?.Descendants(_fb2Ns + "body")
                .FirstOrDefault(b => b.Attribute("name")?.Value == "notes");

            if (notesBody == null || _footnotes.Count == 0) return;

            Write("<mbp:pagebreak />\r\n");
            WriteAnchorTag("notes_section");
            Write("\r\n");

            _isFirstFootnote = true;

            foreach (var section in notesBody.Elements(_fb2Ns + "section"))
            {
                ProcessFootnoteSection(section);
            }
        }

        /// <summary>
        /// Recursively processes footnote section - outputs footnote if id is in _footnotes,
        /// otherwise treats as container and processes children
        /// </summary>
        private void ProcessFootnoteSection(XElement section)
        {
            string id = section.Attribute("id")?.Value;

            if (!string.IsNullOrEmpty(id) && _footnotes.TryGetValue(id, out string footnoteText))
            {
                // This is a footnote - output it
                if (!_isFirstFootnote)
                {
                    Write("<div height=\"0.5em\"></div>\r\n");
                    Write("<hr width=\"100%\" />\r\n");
                    Write("<div height=\"0.5em\"></div>\r\n");
                }
                _isFirstFootnote = false;

                Write("<p width=\"0\" align=\"justify\">\r\n");
                Write("<font size=\"-1\">\r\n");
                WriteAnchorTagWithFilepos(id, "back_" + id, "&#8203;");
                Write(EscapeHtml(footnoteText));
                Write("\r\n");
                Write("</font>\r\n");
                Write("</p>\r\n");
            }
            else
            {
                // Container section - output title if present, then process children
                var title = section.Element(_fb2Ns + "title");
                if (title != null)
                {
                    var titleText = ExtractText(title);
                    if (!string.IsNullOrWhiteSpace(titleText))
                    {
                        Write("<p width=\"0\" align=\"center\">\r\n");
                        Write("<b>");
                        Write(EscapeHtml(titleText));
                        Write("</b>\r\n");
                        Write("</p>\r\n");
                        Write("<div height=\"1em\"></div>\r\n");
                    }
                }

                foreach (var child in section.Elements(_fb2Ns + "section"))
                {
                    ProcessFootnoteSection(child);
                }
            }
        }

        #region Stream Writing Helpers

        private void Write(string text)
        {
            _htmlWriter.Write(text);
        }

        /// <summary>
        /// Gets current byte position in the stream (after flushing)
        /// </summary>
        private int GetCurrentPosition()
        {
            _htmlWriter.Flush();
            return (int)_htmlStream.Position;
        }

        /// <summary>
        /// Writes a simple anchor tag and records position BEFORE the tag
        /// Format: <a id="anchorId" />
        /// </summary>
        private void WriteAnchorTag(string id)
        {
            int position = GetCurrentPosition();
            _anchors[id] = position;
            Write("<a id=\"");
            Write(id);
            Write("\" />");
        }

        /// <summary>
        /// Writes anchor tag with filepos for footnote back-link
        /// Records position BEFORE the tag for the anchor
        /// Format: <a filepos=XXXXXXXXXX>text</a>
        /// </summary>
        private void WriteAnchorTagWithFilepos(string anchorId, string targetId, string text)
        {
            // Record anchor position BEFORE the <a tag
            int position = GetCurrentPosition();
            _anchors[anchorId] = position;

            Write("<a ");
            WriteFileposPlaceholder(targetId);
            Write(">");
            Write(text);
            Write("</a>");
        }

        /// <summary>
        /// Writes a link tag with filepos for footnote reference in text
        /// Records position BEFORE the tag for the anchor
        /// Format: <a filepos=XXXXXXXXXX>content</a>
        /// </summary>
        private void WriteFootnoteRefTag(string backAnchorId, string targetFootnoteId, string linkText)
        {
            // Record anchor position BEFORE the <a tag
            int position = GetCurrentPosition();
            _anchors[backAnchorId] = position;

            Write("<a ");
            WriteFileposPlaceholder(targetFootnoteId);
            Write(">");
            Write("<font size=\"-1\">");
            Write("<sup>");
            Write(EscapeHtml(linkText));
            Write("</sup>");
            Write("</font>");
            Write("</a>");
        }

        /// <summary>
        /// Writes filepos=0000000000 and records patch location
        /// </summary>
        private void WriteFileposPlaceholder(string targetAnchorId)
        {
            Write("filepos=");
            int position = GetCurrentPosition();
            _patches.Add(new FilePosPatch(position, targetAnchorId));
            Write(FILEPOS_PLACEHOLDER);
        }

        #endregion

        #region Section Processing

        private void ProcessSection(XElement section, int depth)
        {
            var title = section.Element(_fb2Ns + "title");
            if (title != null)
            {
                var titleParagraphs = title.Elements(_fb2Ns + "p").ToList();
                var titleText = ExtractText(title);

                if (!string.IsNullOrWhiteSpace(titleText))
                {
                    _chapterCounter++;
                    string chapterId = "chapter_" + _chapterCounter;
                    _chapters.Add(Tuple.Create(titleText, chapterId, depth));

                    WriteAnchorTag(chapterId);
                    Write("\r\n");

                    if (titleParagraphs.Count > 1)
                    {
                        foreach (var p in titleParagraphs)
                        {
                            var pText = ExtractText(p);
                            if (!string.IsNullOrWhiteSpace(pText))
                            {
                                Write("<p width=\"0\" align=\"center\">\r\n");
                                Write("<b>");
                                Write(EscapeHtml(pText));
                                Write("</b>\r\n");
                                Write("</p>\r\n");
                            }
                        }
                    }
                    else
                    {
                        Write("<p width=\"0\" align=\"center\">\r\n");
                        Write("<b>");
                        Write(EscapeHtml(titleText));
                        Write("</b>\r\n");
                        Write("</p>\r\n");
                    }
                    Write("<br />\r\n");
                }
            }

            foreach (var elem in section.Elements())
            {
                switch (elem.Name.LocalName)
                {
                    case "p":
                        ProcessParagraph(elem);
                        break;

                    case "empty-line":
                        Write("<br />\r\n");
                        break;

                    case "image":
                        ProcessImage(elem);
                        break;

                    case "section":
                        ProcessSection(elem, depth + 1);
                        break;
                }
            }

            Write("<mbp:pagebreak />\r\n");
        }

        #endregion

        #region Paragraph Processing (Fully Streaming)

        private void ProcessParagraph(XElement paragraph)
        {
            // Check if paragraph has any meaningful content
            if (!HasContent(paragraph))
                return;

            Write("<p width=\"2em\" align=\"justify\">");
            ProcessNodes(paragraph.Nodes());
            Write("</p>\r\n");
        }

        /// <summary>
        /// Checks if element has any non-whitespace text content
        /// </summary>
        private bool HasContent(XElement element)
        {
            foreach (var node in element.DescendantNodes())
            {
                if (node is XText textNode && !string.IsNullOrWhiteSpace(textNode.Value))
                    return true;
                if (node is XElement elem && elem.Name.LocalName == "image")
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Processes XML nodes and writes directly to stream
        /// </summary>
        private void ProcessNodes(IEnumerable<XNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node is XText textNode)
                {
                    Write(EscapeHtml(textNode.Value));
                }
                else if (node is XElement elem)
                {
                    ProcessElement(elem);
                }
            }
        }

        /// <summary>
        /// Processes single XML element and writes to stream
        /// </summary>
        private void ProcessElement(XElement elem)
        {
            switch (elem.Name.LocalName)
            {
                case "a":
                    ProcessLink(elem);
                    break;

                case "strong":
                    Write("<b>");
                    ProcessNodes(elem.Nodes());
                    Write("</b>");
                    break;

                case "emphasis":
                    Write("<i>");
                    ProcessNodes(elem.Nodes());
                    Write("</i>");
                    break;

                case "image":
                    ProcessInlineImage(elem);
                    break;

                default:
                    // For unknown elements, just process children
                    ProcessNodes(elem.Nodes());
                    break;
            }
        }

        /// <summary>
        /// Processes link element - either footnote reference or regular link
        /// </summary>
        private void ProcessLink(XElement linkElem)
        {
            var href = linkElem.Attribute(_xlinkNs + "href")?.Value;

            if (!string.IsNullOrEmpty(href))
            {
                href = href.TrimStart('#');

                if (_footnotes.ContainsKey(href))
                {
                    // Footnote reference - create bidirectional link
                    // Extract ORIGINAL text from the link element (e.g., "[1]" or "{1}")
                    string originalLinkText = ExtractText(linkElem);

                    Write(" ");
                    WriteFootnoteRefTag("back_" + href, href, originalLinkText);
                }
                else
                {
                    // Regular link - just output text
                    ProcessNodes(linkElem.Nodes());
                }
            }
            else
            {
                // No href - just output text
                ProcessNodes(linkElem.Nodes());
            }
        }

        /// <summary>
        /// Processes inline image element
        /// </summary>
        private void ProcessInlineImage(XElement imgElem)
        {
            var imgHref = imgElem.Attribute(_xlinkNs + "href")?.Value;
            if (!string.IsNullOrEmpty(imgHref))
            {
                imgHref = imgHref.TrimStart('#');
                if (_imageIndices.TryGetValue(imgHref, out int recindex))
                {
                    Write("<img recindex=\"");
                    Write(recindex.ToString("D5"));
                    Write("\" />");
                }
            }
        }

        /// <summary>
        /// Processes standalone image element
        /// </summary>
        private void ProcessImage(XElement imageElem)
        {
            var href = imageElem.Attribute(_xlinkNs + "href")?.Value;
            if (string.IsNullOrEmpty(href)) return;

            href = href.TrimStart('#');
            if (_imageIndices.TryGetValue(href, out int recindex))
            {
                Write("<br />\r\n");
                Write("<p width=\"0\" align=\"center\">\r\n");
                Write("<img recindex=\"");
                Write(recindex.ToString("D5"));
                Write("\" />\r\n");
                Write("</p>\r\n");
                Write("<br />\r\n");
            }
        }

        #endregion

        #endregion

        #region Utility Methods

        private string ExtractText(XElement element)
        {
            return string.Join(" ", element.DescendantNodes()
                .OfType<XText>()
                .Select(t => t.Value.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        private string EscapeHtml(string text)
        {
            return System.Net.WebUtility.HtmlEncode(text ?? "");
        }

        #endregion
    }
}