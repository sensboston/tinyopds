using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Drawing;
using System.Drawing.Imaging;

using FB2Library;
using FB2Library.HeaderItems;
using FB2Library.Elements;
using TinyOPDS.Data;

namespace TinyOPDS.Parsers
{
    public class FB2Parser : BookParser
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override Book Parse(Stream stream, string fileName)
        {
            XDocument xml = null;
            Book book = new Book(fileName);
            try
            {
                FB2File fb2 = new FB2File();
                // Load header only
                stream.Position = 0;
                xml = XDocument.Load(stream);
                fb2.Load(xml, true);
                book.DocumentSize = (UInt32) stream.Length;

                if (fb2.DocumentInfo != null)
                {
                    book.ID = (!string.IsNullOrEmpty(fb2.DocumentInfo.ID) ? fb2.DocumentInfo.ID : Utils.Create(Utils.IsoOidNamespace, fileName).ToString());
                    if (fb2.DocumentInfo.DocumentVersion != null) book.Version = (float) fb2.DocumentInfo.DocumentVersion;
                    if (fb2.DocumentInfo.DocumentDate != null) book.DocumentDate = fb2.DocumentInfo.DocumentDate.DateValue;
                }

                if (fb2.TitleInfo != null)
                {
                    if (fb2.TitleInfo.Cover != null && fb2.TitleInfo.Cover.HasImages()) book.HasCover = true;
                    if (fb2.TitleInfo.BookTitle != null) book.Title = fb2.TitleInfo.BookTitle.Text;
                    if (fb2.TitleInfo.Annotation != null) book.Annotation = fb2.TitleInfo.Annotation.ToString();
                    if (fb2.TitleInfo.Sequences != null && fb2.TitleInfo.Sequences.Count > 0)
                    {
                        book.Sequence = fb2.TitleInfo.Sequences.First().Name.Capitalize(true);
                        if (fb2.TitleInfo.Sequences.First().Number != null)
                        {
                            book.NumberInSequence = (UInt32)(fb2.TitleInfo.Sequences.First().Number);
                        }
                    }
                    if (fb2.TitleInfo.Language != null) book.Language = fb2.TitleInfo.Language;
                    if (fb2.TitleInfo.BookDate != null) book.BookDate = fb2.TitleInfo.BookDate.DateValue;
                    if (fb2.TitleInfo.BookAuthors != null && fb2.TitleInfo.BookAuthors.Count() > 0)
                    {
                        book.Authors = new List<string>();
                        book.Authors.AddRange(from ba in fb2.TitleInfo.BookAuthors select string.Concat(ba.LastName, " ", ba.FirstName, " ", ba.MiddleName).Replace("  ", " ").Capitalize());
                    }
                    if (fb2.TitleInfo.Translators != null && fb2.TitleInfo.Translators.Count() > 0)
                    {
                        book.Translators = new List<string>();
                        book.Translators.AddRange(from ba in fb2.TitleInfo.Translators select string.Concat(ba.LastName, " ", ba.FirstName, " ", ba.MiddleName).Replace("  ", " ").Capitalize());
                    }
                    if (fb2.TitleInfo.Genres != null && fb2.TitleInfo.Genres.Count() > 0)
                    {
                        book.Genres = new List<string>();
                        book.Genres.AddRange((from g in fb2.TitleInfo.Genres select g.Genre).ToList());
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Book.Parse() exception {0} on file: {1}", e.Message, fileName);
            }
            finally
            {
                // Dispose xml document
                xml = null;
            }

            return book;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public override Image GetCoverImage(Stream stream, string fileName)
        {
            Image image = null;
            XDocument xml = null;
            try
            {
                FB2File fb2 = new FB2File();
                stream.Position = 0;
                xml = XDocument.Load(stream);
                fb2.Load(xml, false);

                if (fb2.TitleInfo != null && fb2.TitleInfo.Cover != null && fb2.TitleInfo.Cover.HasImages() && fb2.Images.Count > 0)
                {
                    string coverHRef = fb2.TitleInfo.Cover.CoverpageImages.First().HRef.Substring(1);
                    var binaryObject = fb2.Images.First(item => item.Value.Id == coverHRef);
                    if (binaryObject.Value.BinaryData != null && binaryObject.Value.BinaryData.Length > 0)
                    {
                        using (MemoryStream memStream = new MemoryStream(binaryObject.Value.BinaryData))
                        {
                            image = Image.FromStream(memStream);
                            // Convert image to jpeg
                            ImageFormat fmt = binaryObject.Value.ContentType == ContentTypeEnum.ContentTypePng ? ImageFormat.Png : ImageFormat.Gif;
                            if (binaryObject.Value.ContentType != ContentTypeEnum.ContentTypeJpeg)
                            {
                                image = Image.FromStream(image.ToStream(fmt));
                            }
                            image = image.Resize(CoverImage.CoverSize);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Book.GetCoverImage() exception {0} on file: {1}", e.Message, fileName);
            }
            // Dispose xml document
            xml = null;
            return image;
        }
    }
}
