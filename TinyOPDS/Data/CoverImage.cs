/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the CoverImage class and Image extensions
 *
 */

using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;

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
                        // For regular files, check existence
                        if (!File.Exists(book.FilePath))
                        {
                            Log.WriteLine(LogLevel.Error, "Book file not found: {0}", book.FilePath);
                            return;
                        }

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

            // Generate default cover if no cover was found
            if (_cover == null)
            {
                Log.WriteLine(LogLevel.Info, "No cover found, generating default cover for: {0}", book.Title);
                try
                {
                    string author = book.Authors.FirstOrDefault() ?? "Unknown Author";
                    _cover = GenerateDefaultCover(author, book.Title);
                    if (_cover != null)
                    {
                        Log.WriteLine(LogLevel.Info, "Default cover generated successfully");
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Error generating default cover: {0}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Generate a default cover image with author and title text
        /// </summary>
        /// <param name="author">Author name</param>
        /// <param name="title">Book title</param>
        /// <returns>Generated cover image</returns>
        private Image GenerateDefaultCover(string author, string title)
        {
            try
            {
                // Load background image from embedded resource
                Image backgroundImage = LoadBackgroundImage();
                if (backgroundImage == null)
                {
                    Log.WriteLine(LogLevel.Warning, "Could not load background image, creating solid color background");
                    backgroundImage = new Bitmap(CoverSize.Width, CoverSize.Height);
                    using (Graphics tempG = Graphics.FromImage(backgroundImage))
                    {
                        // Create a dark leather-like background
                        using (var brush = new SolidBrush(Color.FromArgb(45, 35, 25)))
                        {
                            tempG.FillRectangle(brush, 0, 0, CoverSize.Width, CoverSize.Height);
                        }
                    }
                }

                // Resize background to cover size if needed
                Image resizedBackground = backgroundImage.Resize(CoverSize, false);
                backgroundImage.Dispose();

                // Create graphics object for drawing text
                using (Graphics g = Graphics.FromImage(resizedBackground))
                {
                    // Set high quality rendering
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAlias;
                    g.CompositingQuality = CompositingQuality.HighQuality;

                    // Define colors - "golden" ones
                    Color goldColor = Color.FromArgb(236, 216, 145);
                    Color shadowColor = Color.FromArgb(100, 80, 60, 40);

                    // Create fonts
                    Font authorFont = GetBestFont("Times New Roman", 32, FontStyle.Bold | FontStyle.Italic);
                    Font titleFont = GetBestFont("Times New Roman", 40, FontStyle.Bold);

                    // Calculate text areas - измененные позиции
                    Rectangle authorArea = new Rectangle(40, 120, CoverSize.Width - 80, 160);
                    Rectangle titleArea = new Rectangle(40, CoverSize.Height - 400, CoverSize.Width - 80, 260);

                    // Draw author text (top, moved down)
                    DrawTextWithShadow(g, author, authorFont, goldColor, shadowColor, authorArea, true);

                    // Draw title text (bottom, moved up)
                    DrawTextWithShadow(g, title, titleFont, goldColor, shadowColor, titleArea, false);

                    // Clean up fonts
                    authorFont.Dispose();
                    titleFont.Dispose();
                }

                return resizedBackground;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GenerateDefaultCover: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Load background image from embedded resource
        /// </summary>
        /// <returns>Background image or null if not found</returns>
        private Image LoadBackgroundImage()
        {
            try
            {
                // Get all manifest resource names to find the correct one
                string[] resourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                string resourceName = null;

                // Look for book_cover.jpg in Resources folder
                foreach (string name in resourceNames)
                {
                    if (name.EndsWith("Resources.book_cover.jpg"))
                    {
                        resourceName = name;
                        break;
                    }
                }

                // Fallback: look for any book_cover.jpg
                if (resourceName == null)
                {
                    foreach (string name in resourceNames)
                    {
                        if (name.EndsWith("book_cover.jpg"))
                        {
                            resourceName = name;
                            break;
                        }
                    }
                }

                if (resourceName != null)
                {
                    using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                    {
                        if (stream != null && stream.Length > 0)
                        {
                            return Image.FromStream(stream);
                        }
                    }
                }
                else
                {
                    Log.WriteLine(LogLevel.Warning, "book_cover.jpg resource not found in assembly");
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Could not load embedded background image: {0}", ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Get the best available font, falling back to defaults if needed
        /// </summary>
        /// <param name="fontName">Preferred font name</param>
        /// <param name="size">Font size</param>
        /// <param name="style">Font style</param>
        /// <returns>Font object</returns>
        private Font GetBestFont(string fontName, float size, FontStyle style)
        {
            try
            {
                return new Font(fontName, size, style);
            }
            catch
            {
                try
                {
                    // Fallback to Georgia
                    return new Font("Georgia", size, style);
                }
                catch
                {
                    try
                    {
                        // Final fallback to system serif font
                        return new Font(FontFamily.GenericSerif, size, style);
                    }
                    catch
                    {
                        // Last resort - default font
                        return new Font(FontFamily.GenericSansSerif, size, style);
                    }
                }
            }
        }

        /// <summary>
        /// Draw text with shadow effect
        /// </summary>
        /// <param name="g">Graphics object</param>
        /// <param name="text">Text to draw</param>
        /// <param name="font">Font to use</param>
        /// <param name="textColor">Main text color</param>
        /// <param name="shadowColor">Shadow color</param>
        /// <param name="area">Area to draw in</param>
        /// <param name="isAuthor">True if this is author text (top), false for title (bottom)</param>
        private void DrawTextWithShadow(Graphics g, string text, Font font, Color textColor, Color shadowColor, Rectangle area, bool isAuthor)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Create brushes
            using (Brush textBrush = new SolidBrush(textColor))
            using (Brush shadowBrush = new SolidBrush(shadowColor))
            {
                // Set text alignment
                StringFormat format = new StringFormat();
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = isAuthor ? StringAlignment.Near : StringAlignment.Far;
                format.Trimming = StringTrimming.Word;
                format.FormatFlags = StringFormatFlags.LineLimit;

                // Calculate optimal font size for the text area
                Font scaledFont = GetOptimalFontSize(g, text, font, area);

                // Draw shadow (offset by 2 pixels)
                Rectangle shadowArea = new Rectangle(area.X + 2, area.Y + 2, area.Width, area.Height);
                g.DrawString(text, scaledFont, shadowBrush, shadowArea, format);

                // Draw main text
                g.DrawString(text, scaledFont, textBrush, area, format);

                // Clean up
                if (scaledFont != font)
                    scaledFont.Dispose();
                format.Dispose();
            }
        }

        /// <summary>
        /// Calculate optimal font size to fit text in given area
        /// </summary>
        /// <param name="g">Graphics object</param>
        /// <param name="text">Text to measure</param>
        /// <param name="baseFont">Base font to scale</param>
        /// <param name="area">Target area</param>
        /// <returns>Optimally sized font</returns>
        private Font GetOptimalFontSize(Graphics g, string text, Font baseFont, Rectangle area)
        {
            float fontSize = baseFont.Size;
            Font testFont = new Font(baseFont.FontFamily, fontSize, baseFont.Style);

            try
            {
                // Measure text with current font
                SizeF textSize = g.MeasureString(text, testFont, area.Width);

                // Scale down if text is too large
                while ((textSize.Width > area.Width || textSize.Height > area.Height) && fontSize > 8)
                {
                    fontSize -= 1;
                    testFont.Dispose();
                    testFont = new Font(baseFont.FontFamily, fontSize, baseFont.Style);
                    textSize = g.MeasureString(text, testFont, area.Width);
                }

                return testFont;
            }
            catch
            {
                testFont?.Dispose();
                return new Font(baseFont.FontFamily, Math.Max(8, fontSize), baseFont.Style);
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