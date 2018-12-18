/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines the CoverImage class and Image extensions
 * 
 ************************************************************/

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

using Ionic.Zip;
using TinyOPDS.Parsers;

namespace TinyOPDS.Data
{
    /// <summary>
    /// 
    /// </summary>
    public class CoverImage
    {
        public static Size CoverSize = new Size(480, 800);
        public static Size ThumbnailSize = new Size(48, 80);

        private Image _cover;
        private Image _thumbnail { get { return (_cover != null) ? _cover.Resize(ThumbnailSize) : null; } }
        public Stream CoverImageStream { get { return _cover.ToStream(ImageFormat.Jpeg); } }
        public Stream ThumbnailImageStream { get { return _thumbnail.ToStream(ImageFormat.Jpeg); } }
        public bool HasImages { get { return _cover != null; } }
        public string ID { get; set; }

        public CoverImage(Book book)
        {
            _cover = null;
            ID = book.ID;
            try
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    if (book.FilePath.ToLower().Contains(".zip@"))
                    {
                        string[] pathParts = book.FilePath.Split('@');
                        using (ZipFile zipFile = new ZipFile(pathParts[0]))
                        {
                            ZipEntry entry = zipFile.Entries.First(e => e.FileName.Contains(pathParts[1]));
                            if (entry != null) entry.Extract(memStream);
                        }
                    }
                    else
                    {
                        using (FileStream stream = new FileStream(book.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            stream.CopyTo(memStream);
                    }

                    _cover = (book.BookType == BookType.EPUB) ? new ePubParser().GetCoverImage(memStream, book.FilePath) 
                                                              : new FB2Parser().GetCoverImage(memStream, book.FilePath);
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "file {0}, exception {1}", book.FilePath, e.Message);
            }
        }
    }

    public static class ImageExtensions
    {
        public static Stream ToStream(this Image image, ImageFormat format)
        {
            MemoryStream stream = null;
            try
            {
                stream = new MemoryStream();
                if (image != null)
                {
                    image.Save(stream, format);
                    stream.Position = 0;
                }
            }
            catch
            {
                if (stream != null) stream.Dispose();
            }
            return stream;
        }

        public static Image Resize(this Image image, Size size, bool preserveAspectRatio = true)
        {
            if (image == null) return null;
            int newWidth, newHeight;
            if (preserveAspectRatio)
            {
                int originalWidth = image.Width;
                int originalHeight = image.Height;
                float percentWidth = (float)size.Width / (float)originalWidth;
                float percentHeight = (float)size.Height / (float)originalHeight;
                float percent = percentHeight < percentWidth ? percentHeight : percentWidth;
                newWidth = (int)(originalWidth * percent);
                newHeight = (int)(originalHeight * percent);
            }
            else
            {
                newWidth = size.Width;
                newHeight = size.Height;
            }
            Image newImage = null;
            try
            {
                newImage = new Bitmap(newWidth, newHeight);
                using (Graphics graphicsHandle = Graphics.FromImage(newImage))
                {
                    graphicsHandle.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphicsHandle.DrawImage(image, 0, 0, newWidth, newHeight);
                }
            }
            catch
            {
                if (newImage != null) newImage.Dispose();
            }
            return newImage;
        }
    }
}
