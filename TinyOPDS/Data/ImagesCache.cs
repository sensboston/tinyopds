/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Enhanced image caching class with configurable RAM/Disk storage
 *
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace TinyOPDS.Data
{
    public static class ImagesCache
    {
        private static readonly object lockObject = new object();

        // RAM cache for covers (when CacheImagesInMemory = true)
        private static readonly Dictionary<string, CoverImageData> ramCoversCache = new Dictionary<string, CoverImageData>();
        private static readonly Queue<string> coversAccessOrder = new Queue<string>(); // FIFO for covers

        // RAM cache for thumbnails (always enabled)
        private static readonly Dictionary<string, byte[]> thumbnailsCache = new Dictionary<string, byte[]>();

        // Disk cache paths
        private static readonly string cacheBasePath;
        private static readonly string coversPath;
        private static readonly string thumbsPath;

        // Current RAM usage tracking
        private static long currentRamUsageBytes = 0;

        static ImagesCache()
        {
            cacheBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cached_pics");
            coversPath = Path.Combine(cacheBasePath, "covers");
            thumbsPath = Path.Combine(cacheBasePath, "thumbs");

            EnsureCacheDirectories();
        }

        private struct CoverImageData
        {
            public byte[] ImageData;
            public int Width;
            public int Height;
            public long SizeBytes;
        }

        private static void EnsureCacheDirectories()
        {
            if (!TinyOPDS.Properties.Settings.Default.CacheImagesInMemory)
            {
                try
                {
                    if (!Directory.Exists(cacheBasePath))
                        Directory.CreateDirectory(cacheBasePath);

                    if (!Directory.Exists(coversPath))
                        Directory.CreateDirectory(coversPath);

                    if (!Directory.Exists(thumbsPath))
                        Directory.CreateDirectory(thumbsPath);
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Failed to create cache directories: {0}", ex.Message);
                }
            }
        }

        public static void Add(CoverImage image)
        {
            if (image?.ID == null || !image.HasImages) return;

            lock (lockObject)
            {
                // Always cache thumbnails in RAM (they're tiny)
                CacheThumbnailInRam(image);

                // Cache covers based on settings
                if (Properties.Settings.Default.CacheImagesInMemory)
                {
                    CacheCoverInRam(image);
                }
                else
                {
                    CacheCoverOnDisk(image);
                }
            }
        }

        private static void CacheThumbnailInRam(CoverImage image)
        {
            try
            {
                if (image.ThumbnailImageStream != null && !thumbnailsCache.ContainsKey(image.ID))
                {
                    image.ThumbnailImageStream.Position = 0;
                    byte[] thumbnailData = new byte[image.ThumbnailImageStream.Length];
                    image.ThumbnailImageStream.Read(thumbnailData, 0, thumbnailData.Length);
                    thumbnailsCache[image.ID] = thumbnailData;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Failed to cache thumbnail for {0}: {1}", image.ID, ex.Message);
            }
        }

        private static void CacheCoverInRam(CoverImage image)
        {
            try
            {
                if (image.CoverImageStream != null && !ramCoversCache.ContainsKey(image.ID))
                {
                    image.CoverImageStream.Position = 0;
                    byte[] coverData = new byte[image.CoverImageStream.Length];
                    image.CoverImageStream.Read(coverData, 0, coverData.Length);

                    var coverImageData = new CoverImageData
                    {
                        ImageData = coverData,
                        Width = 0,
                        Height = 0,
                        SizeBytes = coverData.Length
                    };

                    // Check RAM limit
                    long maxBytes = Properties.Settings.Default.MaxRAMImageCacheSizeMB * 1024L * 1024L;
                    long neededSpace = coverImageData.SizeBytes;

                    // Remove oldest covers if needed (FIFO)
                    while (currentRamUsageBytes + neededSpace > maxBytes && coversAccessOrder.Count > 0)
                    {
                        string oldestId = coversAccessOrder.Dequeue();
                        if (ramCoversCache.TryGetValue(oldestId, out var oldCover))
                        {
                            currentRamUsageBytes -= oldCover.SizeBytes;
                            ramCoversCache.Remove(oldestId);
                        }
                    }

                    // Add new cover
                    ramCoversCache[image.ID] = coverImageData;
                    coversAccessOrder.Enqueue(image.ID);
                    currentRamUsageBytes += coverImageData.SizeBytes;
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Failed to cache cover in RAM for {0}: {1}", image.ID, ex.Message);
            }
        }

        private static void CacheCoverOnDisk(CoverImage image)
        {
            try
            {
                string coverPath = Path.Combine(coversPath, image.ID + ".jpg");
                string thumbPath = Path.Combine(thumbsPath, image.ID + ".jpg");

                if (image.CoverImageStream != null && !File.Exists(coverPath))
                {
                    using (var diskImage = Image.FromStream(image.CoverImageStream))
                    {
                        diskImage.Save(coverPath, ImageFormat.Jpeg);
                    }
                }

                if (image.ThumbnailImageStream != null && !File.Exists(thumbPath))
                {
                    using (var diskThumb = Image.FromStream(image.ThumbnailImageStream))
                    {
                        diskThumb.Save(thumbPath, ImageFormat.Jpeg);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Failed to cache images on disk for {0}: {1}", image.ID, ex.Message);
            }
        }

        public static bool HasImage(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            lock (lockObject)
            {
                // Check thumbnails (always in RAM)
                bool hasThumbnail = thumbnailsCache.ContainsKey(id);

                // Check covers based on cache mode
                bool hasCover = false;
                if (Properties.Settings.Default.CacheImagesInMemory)
                {
                    hasCover = ramCoversCache.ContainsKey(id);
                }
                else
                {
                    string coverPath = Path.Combine(coversPath, id + ".jpg");
                    hasCover = File.Exists(coverPath);
                }

                return hasThumbnail || hasCover;
            }
        }

        public static CachedCoverImage GetImage(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            lock (lockObject)
            {
                try
                {
                    byte[] coverData = null;
                    byte[] thumbnailData = null;

                    // Get thumbnail data (always from RAM)
                    thumbnailsCache.TryGetValue(id, out thumbnailData);

                    // Get cover data based on cache mode
                    if (Properties.Settings.Default.CacheImagesInMemory)
                    {
                        if (ramCoversCache.TryGetValue(id, out var coverImageData))
                        {
                            coverData = coverImageData.ImageData;
                        }
                    }
                    else
                    {
                        string coverPath = Path.Combine(coversPath, id + ".jpg");
                        if (File.Exists(coverPath))
                        {
                            coverData = File.ReadAllBytes(coverPath);
                        }

                        if (thumbnailData == null)
                        {
                            string thumbPath = Path.Combine(thumbsPath, id + ".jpg");
                            if (File.Exists(thumbPath))
                            {
                                thumbnailData = File.ReadAllBytes(thumbPath);
                            }
                        }
                    }

                    if (coverData != null || thumbnailData != null)
                    {
                        return new CachedCoverImage(id, coverData, thumbnailData);
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Failed to get cached image for {0}: {1}", id, ex.Message);
                }
            }

            return null;
        }

        public static void ClearCache()
        {
            lock (lockObject)
            {
                ramCoversCache.Clear();
                thumbnailsCache.Clear();
                coversAccessOrder.Clear();
                currentRamUsageBytes = 0;

                try
                {
                    if (Directory.Exists(coversPath))
                    {
                        foreach (var file in Directory.GetFiles(coversPath, "*.jpg"))
                            File.Delete(file);
                    }

                    if (Directory.Exists(thumbsPath))
                    {
                        foreach (var file in Directory.GetFiles(thumbsPath, "*.jpg"))
                            File.Delete(file);
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Failed to clear disk cache: {0}", ex.Message);
                }

                Log.WriteLine("Image cache cleared");
            }
        }

        public static CacheStats GetStats()
        {
            lock (lockObject)
            {
                var stats = new CacheStats
                {
                    ThumbnailsCacheCount = thumbnailsCache.Count,
                    CacheMode = Properties.Settings.Default.CacheImagesInMemory ? "RAM" : "Disk"
                };

                if (Properties.Settings.Default.CacheImagesInMemory)
                {
                    stats.CoversCacheCount = ramCoversCache.Count;
                    stats.RamUsageMB = currentRamUsageBytes / (1024.0 * 1024.0);
                    stats.RamLimitMB = TinyOPDS.Properties.Settings.Default.MaxRAMImageCacheSizeMB;
                }
                else
                {
                    try
                    {
                        if (Directory.Exists(coversPath))
                            stats.CoversCacheCount = Directory.GetFiles(coversPath, "*.jpg").Length;
                    }
                    catch { }
                }

                return stats;
            }
        }

        public struct CacheStats
        {
            public int CoversCacheCount;
            public int ThumbnailsCacheCount;
            public string CacheMode;
            public double RamUsageMB;
            public int RamLimitMB;
        }
    }

    /// <summary>
    /// CoverImage implementation for cached images
    /// </summary>
    public class CachedCoverImage
    {
        private readonly byte[] _coverData;
        private readonly byte[] _thumbnailData;
        private readonly string _id;

        public CachedCoverImage(string id, byte[] coverData, byte[] thumbnailData)
        {
            _id = id;
            _coverData = coverData;
            _thumbnailData = thumbnailData;
        }

        public string ID => _id;

        public Stream CoverImageStream
        {
            get
            {
                // Always create NEW MemoryStream from cached data
                return _coverData != null ? new MemoryStream(_coverData) : null;
            }
        }

        public Stream ThumbnailImageStream
        {
            get
            {
                // Always create NEW MemoryStream from cached data
                return _thumbnailData != null ? new MemoryStream(_thumbnailData) : null;
            }
        }

        public bool HasImages => _coverData != null || _thumbnailData != null;
    }
}