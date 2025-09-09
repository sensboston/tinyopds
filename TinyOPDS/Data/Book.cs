/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the Book class
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
                // Book ID always must be in GUID form
                if (!string.IsNullOrEmpty(value) && Guid.TryParse(value, out Guid guid))
                {
                    id = value;
                    // Check if this is a trusted ID (not a placeholder)
                    DocumentIDTrusted = IsTrustedGuid(guid);
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

        // Replaced single Sequence/NumberInSequence with list
        public List<BookSequenceInfo> Sequences { get; set; }

        // LEGACY COMPATIBILITY: Properties for backward compatibility
        // These will be used during data migration from old format
        public string Sequence
        {
            get { return Sequences?.FirstOrDefault()?.Name ?? string.Empty; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    // If sequences list doesn't exist, create it
                    if (Sequences == null) Sequences = new List<BookSequenceInfo>();

                    // If no sequences or first sequence is different, set it
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
        public BookType BookType { get { return Path.GetExtension(FilePath).ToLower().Contains("epub") ? BookType.EPUB : Data.BookType.FB2; } }
        public bool IsValid { get { return (!string.IsNullOrEmpty(Title) && Title.IsValidUTF() && Authors.Count > 0 && Genres.Count > 0); } }
        public DateTime AddedDate { get; set; }

        // New fields for improved duplicate detection
        public bool DocumentIDTrusted { get; set; }
        public string DuplicateKey { get; set; }
        public string ReplacedByID { get; set; }
        public string ContentHash { get; set; }

        /// <summary>
        /// Extract archive priority from filename (for archives with numbered ranges)
        /// Returns the ending number from patterns like "fb2-000024-030559.zip"
        /// </summary>
        public int GetArchivePriority()
        {
            if (string.IsNullOrEmpty(FileName))
                return 0;

            // Extract archive name from composite filename (e.g., "fb2-000024-030559.zip@book.fb2")
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
                // Return the ending number as priority (higher = newer)
                if (int.TryParse(match.Groups[2].Value, out int endNumber))
                {
                    return endNumber;
                }
            }

            // Not from a numbered archive
            return 0;
        }

        /// <summary>
        /// Check if GUID is trusted (not a placeholder from bad converters)
        /// MODIFIED: Less aggressive checking to avoid false positives
        /// </summary>
        private bool IsTrustedGuid(Guid guid)
        {
            // Check for common placeholder GUIDs
            if (guid == Guid.Empty) return false;

            string guidStr = guid.ToString().ToLowerInvariant();

            // Only check for the most obvious placeholders
            if (guidStr == "00000000-0000-0000-0000-000000000000") return false;
            if (guidStr == "11111111-1111-1111-1111-111111111111") return false;
            if (guidStr == "12345678-1234-1234-1234-123456789012") return false;
            if (guidStr == "ffffffff-ffff-ffff-ffff-ffffffffffff") return false;

            // Check for timestamp strings that are not proper GUIDs
            // LibRusEc kit generates IDs like "Mon Jun 10 19:52:43 2013" which are NOT proper GUIDs
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

            // Removed the checks for all bytes being the same and sequential patterns
            // as they might reject valid GUIDs

            // If it passed all checks, it's likely from FictionBookEditor or similar proper tool
            return true;
        }

        /// <summary>
        /// Generate duplicate detection key based on title, first author and language
        /// MODIFIED: More precise key generation to avoid false positives
        /// </summary>
        public string GenerateDuplicateKey()
        {
            if (string.IsNullOrEmpty(Title) || Authors == null || Authors.Count == 0)
                return string.Empty;

            // Extract volume info if present
            var (original, normalized) = ExtractVolumeInfo(Title);

            string normalizedTitle = NormalizeForDuplicateKey(Title);
            string firstAuthor = Authors.First();
            string normalizedAuthor = NormalizeAuthorForDuplicateKey(firstAuthor);
            string lang = string.IsNullOrEmpty(Language) ? "unknown" : Language.ToLowerInvariant();

            // Include translator info if present (from Translators list)
            string translatorInfo = "";
            if (Translators != null && Translators.Count > 0)
            {
                // Include ALL translators in the key to distinguish different translations
                var translatorNames = Translators.Select(t => NormalizeAuthorForDuplicateKey(t)).OrderBy(t => t);
                translatorInfo = "trans_" + string.Join("_", translatorNames);
            }

            // Include sequence and number if present to distinguish series books
            // MODIFIED: Work with new Sequences list
            string sequenceInfo = "";
            if (Sequences != null && Sequences.Count > 0)
            {
                // Use first sequence for duplicate key (primary sequence)
                var firstSequence = Sequences.First();
                if (!string.IsNullOrEmpty(firstSequence.Name))
                {
                    sequenceInfo = NormalizeForDuplicateKey(firstSequence.Name);
                    if (firstSequence.NumberInSequence > 0)
                        sequenceInfo += "_" + firstSequence.NumberInSequence.ToString();
                }
            }

            // MODIFIED: Only add volume info if it was explicitly found in the title
            // Don't add "vol0" to all books without volume info
            if (!string.IsNullOrEmpty(normalized))
            {
                normalizedTitle += " " + normalized;
            }

            // Add translator info if present
            if (!string.IsNullOrEmpty(translatorInfo))
            {
                normalizedTitle += " " + translatorInfo;
            }

            // Build the key with all relevant parts
            string keySource = $"{normalizedTitle}|{normalizedAuthor}|{lang}|{sequenceInfo}";

            // Generate MD5 hash for compact storage
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(keySource);
                byte[] hash = md5.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Generate content hash from file (first 10KB to avoid full file read)
        /// </summary>
        public string GenerateContentHash(Stream fileStream)
        {
            if (fileStream == null || !fileStream.CanRead)
                return null;

            try
            {
                long originalPosition = fileStream.Position;
                fileStream.Position = 0;

                // Read first 10KB or whole file if smaller
                int bytesToRead = (int)Math.Min(10240, fileStream.Length);
                byte[] buffer = new byte[bytesToRead];
                fileStream.Read(buffer, 0, bytesToRead);

                // Reset stream position
                fileStream.Position = originalPosition;

                // Generate hash
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(buffer);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch
            {
                return null;
            }
        }

        private static readonly Regex DashesRegex = new Regex(@"[‐‑‒–—―−⸺⸻﹘﹣－ｰ─━]", RegexOptions.Compiled);
        private static readonly Regex QuotesRegex = new Regex(@"[\u0022\u0027\u2018\u2019\u201C\u201D\u00AB\u00BB\u201E\u201A\u0060\u00B4\u2032\u2033\u2039\u203A]", RegexOptions.Compiled);
        private static readonly Regex ParenthesesRegex = new Regex(@"\([^)]*\)|\[[^\]]*\]", RegexOptions.Compiled);
        private static readonly Regex PunctuationRegex = new Regex(@"[^\w\s'-]", RegexOptions.Compiled);
        private static readonly Regex EdgePunctuationRegex = new Regex(@"(\s|^)[-']|[-'](\s|$)", RegexOptions.Compiled);
        private static readonly Regex MultiSpaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Normalize string for duplicate key generation
        /// FIXED: Don't remove content if entire title is in brackets
        /// </summary>
        private string NormalizeForDuplicateKey(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string originalText = text;
            text = text.ToLowerInvariant();

            // CRITICAL FIX: Check if entire title is in brackets
            bool wholeTitleInBrackets = false;
            string textWithoutOuterBrackets = text.Trim();

            if ((textWithoutOuterBrackets.StartsWith("[") && textWithoutOuterBrackets.EndsWith("]")) ||
                (textWithoutOuterBrackets.StartsWith("(") && textWithoutOuterBrackets.EndsWith(")")))
            {
                wholeTitleInBrackets = true;
                // Remove only the outer brackets, keep the content
                textWithoutOuterBrackets = textWithoutOuterBrackets.Substring(1, textWithoutOuterBrackets.Length - 2).Trim();
                text = textWithoutOuterBrackets;
            }

            string volumeNormalized = "";
            string translatorNormalized = "";
            string editionNormalized = "";
            bool collectionMarker = false;

            // Only process inner brackets if the whole title wasn't in brackets
            if (!wholeTitleInBrackets && (text.Contains('(') || text.Contains('[')))
            {
                var (original, normalized) = ExtractVolumeInfo(text);
                if (!string.IsNullOrEmpty(original))
                {
                    text = text.Replace(original.ToLowerInvariant(), "");
                    volumeNormalized = normalized;
                }

                var translatorInfo = ExtractTranslatorInfo(text);
                if (!string.IsNullOrEmpty(translatorInfo.original))
                {
                    text = text.Replace(translatorInfo.original.ToLowerInvariant(), "");
                    translatorNormalized = translatorInfo.normalized;
                }

                var editionInfo = ExtractEditionInfo(text);
                if (!string.IsNullOrEmpty(editionInfo.original))
                {
                    text = text.Replace(editionInfo.original.ToLowerInvariant(), "");
                    editionNormalized = editionInfo.normalized;
                }

                // Remove remaining parentheses ONLY if we still have substantial content
                string testRemoval = ParenthesesRegex.Replace(text, " ").Trim();
                if (testRemoval.Length > 5) // Keep brackets content if removal would leave too little
                {
                    text = testRemoval;
                }
            }

            // Quick check for special chars to optimize
            bool hasSpecialChars = text.IndexOfAny(new[] { '«', '"', '–', '—', '…' }) >= 0;

            // Only do expensive replacements if special chars detected
            if (hasSpecialChars)
            {
                text = DashesRegex.Replace(text, "-");
                text = QuotesRegex.Replace(text, " ");
                text = text.Replace("…", "...");
                text = text.Replace("№", " ").Replace("#", " ").Replace("§", " ");
            }

            // These are always needed
            text = PunctuationRegex.Replace(text, " ");
            text = EdgePunctuationRegex.Replace(text, " ");
            text = MultiSpaceRegex.Replace(text, " ").Trim();

            // Check for collection only if keywords might be present
            if (text.Contains("сбор") || text.Contains("собр") || text.Contains("избр") ||
                text.Contains("антол") || text.Contains("collect") || text.Contains("anthol"))
            {
                collectionMarker = DetectCollection(text);
            }

            // Build result
            var result = text;
            if (!string.IsNullOrEmpty(volumeNormalized))
                result += " " + volumeNormalized;
            if (!string.IsNullOrEmpty(translatorNormalized))
                result += " " + translatorNormalized;
            if (!string.IsNullOrEmpty(editionNormalized))
                result += " " + editionNormalized;
            if (collectionMarker)
                result += " _collection_";

            // Final check - ensure we have meaningful content
            if (string.IsNullOrWhiteSpace(result) || result.Length < 3)
            {
                // Emergency fallback - use original with minimal normalization
                result = originalText.ToLowerInvariant().Replace("[", "").Replace("]", "").Replace("(", "").Replace(")", "").Trim();
                Log.WriteLine(LogLevel.Warning, "NormalizeForDuplicateKey produced short result for '{0}', using fallback", originalText);
            }

            return result.Trim();
        }

        /// <summary>
        /// Extract volume/tome/book/part information
        /// </summary>
        private (string original, string normalized) ExtractVolumeInfo(string text)
        {
            // Dictionary for text-to-number conversion
            var textNumbers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"первая", "1"}, {"первый", "1"}, {"первое", "1"},
                {"вторая", "2"}, {"второй", "2"}, {"второе", "2"},
                {"третья", "3"}, {"третий", "3"}, {"третье", "3"},
                {"четвертая", "4"}, {"четвертый", "4"}, {"четвертое", "4"},
                {"пятая", "5"}, {"пятый", "5"}, {"пятое", "5"},
                {"шестая", "6"}, {"шестой", "6"}, {"шестое", "6"},
                {"седьмая", "7"}, {"седьмой", "7"}, {"седьмое", "7"},
                {"восьмая", "8"}, {"восьмой", "8"}, {"восьмое", "8"},
                {"девятая", "9"}, {"девятый", "9"}, {"девятое", "9"},
                {"десятая", "10"}, {"десятый", "10"}, {"десятое", "10"},
                {"one", "1"}, {"first", "1"},
                {"two", "2"}, {"second", "2"},
                {"three", "3"}, {"third", "3"},
                {"four", "4"}, {"fourth", "4"},
                {"five", "5"}, {"fifth", "5"},
                {"six", "6"}, {"sixth", "6"},
                {"seven", "7"}, {"seventh", "7"},
                {"eight", "8"}, {"eighth", "8"},
                {"nine", "9"}, {"ninth", "9"},
                {"ten", "10"}, {"tenth", "10"}
            };

            // Roman numerals conversion
            var romanNumerals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"i", "1"}, {"ii", "2"}, {"iii", "3"}, {"iv", "4"}, {"v", "5"},
                {"vi", "6"}, {"vii", "7"}, {"viii", "8"}, {"ix", "9"}, {"x", "10"},
                {"xi", "11"}, {"xii", "12"}, {"xiii", "13"}, {"xiv", "14"}, {"xv", "15"}
            };

            // Patterns for volume/book/part/tome with various number formats
            var patterns = new[]
            {
                // Numeric patterns
                @"\((том|книга|часть|volume|book|part|vol|кн|ч)\s*[:\.\s]*(\d+)[^)]*\)",
                @"\[(том|книга|часть|volume|book|part|vol|кн|ч)\s*[:\.\s]*(\d+)[^\]]*\]",
                @"(том|книга|часть|volume|book|part|vol|кн|ч)\s*[:\.\s]*(\d+)\b",
                @"\((\d+)\s*(том|книга|часть|volume|book|part|vol|кн|ч)[^)]*\)",
                @"\[(\d+)\s*(том|книга|часть|volume|book|part|vol|кн|ч)[^\]]*\]",
                
                // Text number patterns (e.g., "Часть вторая", "Part Two")
                @"\((том|книга|часть|volume|book|part)\s+([а-яё]+|[a-z]+)[^)]*\)",
                @"\[(том|книга|часть|volume|book|part)\s+([а-яё]+|[a-z]+)[^\]]*\]",
                @"(том|книга|часть|volume|book|part)\s+([а-яё]+|[a-z]+)\b",
                
                // Roman numeral patterns
                @"\((том|книга|часть|volume|book|part|vol)\s*[:\.\s]*([ivxlcdm]+)[^)]*\)",
                @"\[(том|книга|часть|volume|book|part|vol)\s*[:\.\s]*([ivxlcdm]+)[^\]]*\]",
                @"(том|книга|часть|volume|book|part|vol)\s*[:\.\s]*([ivxlcdm]+)\b"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var original = match.Value;
                    string numberStr = "";

                    // Try to extract number from different groups
                    for (int i = 1; i <= match.Groups.Count - 1; i++)
                    {
                        var groupValue = match.Groups[i].Value.Trim();

                        // Check if it's a direct number
                        if (Regex.IsMatch(groupValue, @"^\d+$"))
                        {
                            numberStr = groupValue;
                            break;
                        }

                        // Check if it's a text number
                        if (textNumbers.ContainsKey(groupValue))
                        {
                            numberStr = textNumbers[groupValue];
                            break;
                        }

                        // Check if it's a roman numeral
                        if (romanNumerals.ContainsKey(groupValue.ToLowerInvariant()))
                        {
                            numberStr = romanNumerals[groupValue.ToLowerInvariant()];
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(numberStr))
                    {
                        return (original, $"vol{numberStr}");
                    }
                }
            }

            // Return empty if no volume info found - don't add default "vol0"
            return ("", "");
        }

        /// <summary>
        /// Extract translator information
        /// </summary>
        private (string original, string normalized) ExtractTranslatorInfo(string text)
        {
            // Look for translator information
            var patterns = new[]
            {
                @"\((перевод|пер\.?|переводчик|translation|trans\.?|translator)\s*[:.]?\s*([^)]+)\)",
                @"\[(перевод|пер\.?|переводчик|translation|trans\.?|translator)\s*[:.]?\s*([^]]+)\]",
                @"(перевод|пер\.?|переводчик|translation|trans\.?|translator)\s*[:.]?\s*([а-яёa-z\s\.]+)",
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var original = match.Value;
                    var translatorName = match.Groups[2].Value.Trim();

                    // Normalize translator name (remove initials variations, etc.)
                    translatorName = Regex.Replace(translatorName, @"\s+", "");
                    translatorName = Regex.Replace(translatorName, @"[^\w]", "");

                    if (!string.IsNullOrEmpty(translatorName))
                    {
                        return (original, $"trans_{translatorName.ToLowerInvariant()}");
                    }
                }
            }

            return ("", "");
        }

        /// <summary>
        /// Extract edition information
        /// Preserve edition/revision info to distinguish different versions
        /// </summary>
        private (string original, string normalized) ExtractEditionInfo(string text)
        {
            // Look for edition/revision information
            var patterns = new[]
            {
                @"\((издание|изд\.?|редакция|ред\.?|edition|ed\.?|revision|rev\.?)\s*[:.]?\s*([^)]+)\)",
                @"\[(издание|изд\.?|редакция|ред\.?|edition|ed\.?|revision|rev\.?)\s*[:.]?\s*([^]]+)\]",
                @"(издание|изд\.?|редакция|ред\.?|edition|ed\.?|revision|rev\.?)\s*[:.]?\s*(\d+)",
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var original = match.Value;
                    var editionInfo = match.Groups[2].Value.Trim();

                    // Extract numbers from edition info
                    var numberMatch = Regex.Match(editionInfo, @"\d+");
                    if (numberMatch.Success)
                    {
                        return (original, $"ed{numberMatch.Value}");
                    }
                }
            }

            return ("", "");
        }

        /// <summary>
        /// Detect if this is a collection/anthology
        /// </summary>
        private bool DetectCollection(string text)
        {
            var collectionMarkers = new[]
            {
                "сборник", "собрание", "избранное", "антология", "хрестоматия",
                "collection", "collected", "anthology", "selected", "complete works",
                "omnibus", "compilation"
            };

            text = text.ToLowerInvariant();
            return collectionMarkers.Any(marker => text.Contains(marker));
        }

        /// <summary>
        /// Normalize author name for duplicate key
        /// </summary>
        private string NormalizeAuthorForDuplicateKey(string author)
        {
            if (string.IsNullOrEmpty(author)) return "";

            author = author.ToLowerInvariant();

            // Remove punctuation
            author = Regex.Replace(author, @"[^\w\s]", " ");

            // Remove multiple spaces
            author = Regex.Replace(author, @"\s+", " ").Trim();

            return author;
        }

        /// <summary>
        /// Compare two books to determine which is better/newer
        /// MODIFIED: Added archive priority comparison
        /// Returns: positive if this book is better, negative if other is better, 0 if equal
        /// </summary>
        public int CompareTo(Book other)
        {
            if (other == null) return 1;

            int score = 0;

            // FIRST: Check archive priority for numbered FB2 archives
            int thisPriority = this.GetArchivePriority();
            int otherPriority = other.GetArchivePriority();

            // If both books are from numbered archives, prefer the one from newer archive
            if (thisPriority > 0 && otherPriority > 0)
            {
                if (thisPriority > otherPriority)
                    return 10;  // Strong preference for newer archive
                else if (thisPriority < otherPriority)
                    return -10; // Strong preference against older archive
                // If same archive priority, continue to other comparisons
            }

            // For books with trusted IDs (from FictionBookEditor)
            if (this.DocumentIDTrusted && other.DocumentIDTrusted && this.ID == other.ID)
            {
                // Compare version numbers - higher weight for version differences
                if (this.Version > other.Version) score += 5;
                else if (this.Version < other.Version) score -= 5;

                // Compare document dates
                if (this.DocumentDate > other.DocumentDate.AddDays(1)) score += 2;
                else if (this.DocumentDate < other.DocumentDate.AddDays(-1)) score -= 2;
            }
            else
            {
                // For books without trusted IDs or different IDs

                // Prefer FB2 over EPUB (more metadata)
                if (this.BookType == BookType.FB2 && other.BookType == BookType.EPUB) score += 2;
                else if (this.BookType == BookType.EPUB && other.BookType == BookType.FB2) score -= 2;

                // Compare document dates (with tolerance of 1 day)
                if (this.DocumentDate > other.DocumentDate.AddDays(1)) score += 3;
                else if (this.DocumentDate < other.DocumentDate.AddDays(-1)) score -= 3;

                // Compare file sizes (bigger often means more complete, but only if significant difference)
                if (this.DocumentSize > other.DocumentSize * 1.2) score += 1;
                else if (this.DocumentSize < other.DocumentSize * 0.8) score -= 1;
            }

            // Prefer books with trusted IDs
            if (this.DocumentIDTrusted && !other.DocumentIDTrusted) score += 1;
            else if (!this.DocumentIDTrusted && other.DocumentIDTrusted) score -= 1;

            return score;
        }

        /// <summary>
        /// Check if this book is likely a duplicate of another
        /// MODIFIED: More strict duplicate detection
        /// </summary>
        public bool IsDuplicateOf(Book other)
        {
            if (other == null) return false;

            // Same trusted ID = definite duplicate ONLY if both IDs are trusted
            if (this.DocumentIDTrusted && other.DocumentIDTrusted && this.ID == other.ID)
                return true;

            // Same content hash = exact duplicate
            if (!string.IsNullOrEmpty(this.ContentHash) && this.ContentHash == other.ContentHash)
                return true;

            // Same duplicate key = likely duplicate, but need to check translators
            if (!string.IsNullOrEmpty(this.DuplicateKey) && this.DuplicateKey == other.DuplicateKey)
            {
                // If both have translators, they must match
                if (this.Translators?.Count > 0 && other.Translators?.Count > 0)
                {
                    var thisTranslators = new HashSet<string>(this.Translators.Select(t => t.ToLowerInvariant()));
                    var otherTranslators = new HashSet<string>(other.Translators.Select(t => t.ToLowerInvariant()));
                    return thisTranslators.SetEquals(otherTranslators);
                }

                // If neither has translators, consider duplicate
                if ((this.Translators == null || this.Translators.Count == 0) &&
                    (other.Translators == null || other.Translators.Count == 0))
                {
                    return true;
                }

                // If one has translators and other doesn't, not a duplicate
                return false;
            }

            return false;
        }
    }
}