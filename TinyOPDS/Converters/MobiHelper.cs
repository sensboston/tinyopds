/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * MobiHelper - low-level MOBI format writer (MOBI 6)
 * Handles PalmDB headers, MOBI/EXTH records, and binary structure.
 *
 * v8: Extended MOBI header to 264 bytes for full NCX support.
 *     Fixed breadth-first NCX ordering for Kindle "Go To" menu.
 *
 */

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

using TinyOPDS.Data;

namespace TinyOPDS
{
    /// <summary>
    /// Chapter entry for NCX TOC generation
    /// </summary>
    public class MobiChapter
    {
        public string Title { get; set; }
        public int Offset { get; set; }   // Byte offset in HTML
        public int Depth { get; set; }    // Hierarchy level (0 = top)
    }

    public class MobiHelper
    {
        private const int RECORD_SIZE = 4096;
        private const int NULL_INDEX = unchecked((int)0xFFFFFFFF);

        private readonly Book _book;
        private readonly IReadOnlyList<byte[]> _images;
        private readonly int _coverRecindex;

        public MobiHelper(Book book, IReadOnlyList<byte[]> images, int coverRecindex)
        {
            _book = book ?? throw new ArgumentNullException(nameof(book));
            _images = images ?? Array.Empty<byte[]>();
            _coverRecindex = coverRecindex;
        }

        /// <summary>
        /// Writes complete MOBI file to the output stream (without NCX)
        /// </summary>
        public void WriteMobiFile(Stream stream, byte[] htmlData)
        {
            WriteMobiFile(stream, htmlData, null);
        }

        /// <summary>
        /// Writes complete MOBI file with NCX TOC support
        /// </summary>
        /// <param name="stream">Output stream</param>
        /// <param name="htmlData">HTML content as UTF-8 bytes</param>
        /// <param name="chapters">Chapter list for NCX generation (null = no NCX)</param>
        public void WriteMobiFile(Stream stream, byte[] htmlData, IList<MobiChapter> chapters)
        {
            // Split HTML into text records (max 4096 bytes each)
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

            var imageRecords = new List<byte[]>(_images);

            // Build NCX records if chapters provided
            List<byte[]> ncxRecords = null;
            int ncxRecordIndex = NULL_INDEX;

            if (chapters != null && chapters.Count > 0)
            {
                var ncxBuilder = new MobiNcxBuilder();
                foreach (var ch in chapters)
                {
                    ncxBuilder.AddEntry(ch.Title, ch.Offset, ch.Depth);
                }
                ncxBuilder.Build(htmlData.Length);
                ncxRecords = ncxBuilder.Records;

                if (ncxRecords.Count > 0)
                {
                    // NCX starts after text + images
                    ncxRecordIndex = 1 + textRecords.Count + imageRecords.Count;
                }
            }

            // Calculate record indices
            int ncxRecordCount = ncxRecords?.Count ?? 0;
            int firstImageRecordIndex = imageRecords.Count > 0 ? 1 + textRecords.Count : 0;
            int flisRecordIndex = 1 + textRecords.Count + imageRecords.Count + ncxRecordCount;
            int fcisRecordIndex = flisRecordIndex + 1;
            int eofRecordIndex = fcisRecordIndex + 1;
            int totalRecords = eofRecordIndex + 1;

            // Build special records
            byte[] record0 = BuildRecord0(htmlData.Length, textRecords.Count,
                                          firstImageRecordIndex, flisRecordIndex, fcisRecordIndex,
                                          ncxRecordIndex);
            byte[] flisRecord = BuildFLISRecord();
            byte[] fcisRecord = BuildFCISRecord(htmlData.Length);
            byte[] eofRecord = new byte[] { 0xE9, 0x8E, 0x0D, 0x0A };

            // Calculate record offsets
            int headerEnd = 78 + (totalRecords * 8) + 2;
            var recordOffsets = new List<int>();
            int currentOffset = headerEnd;

            // Record 0
            recordOffsets.Add(currentOffset);
            currentOffset += record0.Length;

            // Text records
            foreach (var rec in textRecords)
            {
                recordOffsets.Add(currentOffset);
                currentOffset += rec.Length;
            }

            // Image records
            foreach (var img in imageRecords)
            {
                recordOffsets.Add(currentOffset);
                currentOffset += img.Length;
            }

            // NCX records (INDX Master, INDX Data, CNCX)
            if (ncxRecords != null)
            {
                foreach (var ncx in ncxRecords)
                {
                    recordOffsets.Add(currentOffset);
                    currentOffset += ncx.Length;
                }
            }

            // FLIS
            recordOffsets.Add(currentOffset);
            currentOffset += flisRecord.Length;

            // FCIS
            recordOffsets.Add(currentOffset);
            currentOffset += fcisRecord.Length;

            // EOF
            recordOffsets.Add(currentOffset);

            // Write PalmDB header
            WritePalmDbHeader(stream, _book.Title, totalRecords);

            // Write record info entries
            for (int i = 0; i < totalRecords; i++)
            {
                WriteInt32BE(stream, recordOffsets[i]);
                stream.WriteByte(0);
                int uniqueId = 2 * i;
                stream.WriteByte((byte)((uniqueId >> 16) & 0xFF));
                stream.WriteByte((byte)((uniqueId >> 8) & 0xFF));
                stream.WriteByte((byte)(uniqueId & 0xFF));
            }

            // Gap bytes
            stream.WriteByte(0);
            stream.WriteByte(0);

            // Write all records
            stream.Write(record0, 0, record0.Length);

            foreach (var rec in textRecords)
                stream.Write(rec, 0, rec.Length);

            foreach (var img in imageRecords)
                stream.Write(img, 0, img.Length);

            if (ncxRecords != null)
            {
                foreach (var ncx in ncxRecords)
                    stream.Write(ncx, 0, ncx.Length);
            }

            stream.Write(flisRecord, 0, flisRecord.Length);
            stream.Write(fcisRecord, 0, fcisRecord.Length);
            stream.Write(eofRecord, 0, eofRecord.Length);
        }

        #region PalmDB Header

        private void WritePalmDbHeader(Stream stream, string title, int recordCount)
        {
            // Database name (32 bytes, null-padded)
            byte[] nameBytes = Encoding.ASCII.GetBytes(SanitizeTitle(title));
            stream.Write(nameBytes, 0, Math.Min(nameBytes.Length, 31));
            for (int i = nameBytes.Length; i < 32; i++)
                stream.WriteByte(0);

            // Attributes
            WriteInt16BE(stream, 0);
            // Version
            WriteInt16BE(stream, 0);

            // Creation/modification dates (PalmOS epoch: 1904-01-01)
            int palmTime = (int)((DateTime.UtcNow - new DateTime(1904, 1, 1)).TotalSeconds);
            WriteInt32BE(stream, palmTime);
            WriteInt32BE(stream, palmTime);
            WriteInt32BE(stream, 0); // Last backup
            WriteInt32BE(stream, 0); // Modification number
            WriteInt32BE(stream, 0); // App info offset
            WriteInt32BE(stream, 0); // Sort info offset

            // Type and creator
            stream.Write(Encoding.ASCII.GetBytes("BOOK"), 0, 4);
            stream.Write(Encoding.ASCII.GetBytes("MOBI"), 0, 4);

            // Unique ID seed and next record list
            WriteInt32BE(stream, (2 * recordCount) - 1);
            WriteInt32BE(stream, 0);
            WriteInt16BE(stream, recordCount);
        }

        #endregion

        #region MOBI Records

        private byte[] BuildRecord0(int textLength, int textRecordCount,
                                    int firstImageRecordIndex, int flisRecordIndex, int fcisRecordIndex,
                                    int ncxRecordIndex)
        {
            using (var ms = new MemoryStream())
            {
                // PalmDOC header (16 bytes)
                WriteInt16BE(ms, 1);           // Compression: none
                WriteInt16BE(ms, 0);           // Unused
                WriteInt32BE(ms, textLength);  // Text length
                WriteInt16BE(ms, textRecordCount);
                WriteInt16BE(ms, RECORD_SIZE); // Record size
                WriteInt32BE(ms, 0);           // Current position

                long mobiStart = ms.Position;  // Offset 16

                // MOBI header (starts at offset 16)
                // Header length = 264 for full NCX support (matching kindlegen output)
                ms.Write(Encoding.ASCII.GetBytes("MOBI"), 0, 4);  // 16-19: MOBI magic
                WriteInt32BE(ms, 264);         // 20-23: Header length (264 for full NCX)
                WriteInt32BE(ms, 2);           // 24-27: MOBI type (book)
                WriteInt32BE(ms, 65001);       // 28-31: Text encoding (UTF-8)
                WriteInt32BE(ms, new Random().Next()); // 32-35: Unique ID
                WriteInt32BE(ms, 6);           // 36-39: File version

                // 40-79: Ortographic/inflection/index records (10 x 4 bytes, all NULL)
                for (int i = 0; i < 10; i++)
                    WriteInt32BE(ms, NULL_INDEX);

                // 80-83: First non-book record index
                WriteInt32BE(ms, 1 + textRecordCount);

                // 84-87: Full name offset (placeholder)
                long fullNameOffsetPos = ms.Position;
                WriteInt32BE(ms, 0);

                // 88-91: Full name length
                int titleLen = Encoding.UTF8.GetByteCount(_book.Title);
                WriteInt32BE(ms, titleLen);

                // 92-95: Locale
                WriteInt32BE(ms, 9);  // English
                // 96-99: Input language
                WriteInt32BE(ms, 0);
                // 100-103: Output language
                WriteInt32BE(ms, 0);
                // 104-107: Min version
                WriteInt32BE(ms, 6);

                // 108-111: First image record index
                WriteInt32BE(ms, firstImageRecordIndex > 0 ? firstImageRecordIndex : NULL_INDEX);

                // 112-127: Huffman/HUFF/CDIC records (4 x 4 bytes)
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);

                // 128-131: EXTH flags
                // Use 0x40 (has EXTH only) - 0x50 breaks popup footnotes on older Kindles
                WriteInt32BE(ms, 0x40);

                // 132-163: Unknown (32 bytes)
                ms.Write(new byte[32], 0, 32);

                // 164-167: DRM offset
                WriteInt32BE(ms, NULL_INDEX);
                // 168-171: DRM count
                WriteInt32BE(ms, NULL_INDEX);
                // 172-175: DRM size
                WriteInt32BE(ms, 0);
                // 176-179: DRM flags
                WriteInt32BE(ms, 0);
                // 180-183: Unknown
                WriteInt32BE(ms, 0);

                // 184-191: Unknown (8 bytes)
                ms.Write(new byte[8], 0, 8);

                // 192-193: FDST flow count
                WriteInt16BE(ms, 1);
                // 194-195: Text record count (first content)
                WriteInt16BE(ms, textRecordCount);

                // 196-199: Unknown
                WriteInt32BE(ms, 0);
                // 200-203: FCIS record number
                WriteInt32BE(ms, fcisRecordIndex);
                // 204-207: FCIS record count
                WriteInt32BE(ms, 1);
                // 208-211: FLIS record number
                WriteInt32BE(ms, flisRecordIndex);
                // 212-215: FLIS record count
                WriteInt32BE(ms, 1);

                // 216-223: Unknown (8 bytes)
                ms.Write(new byte[8], 0, 8);

                // 224-227: Unknown
                WriteInt32BE(ms, NULL_INDEX);
                // 228-231: Unknown
                WriteInt32BE(ms, 0);
                // 232-235: Unknown
                WriteInt32BE(ms, 0);
                // 236-239: Unknown
                WriteInt32BE(ms, NULL_INDEX);

                // 240-243: Extra record data flags
                WriteInt32BE(ms, 0);

                // 244-247: NCX INDEX RECORD
                // Keep valid index for TOC, but use EXTH flags 0x40 (not 0x50) for popup footnotes
                WriteInt32BE(ms, ncxRecordIndex);

                // 248-251: Fragment index (NULL for MOBI 6)
                WriteInt32BE(ms, NULL_INDEX);
                // 252-255: Skeleton index (NULL for MOBI 6)
                WriteInt32BE(ms, NULL_INDEX);
                // 256-259: DATP offset (NULL)
                WriteInt32BE(ms, NULL_INDEX);
                // 260-263: Guide index (NULL for MOBI 6)
                WriteInt32BE(ms, NULL_INDEX);

                // Pad to header length = 264 bytes from MOBI start
                // Currently at offset 248 from MOBI start, need 16 more bytes
                ms.Write(new byte[16], 0, 16);  // 264-279: padding

                // EXTH header
                byte[] exth = BuildExthHeader(firstImageRecordIndex);
                ms.Write(exth, 0, exth.Length);

                // Write full name offset
                int fullNameOffset = (int)ms.Position;
                long pos = ms.Position;
                ms.Position = fullNameOffsetPos;
                WriteInt32BE(ms, fullNameOffset);
                ms.Position = pos;

                // Full name
                byte[] titleBytes = Encoding.UTF8.GetBytes(_book.Title);
                ms.Write(titleBytes, 0, titleBytes.Length);

                // Padding to 4-byte boundary
                int pad = (4 - ((int)ms.Length % 4)) % 4;
                if (pad > 0) ms.Write(new byte[pad], 0, pad);
                ms.Write(new byte[4], 0, 4);

                return ms.ToArray();
            }
        }

        private byte[] BuildExthHeader(int firstImageRecordIndex)
        {
            using (var ms = new MemoryStream())
            {
                var records = new List<byte[]>();

                // Author
                if (_book.Authors?.Count > 0)
                    records.Add(BuildExthRecord(100, Encoding.UTF8.GetBytes(_book.Authors[0])));

                // Updated title
                records.Add(BuildExthRecord(503, Encoding.UTF8.GetBytes(_book.Title)));

                // Document type
                records.Add(BuildExthRecord(501, Encoding.ASCII.GetBytes("EBOK")));

                // Various flags
                records.Add(BuildExthRecord(204, GetInt32Bytes(201)));
                records.Add(BuildExthRecord(205, GetInt32Bytes(2)));
                records.Add(BuildExthRecord(206, GetInt32Bytes(9)));
                records.Add(BuildExthRecord(207, GetInt32Bytes(0)));

                // Cover image
                if (firstImageRecordIndex > 0 && _coverRecindex > 0)
                {
                    int coverOffset = _coverRecindex - 1;
                    records.Add(BuildExthRecord(201, GetInt32Bytes(coverOffset)));
                    records.Add(BuildExthRecord(203, GetInt32Bytes(0)));
                }

                // Calculate sizes
                int dataSize = 0;
                foreach (var r in records) dataSize += r.Length;

                int headerSize = 12 + dataSize;
                int padding = (4 - (headerSize % 4)) % 4;

                // Write EXTH header
                ms.Write(Encoding.ASCII.GetBytes("EXTH"), 0, 4);
                WriteInt32BE(ms, headerSize + padding);
                WriteInt32BE(ms, records.Count);

                foreach (var r in records)
                    ms.Write(r, 0, r.Length);

                // Padding
                for (int i = 0; i < padding; i++)
                    ms.WriteByte(0);

                return ms.ToArray();
            }
        }

        private byte[] BuildExthRecord(int type, byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                WriteInt32BE(ms, type);
                WriteInt32BE(ms, 8 + data.Length);
                ms.Write(data, 0, data.Length);
                return ms.ToArray();
            }
        }

        private byte[] BuildFLISRecord()
        {
            using (var ms = new MemoryStream())
            {
                ms.Write(Encoding.ASCII.GetBytes("FLIS"), 0, 4);
                WriteInt32BE(ms, 8);
                WriteInt16BE(ms, 65);
                WriteInt16BE(ms, 0);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, unchecked((int)0xFFFFFFFF));
                WriteInt16BE(ms, 1);
                WriteInt16BE(ms, 3);
                WriteInt32BE(ms, 3);
                WriteInt32BE(ms, 1);
                WriteInt32BE(ms, unchecked((int)0xFFFFFFFF));
                return ms.ToArray();
            }
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

        private string SanitizeTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "Untitled";
            var sb = new StringBuilder();
            foreach (char c in title)
            {
                if (c >= 32 && c < 127)
                    sb.Append(c);
            }
            return sb.Length > 0 ? sb.ToString() : "Untitled";
        }

        #endregion
    }
}
