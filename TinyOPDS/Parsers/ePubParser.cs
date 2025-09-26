/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Native EPUB parser without external dependencies
 * FIXED: Always validates dates before returning Book object
 *
 */

using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Drawing;
using System.Net;

using TinyOPDS.Data;

namespace TinyOPDS.Parsers
{
    public class ePubParser : BookParser
    {
        private readonly XNamespace opfNs = "http://www.idpf.org/2007/opf";
        private readonly XNamespace dcNs = "http://purl.org/dc/elements/1.1/";
        private readonly XNamespace containerNs = "urn:oasis:names:tc:opendocument:xmlns:container";

        /// <summary>
        /// Parse EPUB book and normalize author names to standard format
        /// </summary>
        public override Book Parse(Stream stream, string fileName)
        {
            Book book = new Book(fileName);

            try
            {
                book.DocumentSize = (UInt32)stream.Length;

                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
                {
                    // Find and read package.opf
                    string opfPath = FindPackageOpf(archive);
                    if (string.IsNullOrEmpty(opfPath))
                    {
                        Log.WriteLine(LogLevel.Error, "Could not find package.opf in {0}", fileName);
                        // Don't return here - continue to date validation!
                    }
                    else
                    {
                        var opfEntry = archive.GetEntry(opfPath);
                        if (opfEntry == null)
                        {
                            Log.WriteLine(LogLevel.Error, "Could not read package.opf from {0}", fileName);
                            // Don't return here - continue to date validation!
                        }
                        else
                        {
                            XDocument opfDoc;
                            using (var opfStream = opfEntry.Open())
                            {
                                opfDoc = XDocument.Load(opfStream);
                            }

                            // Parse metadata
                            ParseMetadata(opfDoc, book);
                        }
                    }

                    // Always generate unique ID to avoid conflicts
                    if (string.IsNullOrEmpty(book.ID))
                    {
                        book.ID = Guid.NewGuid().ToString();
                        book.DocumentIDTrusted = false;
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Error parsing EPUB {0}: {1}", fileName, e.Message);
            }

            // CRITICAL FIX: Always validate dates before returning, regardless of parsing success
            EnsureValidDates(book, fileName);

            return book;
        }

        /// <summary>
        /// CRITICAL: Ensures book has valid dates - NEVER allows DateTime.MinValue
        /// This method MUST be called before returning any Book object
        /// </summary>
        private void EnsureValidDates(Book book, string fileName)
        {
            DateTime fileDate = DateParser.GetFileDate(fileName);

            // Validate BookDate
            if (book.BookDate == DateTime.MinValue ||
                book.BookDate == default ||
                book.BookDate.Year <= 1 ||
                book.BookDate.Year < 1800 ||
                book.BookDate.Year > DateTime.Now.Year + 10)
            {
                book.BookDate = fileDate;
            }

            // Set DocumentDate to file date (EPUBs don't have separate document date)
            // Always use file date for DocumentDate in EPUB
            book.DocumentDate = fileDate;

            // Final safety check - should never happen after above validations
            if (book.BookDate.Year <= 1 || book.DocumentDate.Year <= 1)
            {
                Log.WriteLine(LogLevel.Error,
                    "CRITICAL: Date validation failed for {0}, forcing current date", fileName);
                book.BookDate = DateTime.Now;
                book.DocumentDate = DateTime.Now;
            }
        }

        /// <summary>
        /// Find package.opf path from container.xml
        /// </summary>
        private string FindPackageOpf(ZipArchive archive)
        {
            var containerEntry = archive.GetEntry("META-INF/container.xml");
            if (containerEntry == null)
                return null;

            try
            {
                using (var stream = containerEntry.Open())
                {
                    var containerDoc = XDocument.Load(stream);
                    var rootfile = containerDoc.Descendants(containerNs + "rootfile").FirstOrDefault();
                    return rootfile?.Attribute("full-path")?.Value;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error reading container.xml: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Parse metadata from package.opf
        /// </summary>
        private void ParseMetadata(XDocument opfDoc, Book book)
        {
            var metadata = opfDoc.Root?.Element(opfNs + "metadata");
            if (metadata == null) return;

            // Title
            var title = metadata.Element(dcNs + "title")?.Value;
            book.Title = !string.IsNullOrEmpty(title) ? title : book.FileName;

            // Authors
            book.Authors = new List<string>();
            var creators = metadata.Elements(dcNs + "creator");
            foreach (var creator in creators)
            {
                string authorName = creator.Value;
                if (!string.IsNullOrEmpty(authorName))
                {
                    string normalized = NormalizeAuthorName(authorName);
                    if (!string.IsNullOrEmpty(normalized))
                        book.Authors.Add(normalized);
                }
            }

            // Language
            var language = metadata.Element(dcNs + "language")?.Value;
            if (!string.IsNullOrEmpty(language))
                book.Language = language;

            // Date - parse but don't rely on it being valid
            ParseDate(metadata, book);

            // Description/Annotation
            var description = metadata.Element(dcNs + "description")?.Value;
            if (!string.IsNullOrEmpty(description))
                book.Annotation = CleanHtmlFromDescription(description);

            // Subjects/Genres
            var subjects = metadata.Elements(dcNs + "subject").Select(s => s.Value).ToList();
            book.Genres = LookupGenres(subjects);

            // Series information
            ParseSeriesInfo(metadata, book);
        }

        /// <summary>
        /// Parse date from metadata
        /// </summary>
        private void ParseDate(XElement metadata, Book book)
        {
            // Get file date as fallback
            DateTime fileDate = DateParser.GetFileDate(book.FileName);

            var dateElement = metadata.Element(dcNs + "date");
            if (dateElement == null)
            {
                // No date element - will be handled by EnsureValidDates()
                Log.WriteLine(LogLevel.Info, "No date element in EPUB {0}", book.FileName);
                return;
            }

            string dateText = dateElement.Value;
            if (string.IsNullOrEmpty(dateText))
            {
                // Empty date element - will be handled by EnsureValidDates()
                Log.WriteLine(LogLevel.Info, "Empty date element in EPUB {0}", book.FileName);
                return;
            }

            // Parse with fallback
            DateTime parsedDate = DateParser.ParseDate(dateText, fileDate);

            // Additional validation for EPUB dates
            if (parsedDate.Year < 1800 || parsedDate.Year > DateTime.Now.Year + 10)
            {
                Log.WriteLine(LogLevel.Warning,
                    "Suspicious date {0} in EPUB {1}, will use file date in validation",
                    parsedDate, book.FileName);
                // Don't set the date - let EnsureValidDates() handle it
            }
            else
            {
                book.BookDate = parsedDate;
            }
        }

        /// <summary>
        /// Parse series information from metadata
        /// </summary>
        private void ParseSeriesInfo(XElement metadata, Book book)
        {
            try
            {
                string seriesName = null;
                uint seriesIndex = 0;

                // Look for meta elements
                var metas = metadata.Elements(opfNs + "meta");

                foreach (var meta in metas)
                {
                    // Check for calibre format
                    var nameAttr = meta.Attribute("name")?.Value;
                    var contentAttr = meta.Attribute("content")?.Value;

                    if (!string.IsNullOrEmpty(nameAttr))
                    {
                        if (nameAttr.Equals("calibre:series", StringComparison.OrdinalIgnoreCase))
                        {
                            seriesName = contentAttr;
                        }
                        else if (nameAttr.Equals("calibre:series_index", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(contentAttr))
                            {
                                if (float.TryParse(contentAttr, out float index))
                                {
                                    seriesIndex = (uint)index;
                                }
                            }
                        }
                    }

                    // Check for EPUB 3 format
                    var propertyAttr = meta.Attribute("property")?.Value;
                    if (!string.IsNullOrEmpty(propertyAttr))
                    {
                        if (propertyAttr.Equals("belongs-to-collection", StringComparison.OrdinalIgnoreCase))
                        {
                            seriesName = meta.Value;
                        }
                        else if (propertyAttr.Equals("group-position", StringComparison.OrdinalIgnoreCase))
                        {
                            if (uint.TryParse(meta.Value, out uint pos))
                            {
                                seriesIndex = pos;
                            }
                        }
                    }
                }

                // Set series info if found
                if (!string.IsNullOrEmpty(seriesName))
                {
                    book.Sequence = seriesName.Trim();
                    book.NumberInSequence = seriesIndex;
                    Log.WriteLine(LogLevel.Info, "Found series: {0} #{1}", book.Sequence, book.NumberInSequence);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error parsing series info: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Normalize EPUB author name to standard "LastName FirstName MiddleName" format
        /// </summary>
        private string NormalizeAuthorName(string authorName)
        {
            if (string.IsNullOrEmpty(authorName))
                return string.Empty;

            string normalized = authorName.Trim().Capitalize();

            // Handle comma-separated format "Smith, John" or "Smith, John Middle"
            if (normalized.Contains(","))
            {
                var parts = normalized.Split(',');
                if (parts.Length >= 2)
                {
                    string lastName = parts[0].Trim();
                    string remainingNames = parts[1].Trim();

                    if (!string.IsNullOrEmpty(remainingNames))
                    {
                        var nameWords = remainingNames.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        if (nameWords.Length == 1)
                        {
                            // "Smith, John" → "Smith John"
                            return $"{lastName} {nameWords[0]}";
                        }
                        else if (nameWords.Length >= 2)
                        {
                            // "Smith, John Middle" → "Smith John Middle"
                            return $"{lastName} {string.Join(" ", nameWords)}";
                        }
                    }

                    return lastName;
                }
            }

            // Handle space-separated format "John Smith" or "John Middle Smith"
            var words = normalized.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 1)
            {
                // Single word - assume it's LastName
                return words[0];
            }
            else if (words.Length == 2)
            {
                // Two words "John Smith" → "Smith John"
                return $"{words[1]} {words[0]}";
            }
            else if (words.Length >= 3)
            {
                // Three or more words "John Middle Smith" → "Smith John Middle"
                string lastName = words[words.Length - 1];
                var firstAndMiddleNames = new string[words.Length - 1];
                Array.Copy(words, 0, firstAndMiddleNames, 0, words.Length - 1);
                return $"{lastName} {string.Join(" ", firstAndMiddleNames)}";
            }

            return normalized;
        }

        /// <summary>
        /// Clean HTML tags from description
        /// </summary>
        private string CleanHtmlFromDescription(string htmlDescription)
        {
            if (string.IsNullOrEmpty(htmlDescription))
                return string.Empty;

            // Remove HTML tags
            string cleaned = Regex.Replace(
                htmlDescription,
                @"<[^>]*>",
                " ",
                RegexOptions.IgnoreCase
            );

            // Decode HTML entities
            cleaned = WebUtility.HtmlDecode(cleaned);

            // Clean up multiple spaces
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

            return cleaned;
        }

        /// <summary>
        /// Lookup genres based on subjects
        /// </summary>
        private List<string> LookupGenres(List<string> subjects)
        {
            List<string> genres = new List<string>();
            if (subjects == null || subjects.Count < 1)
            {
                genres.Add("prose");
            }
            else
            {
                foreach (string subj in subjects)
                {
                    var genre = Library.SoundexedGenres
                        .Where(g => g.Key.StartsWith(subj.SoundexByWord()) &&
                                   g.Key.WordsCount() <= subj.WordsCount() + 1)
                        .FirstOrDefault();

                    if (genre.Key != null)
                        genres.Add(genre.Value);
                }

                if (genres.Count < 1)
                    genres.Add("prose");
            }
            return genres;
        }

        /// <summary>
        /// Get cover image from EPUB file
        /// </summary>
        public override Image GetCoverImage(Stream stream, string fileName)
        {
            Image image = null;

            try
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
                {
                    // Try to find cover from manifest
                    string coverPath = FindCoverPath(archive);

                    if (!string.IsNullOrEmpty(coverPath))
                    {
                        var coverEntry = archive.GetEntry(coverPath);
                        if (coverEntry != null)
                        {
                            using (var coverStream = coverEntry.Open())
                            using (var memStream = new MemoryStream())
                            {
                                coverStream.CopyTo(memStream);
                                memStream.Position = 0;
                                image = Image.FromStream(memStream);
                                image = image.Resize(CoverImage.CoverSize);
                            }
                        }
                    }

                    // Fallback: search for cover by name
                    if (image == null)
                    {
                        foreach (var entry in archive.Entries)
                        {
                            string entryName = entry.Name.ToLower();
                            if (entryName.Contains("cover") &&
                                (entryName.EndsWith(".jpg") || entryName.EndsWith(".jpeg") ||
                                 entryName.EndsWith(".png") || entryName.EndsWith(".gif")))
                            {
                                using (var coverStream = entry.Open())
                                using (var memStream = new MemoryStream())
                                {
                                    coverStream.CopyTo(memStream);
                                    memStream.Position = 0;
                                    image = Image.FromStream(memStream);
                                    image = image.Resize(CoverImage.CoverSize);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "GetCoverImage exception: {0}", e.Message);
            }

            return image;
        }

        /// <summary>
        /// Find cover path from package.opf
        /// </summary>
        private string FindCoverPath(ZipArchive archive)
        {
            try
            {
                string opfPath = FindPackageOpf(archive);
                if (string.IsNullOrEmpty(opfPath))
                    return null;

                var opfEntry = archive.GetEntry(opfPath);
                if (opfEntry == null)
                    return null;

                XDocument opfDoc;
                using (var opfStream = opfEntry.Open())
                {
                    opfDoc = XDocument.Load(opfStream);
                }

                // Get OPF directory for relative paths
                string opfDir = Path.GetDirectoryName(opfPath)?.Replace('\\', '/') ?? "";
                if (!string.IsNullOrEmpty(opfDir))
                    opfDir += "/";

                var manifest = opfDoc.Root?.Element(opfNs + "manifest");
                if (manifest == null) return null;

                // Look for cover in metadata
                var metadata = opfDoc.Root?.Element(opfNs + "metadata");
                if (metadata != null)
                {
                    // Find cover meta element
                    var coverMeta = metadata.Elements(opfNs + "meta")
                        .Where(m => m.Attribute("name")?.Value == "cover")
                        .FirstOrDefault();

                    if (coverMeta != null)
                    {
                        string coverId = coverMeta.Attribute("content")?.Value;
                        if (!string.IsNullOrEmpty(coverId))
                        {
                            // Find item with this ID in manifest
                            var coverItem = manifest.Elements(opfNs + "item")
                                .Where(i => i.Attribute("id")?.Value == coverId)
                                .FirstOrDefault();

                            if (coverItem != null)
                            {
                                string href = coverItem.Attribute("href")?.Value;
                                if (!string.IsNullOrEmpty(href))
                                {
                                    // Handle relative paths
                                    if (!href.StartsWith("/"))
                                        href = opfDir + href;
                                    return href;
                                }
                            }
                        }
                    }
                }

                // Look for cover-image property in manifest
                var coverImageItem = manifest.Elements(opfNs + "item")
                    .Where(i => i.Attribute("properties")?.Value?.Contains("cover-image") == true)
                    .FirstOrDefault();

                if (coverImageItem != null)
                {
                    string href = coverImageItem.Attribute("href")?.Value;
                    if (!string.IsNullOrEmpty(href))
                    {
                        if (!href.StartsWith("/"))
                            href = opfDir + href;
                        return href;
                    }
                }

                // Look for item with id="cover" or id containing "cover"
                var coverItems = manifest.Elements(opfNs + "item")
                    .Where(i =>
                    {
                        string id = i.Attribute("id")?.Value?.ToLower() ?? "";
                        string mediaType = i.Attribute("media-type")?.Value ?? "";
                        return id.Contains("cover") && mediaType.StartsWith("image/");
                    });

                foreach (var item in coverItems)
                {
                    string href = item.Attribute("href")?.Value;
                    if (!string.IsNullOrEmpty(href))
                    {
                        if (!href.StartsWith("/"))
                            href = opfDir + href;
                        return href;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Error finding cover path: {0}", ex.Message);
            }

            return null;
        }
    }
}