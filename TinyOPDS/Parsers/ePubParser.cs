/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * ePub parser implementation - migrated to EpubSharp
 *
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

using TinyOPDS.Data;
using EpubSharp;

namespace TinyOPDS.Parsers
{
    public class ePubParser : BookParser
    {
        /// <summary>
        /// Parse EPUB book and normalize author names to standard format
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override Book Parse(Stream stream, string fileName)
        {
            Book book = new Book(fileName);
            try
            {
                book.DocumentSize = (UInt32)stream.Length;

                // Convert stream to byte array as EpubReader.Read doesn't accept Stream directly
                stream.Position = 0;
                byte[] epubData = new byte[stream.Length];
                stream.Read(epubData, 0, epubData.Length);

                EpubBook epub = EpubReader.Read(epubData);

                // Get unique identifier
                var identifiers = epub.Format.Opf.Metadata.Identifiers;
                book.ID = identifiers?.FirstOrDefault()?.Text ?? Guid.NewGuid().ToString();

                // Parse date
                var dates = epub.Format.Opf.Metadata.Dates;
                if (dates?.Count > 0)
                {
                    try
                    {
                        book.BookDate = DateTime.Parse(dates.First().Text);
                    }
                    catch
                    {
                        int year;
                        if (int.TryParse(dates.First().Text, out year))
                            book.BookDate = new DateTime(year, 1, 1);
                    }
                }

                book.Title = epub.Title ?? fileName;
                book.Authors = new List<string>();

                // Process and normalize author names for EPUB
                if (epub.Authors != null)
                {
                    foreach (var author in epub.Authors)
                    {
                        string normalizedAuthor = NormalizeAuthorName(author);
                        if (!string.IsNullOrEmpty(normalizedAuthor))
                        {
                            book.Authors.Add(normalizedAuthor);
                        }
                    }
                }

                // Handle genres/subjects
                var subjects = epub.Format.Opf.Metadata.Subjects?.ToList() ?? new List<string>();
                book.Genres = LookupGenres(subjects);

                // Handle description
                if (epub.Format.Opf.Metadata.Descriptions?.Count > 0)
                {
                    book.Annotation = CleanHtmlFromDescription(epub.Format.Opf.Metadata.Descriptions.First());
                }

                // Handle language
                if (epub.Format.Opf.Metadata.Languages?.Count > 0)
                    book.Language = epub.Format.Opf.Metadata.Languages.First();
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "exception {0}", e.Message);
            }
            return book;
        }

        /// <summary>
        /// Normalize EPUB author name to standard "LastName FirstName MiddleName" format
        /// EPUB authors can come in various formats: "John Smith", "Smith, John", "John Middle Smith"
        /// </summary>
        /// <param name="authorName">Raw author name from EPUB</param>
        /// <returns>Normalized author name in "LastName FirstName MiddleName" format</returns>
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
        /// Clean HTML tags from description for OPDS compatibility
        /// OPDS clients expect plain text in metadata fields
        /// </summary>
        /// <param name="htmlDescription">Description that may contain HTML tags</param>
        /// <returns>Clean text description</returns>
        private string CleanHtmlFromDescription(string htmlDescription)
        {
            if (string.IsNullOrEmpty(htmlDescription))
                return string.Empty;

            // Remove HTML tags using regex
            string cleaned = System.Text.RegularExpressions.Regex.Replace(
                htmlDescription,
                @"<[^>]*>",
                " ",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            // Decode HTML entities
            cleaned = System.Net.WebUtility.HtmlDecode(cleaned);

            // Clean up multiple spaces and trim
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ").Trim();

            return cleaned;
        }
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
                    var genre = Library.SoundexedGenres.Where(g => g.Key.StartsWith(subj.SoundexByWord()) && g.Key.WordsCount() <= subj.WordsCount() + 1).FirstOrDefault();
                    if (genre.Key != null) genres.Add(genre.Value);
                }
                if (genres.Count < 1) genres.Add("prose");
            }
            return genres;
        }

        /// <summary>
        /// Get cover image from EPUB file
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override Image GetCoverImage(Stream stream, string fileName)
        {
            Image image = null;
            try
            {
                // Convert stream to byte array
                stream.Position = 0;
                byte[] epubData = new byte[stream.Length];
                stream.Read(epubData, 0, epubData.Length);

                EpubBook epub = EpubReader.Read(epubData);

                // EpubSharp provides direct access to cover image
                if (epub.CoverImage != null && epub.CoverImage.Length > 0)
                {
                    using (MemoryStream memStream = new MemoryStream(epub.CoverImage))
                    {
                        image = Image.FromStream(memStream);
                        image = image.Resize(CoverImage.CoverSize);
                    }
                }
                else
                {
                    // Fallback: search through images for cover
                    if (epub.Resources?.Images?.Count > 0)
                    {
                        foreach (var imageFile in epub.Resources.Images)
                        {
                            string imageFileName = imageFile.FileName?.ToLower() ?? string.Empty;
                            if (imageFileName.Contains("cover"))
                            {
                                using (MemoryStream memStream = new MemoryStream(imageFile.Content))
                                {
                                    image = Image.FromStream(memStream);
                                    image = image.Resize(CoverImage.CoverSize);
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "GetCoverImage exception {0}", e.Message);
            }
            return image;
        }
    }
}