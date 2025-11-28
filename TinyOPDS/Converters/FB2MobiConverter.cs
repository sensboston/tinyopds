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
 * TODO: implement INDX records for TOC (system "Go to" menu)
 *
 */

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;

using TinyOPDS.Data;

namespace TinyOPDS
{
    public class FB2MobiConverter
    {
        private const int RECORD_SIZE = 4096;
        private const int NULL_INDEX = unchecked((int)0xFFFFFFFF);
        private const string FILEPOS_PLACEHOLDER = "0000000000";

        private XDocument fb2Xml = null;
        private XNamespace fb2Ns;
        private readonly XNamespace xlinkNs = "http://www.w3.org/1999/xlink";
        private Dictionary<string, byte[]> images = new Dictionary<string, byte[]>();
        private Dictionary<string, int> imageIndices = new Dictionary<string, int>();
        private Dictionary<string, string> footnotes = new Dictionary<string, string>();
        private List<Tuple<string, string, int>> chapters = new List<Tuple<string, string, int>>();
        private int chapterCounter = 0;
        private string coverImageId = null;
        private int coverRecindex = 0;

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

                string htmlContent = GenerateHtmlWithFootnotes(book);
                byte[] htmlBytes = Encoding.UTF8.GetBytes(htmlContent);

                WriteMobiFile(mobiStream, htmlBytes, book);

                Log.WriteLine(LogLevel.Info, "Created MOBI: {0} ({1} images, {2} footnotes, cover: {3})",
                    book.FileName, images.Count, footnotes.Count, coverImageId != null ? "yes" : "no");
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
                fb2Xml = XDocument.Load(stream);
                fb2Ns = fb2Xml.Root?.GetDefaultNamespace() ??
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
            coverImageId = null;
            var coverpage = fb2Xml.Root?.Descendants(fb2Ns + "coverpage").FirstOrDefault();
            if (coverpage == null) return;

            var imageElem = coverpage.Element(fb2Ns + "image");
            if (imageElem == null) return;

            var href = imageElem.Attribute(xlinkNs + "href")?.Value;
            if (string.IsNullOrEmpty(href)) return;

            coverImageId = href.TrimStart('#');
        }

        private void ExtractImages()
        {
            images.Clear();
            var binaries = fb2Xml.Root?.Descendants(fb2Ns + "binary");
            if (binaries == null) return;

            foreach (var binary in binaries)
            {
                try
                {
                    string id = binary.Attribute("id")?.Value;
                    if (string.IsNullOrEmpty(id)) continue;

                    string base64Data = binary.Value.Trim();
                    byte[] imageData = Convert.FromBase64String(base64Data);

                    if (imageData.Length > 0)
                    {
                        images[id] = imageData;
                    }
                }
                catch { }
            }
        }

        private void BuildImageIndices()
        {
            imageIndices.Clear();
            coverRecindex = 0;

            int index = 1;
            foreach (var key in images.Keys)
            {
                imageIndices[key] = index;
                if (key == coverImageId)
                {
                    coverRecindex = index;
                }
                index++;
            }
        }

        private void ExtractFootnotes()
        {
            footnotes.Clear();

            var notesBody = fb2Xml.Root?.Elements(fb2Ns + "body")
                .FirstOrDefault(b => b.Attribute("name")?.Value == "notes");

            if (notesBody == null) return;

            foreach (var section in notesBody.Elements(fb2Ns + "section"))
            {
                string id = section.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(id)) continue;

                var sb = new StringBuilder();
                foreach (var p in section.Elements(fb2Ns + "p"))
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
                    footnotes[id] = sb.ToString().Trim();
                }
            }
        }

        #endregion

        #region HTML Generation with Popup Footnotes

        private string GenerateHtmlWithFootnotes(Book book)
        {
            string html = GenerateHtml(book);

            byte[] htmlBytes = Encoding.UTF8.GetBytes(html);
            var replacements = new Dictionary<string, int>();

            // Find byte positions for footnotes
            foreach (var fnId in footnotes.Keys)
            {
                string backPattern = string.Format("<a id=\"back_{0}\"", fnId);
                int backPos = FindBytePosition(htmlBytes, backPattern);
                if (backPos >= 0)
                {
                    replacements["back_" + fnId] = backPos;
                }

                string notePattern = string.Format("<a filepos={0} id=\"{1}\"", FILEPOS_PLACEHOLDER, fnId);
                int notePos = FindBytePosition(htmlBytes, notePattern);
                if (notePos >= 0)
                {
                    replacements[fnId] = notePos;
                }
            }

            // Find byte positions for chapters (for TOC links)
            foreach (var ch in chapters)
            {
                string chapterPattern = string.Format("<a id=\"{0}\"", ch.Item2);
                int chapterPos = FindBytePosition(htmlBytes, chapterPattern);
                if (chapterPos >= 0)
                {
                    replacements[ch.Item2] = chapterPos;
                }
            }

            // Replace placeholders
            var result = new StringBuilder(html);

            // Replace footnote links
            foreach (var fnId in footnotes.Keys)
            {
                if (replacements.ContainsKey(fnId))
                {
                    string oldValue = string.Format("id=\"back_{0}\" filepos={1}", fnId, FILEPOS_PLACEHOLDER);
                    string newValue = string.Format("id=\"back_{0}\" filepos={1:D10}", fnId, replacements[fnId]);
                    ReplaceAll(result, oldValue, newValue);
                }

                if (replacements.ContainsKey("back_" + fnId))
                {
                    string oldValue = string.Format("filepos={0} id=\"{1}\"", FILEPOS_PLACEHOLDER, fnId);
                    string newValue = string.Format("filepos={0:D10} id=\"{1}\"", replacements["back_" + fnId], fnId);
                    ReplaceFirst(result, oldValue, newValue);
                }
            }

            // Replace TOC chapter links
            foreach (var ch in chapters)
            {
                if (replacements.ContainsKey(ch.Item2))
                {
                    string oldValue = string.Format("<a filepos={0}>{1}</a>", FILEPOS_PLACEHOLDER, EscapeHtml(ch.Item1));
                    string newValue = string.Format("<a filepos={0:D10}>{1}</a>", replacements[ch.Item2], EscapeHtml(ch.Item1));
                    ReplaceFirst(result, oldValue, newValue);
                }
            }

            return result.ToString();
        }

        private int FindBytePosition(byte[] htmlBytes, string pattern)
        {
            byte[] patternBytes = Encoding.UTF8.GetBytes(pattern);

            for (int i = 0; i <= htmlBytes.Length - patternBytes.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < patternBytes.Length; j++)
                {
                    if (htmlBytes[i + j] != patternBytes[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    return i;
            }
            return -1;
        }

        private void ReplaceFirst(StringBuilder sb, string oldValue, string newValue)
        {
            string s = sb.ToString();
            int pos = s.IndexOf(oldValue);
            if (pos >= 0)
            {
                sb.Remove(pos, oldValue.Length);
                sb.Insert(pos, newValue);
            }
        }

        private void ReplaceAll(StringBuilder sb, string oldValue, string newValue)
        {
            string s = sb.ToString();
            if (s.Contains(oldValue))
            {
                sb.Clear();
                sb.Append(s.Replace(oldValue, newValue));
            }
        }

        private string GenerateHtml(Book book)
        {
            var html = new StringBuilder();
            chapters.Clear();
            chapterCounter = 0;

            html.Append("<html>\r\n");
            html.Append("<head>\r\n");
            html.Append("<guide>\r\n");
            html.Append("<reference title=\"Starts here\" type=\"text\" filepos=0000000000 />\r\n");
            html.Append("</guide>\r\n");
            html.Append("</head>\r\n");
            html.Append("<body>\r\n");

            // Title page
            html.Append("<p width=\"0\" align=\"center\">\r\n");
            html.Append("<font size=\"+2\">\r\n");
            html.Append("<b>");
            html.Append(EscapeHtml(book.Title));
            html.Append("</b>\r\n");
            html.Append("</font>\r\n");
            html.Append("</p>\r\n");

            if (book.Authors?.Count > 0)
            {
                html.Append("<p width=\"0\" align=\"center\">");
                html.Append(EscapeHtml(string.Join(", ", book.Authors)));
                html.Append("</p>\r\n");
            }

            html.Append("<br />\r\n");

            var mainBody = fb2Xml.Root?.Elements(fb2Ns + "body")
                .FirstOrDefault(b => b.Attribute("name")?.Value != "notes");

            if (mainBody != null)
            {
                foreach (var section in mainBody.Elements(fb2Ns + "section"))
                {
                    ProcessSection(section, html, 0);
                }
            }

            // Footnotes section
            if (footnotes.Count > 0)
            {
                html.Append("<mbp:pagebreak />\r\n");
                html.Append("<a id=\"notes_section\" />\r\n");
                html.Append("<div height=\"1em\"></div>\r\n");

                int noteNum = 1;
                foreach (var fn in footnotes)
                {
                    html.Append("<p width=\"0\" align=\"justify\">\r\n");
                    html.Append("<font size=\"-1\">\r\n");
                    html.AppendFormat("<a filepos={0} id=\"{1}\">{2}).</a> {3}\r\n",
                        FILEPOS_PLACEHOLDER, fn.Key, noteNum, EscapeHtml(fn.Value));
                    html.Append("</font>\r\n");
                    html.Append("</p>\r\n");
                    noteNum++;
                }
            }

            // TOC section (also serves as buffer after last footnote)
            html.Append("<mbp:pagebreak />\r\n");
            html.Append("<a id=\"toc_section\" />\r\n");
            html.Append("<div>\r\n");
            html.Append("<div height=\"1em\"></div>\r\n");
            foreach (var ch in chapters)
            {
                html.Append("<div align=\"left\">\r\n");
                // Add blockquotes for indentation based on depth
                for (int i = 0; i <= ch.Item3; i++)
                {
                    html.Append("<blockquote>");
                }
                html.AppendFormat("<a filepos={0}>{1}</a>", FILEPOS_PLACEHOLDER, EscapeHtml(ch.Item1));
                for (int i = 0; i <= ch.Item3; i++)
                {
                    html.Append("</blockquote>");
                }
                html.Append("\r\n</div>\r\n");
            }
            html.Append("</div>\r\n");
            html.Append("<mbp:pagebreak />\r\n");

            html.Append("</body>\r\n");
            html.Append("</html>\r\n");

            return html.ToString();
        }

        private void ProcessSection(XElement section, StringBuilder html, int depth)
        {
            var title = section.Element(fb2Ns + "title");
            if (title != null)
            {
                var titleParagraphs = title.Elements(fb2Ns + "p").ToList();
                var titleText = ExtractText(title);

                if (!string.IsNullOrWhiteSpace(titleText))
                {
                    chapterCounter++;
                    string chapterId = "chapter_" + chapterCounter;
                    chapters.Add(Tuple.Create(titleText, chapterId, depth));

                    html.AppendFormat("<a id=\"{0}\" />\r\n", chapterId);

                    if (titleParagraphs.Count > 1)
                    {
                        // Multiple paragraphs in title
                        foreach (var p in titleParagraphs)
                        {
                            var pText = ExtractText(p);
                            if (!string.IsNullOrWhiteSpace(pText))
                            {
                                html.Append("<p width=\"0\" align=\"center\">\r\n");
                                html.Append("<b>");
                                html.Append(EscapeHtml(pText));
                                html.Append("</b>\r\n");
                                html.Append("</p>\r\n");
                            }
                        }
                    }
                    else
                    {
                        // Single paragraph or plain text
                        html.Append("<p width=\"0\" align=\"center\">\r\n");
                        html.Append("<b>");
                        html.Append(EscapeHtml(titleText));
                        html.Append("</b>\r\n");
                        html.Append("</p>\r\n");
                    }
                    html.Append("<br />\r\n");
                }
            }

            foreach (var elem in section.Elements())
            {
                switch (elem.Name.LocalName)
                {
                    case "p":
                        ProcessParagraph(elem, html);
                        break;

                    case "empty-line":
                        html.Append("<br />\r\n");
                        break;

                    case "image":
                        ProcessImage(elem, html);
                        break;

                    case "section":
                        ProcessSection(elem, html, depth + 1);
                        break;
                }
            }

            html.Append("<mbp:pagebreak />\r\n");
        }

        private void ProcessParagraph(XElement paragraph, StringBuilder html)
        {
            var sb = new StringBuilder();

            foreach (var node in paragraph.Nodes())
            {
                if (node is XText textNode)
                {
                    sb.Append(EscapeHtml(textNode.Value));
                }
                else if (node is XElement elemNode)
                {
                    switch (elemNode.Name.LocalName)
                    {
                        case "a":
                            var href = elemNode.Attribute(xlinkNs + "href")?.Value;
                            if (!string.IsNullOrEmpty(href))
                            {
                                href = href.TrimStart('#');
                                if (footnotes.ContainsKey(href))
                                {
                                    var fnList = footnotes.Keys.ToList();
                                    int noteNum = fnList.IndexOf(href) + 1;

                                    sb.AppendFormat(" <a id=\"back_{0}\" filepos={1}>",
                                        href, FILEPOS_PLACEHOLDER);
                                    sb.Append("<font size=\"-1\">");
                                    sb.AppendFormat("<sup>[{0}]</sup>", noteNum);
                                    sb.Append("</font>");
                                    sb.Append("</a>");
                                }
                                else
                                {
                                    sb.Append(EscapeHtml(ExtractText(elemNode)));
                                }
                            }
                            else
                            {
                                sb.Append(EscapeHtml(ExtractText(elemNode)));
                            }
                            break;

                        case "strong":
                            sb.AppendFormat("<b>{0}</b>", EscapeHtml(ExtractText(elemNode)));
                            break;

                        case "emphasis":
                            sb.AppendFormat("<i>{0}</i>", EscapeHtml(ExtractText(elemNode)));
                            break;

                        case "image":
                            var imgHref = elemNode.Attribute(xlinkNs + "href")?.Value;
                            if (!string.IsNullOrEmpty(imgHref))
                            {
                                imgHref = imgHref.TrimStart('#');
                                if (imageIndices.ContainsKey(imgHref))
                                {
                                    int recindex = imageIndices[imgHref];
                                    sb.AppendFormat("<img recindex=\"{0:D5}\" />", recindex);
                                }
                            }
                            break;

                        default:
                            sb.Append(EscapeHtml(ExtractText(elemNode)));
                            break;
                    }
                }
            }

            var text = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                html.Append("<p>");
                html.Append(text);
                html.Append("</p>\r\n");
            }
        }

        private void ProcessImage(XElement imageElem, StringBuilder html)
        {
            var href = imageElem.Attribute(xlinkNs + "href")?.Value;
            if (string.IsNullOrEmpty(href)) return;

            href = href.TrimStart('#');
            if (imageIndices.ContainsKey(href))
            {
                int recindex = imageIndices[href];
                html.Append("<br />\r\n");
                html.Append("<p width=\"0\" align=\"center\">\r\n");
                html.AppendFormat("<img recindex=\"{0:D5}\" />\r\n", recindex);
                html.Append("</p>\r\n");
                html.Append("<br />\r\n");
            }
        }

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

        #region MOBI Writing

        private void WriteMobiFile(Stream stream, byte[] htmlData, Book book)
        {
            var textRecords = new List<byte[]>();
            int offset = 0;
            while (offset < htmlData.Length)
            {
                int size = Math.Min(RECORD_SIZE, htmlData.Length - offset);
                byte[] record = new byte[size];
                Array.Copy(htmlData, offset, record, 0, size);
                textRecords.Add(record);
                offset += size;
            }

            var imageRecords = new List<byte[]>(images.Values);

            int firstImageRecordIndex = imageRecords.Count > 0 ? 1 + textRecords.Count : 0;
            int flisRecordIndex = 1 + textRecords.Count + imageRecords.Count;
            int fcisRecordIndex = flisRecordIndex + 1;
            int eofRecordIndex = fcisRecordIndex + 1;
            int totalRecords = eofRecordIndex + 1;

            byte[] record0 = BuildRecord0(book, htmlData.Length, textRecords.Count,
                                          firstImageRecordIndex, flisRecordIndex, fcisRecordIndex);
            byte[] flisRecord = BuildFLISRecord();
            byte[] fcisRecord = BuildFCISRecord(htmlData.Length);
            byte[] eofRecord = new byte[] { 0xE9, 0x8E, 0x0D, 0x0A };

            int headerEnd = 78 + (totalRecords * 8) + 2;
            var recordOffsets = new List<int>();
            int currentOffset = headerEnd;

            recordOffsets.Add(currentOffset);
            currentOffset += record0.Length;

            foreach (var rec in textRecords)
            {
                recordOffsets.Add(currentOffset);
                currentOffset += rec.Length;
            }

            foreach (var img in imageRecords)
            {
                recordOffsets.Add(currentOffset);
                currentOffset += img.Length;
            }

            recordOffsets.Add(currentOffset);
            currentOffset += flisRecord.Length;

            recordOffsets.Add(currentOffset);
            currentOffset += fcisRecord.Length;

            recordOffsets.Add(currentOffset);

            WritePalmDbHeader(stream, book.Title, totalRecords);

            for (int i = 0; i < totalRecords; i++)
            {
                WriteInt32BE(stream, recordOffsets[i]);
                stream.WriteByte(0);
                int uniqueId = 2 * i;
                stream.WriteByte((byte)((uniqueId >> 16) & 0xFF));
                stream.WriteByte((byte)((uniqueId >> 8) & 0xFF));
                stream.WriteByte((byte)(uniqueId & 0xFF));
            }

            stream.WriteByte(0);
            stream.WriteByte(0);

            stream.Write(record0, 0, record0.Length);

            foreach (var rec in textRecords)
                stream.Write(rec, 0, rec.Length);

            foreach (var img in imageRecords)
                stream.Write(img, 0, img.Length);

            stream.Write(flisRecord, 0, flisRecord.Length);
            stream.Write(fcisRecord, 0, fcisRecord.Length);
            stream.Write(eofRecord, 0, eofRecord.Length);
        }

        private void WritePalmDbHeader(Stream stream, string title, int recordCount)
        {
            string safeName = new string(title.Where(c => c < 128 && c >= 32).ToArray());
            if (safeName.Length > 31) safeName = safeName.Substring(0, 31);
            byte[] nameBytes = Encoding.ASCII.GetBytes(safeName);
            stream.Write(nameBytes, 0, nameBytes.Length);
            for (int i = nameBytes.Length; i < 32; i++)
                stream.WriteByte(0);

            WriteInt16BE(stream, 0);
            WriteInt16BE(stream, 0);

            int palmTime = (int)((DateTime.UtcNow - new DateTime(1904, 1, 1)).TotalSeconds);
            WriteInt32BE(stream, palmTime);
            WriteInt32BE(stream, palmTime);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, 0);

            stream.Write(Encoding.ASCII.GetBytes("BOOK"), 0, 4);
            stream.Write(Encoding.ASCII.GetBytes("MOBI"), 0, 4);

            WriteInt32BE(stream, (2 * recordCount) - 1);
            WriteInt32BE(stream, 0);
            WriteInt16BE(stream, recordCount);
        }

        private byte[] BuildRecord0(Book book, int textLength, int textRecordCount,
                                    int firstImageRecordIndex, int flisRecordIndex, int fcisRecordIndex)
        {
            using (var ms = new MemoryStream())
            {
                WriteInt16BE(ms, 1);
                WriteInt16BE(ms, 0);
                WriteInt32BE(ms, textLength);
                WriteInt16BE(ms, textRecordCount);
                WriteInt16BE(ms, RECORD_SIZE);
                WriteInt32BE(ms, 0);

                long mobiStart = ms.Position;

                ms.Write(Encoding.ASCII.GetBytes("MOBI"), 0, 4);
                WriteInt32BE(ms, 232);
                WriteInt32BE(ms, 2);
                WriteInt32BE(ms, 65001);
                WriteInt32BE(ms, new Random().Next());
                WriteInt32BE(ms, 6);

                for (int i = 0; i < 10; i++)
                    WriteInt32BE(ms, NULL_INDEX);

                WriteInt32BE(ms, 1 + textRecordCount);

                long fullNameOffsetPos = ms.Position;
                WriteInt32BE(ms, 0);

                int titleLen = Encoding.UTF8.GetByteCount(book.Title);
                WriteInt32BE(ms, titleLen);
                WriteInt32BE(ms, 9);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 6);

                WriteInt32BE(ms, firstImageRecordIndex > 0 ? firstImageRecordIndex : NULL_INDEX);

                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);

                WriteInt32BE(ms, 0x40);

                ms.Write(new byte[32], 0, 32);

                WriteInt32BE(ms, NULL_INDEX);
                WriteInt32BE(ms, NULL_INDEX);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);

                ms.Write(new byte[8], 0, 8);

                WriteInt16BE(ms, 1);
                WriteInt16BE(ms, textRecordCount);

                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, fcisRecordIndex);
                WriteInt32BE(ms, 1);
                WriteInt32BE(ms, flisRecordIndex);
                WriteInt32BE(ms, 1);

                ms.Write(new byte[8], 0, 8);

                WriteInt32BE(ms, NULL_INDEX);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, NULL_INDEX);

                WriteInt32BE(ms, 1);
                WriteInt32BE(ms, NULL_INDEX);

                long written = ms.Position - mobiStart;
                if (written < 232)
                    ms.Write(new byte[232 - written], 0, (int)(232 - written));

                byte[] exth = BuildExthHeader(book, firstImageRecordIndex);
                ms.Write(exth, 0, exth.Length);

                int fullNameOffset = (int)ms.Position;
                long pos = ms.Position;
                ms.Position = fullNameOffsetPos;
                WriteInt32BE(ms, fullNameOffset);
                ms.Position = pos;

                byte[] titleBytes = Encoding.UTF8.GetBytes(book.Title);
                ms.Write(titleBytes, 0, titleBytes.Length);

                int pad = (4 - ((int)ms.Length % 4)) % 4;
                if (pad > 0) ms.Write(new byte[pad], 0, pad);
                ms.Write(new byte[4], 0, 4);

                return ms.ToArray();
            }
        }

        private byte[] BuildExthHeader(Book book, int firstImageRecordIndex)
        {
            using (var ms = new MemoryStream())
            {
                var records = new List<byte[]>();

                if (book.Authors?.Count > 0)
                    records.Add(BuildExthRecord(100, Encoding.UTF8.GetBytes(book.Authors[0])));

                records.Add(BuildExthRecord(503, Encoding.UTF8.GetBytes(book.Title)));
                records.Add(BuildExthRecord(501, Encoding.ASCII.GetBytes("EBOK")));

                records.Add(BuildExthRecord(204, GetInt32Bytes(201)));
                records.Add(BuildExthRecord(205, GetInt32Bytes(2)));
                records.Add(BuildExthRecord(206, GetInt32Bytes(9)));
                records.Add(BuildExthRecord(207, GetInt32Bytes(0)));

                if (firstImageRecordIndex > 0 && coverRecindex > 0)
                {
                    int coverOffset = coverRecindex - 1;
                    records.Add(BuildExthRecord(201, GetInt32Bytes(coverOffset)));
                    records.Add(BuildExthRecord(203, GetInt32Bytes(0)));
                }

                int dataSize = 0;
                foreach (var r in records) dataSize += r.Length;

                int headerSize = 12 + dataSize;
                int padding = (4 - (headerSize % 4)) % 4;

                ms.Write(Encoding.ASCII.GetBytes("EXTH"), 0, 4);
                WriteInt32BE(ms, headerSize + padding);
                WriteInt32BE(ms, records.Count);

                foreach (var r in records)
                    ms.Write(r, 0, r.Length);

                if (padding > 0)
                    ms.Write(new byte[padding], 0, padding);

                return ms.ToArray();
            }
        }

        private byte[] BuildExthRecord(int type, byte[] data)
        {
            byte[] rec = new byte[8 + data.Length];
            WriteInt32BE(rec, 0, type);
            WriteInt32BE(rec, 4, 8 + data.Length);
            Array.Copy(data, 0, rec, 8, data.Length);
            return rec;
        }

        private byte[] GetInt32Bytes(int value)
        {
            return new byte[]
            {
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            };
        }

        private byte[] BuildFLISRecord()
        {
            return new byte[]
            {
                0x46, 0x4C, 0x49, 0x53,
                0x00, 0x00, 0x00, 0x08,
                0x00, 0x41, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0xFF,
                0x00, 0x01, 0x00, 0x03,
                0x00, 0x00, 0x00, 0x03,
                0x00, 0x00, 0x00, 0x01,
                0xFF, 0xFF, 0xFF, 0xFF
            };
        }

        private byte[] BuildFCISRecord(int textLength)
        {
            using (var ms = new MemoryStream())
            {
                ms.Write(Encoding.ASCII.GetBytes("FCIS"), 0, 4);
                WriteInt32BE(ms, 20);
                WriteInt32BE(ms, 16);
                WriteInt32BE(ms, 1);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, textLength);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 32);
                WriteInt32BE(ms, 8);
                WriteInt16BE(ms, 1);
                WriteInt16BE(ms, 1);
                WriteInt32BE(ms, 0);
                return ms.ToArray();
            }
        }

        #endregion

        #region Binary Helpers

        private void WriteInt16BE(Stream stream, int value)
        {
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }

        private void WriteInt32BE(Stream stream, int value)
        {
            stream.WriteByte((byte)((value >> 24) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }

        private void WriteInt32BE(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(value & 0xFF);
        }

        #endregion
    }
}