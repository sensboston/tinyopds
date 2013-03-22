using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

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
                book.Title = epub.Title[0];
                book.Authors = new List<string>();
                book.Authors.AddRange(epub.Creator);
                for (int i = 0; i < book.Authors.Count; i++) book.Authors[i] = book.Authors[i].Capitalize();
                book.Genres = new List<string>(); 
                book.Genres.AddRange(epub.Subject);
                if (book.Genres.Count < 1) book.Genres.Add("prose");
                if (epub.Description != null && epub.Description.Count > 0) book.Annotation = epub.Description.First();
                if (epub.Language != null && epub.Language.Count > 0) book.Language = epub.Language.First();
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "exception {0}" , e.Message);
            }
            return book;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override Image GetCoverImage(Stream stream, string fileName)
        {
            try
            {
                stream.Position = 0;
                Epub epub = new Epub(stream);
            }
            catch (Exception e)
            {
                Log.WriteLine("GetCoverImage exception {0}", e.Message);
            }
            return null;
        }
    }
}
