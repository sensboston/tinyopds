/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines some specific String extensions classes:
 * Soundex and Transliteration
 * 
 ************************************************************/

using System;
using System.Text;
using System.Collections.Generic;

namespace TinyOPDS
{
    public static class StringExtensions
    {
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
                soundexes.Add(Soundex(str));
            }
            return string.Join(" ", soundexes);
        }

        public static string Soundex(string word)
        {
            word = Transliteration.Front(word, TransliterationType.ISO);
            StringBuilder result = new StringBuilder();
            if (word != null && word.Length > 0)
            {
                string previousCode = "", currentCode = "", currentLetter = "";
                result.Append(word.Substring(0, 1));
                for (int i = 1; i < word.Length; i++)
                {
                    currentLetter = word.Substring(i, 1).ToLower();
                    currentCode = "";

                    if ("bfpv".IndexOf(currentLetter) > -1) currentCode = "1";
                    else if ("cgjkqsxz".IndexOf(currentLetter) > -1) currentCode = "2";
                    else if ("dt".IndexOf(currentLetter) > -1) currentCode = "3";
                    else if (currentLetter == "l") currentCode = "4";
                    else if ("mn".IndexOf(currentLetter) > -1) currentCode = "5";
                    else if (currentLetter == "r") currentCode = "6";

                    if (currentCode != previousCode) result.Append(currentCode);
                    if (result.Length == 4) break;
                    if (currentCode != "") previousCode = currentCode;
                }
            }

            if (result.Length < 4)
                result.Append(new String('0', 4 - result.Length));

            return result.ToString().ToUpper();
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
