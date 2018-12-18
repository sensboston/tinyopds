/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * ePub parser class implementation
 * 
 ************************************************************/

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
        /// 
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
                book.Authors.AddRange(epub.Creator);
                for (int i = 0; i < book.Authors.Count; i++) book.Authors[i] = book.Authors[i].Capitalize();
                book.Genres = LookupGenres(epub.Subject);
                if (epub.Description != null && epub.Description.Count > 0) book.Annotation = epub.Description.First();
                if (epub.Language != null && epub.Language.Count > 0) book.Language = epub.Language.First();

                // Lookup cover
                if (epub.ExtendedData != null)
                {
                    foreach (ExtendedData value in epub.ExtendedData.Values)
                    {
                        string s = value.FileName.ToLower();
                        if (s.Contains(".jpeg") || s.Contains(".jpg") || s.Contains(".png"))
                        {
                            if (value.ID.ToLower().Contains("cover") || s.Contains("cover"))
                            {
                                book.HasCover = true;
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "exception {0}" , e.Message);
            }
            return book;
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
                    var genre = Library.SoundexedGenres.Where(g => g.Key.StartsWith(subj.SoundexByWord()) && g.Key.WordsCount() <= subj.WordsCount()+1).FirstOrDefault();
                    if (genre.Key != null) genres.Add(genre.Value);
                }
                if (genres.Count < 1) genres.Add("prose");
            }
            return genres;
        }

        /// <summary>
        /// 
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
