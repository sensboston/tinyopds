/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * PDF to FB2 conversion: the text layer of the PDF is rebuilt into
 * paragraphs and chapters (no OCR, scanned pages have no text)
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace TinyOPDS
{
    public class PdfFb2Converter
    {
        public const int MaxPages = 3000;

        public enum Result { Converted, NoText, NotReadable }

        /// <summary>
        /// Why the last conversion returned no document
        /// </summary>
        public Result LastResult { get; private set; }

        // Frequent words: a text without them is not a text, but character codes of a font without
        // the Unicode map (some PDFs are made so on purpose to prevent copying)
        private static readonly HashSet<string> frequentWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "of", "to", "a", "in", "is", "was", "he", "she", "it", "that", "for", "you", "with", "his", "her",
            "и", "в", "не", "на", "что", "он", "она", "с", "я", "как", "а", "то", "но", "по",
            "der", "die", "und", "das", "le", "la", "et", "les", "de", "el", "y", "que", "en", "w", "się", "nie", "та", "що", "і"
        };

        private static readonly XNamespace fb2 = "http://www.gribuser.ru/xml/fictionbook/2.0";
        // "Chapter 12", "PART TWO", "Глава первая": a capitalized word and a few words more, no punctuation
        private static readonly Regex chapterTitle = new Regex(
            @"^(Chapter|CHAPTER|Part|PART|Book|BOOK|Prologue|PROLOGUE|Epilogue|EPILOGUE|Глава|ГЛАВА|Часть|ЧАСТЬ|Пролог|ПРОЛОГ|Эпилог|ЭПИЛОГ)(\s+[\w\-–]+){0,4}$",
            RegexOptions.Compiled);
        private static readonly Regex pageNumber = new Regex(@"^[\divxlcIVXLC\s\-–—.]{1,10}$", RegexOptions.Compiled);

        private class Line
        {
            public int Page;
            public string Text;
            public double Left, Right, Top, Bottom, FontSize;
            // Margins the line is measured against: of the neighbour lines
            public double RefLeft, RefRight;
            public bool FirstInPage, LastInPage;
        }

        private class Paragraph
        {
            public StringBuilder Text = new StringBuilder();
            public double FontSize;
            public bool IsTitle;
        }

        /// <summary>
        /// Converts the PDF to an FB2 document. Returns null if the PDF has no text (scanned pages).
        /// </summary>
        public XDocument Convert(string pdfPath, string fallbackTitle)
        {
            using (var pdf = PdfDocument.Open(pdfPath))
            {
                if (pdf.NumberOfPages > MaxPages)
                {
                    Log.WriteLine(LogLevel.Warning, "PDF has too many pages: {0}", pdf.NumberOfPages);
                    return null;
                }

                var lines = new List<Line>();
                for (int n = 1; n <= pdf.NumberOfPages; n++)
                {
                    lines.AddRange(ReadPage(pdf, n));
                }
                LastResult = Result.NoText;
                if (lines.Count == 0) return null;

                if (!IsReadable(lines))
                {
                    LastResult = Result.NotReadable;
                    Log.WriteLine(LogLevel.Warning, "PDF text is not readable (fonts without the Unicode map)");
                    return null;
                }

                RemoveRunningHeaders(lines, pdf.NumberOfPages);
                var paragraphs = BuildParagraphs(lines);
                if (paragraphs.Count == 0) return null;

                LastResult = Result.Converted;

                string title = Clean(pdf.Information.Title);
                if (string.IsNullOrEmpty(title)) title = fallbackTitle;
                return BuildFb2(paragraphs, title, Clean(pdf.Information.Author));
            }
        }

        private static IEnumerable<Line> ReadPage(PdfDocument pdf, int number)
        {
            var result = new List<Line>();
            try
            {
                var page = pdf.GetPage(number);
                if (page.Letters.Count == 0) return result;

                // Lines are built from words directly: books are set in one column, and the page segmenters
                // may split justified lines with wide spaces into blocks and put them out of order
                var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters)
                    .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                    .OrderByDescending(w => w.BoundingBox.Bottom).ToList();

                var rows = new List<List<UglyToad.PdfPig.Content.Word>>();
                foreach (var word in words)
                {
                    double size = word.Letters.Count > 0 ? word.Letters.Max(l => l.PointSize) : 10;
                    var row = rows.LastOrDefault();
                    if (row != null && Math.Abs(row[0].BoundingBox.Bottom - word.BoundingBox.Bottom) < size * 0.4) row.Add(word);
                    else rows.Add(new List<UglyToad.PdfPig.Content.Word> { word });
                }

                foreach (var row in rows)
                {
                    var ordered = row.OrderBy(w => w.BoundingBox.Left).ToList();
                    string text = NormalizeText(string.Join(" ", ordered.Select(w => w.Text)));
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    var letters = ordered.SelectMany(w => w.Letters).ToList();
                    result.Add(new Line
                    {
                        Page = number,
                        Text = text,
                        Left = ordered.First().BoundingBox.Left,
                        Right = ordered.Last().BoundingBox.Right,
                        Top = ordered.Max(w => w.BoundingBox.Top),
                        Bottom = ordered.Min(w => w.BoundingBox.Bottom),
                        FontSize = letters.Count > 0 ? letters.Average(l => l.PointSize) : 0
                    });
                }

                // Margins of the neighbour lines: quotes, letters and dialogs often have margins of their own
                for (int i = 0; i < result.Count; i++)
                {
                    var near = result.Skip(Math.Max(0, i - 3)).Take(7).ToList();
                    result[i].RefLeft = Mode(near.Select(l => l.Left));
                    result[i].RefRight = near.Max(l => l.Right);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "PDF page {0} skipped: {1}", number, ex.Message);
            }

            if (result.Count > 0)
            {
                result[0].FirstInPage = true;
                result[result.Count - 1].LastInPage = true;
            }
            return result;
        }

        private static bool IsReadable(List<Line> lines)
        {
            var words = lines.Skip(lines.Count / 10).Take(2000)
                .SelectMany(l => l.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(w => w.Trim(".,;:!?\"'“”‘’()«»—-".ToCharArray()))
                .Where(w => w.Length > 0).ToList();
            if (words.Count < 50) return true;
            return words.Count(w => frequentWords.Contains(w)) >= words.Count / 50;
        }

        // The most frequent coordinate, rounded to 2 points
        private static double Mode(IEnumerable<double> values)
        {
            return values.GroupBy(v => Math.Round(v / 2) * 2).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
        }

        private static string NormalizeText(string text)
        {
            text = RemoveInvalidXmlChars(text);
            return text.Replace("ﬀ", "ff").Replace("ﬁ", "fi").Replace("ﬂ", "fl")
                       .Replace("ﬃ", "ffi").Replace("ﬄ", "ffl").Replace(' ', ' ').Trim();
        }

        /// <summary>
        /// Page numbers and running headers or footers: the first and last lines of pages
        /// that are numbers, or repeat (numbers aside) on many pages
        /// </summary>
        private static void RemoveRunningHeaders(List<Line> lines, int pages)
        {
            var edges = lines.Where(l => l.FirstInPage || l.LastInPage).ToList();
            var repeated = edges.GroupBy(l => Regex.Replace(l.Text.ToLowerInvariant(), @"\d+", "#"))
                                .Where(g => g.Count() >= Math.Max(3, pages / 10))
                                .Select(g => g.Key);
            var repeatedSet = new HashSet<string>(repeated);
            lines.RemoveAll(l => (l.FirstInPage || l.LastInPage) &&
                (pageNumber.IsMatch(l.Text) || repeatedSet.Contains(Regex.Replace(l.Text.ToLowerInvariant(), @"\d+", "#"))));
        }

        private static List<Paragraph> BuildParagraphs(List<Line> lines)
        {
            // Body text: the most used font size, left and right margins of the most lines
            double bodySize = lines.GroupBy(l => Math.Round(l.FontSize)).OrderByDescending(g => g.Sum(l => l.Text.Length)).First().Key;
            double lineSpacing = lines.Zip(lines.Skip(1), (a, b) => a.Page == b.Page && b.Bottom < a.Bottom ? a.Bottom - b.Bottom : 0)
                                      .Where(d => d > 0).GroupBy(d => Math.Round(d)).OrderByDescending(g => g.Count())
                                      .Select(g => g.Key).DefaultIfEmpty(bodySize * 1.2).First();

            var paragraphs = new List<Paragraph>();
            Paragraph current = null;
            Line previous = null;

            foreach (var line in lines)
            {
                double size = Math.Max(line.FontSize, 1);
                bool indented = line.Left > line.RefLeft + size * 0.5;
                bool centered = line.Left > line.RefLeft + size * 2 && line.Right < line.RefRight - size * 2;
                bool bigger = line.FontSize > bodySize * 1.2;
                bool isTitle = line.Text.Length <= 60 && (bigger || chapterTitle.IsMatch(line.Text));

                // A title of two or more lines (in a bigger font: lines of contents are titles of their own)
                if (isTitle && bigger && current != null && current.IsTitle && previous != null && line.Page == previous.Page &&
                    previous.Bottom - line.Bottom < size * 2.5 && Math.Abs(line.FontSize - current.FontSize) < 1)
                {
                    current.Text.Append(' ').Append(line.Text);
                    previous = line;
                    continue;
                }

                bool startNew = current == null || isTitle || current.IsTitle || centered || indented;
                if (!startNew && previous != null)
                {
                    bool previousShort = previous.Right < previous.RefRight - size * 3;
                    bool sizeChanged = Math.Abs(line.FontSize - current.FontSize) > bodySize * 0.15;
                    bool gap = line.Page == previous.Page && previous.Bottom - line.Bottom > lineSpacing * 1.6;
                    startNew = previousShort || sizeChanged || gap;
                }

                if (startNew)
                {
                    current = new Paragraph { FontSize = line.FontSize, IsTitle = isTitle };
                    current.Text.Append(line.Text);
                    paragraphs.Add(current);
                }
                else
                {
                    AppendLine(current.Text, line.Text);
                }
                // A centered line (title pages, epigraphs) is a paragraph of its own
                previous = centered ? new Line { Right = 0, RefRight = line.RefRight, Page = line.Page, Bottom = line.Bottom } : line;
            }

            // Three or more titles in a row with no text between them are a table of contents
            for (int i = 0; i < paragraphs.Count;)
            {
                int j = i;
                while (j < paragraphs.Count && paragraphs[j].IsTitle) j++;
                if (j - i >= 3) for (int k = i; k < j; k++) paragraphs[k].IsTitle = false;
                i = Math.Max(j, i + 1);
            }
            return paragraphs;
        }

        /// <summary>
        /// Joins the next line of a paragraph: a word broken with a hyphen is joined back,
        /// a dash at the end of the line is kept without a space
        /// </summary>
        private static void AppendLine(StringBuilder text, string line)
        {
            int last = text.Length - 1;
            char end = text[last];
            if (end == '-' && last > 0 && char.IsLetter(text[last - 1]) && char.IsLower(line[0]))
            {
                text.Length = last;
                text.Append(line);
            }
            else if (end == '—' || end == '-' || end == '/')
            {
                text.Append(line);
            }
            else
            {
                text.Append(' ').Append(line);
            }
        }

        private static XDocument BuildFb2(List<Paragraph> paragraphs, string title, string author)
        {
            var authorElement = new XElement(fb2 + "author");
            var names = (author ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (names.Length == 0) authorElement.Add(new XElement(fb2 + "last-name", "Unknown"));
            else
            {
                if (names.Length > 1) authorElement.Add(new XElement(fb2 + "first-name", string.Join(" ", names.Take(names.Length - 1))));
                authorElement.Add(new XElement(fb2 + "last-name", names.Last()));
            }

            string allText = string.Concat(paragraphs.Take(200).Select(p => p.Text.ToString()));
            int cyrillic = allText.Count(c => c >= 'Ѐ' && c <= 'ӿ');
            int letters = allText.Count(char.IsLetter);
            string lang = letters > 0 && cyrillic > letters / 3 ? "ru" : "en";

            var body = new XElement(fb2 + "body");
            XElement section = null;
            foreach (var p in paragraphs)
            {
                string text = p.Text.ToString();
                if (p.IsTitle || section == null)
                {
                    section = new XElement(fb2 + "section");
                    body.Add(section);
                    if (p.IsTitle)
                    {
                        section.Add(new XElement(fb2 + "title", new XElement(fb2 + "p", text)));
                        continue;
                    }
                }
                section.Add(new XElement(fb2 + "p", text));
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", null),
                new XElement(fb2 + "FictionBook",
                    new XElement(fb2 + "description",
                        new XElement(fb2 + "title-info",
                            new XElement(fb2 + "genre", "prose"),
                            authorElement,
                            new XElement(fb2 + "book-title", title),
                            new XElement(fb2 + "lang", lang)),
                        new XElement(fb2 + "document-info",
                            new XElement(fb2 + "author", new XElement(fb2 + "nickname", "TinyOPDS")),
                            new XElement(fb2 + "program-used", "TinyOPDS PDF converter"),
                            new XElement(fb2 + "date", new XAttribute("value", DateTime.Now.ToString("yyyy-MM-dd")), DateTime.Now.ToString("yyyy-MM-dd")),
                            new XElement(fb2 + "id", Guid.NewGuid().ToString()),
                            new XElement(fb2 + "version", "1.0"))),
                    body));
        }

        private static string Clean(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return RemoveInvalidXmlChars(value).Trim();
        }

        /// <summary>
        /// PDF text may have control characters and broken surrogate pairs that can't be written to XML
        /// </summary>
        private static string RemoveInvalidXmlChars(string text)
        {
            var result = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    result.Append(c).Append(text[++i]);
                }
                else if (c == '	')
                {
                    result.Append(' ');
                }
                else if (System.Xml.XmlConvert.IsXmlChar(c) && !char.IsControl(c))
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }
    }
}
