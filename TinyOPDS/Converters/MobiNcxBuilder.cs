/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * MobiNcxBuilder - generates NCX/INDX records for MOBI "Go to" menu
 * Supports hierarchical TOC with parent/child relationships.
 *
 * v8: NCX entries must be in BREADTH-FIRST order (all depth 0 first, then depth 1, etc.)
 *     Parent/child indices are recalculated after reordering.
 *     Based on binary analysis of kindlegen output.
 *
 */

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace TinyOPDS
{
    /// <summary>
    /// Table of Contents entry for NCX index
    /// </summary>
    public class NcxTocEntry
    {
        public int Index { get; set; }
        public string Title { get; set; }
        public int Offset { get; set; }      // Byte position in HTML
        public int Length { get; set; }      // Section length in bytes
        public int Depth { get; set; }       // Hierarchy level (0 = top)
        public int? Parent { get; set; }     // Parent entry index
        public int? FirstChild { get; set; }
        public int? LastChild { get; set; }
    }

    /// <summary>
    /// Builds MOBI NCX index records (INDX, CNCX) for Kindle "Go To" menu.
    /// Generates flat or hierarchical TOC without modifying text records.
    /// </summary>
    public class MobiNcxBuilder
    {
        private const int INDX_HEADER_LEN = 192;

        private List<NcxTocEntry> _entries = new List<NcxTocEntry>();
        private byte[] _cncxRecord;
        private Dictionary<int, int> _cncxOffsets = new Dictionary<int, int>();

        /// <summary>
        /// Generated records: [INDX Master, INDX Data, CNCX]
        /// </summary>
        public List<byte[]> Records { get; private set; } = new List<byte[]>();

        /// <summary>
        /// Number of generated records (for calculating indices)
        /// </summary>
        public int RecordCount => Records.Count;

        #region Public API

        /// <summary>
        /// Add a TOC entry
        /// </summary>
        /// <param name="title">Chapter title for display</param>
        /// <param name="offset">Byte offset in HTML content</param>
        /// <param name="depth">Hierarchy depth (0 = top level)</param>
        public void AddEntry(string title, int offset, int depth)
        {
            _entries.Add(new NcxTocEntry
            {
                Index = _entries.Count,
                Title = title ?? "",
                Offset = offset,
                Depth = depth
            });
        }

        /// <summary>
        /// Build all NCX records after entries are added.
        /// Call this before accessing Records property.
        /// </summary>
        /// <param name="totalTextLength">Total HTML content length for calculating last entry size</param>
        public void Build(int totalTextLength)
        {
            Records.Clear();

            if (_entries.Count == 0)
                return;

            CalculateLengths(totalTextLength);
            CalculateHierarchy();
            // CRITICAL: Kindle expects entries in BREADTH-FIRST order (all depth 0 first, then depth 1, etc.)
            ReorderBreadthFirst();
            BuildCncxRecord();
            BuildIndxRecords();
        }

        #endregion

        #region Length and Hierarchy Calculation

        private void CalculateLengths(int totalTextLength)
        {
            // For hierarchical TOC, parent length must include all children.
            // Length = distance to next entry at same or higher level (lower depth number)
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                int nextOffset = totalTextLength;  // Default to end of document

                // Find next entry at same or higher level (depth <= current)
                for (int j = i + 1; j < _entries.Count; j++)
                {
                    if (_entries[j].Depth <= entry.Depth)
                    {
                        nextOffset = _entries[j].Offset;
                        break;
                    }
                }

                entry.Length = Math.Max(1, nextOffset - entry.Offset);
            }
        }

        private void CalculateHierarchy()
        {
            // Calculate hierarchy based on original (document) order
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];

                // Find parent (first previous entry with smaller depth)
                for (int j = i - 1; j >= 0; j--)
                {
                    if (_entries[j].Depth < entry.Depth)
                    {
                        entry.Parent = j;
                        break;
                    }
                }

                // Find children (direct children only, depth = entry.depth + 1)
                int? firstChild = null;
                int? lastChild = null;

                for (int j = i + 1; j < _entries.Count; j++)
                {
                    // Stop if we hit same or higher level
                    if (_entries[j].Depth <= entry.Depth)
                        break;

                    // Direct child
                    if (_entries[j].Depth == entry.Depth + 1)
                    {
                        if (!firstChild.HasValue)
                            firstChild = j;
                        lastChild = j;
                    }
                }

                entry.FirstChild = firstChild;
                entry.LastChild = lastChild;
            }
        }

        /// <summary>
        /// Reorder entries in breadth-first order (all depth 0 first, then depth 1, etc.)
        /// and update all parent/child index references.
        /// </summary>
        private void ReorderBreadthFirst()
        {
            if (_entries.Count <= 1)
                return;

            // Group by depth, preserving original order within each depth level
            var orderedEntries = _entries
                .OrderBy(e => e.Depth)
                .ThenBy(e => e.Index)
                .ToList();

            // Create mapping from old index to new index
            var oldToNew = new Dictionary<int, int>();
            for (int newIdx = 0; newIdx < orderedEntries.Count; newIdx++)
            {
                oldToNew[orderedEntries[newIdx].Index] = newIdx;
            }

            // Update all index references
            foreach (var entry in orderedEntries)
            {
                // Update parent reference
                if (entry.Parent.HasValue)
                    entry.Parent = oldToNew[entry.Parent.Value];

                // Update child references
                if (entry.FirstChild.HasValue)
                    entry.FirstChild = oldToNew[entry.FirstChild.Value];
                if (entry.LastChild.HasValue)
                    entry.LastChild = oldToNew[entry.LastChild.Value];

                // Update entry's own index
                entry.Index = oldToNew[entry.Index];
            }

            // Replace entries list with reordered list
            _entries = orderedEntries;
        }

        #endregion

        #region CNCX Builder

        private void BuildCncxRecord()
        {
            _cncxOffsets.Clear();

            using (var ms = new MemoryStream())
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    string title = _entries[i].Title;

                    // Store offset for this entry (using new index)
                    _cncxOffsets[i] = (int)ms.Position;

                    // Write: [length as VWI][UTF-8 bytes]
                    byte[] titleBytes = Encoding.UTF8.GetBytes(title);
                    byte[] lenBytes = EncodeVwi(titleBytes.Length);

                    ms.Write(lenBytes, 0, lenBytes.Length);
                    ms.Write(titleBytes, 0, titleBytes.Length);
                }

                // Pad to 4-byte boundary
                while (ms.Length % 4 != 0)
                    ms.WriteByte(0);

                _cncxRecord = ms.ToArray();
            }
        }

        #endregion

        #region INDX Records Builder

        private void BuildIndxRecords()
        {
            // Build INDX Data record first (need entry count for master)
            byte[] indxData = BuildIndxDataRecord();

            // Build INDX Master record
            byte[] indxMaster = BuildIndxMasterRecord();

            // Add records in order: Master, Data, CNCX
            Records.Add(indxMaster);
            Records.Add(indxData);
            Records.Add(_cncxRecord);
        }

        private byte[] BuildIndxMasterRecord()
        {
            using (var ms = new MemoryStream())
            {
                // INDX header (192 bytes) - based on KindleUnpack/Calibre analysis
                ms.Write(Encoding.ASCII.GetBytes("INDX"), 0, 4);  // 0-3: Magic
                WriteInt32BE(ms, INDX_HEADER_LEN);      // 4-7: Header length = 192
                WriteInt32BE(ms, 0);                    // 8-11: Index type (0 for NCX)
                WriteInt32BE(ms, 0);                    // 12-15: Unknown, always 0
                WriteInt32BE(ms, 2);                    // 16-19: Unknown (2 for NCX master)
                WriteInt32BE(ms, 0);                    // 20-23: IDXT offset (placeholder)
                WriteInt32BE(ms, 1);                    // 24-27: Number of index records (1 data record)
                WriteInt32BE(ms, 65001);                // 28-31: Encoding (UTF-8 = 65001)
                WriteInt32BE(ms, unchecked((int)0xFFFFFFFF)); // 32-35: Language (-1)
                WriteInt32BE(ms, _entries.Count);       // 36-39: Total entry count
                WriteInt32BE(ms, 0);                    // 40-43: ORDT offset
                WriteInt32BE(ms, 0);                    // 44-47: LIGT offset
                WriteInt32BE(ms, 0);                    // 48-51: LIGT entry count
                WriteInt32BE(ms, 1);                    // 52-55: CNCX record count

                // Pad header to 192 bytes
                while (ms.Length < INDX_HEADER_LEN)
                    ms.WriteByte(0);

                // TAGX block (follows header at offset 192)
                byte[] tagx = BuildTagx();
                ms.Write(tagx, 0, tagx.Length);

                // Geometry entry: last index label for this record
                // Format: [len][label_bytes]
                string lastLabel = (_entries.Count - 1).ToString();
                byte[] lastLabelBytes = Encoding.ASCII.GetBytes(lastLabel);
                ms.WriteByte((byte)lastLabelBytes.Length);
                ms.Write(lastLabelBytes, 0, lastLabelBytes.Length);

                // Align to even boundary before IDXT
                if (ms.Length % 2 != 0)
                    ms.WriteByte(0);

                // IDXT section
                int idxtOffset = (int)ms.Length;
                ms.Write(Encoding.ASCII.GetBytes("IDXT"), 0, 4);
                // Offset to geometry entry (right after TAGX)
                WriteInt16BE(ms, INDX_HEADER_LEN + tagx.Length);

                // Align to 4 bytes
                while (ms.Length % 4 != 0)
                    ms.WriteByte(0);

                // Update IDXT offset in header at offset 20
                byte[] result = ms.ToArray();
                WriteInt32BE(result, 20, idxtOffset);

                return result;
            }
        }

        private byte[] BuildIndxDataRecord()
        {
            using (var ms = new MemoryStream())
            {
                // INDX header (192 bytes) - Data record structure
                ms.Write(Encoding.ASCII.GetBytes("INDX"), 0, 4);  // 0-3: Magic
                WriteInt32BE(ms, INDX_HEADER_LEN);      // 4-7: Header length = 192
                WriteInt32BE(ms, 0);                    // 8-11: Index type (0 for data record)
                WriteInt32BE(ms, 1);                    // 12-15: Unknown (1 = data record indicator)
                WriteInt32BE(ms, 0);                    // 16-19: Unknown
                WriteInt32BE(ms, 0);                    // 20-23: IDXT offset (placeholder)
                WriteInt32BE(ms, _entries.Count);       // 24-27: Entry count in this record
                WriteInt32BE(ms, unchecked((int)0xFFFFFFFF)); // 28-31: Encoding (-1 for data record)
                WriteInt32BE(ms, unchecked((int)0xFFFFFFFF)); // 32-35: Language (-1)
                WriteInt32BE(ms, 0);                    // 36-39: Total entries (0 for data)
                WriteInt32BE(ms, 0);                    // 40-43: ORDT offset
                WriteInt32BE(ms, 0);                    // 44-47: LIGT offset
                WriteInt32BE(ms, 0);                    // 48-51: LIGT entry count
                WriteInt32BE(ms, 0);                    // 52-55: CNCX count (0 for data record)

                // Pad header to 192 bytes
                while (ms.Length < INDX_HEADER_LEN)
                    ms.WriteByte(0);

                // Write index entries and collect offsets for IDXT
                var entryOffsets = new List<int>();

                for (int i = 0; i < _entries.Count; i++)
                {
                    entryOffsets.Add((int)ms.Length);
                    byte[] entryData = EncodeIndexEntry(_entries[i]);
                    ms.Write(entryData, 0, entryData.Length);
                }

                // Align to even boundary before IDXT
                if (ms.Length % 2 != 0)
                    ms.WriteByte(0);

                // IDXT section
                int idxtOffset = (int)ms.Length;
                ms.Write(Encoding.ASCII.GetBytes("IDXT"), 0, 4);
                foreach (int off in entryOffsets)
                    WriteInt16BE(ms, off);

                // Align to 4 bytes
                while (ms.Length % 4 != 0)
                    ms.WriteByte(0);

                // Update IDXT offset in header at offset 20
                byte[] result = ms.ToArray();
                WriteInt32BE(result, 20, idxtOffset);

                return result;
            }
        }

        private byte[] BuildTagx()
        {
            // TAGX block for hierarchical NCX
            // Tags: 1=offset, 2=length, 3=label, 4=depth, 21=parent, 22=first_child, 23=last_child
            using (var ms = new MemoryStream())
            {
                ms.Write(Encoding.ASCII.GetBytes("TAGX"), 0, 4);
                WriteInt32BE(ms, 44);  // TAGX total length
                WriteInt32BE(ms, 1);   // Control byte count

                // Tag definitions: [tag, values_per_entry, bitmask, end_flag]
                byte[] tags = new byte[]
                {
                    1, 1, 0x01, 0,     // offset (bit 0)
                    2, 1, 0x02, 0,     // length (bit 1)
                    3, 1, 0x04, 0,     // label_offset in CNCX (bit 2)
                    4, 1, 0x08, 0,     // depth (bit 3)
                    21, 1, 0x10, 0,    // parent index (bit 4)
                    22, 1, 0x20, 0,    // first_child index (bit 5)
                    23, 1, 0x40, 0,    // last_child index (bit 6)
                    0, 0, 0, 1         // EOF marker
                };
                ms.Write(tags, 0, tags.Length);

                return ms.ToArray();
            }
        }

        private byte[] EncodeIndexEntry(NcxTocEntry entry)
        {
            using (var ms = new MemoryStream())
            {
                // Label: decimal index as ASCII string
                string label = entry.Index.ToString();
                byte[] labelBytes = Encoding.ASCII.GetBytes(label);
                ms.WriteByte((byte)labelBytes.Length);
                ms.Write(labelBytes, 0, labelBytes.Length);

                // Build control byte based on which optional tags are present
                // Base: 0x0F = offset + length + label + depth (always present)
                byte controlByte = 0x0F;
                if (entry.Parent.HasValue) controlByte |= 0x10;
                if (entry.FirstChild.HasValue) controlByte |= 0x20;
                if (entry.LastChild.HasValue) controlByte |= 0x40;
                ms.WriteByte(controlByte);

                // Tag 1: offset (VWI)
                WriteVwi(ms, entry.Offset);

                // Tag 2: length (VWI)
                WriteVwi(ms, entry.Length);

                // Tag 3: label offset in CNCX (VWI)
                int cncxOffset = _cncxOffsets.ContainsKey(entry.Index)
                    ? _cncxOffsets[entry.Index] : 0;
                WriteVwi(ms, cncxOffset);

                // Tag 4: depth (VWI)
                WriteVwi(ms, entry.Depth);

                // Tag 21: parent (VWI) - only if present
                if (entry.Parent.HasValue)
                    WriteVwi(ms, entry.Parent.Value);

                // Tag 22: first_child (VWI) - only if present
                if (entry.FirstChild.HasValue)
                    WriteVwi(ms, entry.FirstChild.Value);

                // Tag 23: last_child (VWI) - only if present
                if (entry.LastChild.HasValue)
                    WriteVwi(ms, entry.LastChild.Value);

                return ms.ToArray();
            }
        }

        #endregion

        #region VWI Encoding

        /// <summary>
        /// Encode integer as Variable Width Integer (forward encoding).
        /// 7 bits per byte, high bit (0x80) set on LAST byte.
        /// </summary>
        private byte[] EncodeVwi(int value)
        {
            if (value < 0)
                throw new ArgumentException("VWI value must be non-negative");

            if (value == 0)
                return new byte[] { 0x80 };

            var bytes = new List<byte>();
            int v = value;
            while (v > 0)
            {
                bytes.Add((byte)(v & 0x7F));
                v >>= 7;
            }

            // Reverse to big-endian order
            bytes.Reverse();

            // Set high bit on last byte only
            bytes[bytes.Count - 1] |= 0x80;

            return bytes.ToArray();
        }

        private void WriteVwi(Stream stream, int value)
        {
            byte[] bytes = EncodeVwi(value);
            stream.Write(bytes, 0, bytes.Length);
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
