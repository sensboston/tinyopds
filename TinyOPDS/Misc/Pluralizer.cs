/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Pluralization support for languages with complex plural rules
 *
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TinyOPDS
{
    /// <summary>
    /// Provides pluralization support for languages with complex plural rules
    /// </summary>
    public static class Pluralizer
    {
        private static Dictionary<string, PluralizationData> languages = new Dictionary<string, PluralizationData>();
        private static bool isInitialized = false;

        /// <summary>
        /// Internal data structure for language-specific pluralization
        /// </summary>
        private class PluralizationData
        {
            public Dictionary<string, string[]> Words { get; set; }
            public Dictionary<string, char> WordGenders { get; set; } // Store gender for each word
            public Dictionary<string, string[]> Phrases { get; set; }
            public Dictionary<string, string[]> Participles { get; set; }

            // Reverse lookup dictionaries for normalization
            public Dictionary<string, string> WordReverseLookup { get; set; }
            public Dictionary<string, string> PhraseReverseLookup { get; set; }
            public Dictionary<string, string> ParticipleReverseLookup { get; set; }

            public PluralizationData()
            {
                Words = new Dictionary<string, string[]>();
                WordGenders = new Dictionary<string, char>();
                Phrases = new Dictionary<string, string[]>();
                Participles = new Dictionary<string, string[]>();
                WordReverseLookup = new Dictionary<string, string>();
                PhraseReverseLookup = new Dictionary<string, string>();
                ParticipleReverseLookup = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Initialize pluralizer with data from XDocument (called from Localizer)
        /// </summary>
        /// <param name="xml">XDocument with pluralization data</param>
        public static void LoadPluralizationData(XDocument xml)
        {
            if (xml == null) return;

            languages.Clear();

            try
            {
                var languageElements = xml.Descendants("language");

                foreach (var langElement in languageElements)
                {
                    var langCode = langElement.Attribute("code")?.Value;
                    if (string.IsNullOrEmpty(langCode)) continue;

                    var data = new PluralizationData();

                    // Load words (format: base|gender|singular|genitive-sing|genitive-plural)
                    var words = langElement.Descendants("word");
                    foreach (var wordElement in words)
                    {
                        var parts = wordElement.Value.Split('|');
                        if (parts.Length >= 5)
                        {
                            var baseForm = parts[0].ToLower();
                            var gender = parts[1].Length > 0 ? parts[1][0] : 'm'; // Default to masculine
                            var forms = new string[] { parts[2], parts[3], parts[4] };

                            data.Words[baseForm] = forms;
                            data.WordGenders[baseForm] = gender;

                            // Build reverse lookup
                            foreach (var form in forms)
                            {
                                var formLower = form.ToLower();
                                if (!data.WordReverseLookup.ContainsKey(formLower))
                                    data.WordReverseLookup[formLower] = baseForm;
                            }
                        }
                    }

                    // Load participles (format: base|masculine|feminine|neuter|plural-nom|plural-gen)
                    var participles = langElement.Descendants("participle");
                    foreach (var participleElement in participles)
                    {
                        var parts = participleElement.Value.Split('|');
                        if (parts.Length >= 6)
                        {
                            var baseForm = parts[0].ToLower();
                            // Store all forms: [masculine, feminine, neuter, plural-nom, plural-gen]
                            var forms = new string[] { parts[1], parts[2], parts[3], parts[4], parts[5] };

                            data.Participles[baseForm] = forms;

                            // Build reverse lookup for all forms
                            foreach (var form in forms)
                            {
                                var formLower = form.ToLower();
                                if (!data.ParticipleReverseLookup.ContainsKey(formLower))
                                    data.ParticipleReverseLookup[formLower] = baseForm;
                            }
                        }
                    }

                    // Load phrases (format: base|singular|genitive-sing|genitive-plural)
                    var phrases = langElement.Descendants("phrase");
                    foreach (var phraseElement in phrases)
                    {
                        var parts = phraseElement.Value.Split('|');
                        if (parts.Length >= 4)
                        {
                            var baseForm = parts[0].ToLower();
                            var forms = new string[] { parts[1], parts[2], parts[3] };

                            data.Phrases[baseForm] = forms;

                            // Build reverse lookup
                            foreach (var form in forms)
                            {
                                var formLower = form.ToLower();
                                if (!data.PhraseReverseLookup.ContainsKey(formLower))
                                    data.PhraseReverseLookup[formLower] = baseForm;
                            }
                        }
                    }

                    languages[langCode] = data;
                }

                isInitialized = languages.Count > 0;
                Log.WriteLine(LogLevel.Info, "Pluralizer initialized with {0} languages", languages.Count);
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "LoadPluralizationData() exception: {0}", e.Message);
            }
        }

        /// <summary>
        /// Apply pluralization rules to text
        /// </summary>
        /// <param name="count">Number to use for pluralization (unused but kept for compatibility)</param>
        /// <param name="languageCode">Language code (ru, uk, pl)</param>
        /// <param name="text">Text containing number+word pairs to pluralize</param>
        /// <returns>Text with correct plural forms</returns>
        public static string Apply(int count, string languageCode, string text)
        {
            // Return original text if not initialized or language not supported
            if (!isInitialized || !languages.ContainsKey(languageCode))
                return text;

            var data = languages[languageCode];
            return ApplyPluralization(text, data, languageCode);
        }

        /// <summary>
        /// Check if language is supported for pluralization
        /// </summary>
        public static bool IsLanguageSupported(string languageCode)
        {
            return isInitialized && languages.ContainsKey(languageCode);
        }

        /// <summary>
        /// Apply pluralization with support for participles after comma
        /// </summary>
        private static string ApplyPluralization(string text, PluralizationData data, string languageCode)
        {
            // Special pattern for two numbers with preposition
            var twoNumbersPattern = @"(\d+)\s+(\S+)\s+(от|від|od)\s+(\d+)\s+(\S+)";
            var twoNumbersMatch = Regex.Match(text, twoNumbersPattern);

            if (twoNumbersMatch.Success)
            {
                return ProcessTwoNumbersWithPreposition(twoNumbersMatch, data, languageCode);
            }

            // Original processing for other patterns
            var result = new StringBuilder();
            var processedPositions = new HashSet<int>();

            // Regex patterns
            var nounWithParticiplePattern = @"(\d+)\s+(\S+),\s+(\S+)"; // number + word, word
            var twoWordPattern = @"(\d+)\s+(\S+\s+\S+)";
            var oneWordPattern = @"(\d+)\s+(\S+)";

            int currentPos = 0;

            while (currentPos < text.Length)
            {
                bool matched = false;

                // Skip if already processed
                if (processedPositions.Contains(currentPos))
                {
                    currentPos++;
                    continue;
                }

                // First try to match noun with participle after comma (e.g., "3 книг, отсортированных")
                var nounParticipleRegex = new Regex(nounWithParticiplePattern);
                var nounParticipleMatch = nounParticipleRegex.Match(text, currentPos);

                if (nounParticipleMatch.Success && nounParticipleMatch.Index == currentPos)
                {
                    var numberStr = nounParticipleMatch.Groups[1].Value;
                    var noun = nounParticipleMatch.Groups[2].Value;
                    var participle = nounParticipleMatch.Groups[3].Value;

                    if (int.TryParse(numberStr, out int num))
                    {
                        // Process noun and participle together
                        var processedPair = ProcessNounWithParticiple(noun, participle, num, data, languageCode);
                        if (processedPair != null)
                        {
                            result.Append(numberStr + " " + processedPair);
                            currentPos = nounParticipleMatch.Index + nounParticipleMatch.Length;
                            matched = true;

                            // Mark positions as processed
                            for (int i = nounParticipleMatch.Index; i < currentPos; i++)
                                processedPositions.Add(i);
                        }
                    }
                }

                // If no noun+participle match, try two words (for phrases)
                if (!matched)
                {
                    var twoWordRegex = new Regex(twoWordPattern);
                    var twoWordMatch = twoWordRegex.Match(text, currentPos);

                    if (twoWordMatch.Success && twoWordMatch.Index == currentPos)
                    {
                        var numberStr = twoWordMatch.Groups[1].Value;
                        var phrase = twoWordMatch.Groups[2].Value;

                        if (int.TryParse(numberStr, out int num))
                        {
                            var processedPhrase = ProcessPhrase(phrase, num, data, languageCode);
                            if (processedPhrase != null)
                            {
                                result.Append(numberStr + " " + processedPhrase);
                                currentPos = twoWordMatch.Index + twoWordMatch.Length;
                                matched = true;

                                // Mark positions as processed
                                for (int i = twoWordMatch.Index; i < currentPos; i++)
                                    processedPositions.Add(i);
                            }
                        }
                    }
                }

                // If no phrase match, try single word
                if (!matched)
                {
                    var oneWordRegex = new Regex(oneWordPattern);
                    var oneWordMatch = oneWordRegex.Match(text, currentPos);

                    if (oneWordMatch.Success && oneWordMatch.Index == currentPos)
                    {
                        var numberStr = oneWordMatch.Groups[1].Value;
                        var word = oneWordMatch.Groups[2].Value;

                        if (int.TryParse(numberStr, out int num))
                        {
                            var processedWord = ProcessWord(word, num, data, languageCode);
                            if (processedWord != null)
                            {
                                result.Append(numberStr + " " + processedWord);
                                currentPos = oneWordMatch.Index + oneWordMatch.Length;
                                matched = true;

                                // Mark positions as processed
                                for (int i = oneWordMatch.Index; i < currentPos; i++)
                                    processedPositions.Add(i);
                            }
                        }
                    }
                }

                // If no match, copy character and advance
                if (!matched)
                {
                    if (currentPos < text.Length)
                    {
                        result.Append(text[currentPos]);
                        currentPos++;
                    }
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Process two numbers with preposition between them
        /// </summary>
        private static string ProcessTwoNumbersWithPreposition(Match match, PluralizationData data, string languageCode)
        {
            int firstNumber = int.Parse(match.Groups[1].Value);
            string firstWord = match.Groups[2].Value;
            string preposition = match.Groups[3].Value;
            int secondNumber = int.Parse(match.Groups[4].Value);
            string secondWord = match.Groups[5].Value;

            // Process first word normally
            string processedFirst = ProcessWord(firstWord, firstNumber, data, languageCode);
            if (processedFirst == null) processedFirst = firstWord;

            // Process second word with special rule for prepositions
            string processedSecond = ProcessWordAfterPreposition(secondWord, secondNumber, data, languageCode);
            if (processedSecond == null) processedSecond = secondWord;

            return string.Format("{0} {1} {2} {3} {4}",
                firstNumber, processedFirst, preposition, secondNumber, processedSecond);
        }

        /// <summary>
        /// Process word after preposition with special rules
        /// </summary>
        private static string ProcessWordAfterPreposition(string word, int number, PluralizationData data, string languageCode)
        {
            var wordLower = word.ToLower();

            // Find base form
            string baseForm = null;
            if (data.WordReverseLookup.ContainsKey(wordLower))
            {
                baseForm = data.WordReverseLookup[wordLower];
            }
            else if (data.Words.ContainsKey(wordLower))
            {
                baseForm = wordLower;
            }

            if (baseForm != null && data.Words.ContainsKey(baseForm))
            {
                var forms = data.Words[baseForm];

                // Special rule for numbers 2-4 after prepositions in Slavic languages
                int formIndex = GetFormIndexAfterPreposition(number, languageCode);
                var newForm = forms[formIndex];

                // Preserve case pattern
                return PreserveCase(word, newForm);
            }

            return null;
        }

        /// <summary>
        /// Get form index for word after preposition
        /// </summary>
        private static int GetFormIndexAfterPreposition(int number, string languageCode)
        {
            number = Math.Abs(number);

            if (languageCode == "ru" || languageCode == "uk" || languageCode == "pl")
            {
                // After prepositions like "от", "від", "od", use genitive plural for 2-4
                int lastDigit = number % 10;
                int lastTwoDigits = number % 100;

                // 11-14 are exceptions - always genitive plural
                if (lastTwoDigits >= 11 && lastTwoDigits <= 14)
                    return 2; // genitive plural

                // Numbers >= 1000 always use genitive plural
                if (number >= 1000)
                    return 2; // genitive plural

                // 1 (except 11) - genitive singular
                if (lastDigit == 1)
                    return 1; // genitive singular (после предлога)

                // 2-4 (except 12-14) - GENITIVE PLURAL after preposition!
                if (lastDigit >= 2 && lastDigit <= 4)
                    return 2; // genitive plural (после предлога!)

                // 0, 5-9 and others - genitive plural
                return 2; // genitive plural
            }

            // Default to first form
            return 0;
        }

        /// <summary>
        /// Process noun with participle after comma
        /// </summary>
        private static string ProcessNounWithParticiple(string noun, string participle, int number,
            PluralizationData data, string languageCode)
        {
            var nounLower = noun.ToLower();
            var participleLower = participle.ToLower();

            // Find noun base form and gender
            string nounBase = null;
            char gender = 'm';

            if (data.WordReverseLookup.ContainsKey(nounLower))
            {
                nounBase = data.WordReverseLookup[nounLower];
            }
            else if (data.Words.ContainsKey(nounLower))
            {
                nounBase = nounLower;
            }

            if (nounBase != null && data.WordGenders.ContainsKey(nounBase))
            {
                gender = data.WordGenders[nounBase];
            }

            // Find participle base form
            string participleBase = null;
            if (data.ParticipleReverseLookup.ContainsKey(participleLower))
            {
                participleBase = data.ParticipleReverseLookup[participleLower];
            }
            else if (data.Participles.ContainsKey(participleLower))
            {
                participleBase = participleLower;
            }

            // Process both if found
            if (nounBase != null && participleBase != null &&
                data.Words.ContainsKey(nounBase) && data.Participles.ContainsKey(participleBase))
            {
                // Get noun form
                var nounForms = data.Words[nounBase];
                int nounFormIndex = GetFormIndex(number, languageCode);
                var newNoun = nounForms[nounFormIndex];

                // Get participle form based on number and gender
                var participleForms = data.Participles[participleBase];
                var newParticiple = GetParticipleForm(participleForms, number, gender, languageCode);

                // Preserve case
                newNoun = PreserveCase(noun, newNoun);
                newParticiple = PreserveCase(participle, newParticiple);

                return newNoun + ", " + newParticiple;
            }

            return null;
        }

        /// <summary>
        /// Get correct participle form based on number and gender
        /// </summary>
        private static string GetParticipleForm(string[] participleForms, int number, char gender, string languageCode)
        {
            // participleForms: [masculine, feminine, neuter, plural-nom, plural-gen]
            number = Math.Abs(number);

            if (languageCode == "ru" || languageCode == "uk")
            {
                int lastDigit = number % 10;
                int lastTwoDigits = number % 100;

                // Singular (1, except 11)
                if (lastDigit == 1 && lastTwoDigits != 11 && number < 1000)
                {
                    // Return form based on gender
                    switch (gender)
                    {
                        case 'f': return participleForms[1]; // feminine
                        case 'n': return participleForms[2]; // neuter
                        default: return participleForms[0]; // masculine
                    }
                }
                // 2-4 (except 12-14) - plural nominative
                else if (lastDigit >= 2 && lastDigit <= 4 &&
                         (lastTwoDigits < 12 || lastTwoDigits > 14) &&
                         number < 1000)
                {
                    return participleForms[3]; // plural nominative
                }
                // Other cases - plural genitive
                else
                {
                    return participleForms[4]; // plural genitive
                }
            }
            else if (languageCode == "pl")
            {
                int lastDigit = number % 10;
                int lastTwoDigits = number % 100;

                // Singular
                if (number == 1)
                {
                    switch (gender)
                    {
                        case 'f': return participleForms[1]; // feminine
                        case 'n': return participleForms[2]; // neuter
                        default: return participleForms[0]; // masculine
                    }
                }
                // 2-4 (except 12-14) - plural nominative
                else if (lastDigit >= 2 && lastDigit <= 4 &&
                         (lastTwoDigits < 12 || lastTwoDigits > 14))
                {
                    return participleForms[3]; // plural nominative
                }
                // Other cases - plural genitive
                else
                {
                    return participleForms[4]; // plural genitive
                }
            }

            return participleForms[0]; // Default to masculine
        }

        /// <summary>
        /// Process phrase with adjective and noun
        /// </summary>
        private static string ProcessPhrase(string phrase, int number, PluralizationData data, string languageCode)
        {
            var phraseLower = phrase.ToLower();

            // Try to find base form through reverse lookup
            string baseForm = null;
            if (data.PhraseReverseLookup.ContainsKey(phraseLower))
            {
                baseForm = data.PhraseReverseLookup[phraseLower];
            }
            else if (data.Phrases.ContainsKey(phraseLower))
            {
                baseForm = phraseLower;
            }

            if (baseForm != null && data.Phrases.ContainsKey(baseForm))
            {
                var forms = data.Phrases[baseForm];
                int formIndex = GetFormIndex(number, languageCode);
                var newForm = forms[formIndex];

                // Preserve case pattern
                return PreserveCase(phrase, newForm);
            }

            return null;
        }

        /// <summary>
        /// Process single word
        /// </summary>
        private static string ProcessWord(string word, int number, PluralizationData data, string languageCode)
        {
            var wordLower = word.ToLower();

            // Try to find base form through reverse lookup
            string baseForm = null;
            if (data.WordReverseLookup.ContainsKey(wordLower))
            {
                baseForm = data.WordReverseLookup[wordLower];
            }
            else if (data.Words.ContainsKey(wordLower))
            {
                baseForm = wordLower;
            }

            if (baseForm != null && data.Words.ContainsKey(baseForm))
            {
                var forms = data.Words[baseForm];
                int formIndex = GetFormIndex(number, languageCode);
                var newForm = forms[formIndex];

                // Preserve case pattern
                return PreserveCase(word, newForm);
            }

            return null;
        }

        /// <summary>
        /// Determine which form to use based on number and language rules
        /// </summary>
        private static int GetFormIndex(int number, string languageCode)
        {
            number = Math.Abs(number);

            if (languageCode == "ru" || languageCode == "uk")
            {
                // Russian/Ukrainian rules
                int lastDigit = number % 10;
                int lastTwoDigits = number % 100;

                // 11-14 are exceptions - always genitive plural
                if (lastTwoDigits >= 11 && lastTwoDigits <= 14)
                    return 2; // genitive plural

                // Numbers >= 1000 always use genitive plural
                if (number >= 1000)
                    return 2; // genitive plural

                // 1 (except 11) - nominative singular
                if (lastDigit == 1)
                    return 0; // nominative singular

                // 2-4 (except 12-14) - genitive singular
                if (lastDigit >= 2 && lastDigit <= 4)
                    return 1; // genitive singular

                // 0, 5-9 and others - genitive plural
                return 2; // genitive plural
            }
            else if (languageCode == "pl")
            {
                // Polish rules
                int lastDigit = number % 10;
                int lastTwoDigits = number % 100;

                // 1 - nominative singular
                if (number == 1)
                    return 0; // nominative singular

                // 12-14 are exceptions - genitive plural
                if (lastTwoDigits >= 12 && lastTwoDigits <= 14)
                    return 2; // genitive plural

                // 2-4 (except 12-14) - nominative plural
                if (lastDigit >= 2 && lastDigit <= 4)
                    return 1; // nominative plural

                // 0, 5-9 and others - genitive plural
                return 2; // genitive plural
            }

            // Default to first form
            return 0;
        }

        /// <summary>
        /// Preserve case pattern from original to new text
        /// </summary>
        private static string PreserveCase(string original, string newText)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(newText))
                return newText;

            // All uppercase
            if (original == original.ToUpper())
                return newText.ToUpper();

            // All lowercase
            if (original == original.ToLower())
                return newText.ToLower();

            // Title case (first letter uppercase)
            if (char.IsUpper(original[0]))
            {
                if (newText.Length > 0)
                {
                    return char.ToUpper(newText[0]) +
                           (newText.Length > 1 ? newText.Substring(1).ToLower() : "");
                }
            }

            // Mixed case - try to preserve pattern for multi-word phrases
            var originalWords = original.Split(' ');
            var newWords = newText.Split(' ');

            if (originalWords.Length == newWords.Length && originalWords.Length > 1)
            {
                var result = new StringBuilder();
                for (int i = 0; i < originalWords.Length; i++)
                {
                    if (i > 0) result.Append(' ');

                    // Apply case pattern from each original word to corresponding new word
                    if (originalWords[i].Length > 0 && newWords[i].Length > 0 &&
                        char.IsUpper(originalWords[i][0]))
                    {
                        result.Append(char.ToUpper(newWords[i][0]));
                        if (newWords[i].Length > 1)
                            result.Append(newWords[i].Substring(1).ToLower());
                    }
                    else
                    {
                        result.Append(newWords[i].ToLower());
                    }
                }
                return result.ToString();
            }

            // Default: return as is
            return newText;
        }
    }
}