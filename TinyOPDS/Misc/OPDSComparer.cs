/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the custom comparer class
 *
 */

using System.Collections.Generic;

using TinyOPDS.Data;

namespace TinyOPDS
{
    public class OPDSComparer : IComparer<object>
    {
        private bool cyrillicFirst;

        public OPDSComparer(bool cyrillicFirst = true)
        {
            this.cyrillicFirst = cyrillicFirst;
        }

        public int Compare(object x, object y)
        {
            string x1 = string.Empty, y1 = string.Empty;
            if (x is string)
            {
                x1 = x as string;
                y1 = y as string;
            }
            else if (x is Genre)
            {
                x1 = cyrillicFirst ? (x as Genre).Translation : (x as Genre).Name;
                y1 = cyrillicFirst ? (y as Genre).Translation : (y as Genre).Name;
            }

            // Handle empty strings
            if (string.IsNullOrEmpty(x1) && string.IsNullOrEmpty(y1)) return 0;
            if (string.IsNullOrEmpty(x1)) return 1;
            if (string.IsNullOrEmpty(y1)) return -1;

            // Get script type for first character of each string
            ScriptType xScript = GetScriptType(x1[0]);
            ScriptType yScript = GetScriptType(y1[0]);

            // If scripts are different, sort by script priority
            if (xScript != yScript)
            {
                // Determine group priorities based on cyrillicFirst flag
                int xPriority = GetScriptPriority(xScript, cyrillicFirst);
                int yPriority = GetScriptPriority(yScript, cyrillicFirst);

                if (xPriority != yPriority)
                    return xPriority.CompareTo(yPriority);
            }

            // Within the same script group, use standard string comparison
            return string.Compare(x1, y1, true);
        }

        /// <summary>
        /// Script type enumeration
        /// </summary>
        private enum ScriptType
        {
            Cyrillic,
            Latin,
            Other
        }

        /// <summary>
        /// Get script priority for sorting (lower value = higher priority)
        /// </summary>
        private int GetScriptPriority(ScriptType script, bool cyrillicFirst)
        {
            switch (script)
            {
                case ScriptType.Cyrillic:
                    return cyrillicFirst ? 0 : 1;
                case ScriptType.Latin:
                    return cyrillicFirst ? 1 : 0;
                case ScriptType.Other:
                    return 2; // Always last
                default:
                    return 3;
            }
        }

        /// <summary>
        /// Determine the script type of a character
        /// </summary>
        private ScriptType GetScriptType(char c)
        {
            if (IsCyrillicLetter(c))
                return ScriptType.Cyrillic;
            else if (IsLatinLetter(c))
                return ScriptType.Latin;
            else
                return ScriptType.Other;
        }

        /// <summary>
        /// Check if character is Russian/Ukrainian Cyrillic letter
        /// </summary>
        private bool IsCyrillicLetter(char c)
        {
            // Russian alphabet: А-Я (0x0410-0x042F), а-я (0x0430-0x044F)
            if ((c >= '\u0410' && c <= '\u042F') || (c >= '\u0430' && c <= '\u044F'))
                return true;

            // Additional Russian letters
            if (c == '\u0401' || c == '\u0451') // Ё, ё
                return true;

            // Ukrainian specific letters
            switch (c)
            {
                case '\u0404': // Є
                case '\u0454': // є
                case '\u0406': // І
                case '\u0456': // і
                case '\u0407': // Ї
                case '\u0457': // ї
                case '\u0490': // Ґ
                case '\u0491': // ґ
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Check if character is Latin letter (English + European with diacritics)
        /// </summary>
        private bool IsLatinLetter(char c)
        {
            // Basic Latin: A-Z (0x0041-0x005A), a-z (0x0061-0x007A)
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                return true;

            // Latin-1 Supplement (Western European): 0x00C0-0x00FF
            // Exclude multiplication (0x00D7) and division (0x00F7) signs
            if (c >= '\u00C0' && c <= '\u00FF' && c != '\u00D7' && c != '\u00F7')
                return true;

            // Latin Extended-A (European diacritics): 0x0100-0x017F
            if (c >= '\u0100' && c <= '\u017F')
                return true;

            // Latin Extended-B (less common): 0x0180-0x024F
            if (c >= '\u0180' && c <= '\u024F')
                return true;

            return false;
        }
    }
}