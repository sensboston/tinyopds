/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the Book class
 * 
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TinyOPDS.Data
{
    /// <summary>
    /// Supported book types
    /// </summary>
    public enum BookType
    {
        FB2,
        EPUB,
    }

    /// <summary>
    /// Book sequence information
    /// </summary>
    public class BookSequenceInfo
    {
        public string Name { get; set; }
        public uint NumberInSequence { get; set; }

        public BookSequenceInfo(string name, uint number = 0)
        {
            Name = name;
            NumberInSequence = number;
        }
    }

    /// <summary>
    /// Base data class
    /// </summary>
    public class Book
    {
        // Pattern for FBD (Fiction Book Designer) IDs
        private static readonly Regex FBD_ID_PATTERN = new Regex(
            @"^FBD-[A-Z0-9]{6,10}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{12,16}$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public Book(string fileName = "")
        {
            Version = 1;
            FileName = fileName;
            if (!string.IsNullOrEmpty(FileName) && FileName.IndexOf(Library.LibraryPath) == 0)
            {
                FileName = FileName.Substring(Library.LibraryPath.Length + 1);
            }
            Title = Annotation = Language = string.Empty;
            BookDate = DocumentDate = DateTime.MinValue;
            Authors = new List<string>();
            Translators = new List<string>();
            Genres = new List<string>();
            Sequences = new List<BookSequenceInfo>();
            DocumentIDTrusted = false;
            DuplicateKey = string.Empty;
            ReplacedByID = null;
            ContentHash = null;
        }

        private string id = string.Empty;
        public string ID
        {
            get { return id; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    // Check if this is a trusted ID format
                    if (IsValidTrustedID(value))
                    {
                        id = value;
                        DocumentIDTrusted = true;
                    }
                    else
                    {
                        // Generate UUID v3 from filename for non-trusted IDs
                        id = Utils.CreateGuid(Utils.IsoOidNamespace, FileName).ToString();
                        DocumentIDTrusted = false;
                    }
                }
                else
                {
                    id = Utils.CreateGuid(Utils.IsoOidNamespace, FileName).ToString();
                    DocumentIDTrusted = false;
                }
                id = id.Replace("{", "").Replace("}", "");
            }
        }

        public float Version { get; set; }
        public string FileName { get; private set; }
        public string FilePath
        {
            get
            {
                string path = Path.Combine(Library.LibraryPath, FileName).Replace("\\\\", "\\");
                return Utils.IsLinux ? path.Replace('\\', '/') : path;
            }
        }
        public string Title { get; set; }
        public string Language { get; set; }
        public DateTime BookDate { get; set; }
        public DateTime DocumentDate { get; set; }

        // List of sequences for multi-series books
        public List<BookSequenceInfo> Sequences { get; set; }

        // Legacy compatibility properties
        public string Sequence
        {
            get { return Sequences?.FirstOrDefault()?.Name ?? string.Empty; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if (Sequences == null) Sequences = new List<BookSequenceInfo>();
                    if (Sequences.Count == 0)
                        Sequences.Add(new BookSequenceInfo(value, 0));
                    else if (Sequences[0].Name != value)
                        Sequences[0] = new BookSequenceInfo(value, Sequences[0].NumberInSequence);
                }
            }
        }

        public uint NumberInSequence
        {
            get { return Sequences?.FirstOrDefault()?.NumberInSequence ?? 0; }
            set
            {
                if (Sequences == null) Sequences = new List<BookSequenceInfo>();
                if (Sequences.Count == 0)
                    Sequences.Add(new BookSequenceInfo(string.Empty, value));
                else
                    Sequences[0].NumberInSequence = value;
            }
        }

        public string Annotation { get; set; }
        public UInt32 DocumentSize { get; set; }
        public List<string> Authors { get; set; }
        public List<string> Translators { get; set; }
        public List<string> Genres { get; set; }
        public BookType BookType { get { return Path.GetExtension(FilePath).ToLower().Contains("epub") ? BookType.EPUB : BookType.FB2; } }
        public bool IsValid { get { return !string.IsNullOrEmpty(Title) && Title.IsValidUTF() && Authors.Count > 0 && Genres.Count > 0; } }
        public DateTime AddedDate { get; set; }
        public DateTime? LastDownloadDate { get; set; }

        public bool DocumentIDTrusted { get; set; }
        public string DuplicateKey { get; set; }
        public string ReplacedByID { get; set; }

        // This is used for duplicate detection instead of file content hash
        public string ContentHash { get; set; }

        /// <summary>
        /// Check if ID is in a trusted format (FBD, LibRusEc, or valid GUID)
        /// </summary>
        private static bool IsValidTrustedID(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            // Clean the ID
            id = id.Trim();

            // Check FBD format (Fiction Book Designer)
            if (FBD_ID_PATTERN.IsMatch(id))
                return true;

            // Check LibRusEc format (numeric ID > 100000)
            if (long.TryParse(id, out long numericId) && numericId > 100000)
                return true;

            // Check if it's a trusted GUID
            if (Guid.TryParse(id, out Guid guid))
                return IsTrustedGuid(guid);

            return false;
        }

        /// <summary>
        /// Extract archive priority from filename
        /// </summary>
        public int GetArchivePriority()
        {
            if (string.IsNullOrEmpty(FileName))
                return 0;

            // Extract archive name from composite filename
            string archiveName = FileName;
            int atIndex = FileName.IndexOf('@');
            if (atIndex > 0)
            {
                archiveName = FileName.Substring(0, atIndex);
            }

            // Pattern for numbered FB2 archives: fb2-NNNNNN-NNNNNN.zip
            var match = Regex.Match(archiveName, @"fb2-(\d+)-(\d+)\.zip", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                // Return the ending number as priority
                if (int.TryParse(match.Groups[2].Value, out int endNumber))
                {
                    return endNumber;
                }
            }

            return 0;
        }

        /// <summary>
        /// Check if GUID is trusted (not a placeholder)
        /// </summary>
        public static bool IsTrustedGuid(Guid guid)
        {
            if (guid == Guid.Empty) return false;

            string guidStr = guid.ToString().ToLowerInvariant();

            // Check for common placeholders
            if (guidStr == "00000000-0000-0000-0000-000000000000") return false;
            if (guidStr == "11111111-1111-1111-1111-111111111111") return false;
            if (guidStr == "12345678-1234-1234-1234-123456789012") return false;
            if (guidStr == "ffffffff-ffff-ffff-ffff-ffffffffffff") return false;

            // Check for timestamp strings (LibRusEc kit issue)
            if (guidStr.Contains("mon") || guidStr.Contains("tue") || guidStr.Contains("wed") ||
                guidStr.Contains("thu") || guidStr.Contains("fri") || guidStr.Contains("sat") ||
                guidStr.Contains("sun") || guidStr.Contains("jan") || guidStr.Contains("feb") ||
                guidStr.Contains("mar") || guidStr.Contains("apr") || guidStr.Contains("may") ||
                guidStr.Contains("jun") || guidStr.Contains("jul") || guidStr.Contains("aug") ||
                guidStr.Contains("sep") || guidStr.Contains("oct") || guidStr.Contains("nov") ||
                guidStr.Contains("dec"))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Generate duplicate detection key
        /// </summary>
        public string GenerateDuplicateKey()
        {
            if (string.IsNullOrEmpty(Title) || Authors == null || Authors.Count == 0)
                return string.Empty;

            return CreateDuplicateKey(Title, Authors, Language, Translators, Sequences);
        }

        // Volume written in the title: "Том 2", "часть III", "Книга вторая", "Вторая часть"
        private const string VolumeWords = @"(том|книга|часть|volume|book|part|vol|кн|ч)";
        private static readonly Regex VolumeAfterWordRegex = new Regex(
            @"\b" + VolumeWords + @"\.?\s*[:.\s]*(\d+|[ivx]+)\b", RegexOptions.Compiled);
        private static readonly Regex VolumeBeforeWordRegex = new Regex(
            @"\b(\d+|перв\w*|втор\w*|трет\w*|четверт\w*|пят\w*|шест\w*|седьм\w*|восьм\w*|девят\w*|десят\w*|first|second|third|fourth|fifth)[\s-]*(й|я|е|ая|ый|ое)?\s+" + VolumeWords + @"\b",
            RegexOptions.Compiled);
        private static readonly Regex BracketedNumberRegex = new Regex(@"\((\s*\d+\s*)\)|\[(\s*\d+\s*)\]", RegexOptions.Compiled);
        private static readonly Regex BracketsRegex = new Regex(@"\([^)]*\)|\[[^\]]*\]", RegexOptions.Compiled);
        private static readonly Regex NonWordRegex = new Regex(@"[^\w]+", RegexOptions.Compiled);
        private static readonly Regex NonNameRegex = new Regex(@"[^\w\s]", RegexOptions.Compiled);

        private static readonly Dictionary<string, int> RomanNumbers = new Dictionary<string, int>
        {
            {"i", 1}, {"ii", 2}, {"iii", 3}, {"iv", 4}, {"v", 5}, {"vi", 6}, {"vii", 7}, {"viii", 8},
            {"ix", 9}, {"x", 10}, {"xi", 11}, {"xii", 12}, {"xiii", 13}, {"xiv", 14}, {"xv", 15}
        };

        // Ordinal word stems, checked in this order
        private static readonly (string stem, int number)[] OrdinalStems =
        {
            ("перв", 1), ("втор", 2), ("трет", 3), ("четверт", 4), ("пят", 5), ("шест", 6), ("седьм", 7),
            ("восьм", 8), ("девят", 9), ("десят", 10), ("first", 1), ("second", 2), ("third", 3), ("fourth", 4), ("fifth", 5)
        };

        /// <summary>
        /// Key of the copies of the same book: title (with volume number), authors, language and translators.
        /// Series are not used: the same book is often listed in different series or with different numbers.
        /// Names are normalized to survive the usual differences between copies (patronymic, ё/е, initials of translators)
        /// </summary>
        public static string CreateDuplicateKey(string title, IEnumerable<string> authors, string language,
            IEnumerable<string> translators, IEnumerable<BookSequenceInfo> sequences)
        {
            string normalizedTitle = NormalizeTitleForDuplicateKey(title);

            // Series number matters only when the series is named as the book (volumes of one long title)
            string volume = string.Empty;
            if (sequences != null)
            {
                foreach (var sequence in sequences)
                {
                    if (sequence.NumberInSequence > 0 && !string.IsNullOrEmpty(sequence.Name) &&
                        NormalizeText(sequence.Name.ToLowerInvariant().Replace('ё', 'е')) == normalizedTitle)
                        volume = sequence.NumberInSequence.ToString();
                }
            }

            string lang = string.IsNullOrEmpty(language) ? "unknown" : language.ToLowerInvariant();
            if (lang.Length > 2) lang = lang.Substring(0, 2);

            string keySource = string.Join("|",
                normalizedTitle,
                string.Join(",", NormalizeNames(authors, AuthorForDuplicateKey)),
                lang,
                string.Join(",", NormalizeNames(translators, TranslatorForDuplicateKey)),
                volume);

            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(keySource));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static IEnumerable<string> NormalizeNames(IEnumerable<string> names, Func<string, string> normalize)
        {
            if (names == null) return Enumerable.Empty<string>();
            return names.Select(normalize).Distinct().OrderBy(n => n, StringComparer.Ordinal);
        }

        private static string[] NameWords(string name)
        {
            return NonNameRegex.Replace((name ?? string.Empty).ToLowerInvariant().Replace('ё', 'е'), " ")
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        // Last and first name: patronymic is often missing in one of the copies
        private static string AuthorForDuplicateKey(string author)
        {
            return string.Join(" ", NameWords(author).Take(2));
        }

        // Last name and initial: translators are often listed by initials
        private static string TranslatorForDuplicateKey(string translator)
        {
            var words = NameWords(translator);
            if (words.Length == 0) return string.Empty;
            return words.Length > 1 ? words[0] + " " + words[1][0] : words[0];
        }

        private static string NormalizeText(string text)
        {
            return NonWordRegex.Replace(text, " ").Trim();
        }

        private static string VolumeNumber(string text)
        {
            if (text.All(char.IsDigit)) return int.TryParse(text, out int n) ? n.ToString() : text.TrimStart('0');
            if (RomanNumbers.TryGetValue(text, out int roman)) return roman.ToString();
            foreach (var (stem, number) in OrdinalStems)
                if (text.StartsWith(stem, StringComparison.Ordinal)) return number.ToString();
            return text;
        }

        private static string NormalizeTitleForDuplicateKey(string title)
        {
            string text = (title ?? string.Empty).ToLowerInvariant().Replace('ё', 'е');

            var volumes = VolumeAfterWordRegex.Matches(text).Cast<Match>().Select(m => VolumeNumber(m.Groups[2].Value))
                .Concat(VolumeBeforeWordRegex.Matches(text).Cast<Match>().Select(m => VolumeNumber(m.Groups[1].Value)))
                .ToList();
            text = VolumeAfterWordRegex.Replace(VolumeBeforeWordRegex.Replace(text, " "), " ");

            // Numbers in brackets are kept ("Стихи (2)"), other notes in brackets ("(СИ)", "(сборник)") are dropped
            text = BracketedNumberRegex.Replace(text, m => " " + (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value) + " ");
            string withoutNotes = BracketsRegex.Replace(text, " ");
            if (NormalizeText(withoutNotes).Length >= 3) text = withoutNotes;

            text = NormalizeText(text);
            if (volumes.Count > 0) text += " vol" + string.Join(".", volumes);
            return text;
        }

        /// <summary>
        /// Number of the book in the archive entry name (numbered archive "fb2-NNNNNN-NNNNNN.zip@NNNNNN.fb2")
        /// </summary>
        public long GetArchiveEntryNumber()
        {
            if (string.IsNullOrEmpty(FileName)) return 0;
            int atIndex = FileName.IndexOf('@');
            if (atIndex < 0) return 0;
            string entry = Path.GetFileNameWithoutExtension(FileName.Substring(atIndex + 1));
            return long.TryParse(entry, out long number) ? number : 0;
        }

        /// <summary>
        /// Strict order of the copies of the same book, the greatest copy is kept.
        /// Criteria are compared one by one (not summed up), so the order is transitive and
        /// the kept copy doesn't depend on the order the books were scanned in
        /// </summary>
        public int CompareTo(Book other)
        {
            if (other == null) return 1;

            // Newer numbered archive, then newer book in it
            int result = GetArchivePriority().CompareTo(other.GetArchivePriority());
            if (result == 0) result = GetArchiveEntryNumber().CompareTo(other.GetArchiveEntryNumber());
            // FB2 over EPUB
            if (result == 0) result = (BookType == BookType.FB2).CompareTo(other.BookType == BookType.FB2);
            if (result == 0) result = Version.CompareTo(other.Version);
            if (result == 0) result = DocumentDate.Date.CompareTo(other.DocumentDate.Date);
            if (result == 0) result = DocumentSize.CompareTo(other.DocumentSize);
            if (result == 0) result = string.CompareOrdinal(FileName, other.FileName);
            return Math.Sign(result);
        }
    }
}