/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * FB2 to MOBI converter with two-pass HTML generation
 * Uses filepos byte offsets for internal links (Kindle compatible)
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
        private XDocument fb2Xml = null;
        private XNamespace fb2Ns;
        private readonly XNamespace xlinkNs = "http://www.w3.org/1999/xlink";
        private readonly Dictionary<string, byte[]> images = new Dictionary<string, byte[]>();
        private readonly Dictionary<string, string> footnotes = new Dictionary<string, string>();

        // Two-pass link resolution
        private MemoryStream htmlBuffer;
        private Dictionary<string, long> anchorOffsets;
        private List<(long placeholderPos, string targetId)> linkPlaceholders;

        private const string FILEPOS_PLACEHOLDER = "0000000000";

        public bool ConvertToMobiStream(Book book, Stream fb2Stream, Stream mobiStream)
        {
            try
            {
                if (!LoadFB2Xml(fb2Stream))
                {
                    Log.WriteLine(LogLevel.Error, "Failed to parse FB2: {0}", book.FileName);
                    return false;
                }

                ExtractFootnotes();
                byte[] htmlBytes = GenerateHtmlWithLinks(book);

                WriteMobiFile(mobiStream, htmlBytes, book);

                Log.WriteLine(LogLevel.Info, "Created MOBI for: {0} ({1} footnotes)",
                    book.FileName, footnotes.Count);
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "MOBI conversion error: {0}", ex.Message);
                return false;
            }
        }

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

        private int ExtractNumber(string id)
        {
            var digits = new string(id.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out int num))
                return num;
            return int.MaxValue;
        }

        /// <summary>
        /// Two-pass HTML generation with filepos link resolution
        /// </summary>
        private byte[] GenerateHtmlWithLinks(Book book)
        {
            htmlBuffer = new MemoryStream();
            anchorOffsets = new Dictionary<string, long>();
            linkPlaceholders = new List<(long, string)>();

            // Pass 1: Generate HTML with placeholders
            GenerateHtmlContent(book);

            // Pass 2: Resolve links
            byte[] result = htmlBuffer.ToArray();
            ResolveLinkPlaceholders(result);

            return result;
        }

        private void Write(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            htmlBuffer.Write(bytes, 0, bytes.Length);
        }

        private void WriteLine(string text)
        {
            Write(text);
            Write("\n");
        }

        private void WriteAnchor(string id)
        {
            // Record byte position for this anchor
            anchorOffsets[id] = htmlBuffer.Position;
        }

        private void WriteLink(string targetId, string linkText)
        {
            // filepos without quotes (as per MOBI spec)
            Write("<a filepos=");
            linkPlaceholders.Add((htmlBuffer.Position, targetId));
            Write(FILEPOS_PLACEHOLDER);
            Write(">");
            Write(linkText);
            Write("</a>");
        }

        private void ResolveLinkPlaceholders(byte[] html)
        {
            foreach (var (placeholderPos, targetId) in linkPlaceholders)
            {
                if (anchorOffsets.TryGetValue(targetId, out long targetOffset))
                {
                    string offsetStr = targetOffset.ToString("D10");
                    byte[] offsetBytes = Encoding.ASCII.GetBytes(offsetStr);
                    Array.Copy(offsetBytes, 0, html, placeholderPos, 10);
                }
                else
                {
                    Log.WriteLine(LogLevel.Warning, "Unresolved link target: {0}", targetId);
                }
            }
        }

        private void GenerateHtmlContent(Book book)
        {
            WriteLine("<!DOCTYPE html>");
            WriteLine("<html>");
            WriteLine("<head>");
            WriteLine("<meta charset=\"UTF-8\"/>");
            Write("<title>");
            Write(EscapeHtml(book.Title));
            WriteLine("</title>");
            WriteLine("</head>");
            WriteLine("<body>");

            // Book title
            Write("<p width=\"0\" align=\"center\"><font size=\"+2\"><b>");
            Write(EscapeHtml(book.Title));
            WriteLine("</b></font></p>");

            // Authors
            if (book.Authors?.Count > 0)
            {
                Write("<p width=\"0\" align=\"center\">");
                Write(EscapeHtml(string.Join(", ", book.Authors)));
                WriteLine("</p>");
            }
            WriteLine("<br/>");
            WriteLine("<hr/>");

            // Main body
            var mainBody = fb2Xml.Root?.Elements(fb2Ns + "body")
                .FirstOrDefault(b => b.Attribute("name")?.Value != "notes");

            if (mainBody != null)
            {
                var topSections = mainBody.Elements(fb2Ns + "section").ToList();
                bool foundFirstTitled = false;
                for (int i = 0; i < topSections.Count; i++)
                {
                    bool hasTitle = topSections[i].Element(fb2Ns + "title") != null;
                    bool needsPagebreak = hasTitle && foundFirstTitled;
                    if (hasTitle) foundFirstTitled = true;
                    ProcessSection(topSections[i], needsPagebreak);
                }
            }

            // Footnotes section
            if (footnotes.Count > 0)
            {
                var sortedFootnotes = footnotes.OrderBy(fn => ExtractNumber(fn.Key)).ToList();

                foreach (var fn in sortedFootnotes)
                {
                    WriteLine("<mbp:pagebreak/>");

                    int noteNum = ExtractNumber(fn.Key);

                    // Padding to absorb Kindle's display quirk (shows ~10 chars before filepos)
                    Write("          ");
                    WriteAnchor("note_" + fn.Key);
                    WriteLine("");

                    Write("<p width=\"0\" align=\"center\"><b>[");
                    Write(noteNum.ToString());
                    WriteLine("]</b></p>");
                    WriteLine("<br/>");

                    Write("<p>");
                    Write(EscapeHtml(fn.Value));
                    WriteLine("</p>");
                    WriteLine("<br/>");

                    // Back link
                    Write("<center><font size=\"+4\"><b>");
                    WriteLink("back_" + fn.Key, "←");
                    WriteLine("</b></font></center>");
                }
            }

            WriteLine("</body>");
            WriteLine("</html>");
        }

        private void ProcessSection(XElement section, bool needsPagebreak)
        {
            if (needsPagebreak)
            {
                WriteLine("<mbp:pagebreak/>");
            }

            var title = section.Element(fb2Ns + "title");
            if (title != null)
            {
                var paragraphs = title.Elements(fb2Ns + "p").ToList();
                if (paragraphs.Count > 0)
                {
                    foreach (var p in paragraphs)
                    {
                        var text = ExtractText(p);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            Write("<p width=\"0\" align=\"center\"><b>");
                            Write(EscapeHtml(text));
                            WriteLine("</b></p>");
                        }
                    }
                }
                else
                {
                    var titleText = ExtractText(title);
                    if (!string.IsNullOrWhiteSpace(titleText))
                    {
                        Write("<p width=\"0\" align=\"center\"><b>");
                        Write(EscapeHtml(titleText));
                        WriteLine("</b></p>");
                    }
                }
                WriteLine("<br/>");
            }

            var childSections = new List<XElement>();

            foreach (var elem in section.Elements())
            {
                switch (elem.Name.LocalName)
                {
                    case "p":
                        ProcessParagraph(elem);
                        break;

                    case "empty-line":
                        WriteLine("<br/>");
                        break;

                    case "section":
                        childSections.Add(elem);
                        break;
                }
            }

            // Process nested sections
            bool foundFirstTitled = false;
            for (int i = 0; i < childSections.Count; i++)
            {
                bool hasTitle = childSections[i].Element(fb2Ns + "title") != null;
                bool childNeedsPagebreak = hasTitle && foundFirstTitled;
                if (hasTitle) foundFirstTitled = true;
                ProcessSection(childSections[i], childNeedsPagebreak);
            }

            WriteLine("<br/>");
        }

        private void ProcessParagraph(XElement paragraph)
        {
            var content = new StringBuilder();
            var footnoteIds = new List<string>();

            // First pass: collect ALL footnote IDs in paragraph
            foreach (var node in paragraph.Nodes())
            {
                if (node is XElement elemNode && elemNode.Name.LocalName == "a")
                {
                    var href = elemNode.Attribute(xlinkNs + "href")?.Value;
                    if (!string.IsNullOrEmpty(href))
                    {
                        href = href.TrimStart('#');
                        if (footnotes.ContainsKey(href))
                        {
                            footnoteIds.Add(href);
                        }
                    }
                }
            }

            // Place back-anchors for ALL footnotes at paragraph start
            foreach (var fnId in footnoteIds)
            {
                Write("          ");
                WriteAnchor("back_" + fnId);
                WriteLine("");
            }

            // Second pass: build paragraph content
            Write("<p>");
            foreach (var node in paragraph.Nodes())
            {
                if (node is XText textNode)
                {
                    content.Append(EscapeHtml(textNode.Value));
                }
                else if (node is XElement elemNode)
                {
                    if (elemNode.Name.LocalName == "a")
                    {
                        var href = elemNode.Attribute(xlinkNs + "href")?.Value;
                        if (!string.IsNullOrEmpty(href))
                        {
                            href = href.TrimStart('#');
                            if (footnotes.ContainsKey(href))
                            {
                                // Flush content before link
                                if (content.Length > 0)
                                {
                                    Write(content.ToString());
                                    content.Clear();
                                }

                                int noteNum = ExtractNumber(href);
                                Write("<sup>");
                                WriteLink("note_" + href, "[" + noteNum + "]");
                                Write("</sup>");
                            }
                            else
                            {
                                content.Append(EscapeHtml(ExtractText(elemNode)));
                            }
                        }
                        else
                        {
                            content.Append(EscapeHtml(ExtractText(elemNode)));
                        }
                    }
                    else
                    {
                        content.Append(EscapeHtml(ExtractText(elemNode)));
                    }
                }
            }

            // Write remaining content
            var text = content.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                Write(text);
            }
            WriteLine("</p>");
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

        private void WriteMobiFile(Stream stream, byte[] htmlData, Book book)
        {
            var textRecords = new List<byte[]>();
            int offset = 0;
            while (offset < htmlData.Length)
            {
                int size = Math.Min(4096, htmlData.Length - offset);
                byte[] record = new byte[size];
                Array.Copy(htmlData, offset, record, 0, size);
                textRecords.Add(record);
                offset += size;
            }

            int totalRecords = 1 + textRecords.Count + 3;

            WritePalmDbHeader(stream, book.Title, totalRecords);

            int currentOffset = 78 + (totalRecords * 8) + 2;
            int headerSize = CalculateHeaderSize(book);

            WriteInt32BE(stream, currentOffset);
            stream.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
            currentOffset += headerSize;

            foreach (var rec in textRecords)
            {
                WriteInt32BE(stream, currentOffset);
                stream.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
                currentOffset += rec.Length;
            }

            int flisIndex = 1 + textRecords.Count;
            int fcisIndex = flisIndex + 1;

            WriteInt32BE(stream, currentOffset);
            stream.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
            currentOffset += 36;

            WriteInt32BE(stream, currentOffset);
            stream.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
            currentOffset += 52;

            WriteInt32BE(stream, currentOffset);
            stream.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);

            stream.Write(new byte[] { 0, 0 }, 0, 2);

            long record0Start = stream.Position;

            WriteInt16BE(stream, 1);
            WriteInt16BE(stream, 0);
            WriteInt32BE(stream, htmlData.Length);
            WriteInt16BE(stream, textRecords.Count);
            WriteInt16BE(stream, 4096);
            WriteInt32BE(stream, 0);

            WriteMobiHeader(stream, book, textRecords.Count, flisIndex, fcisIndex);
            WriteExthHeader(stream, book);

            byte[] titleBytes = Encoding.UTF8.GetBytes(book.Title);
            stream.Write(titleBytes, 0, titleBytes.Length);

            long headerWritten = stream.Position - record0Start;
            if (headerWritten < headerSize)
            {
                stream.Write(new byte[headerSize - headerWritten], 0, (int)(headerSize - headerWritten));
            }

            foreach (var record in textRecords)
            {
                stream.Write(record, 0, record.Length);
            }

            WriteFLISRecord(stream);
            WriteFCISRecord(stream, htmlData.Length);
            WriteEOFRecord(stream);
        }

        private int CalculateHeaderSize(Book book)
        {
            int size = 16 + 232;

            int exthSize = 12;

            if (book.Authors?.Count > 0)
            {
                exthSize += 8 + Encoding.UTF8.GetByteCount(book.Authors[0]);
            }

            exthSize += 8 + Encoding.UTF8.GetByteCount(book.Title);
            exthSize += 8 + 4;
            exthSize += 8 + 4;
            exthSize += 8 + 4;
            exthSize += 8 + 4;

            int padding = (4 - (exthSize % 4)) % 4;
            exthSize += padding;

            size += exthSize;
            size += Encoding.UTF8.GetByteCount(book.Title);

            int remainder = size % 4;
            if (remainder != 0)
            {
                size += (4 - remainder);
            }

            return size;
        }

        private void WritePalmDbHeader(Stream stream, string name, int recordCount)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            int nameLen = Math.Min(nameBytes.Length, 31);

            stream.Write(nameBytes, 0, nameLen);
            stream.Write(new byte[32 - nameLen], 0, 32 - nameLen);

            WriteInt16BE(stream, 0);
            WriteInt16BE(stream, 0);

            uint palmDate = (uint)(DateTime.Now - new DateTime(1904, 1, 1)).TotalSeconds;
            WriteInt32BE(stream, (int)palmDate);
            WriteInt32BE(stream, (int)palmDate);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, 0);

            stream.Write(Encoding.ASCII.GetBytes("BOOK"), 0, 4);
            stream.Write(Encoding.ASCII.GetBytes("MOBI"), 0, 4);

            WriteInt32BE(stream, (int)(DateTime.Now.Ticks & 0xFFFFFFFF));
            WriteInt32BE(stream, 0);
            WriteInt16BE(stream, recordCount);
        }

        private void WriteMobiHeader(Stream stream, Book book, int recordCount,
            int flisIndex, int fcisIndex)
        {
            long mobiStart = stream.Position;

            stream.Write(Encoding.ASCII.GetBytes("MOBI"), 0, 4);
            WriteInt32BE(stream, 232);
            WriteInt32BE(stream, 2);
            WriteInt32BE(stream, 65001);
            WriteInt32BE(stream, (int)DateTime.Now.Ticks);
            WriteInt32BE(stream, 6);
            WriteInt32BE(stream, -1);
            WriteInt32BE(stream, -1);
            WriteInt32BE(stream, -1);
            WriteInt32BE(stream, -1);

            for (int i = 0; i < 6; i++)
                WriteInt32BE(stream, -1);

            WriteInt32BE(stream, -1);

            int fullNameOffset = 16 + 232;
            int exthSize = 12;
            if (book.Authors?.Count > 0)
            {
                exthSize += 8 + Encoding.UTF8.GetByteCount(book.Authors[0]);
            }
            exthSize += 8 + Encoding.UTF8.GetByteCount(book.Title);
            exthSize += 8 + 4;
            exthSize += 8 + 4;
            exthSize += 8 + 4;
            exthSize += 8 + 4;
            int padding = (4 - (exthSize % 4)) % 4;
            exthSize += padding;
            fullNameOffset += exthSize;

            WriteInt32BE(stream, fullNameOffset);
            WriteInt32BE(stream, Encoding.UTF8.GetByteCount(book.Title));
            WriteInt32BE(stream, 9);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, 6);
            WriteInt32BE(stream, -1);  // No images
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, 0x40);

            stream.Write(new byte[32], 0, 32);

            WriteInt32BE(stream, -1);
            WriteInt32BE(stream, -1);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, 0);

            stream.Write(new byte[8], 0, 8);

            WriteInt16BE(stream, 1);
            WriteInt16BE(stream, recordCount);

            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, fcisIndex);
            WriteInt32BE(stream, 1);
            WriteInt32BE(stream, flisIndex);
            WriteInt32BE(stream, 1);

            stream.Write(new byte[8], 0, 8);

            WriteInt32BE(stream, -1);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, -1);

            long written = stream.Position - mobiStart;
            if (written < 232)
            {
                stream.Write(new byte[232 - written], 0, (int)(232 - written));
            }
        }

        private void WriteExthHeader(Stream stream, Book book)
        {
            var records = new List<byte[]>();

            if (book.Authors?.Count > 0)
            {
                byte[] authorData = Encoding.UTF8.GetBytes(book.Authors[0]);
                byte[] authorRecord = new byte[8 + authorData.Length];
                WriteInt32BE(authorRecord, 0, 100);
                WriteInt32BE(authorRecord, 4, 8 + authorData.Length);
                Array.Copy(authorData, 0, authorRecord, 8, authorData.Length);
                records.Add(authorRecord);
            }

            byte[] titleData = Encoding.UTF8.GetBytes(book.Title);
            byte[] titleRecord = new byte[8 + titleData.Length];
            WriteInt32BE(titleRecord, 0, 503);
            WriteInt32BE(titleRecord, 4, 8 + titleData.Length);
            Array.Copy(titleData, 0, titleRecord, 8, titleData.Length);
            records.Add(titleRecord);

            byte[] doctypeData = Encoding.UTF8.GetBytes("EBOK");
            byte[] doctypeRecord = new byte[8 + doctypeData.Length];
            WriteInt32BE(doctypeRecord, 0, 501);
            WriteInt32BE(doctypeRecord, 4, 8 + doctypeData.Length);
            Array.Copy(doctypeData, 0, doctypeRecord, 8, doctypeData.Length);
            records.Add(doctypeRecord);

            byte[] creatorSoftRecord = new byte[12];
            WriteInt32BE(creatorSoftRecord, 0, 204);
            WriteInt32BE(creatorSoftRecord, 4, 12);
            WriteInt32BE(creatorSoftRecord, 8, 200);
            records.Add(creatorSoftRecord);

            byte[] creatorMajorRecord = new byte[12];
            WriteInt32BE(creatorMajorRecord, 0, 205);
            WriteInt32BE(creatorMajorRecord, 4, 12);
            WriteInt32BE(creatorMajorRecord, 8, 2);
            records.Add(creatorMajorRecord);

            byte[] creatorMinorRecord = new byte[12];
            WriteInt32BE(creatorMinorRecord, 0, 206);
            WriteInt32BE(creatorMinorRecord, 4, 12);
            WriteInt32BE(creatorMinorRecord, 8, 9);
            records.Add(creatorMinorRecord);

            byte[] creatorBuildRecord = new byte[12];
            WriteInt32BE(creatorBuildRecord, 0, 207);
            WriteInt32BE(creatorBuildRecord, 4, 12);
            WriteInt32BE(creatorBuildRecord, 8, 0);
            records.Add(creatorBuildRecord);

            int exthSize = 12;
            foreach (var rec in records)
            {
                exthSize += rec.Length;
            }

            int exthPadding = (4 - (exthSize % 4)) % 4;
            exthSize += exthPadding;

            stream.Write(Encoding.ASCII.GetBytes("EXTH"), 0, 4);
            WriteInt32BE(stream, exthSize);
            WriteInt32BE(stream, records.Count);

            foreach (var rec in records)
            {
                stream.Write(rec, 0, rec.Length);
            }

            if (exthPadding > 0)
            {
                stream.Write(new byte[exthPadding], 0, exthPadding);
            }
        }

        private void WriteFLISRecord(Stream stream)
        {
            stream.Write(Encoding.ASCII.GetBytes("FLIS"), 0, 4);
            WriteInt32BE(stream, 8);
            WriteInt16BE(stream, 65);
            WriteInt16BE(stream, 0);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, -1);
            WriteInt16BE(stream, 1);
            WriteInt16BE(stream, 3);
            WriteInt32BE(stream, 3);
            WriteInt32BE(stream, 1);
            WriteInt32BE(stream, -1);
        }

        private void WriteFCISRecord(Stream stream, int textLength)
        {
            stream.Write(Encoding.ASCII.GetBytes("FCIS"), 0, 4);
            WriteInt32BE(stream, 20);
            WriteInt32BE(stream, 16);
            WriteInt32BE(stream, 1);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, textLength);
            WriteInt32BE(stream, 0);
            WriteInt32BE(stream, 32);
            WriteInt32BE(stream, 8);
            WriteInt16BE(stream, 1);
            WriteInt16BE(stream, 1);
            WriteInt32BE(stream, 0);
        }

        private void WriteEOFRecord(Stream stream)
        {
            stream.WriteByte(0xE9);
            stream.WriteByte(0x8E);
            stream.WriteByte(0x0D);
            stream.WriteByte(0x0A);
        }

        private void WriteInt16BE(Stream stream, int value)
        {
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private void WriteInt32BE(Stream stream, int value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private void WriteInt32BE(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }
    }
}