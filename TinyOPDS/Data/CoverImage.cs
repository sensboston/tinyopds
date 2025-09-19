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

using TinyOPDS.Parsers;

namespace TinyOPDS.Data
{
    /// <summary>
    /// Cover image class for handling book cover images
    /// </summary>
    public class CoverImage
    {
        // Backward-compatible public sizes
        public static readonly Size CoverSize = new Size(240, 400);
        public static readonly Size ThumbnailSize = new Size(96, 160);

        // Internal design size for default cover
        public static readonly Size DefaultDesignSize = new Size(480, 800);

        private const long CoverJpegQuality = 60L;
        private const long ThumbnailJpegQuality = 60L;

        // Serialize System.Drawing on Linux/Mono
        private static readonly object GdiLock = new object();
        private static bool NeedGdiLock => Utils.IsLinux;

        private readonly Image cover;
        private Image Thumbnail => cover?.Resize(ThumbnailSize);

        public Stream CoverImageStream => cover.ToJpegStream(CoverJpegQuality);
        public Stream ThumbnailImageStream => Thumbnail.ToJpegStream(ThumbnailJpegQuality);
        public bool HasImages => cover != null;
        public string ID { get; set; }

        public CoverImage(Book book)
        {
            cover = null;
            ID = book.ID;
            Log.WriteLine(LogLevel.Info, "Creating cover for book: {0}, FilePath: {1}", book.Title, book.FilePath);

            try
            {
                using (var memStream = new MemoryStream())
                {
                    if (book.FilePath.ToLower().Contains(".zip@"))
                    {
                        var pathParts = book.FilePath.Split('@');
                        Log.WriteLine(LogLevel.Info, "Processing archive: {0}, entry: {1}", pathParts[0], pathParts[1]);

                        if (!File.Exists(pathParts[0]))
                        {
                            Log.WriteLine(LogLevel.Error, "Archive file not found: {0}", pathParts[0]);
                            return;
                        }

                        using (var zipArchive = System.IO.Compression.ZipFile.OpenRead(pathParts[0]))
                        {
                            var entry = zipArchive.Entries.FirstOrDefault(e =>
                                e.FullName.IndexOf(pathParts[1], StringComparison.OrdinalIgnoreCase) >= 0);

                            if (entry != null)
                            {
                                using (var entryStream = entry.Open())
                                {
                                    entryStream.CopyTo(memStream);
                                    Log.WriteLine(LogLevel.Info, "Extracted {0} bytes from archive", memStream.Length);
                                }
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
                        if (!File.Exists(book.FilePath))
                        {
                            Log.WriteLine(LogLevel.Error, "Book file not found: {0}", book.FilePath);
                            return;
                        }

                        using (var stream = new FileStream(book.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
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

                    var extracted = (book.BookType == BookType.EPUB)
                        ? new ePubParser().GetCoverImage(memStream, book.FilePath)
                        : new FB2Parser().GetCoverImage(memStream, book.FilePath);

                    if (extracted != null)
                    {
                        Log.WriteLine(LogLevel.Info, "Cover image extracted: {0}x{1}", extracted.Width, extracted.Height);
                        var fitted = extracted.Resize(CoverSize, preserveAspectRatio: true);
                        extracted.Dispose();
                        cover = fitted;
                    }
                }
            }
            catch (Exception e)
            {
                Log.WriteLine(LogLevel.Error, "Exception in CoverImage constructor for file {0}: {1}", book.FilePath, e.Message);
                Log.WriteLine(LogLevel.Error, "Stack trace: {0}", e.StackTrace);
            }

            if (cover == null)
            {
                Log.WriteLine(LogLevel.Info, "No cover found, generating default cover for: {0}", book.Title);
                try
                {
                    string author = book.Authors.FirstOrDefault() ?? "Unknown Author";
                    var generated = GenerateDefaultCover(author, book.Title);
                    if (generated != null)
                    {
                        Log.WriteLine(LogLevel.Info, "Default cover generated");
                        cover = generated;
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Error generating default cover: {0}", ex.Message);
                }
            }
        }

        private Image GenerateDefaultCover(string author, string title)
        {
            void tuneGraphics(Image _) { }

            // Serialize all GDI+ usage on Linux
            if (NeedGdiLock)
            {
                lock (GdiLock)
                {
                    return GenerateDefaultCoverCore(author, title, tuneGraphics);
                }
            }
            else
            {
                return GenerateDefaultCoverCore(author, title, tuneGraphics);
            }
        }

        private Image GenerateDefaultCoverCore(string author, string title, Action<Image> _)
        {
            try
            {
                Image backgroundImage = LoadBackgroundImage();
                if (backgroundImage == null)
                {
                    backgroundImage = new Bitmap(DefaultDesignSize.Width, DefaultDesignSize.Height, PixelFormat.Format32bppArgb);
                    using (Graphics tempG = Graphics.FromImage(backgroundImage))
                    using (var brush = new SolidBrush(Color.FromArgb(45, 35, 25)))
                        tempG.FillRectangle(brush, 0, 0, DefaultDesignSize.Width, DefaultDesignSize.Height);
                }

                Image design = backgroundImage.Resize(DefaultDesignSize, preserveAspectRatio: false);
                backgroundImage.Dispose();

                using (Graphics g = Graphics.FromImage(design))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAlias;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    Color gold = Color.FromArgb(236, 216, 145);
                    Color shadow = Color.FromArgb(100, 80, 60, 40);

                    var fontSize = Utils.IsLinux ? 26f : 20f;
                    using (Font authorFont = CreateFallbackFont("Times New Roman", fontSize-4f, FontStyle.Bold | FontStyle.Italic))
                    using (Font titleFont = CreateFallbackFont("Times New Roman", fontSize, FontStyle.Bold))
                    {
                        Rectangle authorArea = new Rectangle(40, 120, DefaultDesignSize.Width - 80, 200);
                        Rectangle titleArea = new Rectangle(30, DefaultDesignSize.Height - 360, DefaultDesignSize.Width - 60, 300);

                        DrawTextWithShadow(g, author, authorFont, gold, shadow, authorArea, true);
                        DrawTextWithShadow(g, title, titleFont, gold, shadow, titleArea, false);
                    }
                }

                Image finalFitted = design.Resize(CoverSize, preserveAspectRatio: true);
                design.Dispose();
                return finalFitted;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error in GenerateDefaultCover: {0}", ex.Message);
                return null;
            }
        }

        private Image LoadBackgroundImage()
        {
            try
            {
                string[] resourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                string resourceName = resourceNames.FirstOrDefault(n => n.EndsWith("Resources.book_cover.jpg"))
                                   ?? resourceNames.FirstOrDefault(n => n.EndsWith("book_cover.jpg"));

                if (resourceName != null)
                {
                    using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                    {
                        if (stream != null && stream.Length > 0)
                            return Image.FromStream(stream);
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

        private Font CreateFallbackFont(string primaryName, float size, FontStyle style)
        {
            // Prefer Linux-safe families when running under Mono
            if (Utils.IsLinux)
            {
                // DejaVu families are present on most ARM distros
                try { return new Font("DejaVu Sans", size, style); } catch { }
                try { return new Font("DejaVu Serif", size, style); } catch { }
                try { return new Font(FontFamily.GenericSansSerif, size, style); } catch { }
            }

            return GetBestFont(primaryName, size, style);
        }

        private Font GetBestFont(string fontName, float size, FontStyle style)
        {
            try { return new Font(fontName, size, style); }
            catch
            {
                try { return new Font("Georgia", size, style); }
                catch
                {
                    try { return new Font(FontFamily.GenericSerif, size, style); }
                    catch { return new Font(FontFamily.GenericSansSerif, size, style); }
                }
            }
        }

        private void DrawTextWithShadow(Graphics g, string text, Font font, Color textColor, Color shadowColor, Rectangle area, bool isAuthor)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Normalize area to avoid zero/negative sizes
            var safeArea = new Rectangle(area.X, area.Y, Math.Max(1, area.Width), Math.Max(1, area.Height));

            using (Brush textBrush = new SolidBrush(textColor))
            using (Brush shadowBrush = new SolidBrush(shadowColor))
            using (var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = isAuthor ? StringAlignment.Near : StringAlignment.Center,
                Trimming = StringTrimming.Word
            })
            {
                Font scaledFont = GetOptimalFontSize(g, text.Trim(), font, safeArea, !isAuthor);

                var shadowArea = new Rectangle(safeArea.X + 2, safeArea.Y + 2, safeArea.Width, safeArea.Height);
                g.DrawString(text, scaledFont, shadowBrush, shadowArea, format);
                g.DrawString(text, scaledFont, textBrush, safeArea, format);

                if (!ReferenceEquals(scaledFont, font)) scaledFont.Dispose();
            }
        }

        private static readonly float MinFontSize = 8f;
        private static readonly float MaxFontSize = 72f;

        private Font GetOptimalFontSize(Graphics g, string text, Font baseFont, Rectangle area, bool isTitle = false)
        {
            // MeasureString may crash in libgdiplus if called with crazy sizes or in parallel
            Func<Graphics, string, Font, int, StringFormat, SizeF> safeMeasure = SafeMeasureString;

            float fontSize = Math.Max(MinFontSize, Math.Min(MaxFontSize, baseFont.Size));
            Font testFont = new Font(baseFont.FontFamily, fontSize, baseFont.Style);

            try
            {
                using (var fmt = new StringFormat(StringFormat.GenericDefault)
                {
                    FormatFlags = StringFormatFlags.LineLimit
                })
                {
                    SizeF textSize = safeMeasure(g, text, testFont, Math.Max(1, area.Width), fmt);

                    while ((textSize.Width > area.Width || textSize.Height > area.Height) && fontSize > MinFontSize)
                    {
                        fontSize = Math.Max(MinFontSize, fontSize - 1f);
                        testFont.Dispose();
                        testFont = new Font(baseFont.FontFamily, fontSize, baseFont.Style);
                        textSize = safeMeasure(g, text, testFont, Math.Max(1, area.Width), fmt);
                    }

                    if (isTitle && fontSize > MinFontSize + 2f)
                    {
                        fontSize = Math.Max(MinFontSize, fontSize - 2f);
                        testFont.Dispose();
                        testFont = new Font(baseFont.FontFamily, fontSize, baseFont.Style);
                    }
                }

                return testFont;
            }
            catch
            {
                testFont?.Dispose();
                return new Font(baseFont.FontFamily, Math.Max(MinFontSize, Math.Min(MaxFontSize, fontSize)), baseFont.Style);
            }
        }

        private static SizeF SafeMeasureString(Graphics g, string text, Font font, int width, StringFormat fmt)
        {
            if (string.IsNullOrEmpty(text)) return SizeF.Empty;
            width = Math.Max(1, width);

            if (Utils.IsLinux)
            {
                lock (GdiLock)
                {
                    return g.MeasureString(text, font, width, fmt);
                }
            }
            else
            {
                return g.MeasureString(text, font, width, fmt);
            }
        }
    }

    public static class ImageExtensions
    {
        public static Stream ToJpegStream(this Image image, long quality)
        {
            if (image == null) return null;

            if (Utils.IsLinux)
            {
                lock (CoverImage_GdiLockAccessor.Lock) // small helper to reuse same lock
                {
                    return ToJpegStreamCore(image, quality);
                }
            }
            else
            {
                return ToJpegStreamCore(image, quality);
            }
        }

        private static Stream ToJpegStreamCore(Image image, long quality)
        {
            var stream = new MemoryStream();
            try
            {
                var jpegCodec = ImageCodecInfo.GetImageEncoders()
                    .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

                if (jpegCodec == null)
                {
                    image.Save(stream, ImageFormat.Jpeg);
                }
                else
                {
                    using (var encParams = new EncoderParameters(1))
                    {
                        encParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                        image.Save(stream, jpegCodec, encParams);
                    }
                }
                stream.Position = 0;
                return stream;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error converting image to JPEG stream: {0}", ex.Message);
                stream.Dispose();
                return null;
            }
        }

        // Backward-compatible method
        public static Stream ToStream(this Image image, ImageFormat format)
        {
            if (image == null) return null;

            if (Utils.IsLinux)
            {
                lock (CoverImage_GdiLockAccessor.Lock)
                {
                    return ToStreamCore(image, format);
                }
            }
            else
            {
                return ToStreamCore(image, format);
            }
        }

        private static Stream ToStreamCore(Image image, ImageFormat format)
        {
            var stream = new MemoryStream();
            try
            {
                if (format.Guid == ImageFormat.Jpeg.Guid)
                {
                    var jpegCodec = ImageCodecInfo.GetImageEncoders()
                        .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    if (jpegCodec != null)
                    {
                        using (var encParams = new EncoderParameters(1))
                        {
                            encParams.Param[0] = new EncoderParameter(Encoder.Quality, 60L);
                            image.Save(stream, jpegCodec, encParams);
                        }
                    }
                    else
                    {
                        image.Save(stream, ImageFormat.Jpeg);
                    }
                }
                else
                {
                    image.Save(stream, format);
                }

                stream.Position = 0;
                return stream;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error converting image to stream: {0}", ex.Message);
                stream.Dispose();
                return null;
            }
        }

        public static Image Resize(this Image image, Size box, bool preserveAspectRatio = true)
        {
            if (image == null) return null;

            int newWidth, newHeight;
            if (preserveAspectRatio)
            {
                float pw = box.Width / (float)image.Width;
                float ph = box.Height / (float)image.Height;
                float p = Math.Min(pw, ph);
                newWidth = Math.Max(1, (int)Math.Round(image.Width * p));
                newHeight = Math.Max(1, (int)Math.Round(image.Height * p));
            }
            else
            {
                newWidth = Math.Max(1, box.Width);
                newHeight = Math.Max(1, box.Height);
            }

            Image newImage = null;

            if (Utils.IsLinux)
            {
                lock (CoverImage_GdiLockAccessor.Lock)
                {
                    newImage = ResizeCore(image, newWidth, newHeight);
                }
            }
            else
            {
                newImage = ResizeCore(image, newWidth, newHeight);
            }

            return newImage;
        }

        private static Image ResizeCore(Image image, int newWidth, int newHeight)
        {
            try
            {
                var bmp = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.DrawImage(image, 0, 0, newWidth, newHeight);
                }
                return bmp;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error resizing image: {0}", ex.Message);
                return null;
            }
        }

        // Small accessor to reuse the same lock without making it public
        private static class CoverImage_GdiLockAccessor
        {
            public static object Lock => typeof(TinyOPDS.Data.CoverImage)
                .GetField("GdiLock", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);
        }
    }
}
