using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TinyOPDS
{
    #region String extensions
    public static class StringExtensions
    {
        public static string ToStringWithDeclaration(this XDocument doc)
        {
            StringBuilder sb = new StringBuilder();
            XmlWriterSettings xws = new XmlWriterSettings
            {
                OmitXmlDeclaration = false,
                Indent = true
            };
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
                    soundexes.Add(SoundexRuEn.Compute(str, 6));
                }
            }
            return string.Join(" ", soundexes);
        }

        public static string Soundex(string word)
        {
            return SoundexRuEn.Compute(word, 6);
        }
    }
    #endregion

    #region English/Russian improved Soundex
    public static class SoundexRuEn
    {
        public static string Compute(string input, int length = 6)
        {
            if (string.IsNullOrWhiteSpace(input) || length < 1) return string.Empty;

            string s = input.Trim();
            int firstIdx = IndexOfLetter(s);
            if (firstIdx < 0) return string.Empty;

            bool isCyr = IsCyrillic(s[firstIdx]);
            s = isCyr ? s.ToUpperInvariant() : RemoveDiacritics(s).ToUpperInvariant();

            firstIdx = IndexOfLetter(s);
            if (firstIdx < 0) return string.Empty;

            char firstLetter = s[firstIdx];
            var consonantMap = isCyr ? CyrillicConsonantMap : LatinConsonantMap;
            var vowelMap = isCyr ? CyrillicVowelMap : LatinVowelMap;

            var sb = new StringBuilder(length);
            sb.Append(firstLetter);

            // Collect consonant codes in groups separated by vowels
            var codeGroups = new List<List<char>>();
            var currentGroup = new List<char>();

            for (int i = firstIdx + 1; i < s.Length; i++)
            {
                char c = s[i];
                if (!char.IsLetter(c)) continue;

                // Check if it's a consonant
                if (consonantMap.TryGetValue(c, out char code))
                {
                    currentGroup.Add(code);
                }
                // Check if it's a vowel
                else if (vowelMap.TryGetValue(c, out code))
                {
                    // Vowel ends the consonant group
                    if (currentGroup.Count > 0)
                    {
                        codeGroups.Add(new List<char>(currentGroup));
                        currentGroup.Clear();
                    }

                    // Add vowel code if within first 4 positions
                    if (sb.Length <= 4 && code != '0')
                    {
                        codeGroups.Add(new List<char> { code });
                    }
                }
            }

            // Add remaining group if any
            if (currentGroup.Count > 0)
            {
                codeGroups.Add(currentGroup);
            }

            // Sort consonants within each group for transposition resistance
            foreach (var group in codeGroups)
            {
                // Sort only if it's a consonant group (codes 1-6)
                // Don't sort vowel codes (7-9) as they represent single vowels
                if (group.Count > 1 && group.All(ch => ch >= '1' && ch <= '6'))
                {
                    group.Sort();
                }
            }

            // Build the final code
            char prevCode = '0';
            foreach (var group in codeGroups)
            {
                foreach (var code in group)
                {
                    if (sb.Length >= length) break;

                    // Add code if it's different from previous
                    if (code != prevCode)
                    {
                        sb.Append(code);
                        prevCode = code;
                    }
                }
                if (sb.Length >= length) break;
            }

            // Pad with zeros
            while (sb.Length < length) sb.Append('0');
            if (sb.Length > length) sb.Length = length;
            return sb.ToString();
        }

        private static readonly Dictionary<char, char> LatinConsonantMap = new Dictionary<char, char>
        {
            ['B'] = '1',
            ['F'] = '1',
            ['P'] = '1',
            ['V'] = '1',
            ['C'] = '2',
            ['G'] = '2',
            ['J'] = '2',
            ['K'] = '2',
            ['Q'] = '2',
            ['S'] = '2',
            ['X'] = '2',
            ['Z'] = '2',
            ['D'] = '3',
            ['T'] = '3',
            ['L'] = '4',
            ['M'] = '5',
            ['N'] = '5',
            ['R'] = '6'
        };

        private static readonly Dictionary<char, char> CyrillicConsonantMap = new Dictionary<char, char>
        {
            ['Б'] = '1',
            ['П'] = '1',
            ['Ф'] = '1',
            ['В'] = '1',
            ['Г'] = '2',
            ['К'] = '2',
            ['Х'] = '2',
            ['Ж'] = '2',
            ['З'] = '2',
            ['С'] = '2',
            ['Ц'] = '2',
            ['Ч'] = '2',
            ['Ш'] = '2',
            ['Щ'] = '2',
            ['Д'] = '3',
            ['Т'] = '3',
            ['Л'] = '4',
            ['М'] = '5',
            ['Н'] = '5',
            ['Р'] = '6'
        };

        // Vowel groups for better phonetic matching
        private static readonly Dictionary<char, char> LatinVowelMap = new Dictionary<char, char>
        {
            ['A'] = '7',
            ['O'] = '7',    // A-O group (similar in some accents)
            ['E'] = '8',
            ['I'] = '8',    // E-I group (often confused)
            ['U'] = '9',
            ['Y'] = '9',    // U-Y group 
            ['H'] = '0',
            ['W'] = '0'     // Ignored consonants in English Soundex
        };

        private static readonly Dictionary<char, char> CyrillicVowelMap = new Dictionary<char, char>
        {
            ['А'] = '7',
            ['О'] = '7',    // А-О group (аканье)
            ['И'] = '8',
            ['Е'] = '8',
            ['Ы'] = '8',    // И-Е-Ы group (common confusion)
            ['У'] = '9',
            ['Ю'] = '9',    // У-Ю group
            ['Э'] = '8',    // Э similar to Е
            ['Я'] = '7',    // Я can sound like А
            ['Ё'] = '8',    // Ё similar to Е
            ['Й'] = '0',
            ['Ь'] = '0',
            ['Ъ'] = '0'     // Soft/hard signs and Й - ignored
        };

        private static char CodeOf(char c, Dictionary<char, char> map)
            => map.TryGetValue(c, out var code) ? code : '0';

        private static bool IsCyrillic(char c)
            => (c >= '\u0400' && c <= '\u04FF') || (c >= '\u0500' && c <= '\u052F');

        private static int IndexOfLetter(string s)
        {
            for (int i = 0; i < s.Length; i++)
                if (char.IsLetter(s[i])) return i;
            return -1;
        }

        private static string RemoveDiacritics(string text)
        {
            var norm = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(norm.Length);
            foreach (var ch in norm)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
    #endregion

    #region Transliteration

    public enum TransliterationType
    {
        GOST,
        ISO
    }

    public static class Transliteration
    {
        private static readonly Dictionary<char, string> gostFront = new Dictionary<char, string>() {
            {'Є', "Eh"}, {'І', "I"},  {'і', "i"}, {'№', "#"},  {'є', "eh"}, {'А', "A"}, {'Б', "B"}, {'В', "V"}, {'Г', "G"}, {'Д', "D"}, {'Е', "E"}, {'Ё', "Jo"},
            {'Ж', "Zh"}, {'З', "Z"},  {'И', "I"}, {'Й', "JJ"}, {'К', "K"}, {'Л', "L"}, {'М', "M"}, {'Н', "N"}, {'О', "O"}, {'П', "P"}, {'Р', "R"}, {'С', "S"},
            {'Т', "T"},  {'У', "U"},  {'Ф', "F"}, {'Х', "Kh"}, {'Ц', "C"}, {'Ч', "Ch"}, {'Ш', "Sh"}, {'Щ', "Shh"}, {'Ъ', "'"}, {'Ы', "Y"}, {'Ь', ""}, {'Э', "Eh"},
            {'Ю', "Yu"}, {'Я', "Ya"}, {'а', "a"}, {'б', "b"},  {'в', "v"}, {'г', "g"}, {'д', "d"}, {'е', "e"}, {'ё', "jo"}, {'ж', "zh"}, {'з', "z"}, {'и', "i"},
            {'й', "jj"}, {'к', "k"},  {'л', "l"}, {'м', "m"},  {'н', "n"}, {'о', "o"}, {'п', "p"}, {'р', "r"}, {'с', "s"}, {'т', "t"}, {'у', "u"}, {'ф', "f"},
            {'х', "kh"}, {'ц', "c"},  {'ч', "ch"}, {'ш', "sh"}, {'щ', "shh"}, {'ъ', ""}, {'ы', "y"}, {'ь', ""}, {'э', "eh"}, {'ю', "yu"}, {'я', "ya"},
            {'«', ""}, {'»', ""}, {'—', "-"}, {' ', "_"}
        };

        private static readonly Dictionary<string, char> gostBack = new Dictionary<string, char>();

        private static readonly Dictionary<char, string> isoFront = new Dictionary<char, string>() {
            { 'Є', "Ye" }, { 'І', "I" }, { 'Ѓ', "G" }, { 'і', "i" }, { '№', "#" }, { 'є', "ye" }, { 'ѓ', "g" }, { 'А', "A" }, { 'Б', "B" }, { 'В', "V" }, { 'Г', "G" },
            { 'Д', "D" }, { 'Е', "E" }, { 'Ё', "Yo" }, { 'Ж', "Zh" }, { 'З', "Z" }, { 'И', "I" }, { 'Й', "J" }, { 'К', "K" }, { 'Л', "L" }, { 'М', "M" }, { 'Н', "N" },
            { 'О', "O" }, { 'П', "P" }, { 'Р', "R" }, { 'С', "S" }, { 'Т', "T" }, { 'У', "U" }, { 'Ф', "F" }, { 'Х', "X" }, { 'Ц', "C" }, { 'Ч', "Ch" }, { 'Ш', "Sh" },
            { 'Щ', "Shh" }, { 'Ъ', "'" }, { 'Ы', "Y" }, { 'Ь', "" }, { 'Э', "E" }, { 'Ю', "YU" }, { 'Я', "YA" }, { 'а', "a" }, { 'б', "b" }, { 'в', "v" }, { 'г', "g" },
            { 'д', "d" }, { 'е', "e" }, { 'ё', "yo" }, { 'ж', "zh" }, { 'з', "z" }, { 'и', "i" }, { 'й', "j" }, { 'к', "k" }, { 'л', "l" }, { 'м', "m" }, { 'н', "n" },
            { 'о', "o" }, { 'п', "p" }, { 'р', "r" }, { 'с', "s" }, { 'т', "t" }, { 'у', "u" }, { 'ф', "f" }, { 'х', "x" }, { 'ц', "c" }, { 'ч', "ch" }, { 'ш', "sh" },
            { 'щ', "shh" }, { 'ъ', "" }, { 'ы', "y" }, { 'ь', "" }, { 'э', "e" }, { 'ю', "yu" }, { 'я', "ya" }, { '«', "" }, { '»', "" }, { '—', "-" }, { ' ', "_" }
        };

        private static readonly Dictionary<string, char> isoBack = new Dictionary<string, char>();

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
        public static string Soundex(string word)
        {
            return SoundexRuEn.Compute(word, 6);
        }

        /// <summary>
        /// Applies plural form rules for Slavic languages (Russian, Ukrainian, Polish)
        /// Processes ALL number+word pairs in the text
        /// </summary>
        /// <param name="count">Unused parameter (kept for backward compatibility)</param>
        /// <param name="languageCode">Language code (ru, uk, pl)</param>
        /// <param name="text">Text containing number and word pairs to pluralize</param>
        /// <returns>Text with correct plural forms</returns>
        public static string ApplyPluralForm(int count, string languageCode, string text)
        {
            // Only process Slavic languages that require pluralization
            if (languageCode != "ru" && languageCode != "uk" && languageCode != "pl")
                return text;

            // Dictionary with word forms: [singular, genitive singular, genitive plural]
            var wordForms = new Dictionary<string, Dictionary<string, string[]>>
            {
                ["ru"] = new Dictionary<string, string[]>
                {
                    // Books
                    ["книга"] = new[] { "книга", "книги", "книг" },
                    ["книги"] = new[] { "книга", "книги", "книг" },
                    ["книг"] = new[] { "книга", "книги", "книг" },

                    // Series
                    ["серия"] = new[] { "серия", "серии", "серий" },
                    ["серии"] = new[] { "серия", "серии", "серий" },
                    ["серий"] = new[] { "серия", "серии", "серий" },

                    // Authors
                    ["автор"] = new[] { "автор", "автора", "авторов" },
                    ["автора"] = new[] { "автор", "автора", "авторов" },
                    ["авторов"] = new[] { "автор", "автора", "авторов" }
                },
                ["uk"] = new Dictionary<string, string[]>
                {
                    // Books
                    ["книга"] = new[] { "книга", "книги", "книг" },
                    ["книги"] = new[] { "книга", "книги", "книг" },
                    ["книг"] = new[] { "книга", "книги", "книг" },

                    // Series  
                    ["серія"] = new[] { "серія", "серії", "серій" },
                    ["серії"] = new[] { "серія", "серії", "серій" },
                    ["серій"] = new[] { "серія", "серії", "серій" },

                    // Authors
                    ["автор"] = new[] { "автор", "автора", "авторів" },
                    ["автора"] = new[] { "автор", "автора", "авторів" },
                    ["авторів"] = new[] { "автор", "автора", "авторів" }
                },
                ["pl"] = new Dictionary<string, string[]>
                {
                    // Books
                    ["książka"] = new[] { "książka", "książki", "książek" },
                    ["książki"] = new[] { "książka", "książki", "książek" },
                    ["książek"] = new[] { "książka", "książki", "książek" },

                    // Series
                    ["cykl"] = new[] { "cykl", "cykle", "cykli" },
                    ["cykle"] = new[] { "cykl", "cykle", "cykli" },
                    ["cykli"] = new[] { "cykl", "cykle", "cykli" },

                    // Authors
                    ["autor"] = new[] { "autor", "autorów", "autorów" },
                    ["autorzy"] = new[] { "autor", "autorów", "autorów" },
                    ["autorów"] = new[] { "autor", "autorów", "autorów" }
                }
            };

            if (!wordForms.ContainsKey(languageCode))
                return text;

            var languageForms = wordForms[languageCode];

            // Create reverse lookup dictionary to normalize words to base form
            var reverseDict = new Dictionary<string, string>();
            foreach (var kvp in languageForms)
            {
                // kvp.Key is already a form (could be any form)
                // kvp.Value[0] is always the base form (singular nominative)
                var baseForm = kvp.Value[0];

                // Map all forms to the base form
                foreach (var form in kvp.Value)
                {
                    if (!reverseDict.ContainsKey(form))
                        reverseDict[form] = baseForm;
                }
            }

            // Build a new string with replacements
            var result = new System.Text.StringBuilder();
            var pattern = @"(\d+)\s+(\S+)";
            var regex = new System.Text.RegularExpressions.Regex(pattern);

            int lastIndex = 0;
            var matches = regex.Matches(text);

            // Process each match
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                // Add text before this match
                result.Append(text.Substring(lastIndex, match.Index - lastIndex));

                var numberStr = match.Groups[1].Value;
                var word = match.Groups[2].Value;
                var wordLower = word.ToLower();

                // Try to normalize the word to base form
                string baseForm = wordLower;
                if (reverseDict.ContainsKey(wordLower))
                {
                    baseForm = reverseDict[wordLower];
                }

                if (int.TryParse(numberStr, out int num) && languageForms.ContainsKey(baseForm))
                {
                    // Get correct form based on the number
                    var forms = languageForms[baseForm];
                    int formIndex = GetPluralFormIndex(num, languageCode);
                    var newForm = forms[formIndex];

                    // Preserve original case
                    if (char.IsUpper(word[0]))
                        newForm = char.ToUpper(newForm[0]) + newForm.Substring(1);

                    result.Append(numberStr + " " + newForm);
                }
                else
                {
                    // Keep original if not in dictionary
                    result.Append(match.Value);
                }

                lastIndex = match.Index + match.Length;
            }

            // Add remaining text after last match
            result.Append(text.Substring(lastIndex));

            return result.ToString();
        }

        /// <summary>
        /// Determines plural form index based on number and language rules
        /// </summary>
        private static int GetPluralFormIndex(int number, string languageCode)
        {
            // Get absolute value for negative numbers
            number = Math.Abs(number);

            if (languageCode == "ru" || languageCode == "uk")
            {
                // Russian/Ukrainian rules
                int lastDigit = number % 10;
                int lastTwoDigits = number % 100;

                // 11-14 are exceptions - always genitive plural
                if (lastTwoDigits >= 11 && lastTwoDigits <= 14)
                    return 2; // genitive plural

                // Special case for thousands: numbers >= 1000 always use genitive plural
                // (тысяча авторов, две тысячи авторов, двадцать три тысячи тридцать один авторов)
                if (number >= 1000)
                    return 2; // genitive plural

                // 1 - singular
                if (lastDigit == 1)
                    return 0; // singular (1, 21, 31... but only up to 999)

                // 2-4 (except 12-14) - genitive singular  
                if (lastDigit >= 2 && lastDigit <= 4)
                    return 1; // genitive singular (2-4, 22-24, 32-34... up to 999)

                // 0, 5-9 and others - genitive plural
                return 2; // genitive plural
            }
            else if (languageCode == "pl")
            {
                // Polish rules  
                int lastDigit = number % 10;
                int lastTwoDigits = number % 100;

                // 1 - singular
                if (number == 1)
                    return 0; // singular

                // 12-14 are exceptions - genitive plural
                if (lastTwoDigits >= 12 && lastTwoDigits <= 14)
                    return 2; // genitive plural

                // 2-4 (except 12-14) - nominative plural
                if (lastDigit >= 2 && lastDigit <= 4)
                    return 1; // nominative plural

                // 0, 5-9 and others - genitive plural
                return 2; // genitive plural
            }

            return 0; // default to singular
        }


        #endregion
    }
}