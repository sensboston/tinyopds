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

        private static readonly Dictionary<char, char> RussianSoundexGroups = new Dictionary<char, char>
        {
            {'Б', '1'}, {'П', '1'}, {'В', '1'}, {'Ф', '1'},
            {'Г', '2'}, {'К', '2'}, {'Х', '2'},
            {'Д', '3'}, {'Т', '3'},
            {'Л', '4'}, {'Р', '4'},
            {'М', '5'}, {'Н', '5'},
            {'З', '6'}, {'С', '6'}, {'Ж', '6'}, {'Ш', '6'}, {'Щ', '6'}, {'Ч', '6'}, {'Ц', '6'}
        };

        private static readonly Dictionary<char, char> EnglishSoundexGroups = new Dictionary<char, char>
        {
            {'B', '1'}, {'F', '1'}, {'P', '1'}, {'V', '1'},
            {'C', '2'}, {'G', '2'}, {'J', '2'}, {'K', '2'}, {'Q', '2'}, {'S', '2'}, {'X', '2'}, {'Z', '2'},
            {'D', '3'}, {'T', '3'},
            {'L', '4'},
            {'M', '5'}, {'N', '5'},
            {'R', '6'}
        };

        private static readonly HashSet<char> RussianVowels = new HashSet<char>
        {
            'А', 'Е', 'Ё', 'И', 'О', 'У', 'Ы', 'Э', 'Ю', 'Я', 'Й', 'Ь', 'Ъ'
        };

        private static readonly HashSet<char> EnglishVowels = new HashSet<char>
        {
            'A', 'E', 'I', 'O', 'U', 'Y', 'H', 'W'
        };

        /// <summary>
        /// Universal Soundex algorithm for Russian and English languages
        /// </summary>
        /// <param name="word">Input word in Russian or English</param>
        /// <returns>4-character Soundex code</returns>
        public static string Soundex(string word)
        {
            if (string.IsNullOrEmpty(word))
                return "0000";

            word = word.Trim().ToUpper();
            if (word.Length == 0)
                return "0000";

            char firstChar = word[0];
            bool isRussian = IsRussianChar(firstChar);

            var soundexGroups = isRussian ? RussianSoundexGroups : EnglishSoundexGroups;
            var vowels = isRussian ? RussianVowels : EnglishVowels;

            StringBuilder result = new StringBuilder();
            result.Append(firstChar);

            char prevCode = GetSoundexCode(firstChar, soundexGroups);

            for (int i = 1; i < word.Length && result.Length < 4; i++)
            {
                char currentChar = word[i];

                if (!IsValidChar(currentChar, isRussian))
                    continue;

                char currentCode = GetSoundexCode(currentChar, soundexGroups);

                if (currentCode != '0' && currentCode != prevCode)
                {
                    result.Append(currentCode);
                    prevCode = currentCode;
                }
                else if (vowels.Contains(currentChar))
                {
                    prevCode = '0';
                }
            }

            while (result.Length < 4)
                result.Append('0');

            return result.ToString();
        }

        private static bool IsRussianChar(char c)
        {
            return (c >= 'А' && c <= 'Я') || (c >= 'а' && c <= 'я') || c == 'Ё' || c == 'ё';
        }

        private static bool IsValidChar(char c, bool isRussian)
        {
            if (isRussian)
                return IsRussianChar(c);
            else
                return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        private static char GetSoundexCode(char c, Dictionary<char, char> soundexGroups)
        {
            return soundexGroups.ContainsKey(c) ? soundexGroups[c] : '0';
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

    public static class StringUtils
    {
        private static readonly Dictionary<char, char> RussianSoundexGroups = new Dictionary<char, char>
        {
            {'Б', '1'}, {'П', '1'}, {'В', '1'}, {'Ф', '1'},
            {'Г', '2'}, {'К', '2'}, {'Х', '2'},
            {'Д', '3'}, {'Т', '3'},
            {'Л', '4'}, {'Р', '4'},
            {'М', '5'}, {'Н', '5'},
            {'З', '6'}, {'С', '6'}, {'Ж', '6'}, {'Ш', '6'}, {'Щ', '6'}, {'Ч', '6'}, {'Ц', '6'}
        };

        private static readonly Dictionary<char, char> EnglishSoundexGroups = new Dictionary<char, char>
        {
            {'B', '1'}, {'F', '1'}, {'P', '1'}, {'V', '1'},
            {'C', '2'}, {'G', '2'}, {'J', '2'}, {'K', '2'}, {'Q', '2'}, {'S', '2'}, {'X', '2'}, {'Z', '2'},
            {'D', '3'}, {'T', '3'},
            {'L', '4'},
            {'M', '5'}, {'N', '5'},
            {'R', '6'}
        };

        private static readonly HashSet<char> RussianVowels = new HashSet<char>
        {
            'А', 'Е', 'Ё', 'И', 'О', 'У', 'Ы', 'Э', 'Ю', 'Я', 'Й', 'Ь', 'Ъ'
        };

        private static readonly HashSet<char> EnglishVowels = new HashSet<char>
        {
            'A', 'E', 'I', 'O', 'U', 'Y', 'H', 'W'
        };

        /// <summary>
        /// Universal Soundex algorithm for Russian and English languages
        /// Auto-detects language by first character and applies appropriate phonetic rules
        /// </summary>
        /// <param name="word">Input word in Russian or English</param>
        /// <returns>4-character Soundex code (first char + 3 digits)</returns>
        public static string Soundex(string word)
        {
            if (string.IsNullOrEmpty(word))
                return "0000";

            word = word.Trim().ToUpper();
            if (word.Length == 0)
                return "0000";

            char firstChar = word[0];
            bool isRussian = IsRussianChar(firstChar);

            var soundexGroups = isRussian ? RussianSoundexGroups : EnglishSoundexGroups;
            var vowels = isRussian ? RussianVowels : EnglishVowels;

            StringBuilder result = new StringBuilder();
            result.Append(firstChar);

            char prevCode = GetSoundexCode(firstChar, soundexGroups);

            for (int i = 1; i < word.Length && result.Length < 4; i++)
            {
                char currentChar = word[i];

                if (!IsValidChar(currentChar, isRussian))
                    continue;

                char currentCode = GetSoundexCode(currentChar, soundexGroups);

                if (currentCode != '0' && currentCode != prevCode)
                {
                    result.Append(currentCode);
                    prevCode = currentCode;
                }
                else if (vowels.Contains(currentChar))
                {
                    prevCode = '0';
                }
            }

            while (result.Length < 4)
                result.Append('0');

            return result.ToString();
        }

        private static bool IsRussianChar(char c)
        {
            return (c >= 'А' && c <= 'Я') || (c >= 'а' && c <= 'я') || c == 'Ё' || c == 'ё';
        }

        private static bool IsValidChar(char c, bool isRussian)
        {
            if (isRussian)
                return IsRussianChar(c);
            else
                return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        private static char GetSoundexCode(char c, Dictionary<char, char> soundexGroups)
        {
            return soundexGroups.ContainsKey(c) ? soundexGroups[c] : '0';
        }
    }

    #endregion
}