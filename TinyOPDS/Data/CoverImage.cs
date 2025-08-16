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
    /// Cover image class for handling book cover images
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
            Log.WriteLine(LogLevel.Info, "Creating cover for book: {0}, FilePath: {1}", book.Title, book.FilePath);

            try
            {
                // Check if file exists first
                if (!File.Exists(book.FilePath))
                {
                    Log.WriteLine(LogLevel.Error, "Book file not found: {0}", book.FilePath);
                    return;
                }

                using (MemoryStream memStream = new MemoryStream())
                {
                    if (book.FilePath.ToLower().Contains(".zip@"))
                    {
                        string[] pathParts = book.FilePath.Split('@');
                        Log.WriteLine(LogLevel.Info, "Processing archive: {0}, entry: {1}", pathParts[0], pathParts[1]);

                        if (!File.Exists(pathParts[0]))
                        {
                            Log.WriteLine(LogLevel.Error, "Archive file not found: {0}", pathParts[0]);
                            return;
                        }

                        using (ZipFile zipFile = new ZipFile(pathParts[0]))
                        {
                            ZipEntry entry = zipFile.Entries.FirstOrDefault(e => e.FileName.Contains(pathParts[1]));
                            if (entry != null)
                            {
                                entry.Extract(memStream);
                                Log.WriteLine(LogLevel.Info, "Extracted {0} bytes from archive", memStream.Length);
                            }
                            else
                            {
                                Log.WriteLine(LogLevel.Error, "Entry {0} not found in archive {1}", pathParts[1], pathParts[0]);
                                return;
                            }
                        }
                    }
                    else
                    {
                        using (FileStream stream = new FileStream(book.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            stream.CopyTo(memStream);
                            Log.WriteLine(LogLevel.Info, "Read {0} bytes from file", memStream.Length);
                        }
                    }

                    if (memStream.Length == 0)
                    {
                        Log.WriteLine(LogLevel.Warning, "No data read from book file");
                        return;
                    }

                    Log.WriteLine(LogLevel.Info, "Processing {0} book type", book.BookType);

                    _cover = (book.BookType == BookType.EPUB) ?
                        new ePubParser().GetCoverImage(memStream, book.FilePath)
                        : new FB2Parser().GetCoverImage(memStream, book.FilePath);

                    if (_cover != null)
                    {
                        Log.WriteLine(LogLevel.Info, "Cover image extracted successfully, size: {0}x{1}", _cover.Width, _cover.Height);
                    }
                    else
                    {
                        Log.WriteLine(LogLevel.Warning, "No cover image found in book");
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Exception in CoverImage constructor for file {0}: {1}", book.FilePath, e.Message);
                Log.WriteLine(LogLevel.Error, "Stack trace: {0}", e.StackTrace);
                _cover = null;
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
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error converting image to stream: {0}", ex.Message);
                if (stream != null)
                {
                    stream.Dispose();
                    stream = null;
                }
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
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error resizing image: {0}", ex.Message);
                if (newImage != null)
                {
                    newImage.Dispose();
                    newImage = null;
                }
            }
            return newImage;
        }
    }
}