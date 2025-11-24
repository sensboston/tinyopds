/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * FB2 to MOBI converter with proper Kindle compatibility
 * Creates valid MOBI files with mandatory EXTH records for old Kindle devices
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

        /// <summary>
        /// Convert FB2 stream to MOBI stream
        /// </summary>
        public bool ConvertToMobiStream(Book book, Stream fb2Stream, Stream mobiStream)
        {
            try
            {
                if (!LoadFB2Xml(fb2Stream))
                {
                    Log.WriteLine(LogLevel.Error, "Failed to parse FB2: {0}", book.FileName);
                    return false;
                }

                //ExtractImages();
                ExtractFootnotes();
                string htmlContent = GenerateHtml(book);
                byte[] htmlBytes = Encoding.UTF8.GetBytes(htmlContent);

                WriteMobiFile(mobiStream, htmlBytes, book);

                Log.WriteLine(LogLevel.Info, "Created MOBI for: {0} ({1} images, {2} footnotes)",
                    book.FileName, images.Count, footnotes.Count);
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
                catch
                {
                }
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

        private string GenerateHtml(Book book)
        {
            var html = new StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset=\"UTF-8\"/>");
            html.AppendFormat("<title>{0}</title>\n", EscapeHtml(book.Title));
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: serif; margin: 1em; line-height: 1.4; }");
            html.AppendLine("h1 { text-align: center; margin-bottom: 0.5em; font-size: 1.3em; }");
            html.AppendLine("h3 { text-align: center; margin-top: 0; color: #666; font-size: 1.1em; }");
            html.AppendLine("h2 { margin-top: 1.5em; margin-bottom: 0.5em; font-size: 1.1em; }");
            html.AppendLine("p { margin: 0.5em 0; text-indent: 1.5em; }");
            html.AppendLine("img { max-width: 100%; height: auto; display: block; margin: 1em auto; }");
            html.AppendLine(".footnote-ref { vertical-align: super; font-size: 0.8em; }");
            html.AppendLine(".footnote { font-size: 0.9em; margin: 0.3em 0; padding-left: 1.5em; }");
            html.AppendLine(".footnotes { margin-top: 2em; border-top: 1px solid #ccc; padding-top: 1em; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            html.AppendFormat("<h1>{0}</h1>\n", EscapeHtml(book.Title));
            if (book.Authors?.Count > 0)
            {
                html.AppendFormat("<h3>{0}</h3>\n", EscapeHtml(string.Join(", ", book.Authors)));
            }

            html.AppendLine("<hr/>");

            var mainBody = fb2Xml.Root?.Elements(fb2Ns + "body")
                .FirstOrDefault(b => b.Attribute("name")?.Value != "notes");

            if (mainBody != null)
            {
                foreach (var section in mainBody.Elements(fb2Ns + "section"))
                {
                    ProcessSection(section, html);
                }
            }

            if (footnotes.Count > 0)
            {
                html.AppendLine("<div class=\"footnotes\">");
                html.AppendLine("<h2>Notes:</h2>");
                foreach (var fn in footnotes.OrderBy(kvp => kvp.Key))
                {
                    html.AppendFormat("<p class=\"footnote\" id=\"{0}\">[{1}] {2}</p>\n",
                        fn.Key, fn.Key, EscapeHtml(fn.Value));
                }
                html.AppendLine("</div>");
            }

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private void ProcessSection(XElement section, StringBuilder html)
        {
            var title = section.Element(fb2Ns + "title");
            if (title != null)
            {
                var titleText = ExtractText(title);
                if (!string.IsNullOrWhiteSpace(titleText))
                {
                    html.AppendFormat("<h3>{0}</h3>\n", EscapeHtml(titleText));
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
                        html.AppendLine("<br/>");
                        break;

                    case "image":
                        ProcessImage(elem, html);
                        break;

                    case "section":
                        ProcessSection(elem, html);
                        break;
                }
            }

            html.AppendLine("<mbp:pagebreak/>");
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
                    if (elemNode.Name.LocalName == "a")
                    {
                        var href = elemNode.Attribute(xlinkNs + "href")?.Value;
                        if (!string.IsNullOrEmpty(href))
                        {
                            href = href.TrimStart('#');
                            if (footnotes.ContainsKey(href))
                            {
                                sb.AppendFormat("<a class=\"footnote-ref\" href=\"#{0}\">{1}</a>",
                                    href, EscapeHtml(ExtractText(elemNode)));
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
                    }
                    else
                    {
                        sb.Append(EscapeHtml(ExtractText(elemNode)));
                    }
                }
            }

            var text = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                html.AppendFormat("<p>{0}</p>\n", text);
            }
        }

        private void ProcessImage(XElement imageElem, StringBuilder html)
        {
            var href = imageElem.Attribute(xlinkNs + "href")?.Value;
            if (string.IsNullOrEmpty(href)) return;

            href = href.TrimStart('#');
            if (images.ContainsKey(href))
            {
                html.AppendFormat("<div style=\"text-align:center;\"><img src=\"kindle:embed:{0:D4}\" alt=\"\"/></div>\n",
                    GetImageIndex(href));
            }
        }

        private int GetImageIndex(string imageId)
        {
            int index = 0;
            foreach (var key in images.Keys)
            {
                if (key == imageId) return index;
                index++;
            }
            return -1;
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

            var imageRecords = new List<byte[]>(images.Values);
            int firstImageIndex = images.Count > 0 ? 1 + textRecords.Count : -1;
            int totalRecords = 1 + textRecords.Count + imageRecords.Count + 3;

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

            foreach (var img in imageRecords)
            {
                WriteInt32BE(stream, currentOffset);
                stream.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
                currentOffset += img.Length;
            }

            int flisIndex = 1 + textRecords.Count + imageRecords.Count;
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

            WriteMobiHeader(stream, book, textRecords.Count, firstImageIndex, flisIndex, fcisIndex);

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

            foreach (var image in imageRecords)
            {
                stream.Write(image, 0, image.Length);
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

            byte[] creatorSoft = BitConverter.GetBytes(200);
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
            int firstImageIndex, int flisIndex, int fcisIndex)
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
            WriteInt32BE(stream, firstImageIndex);
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

            int padding = (4 - (exthSize % 4)) % 4;
            exthSize += padding;

            stream.Write(Encoding.ASCII.GetBytes("EXTH"), 0, 4);
            WriteInt32BE(stream, exthSize);
            WriteInt32BE(stream, records.Count);

            foreach (var rec in records)
            {
                stream.Write(rec, 0, rec.Length);
            }

            if (padding > 0)
            {
                stream.Write(new byte[padding], 0, padding);
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
