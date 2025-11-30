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
 */

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

using TinyOPDS.Data;

namespace TinyOPDS
{
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
        /// Writes complete MOBI file to the output stream
        /// </summary>
        /// <param name="stream">Output stream</param>
        /// <param name="htmlData">HTML content as UTF-8 bytes</param>
        public void WriteMobiFile(Stream stream, byte[] htmlData)
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

            // Calculate record indices
            int firstImageRecordIndex = imageRecords.Count > 0 ? 1 + textRecords.Count : 0;
            int flisRecordIndex = 1 + textRecords.Count + imageRecords.Count;
            int fcisRecordIndex = flisRecordIndex + 1;
            int eofRecordIndex = fcisRecordIndex + 1;
            int totalRecords = eofRecordIndex + 1;

            // Build special records
            byte[] record0 = BuildRecord0(htmlData.Length, textRecords.Count,
                                          firstImageRecordIndex, flisRecordIndex, fcisRecordIndex);
            byte[] flisRecord = BuildFLISRecord();
            byte[] fcisRecord = BuildFCISRecord(htmlData.Length);
            byte[] eofRecord = new byte[] { 0xE9, 0x8E, 0x0D, 0x0A };

            // Calculate record offsets
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

            stream.Write(flisRecord, 0, flisRecord.Length);
            stream.Write(fcisRecord, 0, fcisRecord.Length);
            stream.Write(eofRecord, 0, eofRecord.Length);
        }

        #region PalmDB Header

        private void WritePalmDbHeader(Stream stream, string title, int recordCount)
        {
            // Database name (32 bytes, null-padded)
            string safeName = new string(title.Where(c => c < 128 && c >= 32).ToArray());
            if (safeName.Length > 31) safeName = safeName.Substring(0, 31);
            byte[] nameBytes = Encoding.ASCII.GetBytes(safeName);
            stream.Write(nameBytes, 0, nameBytes.Length);
            for (int i = nameBytes.Length; i < 32; i++)
                stream.WriteByte(0);

            // Attributes and version
            WriteInt16BE(stream, 0);
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
                                    int firstImageRecordIndex, int flisRecordIndex, int fcisRecordIndex)
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

                long mobiStart = ms.Position;

                // MOBI header
                ms.Write(Encoding.ASCII.GetBytes("MOBI"), 0, 4);
                WriteInt32BE(ms, 232);         // Header length
                WriteInt32BE(ms, 2);           // MOBI type (book)
                WriteInt32BE(ms, 65001);       // Text encoding (UTF-8)
                WriteInt32BE(ms, new Random().Next()); // Unique ID
                WriteInt32BE(ms, 6);           // File version

                // Ortographic/inflection/index records (all NULL)
                for (int i = 0; i < 10; i++)
                    WriteInt32BE(ms, NULL_INDEX);

                // First non-book record index
                WriteInt32BE(ms, 1 + textRecordCount);

                // Full name offset (will be filled later)
                long fullNameOffsetPos = ms.Position;
                WriteInt32BE(ms, 0);

                // Full name length
                int titleLen = Encoding.UTF8.GetByteCount(_book.Title);
                WriteInt32BE(ms, titleLen);

                // Locale and input/output languages
                WriteInt32BE(ms, 9);  // Locale (English)
                WriteInt32BE(ms, 0);  // Input language
                WriteInt32BE(ms, 0);  // Output language
                WriteInt32BE(ms, 6);  // Min version

                // First image record index
                WriteInt32BE(ms, firstImageRecordIndex > 0 ? firstImageRecordIndex : NULL_INDEX);

                // Huffman/HUFF/CDIC records
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);

                // EXTH flags
                WriteInt32BE(ms, 0x40);

                // Unknown (32 bytes)
                ms.Write(new byte[32], 0, 32);

                // DRM and other indices
                WriteInt32BE(ms, NULL_INDEX);
                WriteInt32BE(ms, NULL_INDEX);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);

                // Unknown (8 bytes)
                ms.Write(new byte[8], 0, 8);

                // FDST and text record info
                WriteInt16BE(ms, 1);
                WriteInt16BE(ms, textRecordCount);

                // Unknown and FLIS/FCIS info
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, fcisRecordIndex);
                WriteInt32BE(ms, 1);
                WriteInt32BE(ms, flisRecordIndex);
                WriteInt32BE(ms, 1);

                // Unknown (8 bytes)
                ms.Write(new byte[8], 0, 8);

                // More indices
                WriteInt32BE(ms, NULL_INDEX);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, 0);
                WriteInt32BE(ms, NULL_INDEX);

                WriteInt32BE(ms, 1);
                WriteInt32BE(ms, NULL_INDEX);

                // Pad MOBI header to 232 bytes
                long written = ms.Position - mobiStart;
                if (written < 232)
                    ms.Write(new byte[232 - written], 0, (int)(232 - written));

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

        private byte[] BuildFLISRecord()
        {
            return new byte[]
            {
                0x46, 0x4C, 0x49, 0x53, // "FLIS"
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

        #endregion
    }
}