/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * ePub parser implementation
 *
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;

using TinyOPDS.Data;
using eBdb.EpubReader;

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
                stream.Position = 0;
                Epub epub = new Epub(stream);
                book.ID = epub.UUID;
                if (epub.Date != null && epub.Date.Count > 0)
                {
                    try { book.BookDate = DateTime.Parse(epub.Date.First().Date); }
                    catch
                    {
                        int year;
                        if (int.TryParse(epub.Date.First().Date, out year)) book.BookDate = new DateTime(year, 1, 1);
                    }
                }
                book.Title = epub.Title[0];
                book.Authors = new List<string>();

                // Process and normalize author names for EPUB
                foreach (var creator in epub.Creator)
                {
                    string normalizedAuthor = NormalizeAuthorName(creator);
                    if (!string.IsNullOrEmpty(normalizedAuthor))
                    {
                        book.Authors.Add(normalizedAuthor);
                    }
                }

                book.Genres = LookupGenres(epub.Subject);
                if (epub.Description != null && epub.Description.Count > 0) book.Annotation = epub.Description.First();
                if (epub.Language != null && epub.Language.Count > 0) book.Language = epub.Language.First();
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
        /// Epub's "subjects" are non-formal and extremely messy :( 
        /// This function will try to find a corresponding genres from the FB2 standard genres by using Soundex algorithm
        /// </summary>
        /// <param name="subjects"></param>
        /// <returns></returns>
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
                stream.Position = 0;
                Epub epub = new Epub(stream);
                if (epub.ExtendedData != null)
                {
                    foreach (ExtendedData value in epub.ExtendedData.Values)
                    {
                        string s = value.FileName.ToLower();
                        if (s.Contains(".jpeg") || s.Contains(".jpg") || s.Contains(".png"))
                        {
                            if (value.ID.ToLower().Contains("cover") || s.Contains("cover"))
                            {
                                using (MemoryStream memStream = new MemoryStream(value.GetContentAsBinary()))
                                {
                                    image = Image.FromStream(memStream);
                                    // Convert image to jpeg
                                    string mimeType = value.MimeType.ToLower();
                                    ImageFormat fmt = mimeType.Contains("png") ? ImageFormat.Png : ImageFormat.Gif;
                                    if (!mimeType.Contains("jpeg"))
                                    {
                                        image = Image.FromStream(image.ToStream(fmt));
                                    }
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