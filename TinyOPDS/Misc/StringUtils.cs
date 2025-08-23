/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines some specific String extensions classes:
 * Soundex and Transliteration
 *
 */

using System;
using System.Text;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace TinyOPDS
{
    public static class StringExtensions
    {
        public static string ToStringWithDeclaration(this XDocument doc)
        {
            StringBuilder sb = new StringBuilder();
            XmlWriterSettings xws = new XmlWriterSettings();
            xws.OmitXmlDeclaration = false;
            xws.Indent = true;
            using (XmlWriter xw = XmlWriter.Create(sb, xws)) doc.WriteTo(xw);
            return sb.ToString().Replace("utf-16", "utf-8");
        }

        public static string Reverse(this string sentence)
        {
            string[] words = sentence.Split(' ');
            Array.Reverse(words);
            return string.Join(" ", words);
        }

        public static string DecodeFromBase64(this string encodedData)
        {
            byte[] encodedDataAsBytes = Convert.FromBase64String(encodedData);
            return Encoding.UTF8.GetString(encodedDataAsBytes);
        }

        public static string SanitizeFileName(this string fileName)
        {
            return String.Join("", fileName.Split(System.IO.Path.GetInvalidFileNameChars()));
        }

        public static string SanitizePathName(this string pathName)
        {
            while (pathName.IndexOf("\\\\") >= 0) pathName = pathName.Replace("\\\\", "\\");
            while (pathName.IndexOf("//") >= 0) pathName = pathName.Replace("//", "/");
            if ((pathName.EndsWith("\\") || pathName.EndsWith("/")) && (pathName.Length > 2)) pathName = pathName.Remove(pathName.Length - 1);
            return pathName;
        }

        public static int WordsCount(this string s)
        {
            return s.Split(' ', ',').Length;
        }

        public static string UrlCombine(this string uri1, string uri2)
        {
            uri1 = uri1.TrimEnd('/');
            uri2 = uri2.TrimStart('/');
            return string.Format("{0}/{1}", uri1, uri2).TrimEnd('/');
        }

        public static bool IsValidUTF(this string s)
        {
            bool valid = true;
            foreach (char c in s) valid &= c != 0xFFFD;
            return valid;
        }

        public static string SoundexByWord(this string data)
        {
            var soundexes = new List<string>();
            foreach (var str in data.Split(' ', ','))
            {
                if (!string.IsNullOrWhiteSpace(str))
                {
                    soundexes.Add(Soundex(str));
                }
            }
            return string.Join(" ", soundexes);
        }

        /// <summary>
        /// Universal Soundex algorithm for both Russian and English languages
        /// </summary>
        /// <param name="word">Input word in Russian or English</param>
        /// <returns>4-character Soundex code</returns>
        public static string Soundex(string word)
        {
            if (string.IsNullOrEmpty(word))
                return "0000";

            word = word.Trim().ToUpperInvariant();
            if (word.Length == 0)
                return "0000";

            StringBuilder result = new StringBuilder();
            result.Append(word[0]); // Keep first character as-is (Cyrillic or Latin)

            string previousCode = GetSoundexCode(word[0]);

            for (int i = 1; i < word.Length && result.Length < 4; i++)
            {
                string currentCode = GetSoundexCode(word[i]);

                // Add code only if it's different from previous and not empty
                if (!string.IsNullOrEmpty(currentCode) && currentCode != previousCode)
                {
                    result.Append(currentCode);
                }

                // Update previous code only if current code is not empty
                if (!string.IsNullOrEmpty(currentCode))
                {
                    previousCode = currentCode;
                }
            }

            // Pad with zeros to make exactly 4 characters
            while (result.Length < 4)
            {
                result.Append('0');
            }

            return result.ToString();
        }

        /// <summary>
        /// Get Soundex code for a character (Russian or English)
        /// </summary>
        /// <param name="c">Character to encode</param>
        /// <returns>Soundex digit or empty string for vowels/ignored chars</returns>
        private static string GetSoundexCode(char c)
        {
            // Russian consonant groups
            switch (c)
            {
                // Group 1: Labials (губные) - Б,П,В,Ф
                case 'Б':
                case 'П':
                case 'В':
                case 'Ф':
                case 'B':
                case 'P':
                case 'F':
                case 'V':
                    return "1";

                // Group 2: Gutturals/Velars (заднеязычные) - Г,К,Х + sibilants Ж,Ш,Щ,Ч
                case 'Г':
                case 'К':
                case 'Х':
                case 'Ж':
                case 'Ш':
                case 'Щ':
                case 'Ч':
                case 'G':
                case 'K':
                case 'Q':
                case 'X':
                case 'J':
                case 'C':
                    return "2";

                // Group 3: Dentals (переднеязычные смычные) - Д,Т
                case 'Д':
                case 'Т':
                case 'D':
                case 'T':
                    return "3";

                // Group 4: Liquids (сонорные) - Л,Р
                case 'Л':
                case 'Р':
                case 'L':
                case 'R':
                    return "4";

                // Group 5: Nasals (носовые) - М,Н
                case 'М':
                case 'Н':
                case 'M':
                case 'N':
                    return "5";

                // Group 6: Fricatives (свистящие) - З,С,Ц + English S,Z
                case 'З':
                case 'С':
                case 'Ц':
                case 'S':
                case 'Z':
                    return "6";

                // Vowels and other characters are ignored (return empty string)
                // Russian vowels: А,Е,Ё,И,О,У,Ы,Э,Ю,Я,Й,Ь,Ъ
                // English vowels: A,E,I,O,U,Y,H,W
                default:
                    return "";
            }
        }
    }

    #region Transliteration

    public enum TransliterationType
    {
        GOST,
        ISO
    }

    public static class Transliteration
    {
        //ГОСТ 16876-71
        private static Dictionary<char, string> gostFront = new Dictionary<char, string>() {
            {'Є', "Eh"}, {'І', "I"},  {'і', "i"}, {'№', "#"},  {'є', "eh"}, {'А', "A"}, {'Б', "B"}, {'В', "V"}, {'Г', "G"}, {'Д', "D"}, {'Е', "E"}, {'Ё', "Jo"},
            {'Ж', "Zh"}, {'З', "Z"},  {'И', "I"}, {'Й', "JJ"}, {'К', "K"}, {'Л', "L"}, {'М', "M"}, {'Н', "N"}, {'О', "O"}, {'П', "P"}, {'Р', "R"}, {'С', "S"},
            {'Т', "T"},  {'У', "U"},  {'Ф', "F"}, {'Х', "Kh"}, {'Ц', "C"}, {'Ч', "Ch"}, {'Ш', "Sh"}, {'Щ', "Shh"}, {'Ъ', "'"}, {'Ы', "Y"}, {'Ь', ""}, {'Э', "Eh"},
            {'Ю', "Yu"}, {'Я', "Ya"}, {'а', "a"}, {'б', "b"},  {'в', "v"}, {'г', "g"}, {'д', "d"}, {'е', "e"}, {'ё', "jo"}, {'ж', "zh"}, {'з', "z"}, {'и', "i"},
            {'й', "jj"}, {'к', "k"},  {'л', "l"}, {'м', "m"},  {'н', "n"}, {'о', "o"}, {'п', "p"}, {'р', "r"}, {'с', "s"}, {'т', "t"}, {'у', "u"}, {'ф', "f"},
            {'х', "kh"}, {'ц', "c"},  {'ч', "ch"}, {'ш', "sh"}, {'щ', "shh"}, {'ъ', ""}, {'ы', "y"}, {'ь', ""}, {'э', "eh"}, {'ю', "yu"}, {'я', "ya"},
            {'«', ""}, {'»', ""}, {'—', "-"}, {' ', "_"}
        };

        private static Dictionary<string, char> gostBack = new Dictionary<string, char>();

        //ISO 9-95
        private static Dictionary<char, string> isoFront = new Dictionary<char, string>() {
            { 'Є', "Ye" }, { 'І', "I" }, { 'Ѓ', "G" }, { 'і', "i" }, { '№', "#" }, { 'є', "ye" }, { 'ѓ', "g" }, { 'А', "A" }, { 'Б', "B" }, { 'В', "V" }, { 'Г', "G" },
            { 'Д', "D" }, { 'Е', "E" }, { 'Ё', "Yo" }, { 'Ж', "Zh" }, { 'З', "Z" }, { 'И', "I" }, { 'Й', "J" }, { 'К', "K" }, { 'Л', "L" }, { 'М', "M" }, { 'Н', "N" },
            { 'О', "O" }, { 'П', "P" }, { 'Р', "R" }, { 'С', "S" }, { 'Т', "T" }, { 'У', "U" }, { 'Ф', "F" }, { 'Х', "X" }, { 'Ц', "C" }, { 'Ч', "Ch" }, { 'Ш', "Sh" },
            { 'Щ', "Shh" }, { 'Ъ', "'" }, { 'Ы', "Y" }, { 'Ь', "" }, { 'Э', "E" }, { 'Ю', "YU" }, { 'Я', "YA" }, { 'а', "a" }, { 'б', "b" }, { 'в', "v" }, { 'г', "g" },
            { 'д', "d" }, { 'е', "e" }, { 'ё', "yo" }, { 'ж', "zh" }, { 'з', "z" }, { 'и', "i" }, { 'й', "j" }, { 'к', "k" }, { 'л', "l" }, { 'м', "m" }, { 'н', "n" },
            { 'о', "o" }, { 'п', "p" }, { 'р', "r" }, { 'с', "s" }, { 'т', "t" }, { 'у', "u" }, { 'ф', "f" }, { 'х', "x" }, { 'ц', "c" }, { 'ч', "ch" }, { 'ш', "sh" },
            { 'щ', "shh" }, { 'ъ', "" }, { 'ы', "y" }, { 'ь', "" }, { 'э', "e" }, { 'ю', "yu" }, { 'я', "ya" }, { '«', "" }, { '»', "" }, { '—', "-" }, { ' ', "_" }
        };

        private static Dictionary<string, char> isoBack = new Dictionary<string, char>();

        static Transliteration()
        {
            foreach (KeyValuePair<char, string> pair in gostFront) gostBack[pair.Value] = pair.Key;
            foreach (KeyValuePair<char, string> pair in isoFront) isoBack[pair.Value] = pair.Key;
        }

        public static string Front(string text, TransliterationType type = TransliterationType.GOST)
        {
            string output = string.Empty;
            Dictionary<char, string> dict = (type == TransliterationType.ISO) ? isoFront : gostFront;
            foreach (char c in text) output += dict.ContainsKey(c) ? dict[c] : c.ToString();
            return output;
        }

        public static string Back(string text, TransliterationType type = TransliterationType.GOST)
        {
            int l = text.Length;
            string output = string.Empty;
            Dictionary<string, char> dict = (type == TransliterationType.ISO) ? isoBack : gostBack;
            int i = 0;
            while (i < l)
            {
                string s = text.Substring(i, Math.Min(3, l - i));
                do
                {
                    if (dict.ContainsKey(s))
                    {
                        output += dict[s];
                        i += s.Length;
                        break;
                    }
                    s = s.Remove(s.Length - 1);
                } while (s.Length > 0);
                i += s.Length == 0 ? 3 : 0;
            }
            return output;
        }
    }
    #endregion
}