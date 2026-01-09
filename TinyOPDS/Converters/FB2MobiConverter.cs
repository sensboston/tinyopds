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
 * v8: Complete HTML rewrite based on kindlegen output analysis
 * v9: Fixed NCX structure based on binary analysis:
 *     - Added book title, Notes section, TOC as NCX entries (depth 0)
 *     - NCX entries in breadth-first order (all depth 0 first, then depth 1)
 *
 * Optimized single-pass algorithm for filepos resolution.
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

        // Footnotes grouped by section (e.g., "Примечания", "Комментарии")
        private class FootnoteGroup
        {
            public string Title { get; set; }
            public List<KeyValuePair<string, string>> Footnotes { get; } = new List<KeyValuePair<string, string>>();
        }
        private List<FootnoteGroup> _footnoteGroups = new List<FootnoteGroup>();

        // Flat list for quick lookup (populated from groups)
        private List<KeyValuePair<string, string>> _footnotes = new List<KeyValuePair<string, string>>();

        // Chapters for TOC: (title, anchorId, depth)
        private List<Tuple<string, string, int>> _chapters = new List<Tuple<string, string, int>>();
        private int _chapterCounter = 0;

        // Single-pass optimization structures
        private List<FilePosPatch> _patches = new List<FilePosPatch>();
        private Dictionary<string, int> _anchors = new Dictionary<string, int>();

        // HTML generation stream
        private MemoryStream _htmlStream;
        private StreamWriter _htmlWriter;

        // Track footnote reference counts for unique back-link IDs
        private Dictionary<string, int> _footnoteRefCounts = new Dictionary<string, int>();

        // Track current paragraph start position for TOCMenu mode back-links
        private int _currentParagraphStart = 0;

        // Special anchors for guide references
        private const string ANCHOR_CONTENT_START = "_content_start_";
        private const string ANCHOR_TOC = "_toc_";

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

                // DEBUG: Output raw HTML to Visual Studio Debug window
                #if DEBUG
                System.Diagnostics.Debug.WriteLine("=== RAW HTML START ===");
                System.Diagnostics.Debug.WriteLine(Encoding.UTF8.GetString(htmlBytes));
                System.Diagnostics.Debug.WriteLine("=== RAW HTML END ===");
                #endif

                // Build chapter list with resolved offsets for NCX
                // Reference kindlegen adds: Title, Parts, Chapters, Notes, TOC
                var mobiChapters = new List<MobiChapter>();

                // 1. Add book title entry (depth 0) - like kindlegen's first entry
                if (_anchors.TryGetValue(ANCHOR_CONTENT_START, out int titleOffset))
                {
                    string bookTitle = string.Join(" ", book.Authors ?? new List<string>()) + " " + book.Title;
                    mobiChapters.Add(new MobiChapter
                    {
                        Title = bookTitle.Trim(),
                        Offset = titleOffset,
                        Depth = 0
                    });
                }

                // 2. Add all chapters from FB2
                foreach (var ch in _chapters)
                {
                    string title = ch.Item1;
                    string anchorId = ch.Item2;
                    int depth = ch.Item3;

                    if (_anchors.TryGetValue(anchorId, out int offset))
                    {
                        mobiChapters.Add(new MobiChapter
                        {
                            Title = title,
                            Offset = offset,
                            Depth = depth
                        });
                    }
                }

                // 3. DISABLED: Notes section in NCX may interfere with popup footnote detection
                // if (_footnoteGroups.Count > 0 && _anchors.TryGetValue("_footnotes_", out int notesOffset))
                // {
                //     mobiChapters.Add(new MobiChapter
                //     {
                //         Title = _footnoteGroups[0].Title,
                //         Offset = notesOffset,
                //         Depth = 0
                //     });
                // }

                // 4. Add TOC entry (depth 0)
                if (_anchors.TryGetValue(ANCHOR_TOC, out int tocOffset))
                {
                    mobiChapters.Add(new MobiChapter
                    {
                        Title = "TOC",
                        Offset = tocOffset,
                        Depth = 0
                    });
                }

                // Write MOBI file using helper
                // When TOCMenu=true: NCX enabled for "Go To" menu, footnotes use separate pages with Back link
                // When TOCMenu=false (default): NCX disabled for popup footnotes, TOC accessible via HTML
                var imageList = _images.Values.ToList();
                var mobiHelper = new MobiHelper(book, imageList, _coverRecindex);
                if (Properties.Settings.Default.TOCMenu)
                {
                    mobiHelper.WriteMobiFile(mobiStream, htmlBytes, mobiChapters);
                }
                else
                {
                    mobiHelper.WriteMobiFile(mobiStream, htmlBytes, null);
                }

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
                        if (id == _coverImageId)
                        {
                            // Convert cover to JPEG (Kindle handles centering)
                            imageData = ConvertToJpeg(imageData);
                        }
                        else if (contentType.Contains("png") || IsPngImage(imageData))
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
        /// Simple conversion to JPEG without resizing
        /// </summary>
        private byte[] ConvertToJpeg(byte[] imageData)
        {
            try
            {
                using (var inputStream = new MemoryStream(imageData))
                using (var originalImage = Image.FromStream(inputStream))
                using (var outputStream = new MemoryStream())
                {
                    var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                        .FirstOrDefault(enc => enc.FormatID == ImageFormat.Jpeg.Guid);
                    if (jpegEncoder != null)
                    {
                        var encoderParams = new EncoderParameters(1);
                        encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
                        originalImage.Save(outputStream, jpegEncoder, encoderParams);
                    }
                    else
                    {
                        originalImage.Save(outputStream, ImageFormat.Jpeg);
                    }
                    return outputStream.ToArray();
                }
            }
            catch
            {
                return imageData;
            }
        }

        private bool IsPngImage(byte[] data)
        {
            if (data.Length < 8) return false;
            return data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47;
        }

        private byte[] ConvertPngToGif(byte[] pngData)
        {
            try
            {
                using (var inputStream = new MemoryStream(pngData))
                using (var originalImage = Image.FromStream(inputStream))
                using (var bitmap = new Bitmap(originalImage.Width, originalImage.Height))
                {
                    bitmap.SetResolution(originalImage.HorizontalResolution, originalImage.VerticalResolution);
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(Color.White);
                        graphics.DrawImageUnscaled(originalImage, 0, 0);
                    }
                    using (var outputStream = new MemoryStream())
                    {
                        bitmap.Save(outputStream, ImageFormat.Gif);
                        return outputStream.ToArray();
                    }
                }
            }
            catch
            {
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
            _footnoteGroups.Clear();

            var notesBody = _fb2Xml.Root?.Descendants(_fb2Ns + "body")
                .FirstOrDefault(b => b.Attribute("name")?.Value == "notes");
            if (notesBody == null) return;

            // Get title from body if present
            string bodyTitle = "Notes";
            var bodyTitleElem = notesBody.Element(_fb2Ns + "title");
            if (bodyTitleElem != null)
            {
                bodyTitle = ExtractText(bodyTitleElem);
            }

            // Create a default group for footnotes
            var defaultGroup = new FootnoteGroup { Title = bodyTitle };

            // Process all sections in notes body
            foreach (var section in notesBody.Elements(_fb2Ns + "section"))
            {
                string id = section.Attribute("id")?.Value;

                if (!string.IsNullOrEmpty(id))
                {
                    // This section IS a footnote (has id directly)
                    // Structure: body > section[id="n_1"]
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
                        var footnote = new KeyValuePair<string, string>(id, sb.ToString().Trim());
                        defaultGroup.Footnotes.Add(footnote);
                        _footnotes.Add(footnote);
                    }
                }
                else
                {
                    // This section is a GROUP containing footnotes
                    // Structure: body > section > section[id="n_1"]
                    var group = new FootnoteGroup();

                    // Extract group title
                    var title = section.Element(_fb2Ns + "title");
                    if (title != null)
                    {
                        group.Title = ExtractText(title);
                    }
                    if (string.IsNullOrWhiteSpace(group.Title))
                    {
                        group.Title = "Notes";
                    }

                    // Extract footnotes from subsections
                    foreach (var subSection in section.Elements(_fb2Ns + "section"))
                    {
                        string subId = subSection.Attribute("id")?.Value;
                        if (string.IsNullOrEmpty(subId)) continue;

                        var sb = new StringBuilder();
                        foreach (var p in subSection.Elements(_fb2Ns + "p"))
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
                            var footnote = new KeyValuePair<string, string>(subId, sb.ToString().Trim());
                            group.Footnotes.Add(footnote);
                            _footnotes.Add(footnote);
                        }
                    }

                    if (group.Footnotes.Count > 0)
                    {
                        _footnoteGroups.Add(group);
                    }
                }
            }

            // Add default group if it has footnotes
            if (defaultGroup.Footnotes.Count > 0)
            {
                _footnoteGroups.Insert(0, defaultGroup);
            }
        }

        #endregion

        #region Optimized HTML Generation (Matching Kindlegen Output)

        private byte[] GenerateHtmlOptimized(Book book)
        {
            _patches.Clear();
            _anchors.Clear();
            _chapters.Clear();
            _chapterCounter = 0;
            _footnoteRefCounts.Clear();

            _htmlStream = new MemoryStream();
            _htmlWriter = new StreamWriter(_htmlStream, new UTF8Encoding(false));

            // HTML header with guide (matching kindlegen format exactly)
            Write("<html><head><guide>");
            Write("<reference title=\"Starts here\" type=\"text\"  ");
            WriteFileposPlaceholder(ANCHOR_CONTENT_START);
            Write(" />");
            Write("<reference title=\"Table of Contents\" type=\"toc\"  ");
            WriteFileposPlaceholder(ANCHOR_TOC);
            Write(" />");
            Write("</guide></head><body>");

            // Title page - annotation section
            Write("<div height=\"2em\" align=\"center\"><blockquote><font size=\"-1\"> ");

            // Get annotation from FB2
            var annotation = _fb2Xml.Root?.Descendants(_fb2Ns + "annotation").FirstOrDefault();
            if (annotation != null)
            {
                Write("<div><blockquote><font size=\"-1\"> ");
                foreach (var p in annotation.Elements(_fb2Ns + "p"))
                {
                    Write("<p width=\"1em\" align=\"justify\">");
                    Write(EscapeHtml(ExtractText(p)));
                    Write("</p> ");
                }
                Write("</font></blockquote></div>");
            }
            Write("<div height=\"1em\"></div> </div>  ");

            // Content start marker
            Write("<mbp:pagebreak/>");
            RecordAnchor(ANCHOR_CONTENT_START);
            Write("<mbp:pagebreak/>");

            // Author and title
            Write("<div  height=\"2em\" width=\"0\"> ");
            Write("<div><font size=\"+1\"><b> ");
            if (book.Authors?.Count > 0)
            {
                Write("<p width=\"0\" align=\"center\">");
                Write(EscapeHtml(string.Join(" ", book.Authors)));
                Write("</p> ");
            }
            Write("<p width=\"0\" align=\"center\">");
            Write(EscapeHtml(book.Title));
            Write("</p> ");
            Write("</b></font></div>");
            Write("<div height=\"1em\"></div> </div>");
            Write("<div height=\"1em\"></div>  ");

            // Main body sections
            var mainBody = _fb2Xml.Root?.Elements(_fb2Ns + "body")
                .FirstOrDefault(b => b.Attribute("name")?.Value != "notes");

            if (mainBody != null)
            {
                foreach (var section in mainBody.Elements(_fb2Ns + "section"))
                {
                    ProcessSectionKindlegen(section, 0);
                }
            }

            // Footnotes section (uses title from FB2 notes body)
            // Choose method based on TOCMenu setting
            if (Properties.Settings.Default.TOCMenu)
            {
                GenerateFootnotesSectionWithTOCMenu();
            }
            else
            {
                GenerateFootnotesSectionKindlegen();
            }

            // TOC section
            Write("<mbp:pagebreak/>");
            RecordAnchor(ANCHOR_TOC);

            // When TOCMenu=true, TOC is in "Go To" menu - just add empty page
            // When TOCMenu=false, generate full HTML TOC
            if (!Properties.Settings.Default.TOCMenu)
            {
                Write("<div>");
                Write("<div align=\"center\"><font size=\"+2\"><b>");
                Write(EscapeHtml("Содержание"));
                Write("</b></font></div>");
                Write("<div height=\"1.5em\"></div>");

                // First entry - book title
                Write("<div align=\"left\">");
                Write("<a ");
                WriteFileposPlaceholder(ANCHOR_CONTENT_START);
                Write(">");
                Write("<b>");
                Write(EscapeHtml(string.Join(" ", book.Authors ?? new List<string>()) + " — " + book.Title));
                Write("</b>");
                Write("</a></div>");
                Write("<div height=\"1em\"></div>");

                // Chapter entries with hierarchy
                foreach (var ch in _chapters)
                {
                    string title = ch.Item1;
                    string anchorId = ch.Item2;
                    int depth = ch.Item3;

                    // Indent based on depth: each level adds margin
                    string indent = "";
                    string fontSize = "";
                    for (int i = 0; i < depth; i++)
                    {
                        indent += "&nbsp;&nbsp;&nbsp;&nbsp;";  // 4 spaces per level
                    }

                    // Font size decreases with depth
                    if (depth == 0)
                        fontSize = "+1";  // Parts - larger
                    else if (depth == 1)
                        fontSize = "";    // Chapters - normal
                    else
                        fontSize = "-1";  // Sub-chapters - smaller

                    Write("<div align=\"left\">");
                    Write(indent);
                    Write("<a ");
                    WriteFileposPlaceholder(anchorId);
                    Write(">");
                    if (!string.IsNullOrEmpty(fontSize))
                        Write("<font size=\"" + fontSize + "\">");
                    Write(EscapeHtml(title));
                    if (!string.IsNullOrEmpty(fontSize))
                        Write("</font>");
                    Write("</a></div>");

                    // Add spacing after top-level items
                    if (depth == 0)
                        Write("<div height=\"0.3em\"></div>");
                }

                // Link to footnotes if any
                if (_footnoteGroups.Count > 0)
                {
                    Write("<div height=\"1em\"></div>");
                    Write("<div align=\"left\">");
                    Write("<a ");
                    WriteFileposPlaceholder("_footnotes_");
                    Write(">");
                    Write(EscapeHtml(_footnoteGroups[0].Title));
                    Write("</a></div>");
                }

                Write("</div>");
            }

            Write("<mbp:pagebreak/></body></html>");

            _htmlWriter.Flush();
            byte[] result = _htmlStream.ToArray();
            _htmlWriter.Dispose();
            _htmlStream.Dispose();

            return result;
        }

        private void ProcessSectionKindlegen(XElement section, int depth)
        {
            var title = section.Element(_fb2Ns + "title");
            if (title != null)
            {
                var titleParagraphs = title.Elements(_fb2Ns + "p").ToList();
                var titleText = ExtractText(title);

                if (!string.IsNullOrWhiteSpace(titleText))
                {
                    _chapterCounter++;
                    string chapterId = "ch_" + _chapterCounter;
                    _chapters.Add(Tuple.Create(titleText, chapterId, depth));

                    // Kindlegen format: pagebreak, empty div, pagebreak, then header
                    Write("<mbp:pagebreak/><div ></div> ");
                    Write("<mbp:pagebreak/>");
                    RecordAnchor(chapterId);
                    Write("<div  height=\"2em\" width=\"0\"> ");
                    Write("<div><font size=\"+1\"><b> ");

                    if (titleParagraphs.Count > 1)
                    {
                        foreach (var p in titleParagraphs)
                        {
                            var pText = ExtractText(p);
                            if (!string.IsNullOrWhiteSpace(pText))
                            {
                                Write("<p width=\"0\" align=\"center\">");
                                Write(EscapeHtml(pText));
                                Write("</p> ");
                            }
                        }
                    }
                    else
                    {
                        Write("<p width=\"0\" align=\"center\">");
                        Write(EscapeHtml(titleText));
                        Write("</p> ");
                    }

                    Write("</b></font></div>");
                    Write("<div height=\"1em\"></div> </div>");
                    Write("<div height=\"1em\"></div> ");
                }
            }

            // Process epigraph
            foreach (var epigraph in section.Elements(_fb2Ns + "epigraph"))
            {
                Write("<div width=\"0\" align=\"justify\"><blockquote width=\"0\"><font size=\"-1\"> ");
                foreach (var p in epigraph.Elements(_fb2Ns + "p"))
                {
                    Write("<p width=\"1em\" align=\"justify\">");
                    Write(EscapeHtml(ExtractText(p)));
                    Write("</p> ");
                }
                Write("</font></blockquote></div> ");
            }

            // Process content
            foreach (var elem in section.Elements())
            {
                switch (elem.Name.LocalName)
                {
                    case "p":
                        ProcessParagraphKindlegen(elem);
                        break;
                    case "empty-line":
                        // Skip - kindlegen doesn't generate anything for empty-line
                        break;
                    case "image":
                        ProcessImage(elem);
                        break;
                    case "section":
                        ProcessSectionKindlegen(elem, depth + 1);
                        break;
                }
            }

            Write("<span></span><mbp:pagebreak/> ");
        }

        private void ProcessParagraphKindlegen(XElement paragraph)
        {
            if (!HasContent(paragraph))
                return;

            Write("<p width=\"1em\" align=\"justify\">");
            // Record paragraph start for TOCMenu mode back-links
            _currentParagraphStart = GetCurrentPosition();
            ProcessNodes(paragraph.Nodes());
            Write("</p> ");
        }

        /// <summary>
        /// Generates footnotes section with popup footnotes (bidirectional filepos links).
        /// Used when TOCMenu=false (default).
        /// </summary>
        private void GenerateFootnotesSectionKindlegen()
        {
            if (_footnoteGroups.Count == 0) return;

            bool isFirstGroup = true;
            foreach (var group in _footnoteGroups)
            {
                if (group.Footnotes.Count == 0) continue;

                // Page break before each section
                Write("<mbp:pagebreak/>");

                // Record anchor for first group (for TOC link)
                if (isFirstGroup)
                {
                    RecordAnchor("_footnotes_");
                    isFirstGroup = false;
                }

                // Section header (kindlegen style)
                Write("<div  height=\"2em\" width=\"0\"> ");
                Write("<div><font size=\"+1\"><b> ");
                Write("<p width=\"0\" align=\"center\">");
                Write(EscapeHtml(group.Title));
                Write("</p> ");
                Write("</b></font></div>");
                Write("<div height=\"1em\"></div> </div>");
                Write("<div height=\"1em\"></div> ");

                // Footnotes in this group
                foreach (var footnote in group.Footnotes)
                {
                    string id = footnote.Key;
                    string text = footnote.Value;
                    string noteNum = ExtractNoteNumber(id);
                    string backId = "back_" + id + "_1";  // First reference (matches text)

                    // Bidirectional: record anchor for link FROM text, add filepos back TO text
                    RecordAnchor(id);  // Position for link from text
                    Write("<p><a ");
                    WriteFileposPlaceholder(backId);  // Link back to text
                    Write(">");
                    Write(noteNum);
                    Write(".</a> ");
                    Write(EscapeHtml(text));
                    Write("</p>");
                }
            }
        }

        /// <summary>
        /// Generates footnotes section with TOC in menu mode.
        /// Each footnote on separate page with "Back" link.
        /// Used when TOCMenu=true.
        /// </summary>
        private void GenerateFootnotesSectionWithTOCMenu()
        {
            if (_footnoteGroups.Count == 0) return;

            bool isFirstGroup = true;
            foreach (var group in _footnoteGroups)
            {
                if (group.Footnotes.Count == 0) continue;

                // Page break and section header for first group only
                if (isFirstGroup)
                {
                    Write("<mbp:pagebreak/>");
                    RecordAnchor("_footnotes_");

                    // Section header (kindlegen style)
                    Write("<div  height=\"2em\" width=\"0\"> ");
                    Write("<div><font size=\"+1\"><b> ");
                    Write("<p width=\"0\" align=\"center\">");
                    Write(EscapeHtml(group.Title));
                    Write("</p> ");
                    Write("</b></font></div>");
                    Write("<div height=\"1em\"></div> </div>");
                    Write("<div height=\"1em\"></div> ");
                    isFirstGroup = false;
                }

                // Each footnote on separate page
                foreach (var footnote in group.Footnotes)
                {
                    string id = footnote.Key;
                    string text = footnote.Value;
                    string noteNum = ExtractNoteNumber(id);
                    string backId = "back_" + id + "_1";  // First reference (matches text)

                    // Page break before each footnote
                    Write("<mbp:pagebreak/>");

                    // Record anchor for link FROM text
                    RecordAnchor(id);

                    // Footnote number and text (NO filepos on number - just plain text)
                    Write("<p><b>");
                    Write(noteNum);
                    Write(".</b> ");
                    Write(EscapeHtml(text));
                    Write("</p>");

                    // "Back" link with filepos to return to text
                    Write("<p align=\"right\"><a ");
                    WriteFileposPlaceholder(backId);
                    Write("><font size=\"+3\"><b>&#8592;</b></font></a></p>");  // ← large arrow symbol
                }
            }

            // Final pagebreak after last footnote (before TOC)
            Write("<mbp:pagebreak/>");
        }

        private void ApplyFileposPatches(byte[] htmlBytes)
        {
            Console.WriteLine($"\n=== APPLYING {_patches.Count} PATCHES ===");
            foreach (var patch in _patches)
            {
                if (_anchors.TryGetValue(patch.TargetAnchorId, out int targetPosition))
                {
                    Console.WriteLine($"PATCH: placeholderPos={patch.PlaceholderPosition} -> '{patch.TargetAnchorId}' = {targetPosition}");
                    #if DEBUG
                    System.Diagnostics.Debug.WriteLine($"PATCH: target={patch.TargetAnchorId}, placeholderPos={patch.PlaceholderPosition}, anchorPos={targetPosition}");
                    #endif
                    string posStr = targetPosition.ToString("D10");
                    byte[] posBytes = Encoding.UTF8.GetBytes(posStr);
                    Array.Copy(posBytes, 0, htmlBytes, patch.PlaceholderPosition, FILEPOS_PLACEHOLDER_LENGTH);
                }
                else
                {
                    Console.WriteLine($"PATCH FAILED: '{patch.TargetAnchorId}' NOT FOUND!");
                    #if DEBUG
                    System.Diagnostics.Debug.WriteLine($"PATCH FAILED: target={patch.TargetAnchorId} NOT FOUND in anchors!");
                    #endif
                }
            }
            Console.WriteLine("=== PATCHES APPLIED ===\n");
        }

        #endregion

        #region Stream Writing Helpers

        private void Write(string text)
        {
            _htmlWriter.Write(text);
        }

        private int GetCurrentPosition()
        {
            _htmlWriter.Flush();
            return (int)_htmlStream.Position;
        }

        private void RecordAnchor(string id)
        {
            int position = GetCurrentPosition();
            _anchors[id] = position;
            // Always log anchor recording for debugging
            Console.WriteLine($"ANCHOR: '{id}' at position {position}");
            #if DEBUG
            System.Diagnostics.Debug.WriteLine($"ANCHOR: id={id}, position={position}");
            #endif
        }

        private void RecordAnchorWithOffset(string id, int offset)
        {
            int position = GetCurrentPosition();
            int finalPos = position + offset;
            _anchors[id] = finalPos;
            #if DEBUG
            System.Diagnostics.Debug.WriteLine($"ANCHOR: id={id}, streamPos={position}, offset={offset}, finalPos={finalPos}");
            #endif
        }

        private void WriteFileposPlaceholder(string targetAnchorId)
        {
            Write("filepos=");
            int position = GetCurrentPosition();
            _patches.Add(new FilePosPatch(position, targetAnchorId));
            Console.WriteLine($"PATCH CREATED: at pos {position}, will link to '{targetAnchorId}'");
            Write(FILEPOS_PLACEHOLDER);
        }

        /// <summary>
        /// Writes footnote reference in text with bidirectional linking
        /// Records anchor for back-navigation, filepos points to footnote
        /// In TOCMenu mode, back-link points to paragraph start for better navigation
        /// </summary>
        private void WriteFootnoteRefKindlegen(string backId, string targetId, string linkText)
        {
            // Record position for back-link FROM footnote
            // In TOCMenu mode, use paragraph start position for better navigation
            if (Properties.Settings.Default.TOCMenu && _currentParagraphStart > 0)
            {
                _anchors[backId] = _currentParagraphStart;
                Console.WriteLine($"ANCHOR (paragraph start): '{backId}' at position {_currentParagraphStart}");
            }
            else
            {
                RecordAnchor(backId);
            }
            // Link TO footnote
            Write("<a ");
            WriteFileposPlaceholder(targetId);
            Write("><sup>");
            Write(EscapeHtml(linkText));
            Write("</sup></a>");
        }

        #endregion

        #region Content Processing

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

        private void ProcessElement(XElement elem)
        {
            switch (elem.Name.LocalName)
            {
                case "a":
                    ProcessLink(elem);
                    break;
                case "strong":
                    Write("<span><b>");
                    ProcessNodes(elem.Nodes());
                    Write("</b></span>");
                    break;
                case "emphasis":
                    Write("<span><i>");
                    ProcessNodes(elem.Nodes());
                    Write("</i></span>");
                    break;
                case "image":
                    ProcessInlineImage(elem);
                    break;
                default:
                    ProcessNodes(elem.Nodes());
                    break;
            }
        }

        private void ProcessLink(XElement linkElem)
        {
            // Check for href in different namespaces: xlink:href, l:href, or plain href
            var href = linkElem.Attribute(_xlinkNs + "href")?.Value
                    ?? linkElem.Attribute(_fb2Ns + "href")?.Value
                    ?? linkElem.Attribute("href")?.Value;

            if (!string.IsNullOrEmpty(href))
            {
                href = href.TrimStart('#');

                if (_footnotes.Any(f => f.Key == href))
                {
                    string originalLinkText = ExtractText(linkElem);

                    // Generate unique back-link ID (like original)
                    if (!_footnoteRefCounts.ContainsKey(href))
                        _footnoteRefCounts[href] = 0;
                    _footnoteRefCounts[href]++;
                    string uniqueBackId = "back_" + href + "_" + _footnoteRefCounts[href];

                    Write(" ");  // Space before footnote link (like original)
                    WriteFootnoteRefKindlegen(uniqueBackId, href, originalLinkText);
                }
                else
                {
                    ProcessNodes(linkElem.Nodes());
                }
            }
            else
            {
                ProcessNodes(linkElem.Nodes());
            }
        }

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

        #region Utility Methods

        private string ExtractText(XElement element)
        {
            return string.Join(" ", element.DescendantNodes()
                .OfType<XText>()
                .Select(t => t.Value.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        private string ExtractNoteNumber(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "?";

            var sb = new StringBuilder();
            bool foundDigit = false;
            foreach (char c in id)
            {
                if (char.IsDigit(c))
                {
                    sb.Append(c);
                    foundDigit = true;
                }
                else if (foundDigit)
                {
                    break;
                }
            }

            return sb.Length > 0 ? sb.ToString() : "?";
        }

        private string EscapeHtml(string text)
        {
            return System.Net.WebUtility.HtmlEncode(text ?? "");
        }

        #endregion
    }
}
