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
        private static readonly object _lockObject = new object();

        // RAM cache for covers (when CacheImagesInMemory = true)
        private static readonly Dictionary<string, CoverImageData> _ramCoversCache = new Dictionary<string, CoverImageData>();
        private static readonly Queue<string> _coversAccessOrder = new Queue<string>(); // FIFO for covers

        // RAM cache for thumbnails (always enabled)
        private static readonly Dictionary<string, byte[]> _thumbnailsCache = new Dictionary<string, byte[]>();

        // Disk cache paths
        private static readonly string _cacheBasePath;
        private static readonly string _coversPath;
        private static readonly string _thumbsPath;

        // Current RAM usage tracking
        private static long _currentRamUsageBytes = 0;

        static ImagesCache()
        {
            // Cache paths relative to application directory
            _cacheBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cached_pics");
            _coversPath = Path.Combine(_cacheBasePath, "covers");
            _thumbsPath = Path.Combine(_cacheBasePath, "thumbs");

            EnsureCacheDirectories();
        }

        private struct CoverImageData
        {
            public byte[] ImageData;
            public int Width;
            public int Height;
            public long SizeBytes;
        }

        /// <summary>
        /// Ensure cache directories exist (only if disk caching is enabled)
        /// </summary>
        private static void EnsureCacheDirectories()
        {
            if (!TinyOPDS.Properties.Settings.Default.CacheImagesInMemory)
            {
                try
                {
                    if (!Directory.Exists(_cacheBasePath))
                        Directory.CreateDirectory(_cacheBasePath);

                    if (!Directory.Exists(_coversPath))
                        Directory.CreateDirectory(_coversPath);

                    if (!Directory.Exists(_thumbsPath))
                        Directory.CreateDirectory(_thumbsPath);
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Failed to create cache directories: {0}", ex.Message);
                }
            }
        }

        /// <summary>
        /// Add image to cache (RAM or disk based on settings)
        /// </summary>
        public static void Add(CoverImage image)
        {
            if (image?.ID == null || !image.HasImages) return;

            lock (_lockObject)
            {
                Log.WriteLine(LogLevel.Info, "Adding image to cache: {0} (RAM mode: {1})",
                    image.ID, Properties.Settings.Default.CacheImagesInMemory);

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

        /// <summary>
        /// Cache thumbnail in RAM (always enabled)
        /// </summary>
        private static void CacheThumbnailInRam(CoverImage image)
        {
            try
            {
                if (image.ThumbnailImageStream != null && !_thumbnailsCache.ContainsKey(image.ID))
                {
                    image.ThumbnailImageStream.Position = 0;
                    byte[] thumbnailData = new byte[image.ThumbnailImageStream.Length];
                    image.ThumbnailImageStream.Read(thumbnailData, 0, thumbnailData.Length);
                    _thumbnailsCache[image.ID] = thumbnailData;
                    Log.WriteLine(LogLevel.Info, "Cached thumbnail for {0} in RAM ({1} bytes)",
                        image.ID, thumbnailData.Length);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Failed to cache thumbnail for {0}: {1}", image.ID, ex.Message);
            }
        }

        /// <summary>
        /// Cache cover image in RAM with size limit
        /// </summary>
        private static void CacheCoverInRam(CoverImage image)
        {
            try
            {
                if (image.CoverImageStream != null && !_ramCoversCache.ContainsKey(image.ID))
                {
                    image.CoverImageStream.Position = 0;
                    byte[] coverData = new byte[image.CoverImageStream.Length];
                    image.CoverImageStream.Read(coverData, 0, coverData.Length);

                    var coverImageData = new CoverImageData
                    {
                        ImageData = coverData,
                        Width = 0, // We don't need dimensions for this implementation
                        Height = 0,
                        SizeBytes = coverData.Length
                    };

                    // Check RAM limit
                    long maxBytes = Properties.Settings.Default.MaxRAMImageCacheSizeMB * 1024L * 1024L;
                    long neededSpace = coverImageData.SizeBytes;

                    // Remove oldest covers if needed (FIFO)
                    while (_currentRamUsageBytes + neededSpace > maxBytes && _coversAccessOrder.Count > 0)
                    {
                        string oldestId = _coversAccessOrder.Dequeue();
                        if (_ramCoversCache.TryGetValue(oldestId, out var oldCover))
                        {
                            _currentRamUsageBytes -= oldCover.SizeBytes;
                            _ramCoversCache.Remove(oldestId);
                        }
                    }

                    // Add new cover
                    _ramCoversCache[image.ID] = coverImageData;
                    _coversAccessOrder.Enqueue(image.ID);
                    _currentRamUsageBytes += coverImageData.SizeBytes;

                    Log.WriteLine(LogLevel.Info, "Cached cover for {0} in RAM ({1} KB, total: {2} MB)",
                        image.ID, coverImageData.SizeBytes / 1024, _currentRamUsageBytes / (1024 * 1024));
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Warning, "Failed to cache cover in RAM for {0}: {1}", image.ID, ex.Message);
            }
        }

        /// <summary>
        /// Cache cover image on disk
        /// </summary>
        private static void CacheCoverOnDisk(CoverImage image)
        {
            try
            {
                string coverPath = Path.Combine(_coversPath, image.ID + ".jpg");
                string thumbPath = Path.Combine(_thumbsPath, image.ID + ".jpg");

                // Save cover to disk if not exists
                if (image.CoverImageStream != null && !File.Exists(coverPath))
                {
                    using (var diskImage = Image.FromStream(image.CoverImageStream))
                    {
                        diskImage.Save(coverPath, ImageFormat.Jpeg);
                    }
                }

                // Save thumbnail to disk if not exists
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

        /// <summary>
        /// Check if image exists in cache
        /// </summary>
        public static bool HasImage(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            lock (_lockObject)
            {
                Log.WriteLine(LogLevel.Info, "Checking cache for image: {0}", id);

                // Check thumbnails (always in RAM)
                bool hasThumbnail = _thumbnailsCache.ContainsKey(id);

                // Check covers based on cache mode
                bool hasCover = false;
                if (Properties.Settings.Default.CacheImagesInMemory)
                {
                    hasCover = _ramCoversCache.ContainsKey(id);
                }
                else
                {
                    string coverPath = Path.Combine(_coversPath, id + ".jpg");
                    hasCover = File.Exists(coverPath);
                }

                bool hasAny = hasThumbnail || hasCover;
                Log.WriteLine(LogLevel.Info, "Cache check for {0}: thumbnail={1}, cover={2}, result={3}",
                    id, hasThumbnail, hasCover, hasAny);

                return hasAny;
            }
        }

        /// <summary>
        /// Get image from cache - FIXED: работает с byte[] данными и создает новые MemoryStream
        /// Возвращает CachedCoverImage вместо CoverImage чтобы избежать проблем с наследованием
        /// </summary>
        public static CachedCoverImage GetImage(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            lock (_lockObject)
            {
                try
                {
                    Log.WriteLine(LogLevel.Info, "Getting cached image: {0}", id);

                    // Get data from caches
                    byte[] coverData = null;
                    byte[] thumbnailData = null;

                    // Get thumbnail data (always from RAM)
                    if (_thumbnailsCache.TryGetValue(id, out thumbnailData))
                    {
                        Log.WriteLine(LogLevel.Info, "Found thumbnail in cache for {0}: {1} bytes",
                            id, thumbnailData.Length);
                    }

                    // Get cover data based on cache mode
                    if (Properties.Settings.Default.CacheImagesInMemory)
                    {
                        // Get cover from RAM
                        if (_ramCoversCache.TryGetValue(id, out var coverImageData))
                        {
                            coverData = coverImageData.ImageData;
                            Log.WriteLine(LogLevel.Info, "Found cover in RAM cache for {0}: {1} bytes",
                                id, coverData.Length);
                        }
                    }
                    else
                    {
                        // Get cover from disk
                        string coverPath = Path.Combine(_coversPath, id + ".jpg");
                        if (File.Exists(coverPath))
                        {
                            coverData = File.ReadAllBytes(coverPath);
                            Log.WriteLine(LogLevel.Info, "Found cover on disk for {0}: {1} bytes",
                                id, coverData.Length);
                        }

                        // Get thumbnail from disk if not in RAM
                        if (thumbnailData == null)
                        {
                            string thumbPath = Path.Combine(_thumbsPath, id + ".jpg");
                            if (File.Exists(thumbPath))
                            {
                                thumbnailData = File.ReadAllBytes(thumbPath);
                                Log.WriteLine(LogLevel.Info, "Found thumbnail on disk for {0}: {1} bytes",
                                    id, thumbnailData.Length);
                            }
                        }
                    }

                    // Create cached cover image if we have any data
                    if (coverData != null || thumbnailData != null)
                    {
                        var result = new CachedCoverImage(id, coverData, thumbnailData);
                        Log.WriteLine(LogLevel.Info, "Created CachedCoverImage for {0}, HasImages: {1}",
                            id, result.HasImages);
                        return result;
                    }
                    else
                    {
                        Log.WriteLine(LogLevel.Info, "No cached data found for {0}", id);
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Failed to get cached image for {0}: {1}", id, ex.Message);
                }
            }

            return null;
        }

        /// <summary>
        /// Clear all caches
        /// </summary>
        public static void ClearCache()
        {
            lock (_lockObject)
            {
                // Clear RAM caches
                _ramCoversCache.Clear();
                _thumbnailsCache.Clear();
                _coversAccessOrder.Clear();
                _currentRamUsageBytes = 0;

                // Clear disk cache if exists
                try
                {
                    if (Directory.Exists(_coversPath))
                    {
                        foreach (var file in Directory.GetFiles(_coversPath, "*.jpg"))
                            File.Delete(file);
                    }

                    if (Directory.Exists(_thumbsPath))
                    {
                        foreach (var file in Directory.GetFiles(_thumbsPath, "*.jpg"))
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

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public static CacheStats GetStats()
        {
            lock (_lockObject)
            {
                var stats = new CacheStats
                {
                    ThumbnailsCacheCount = _thumbnailsCache.Count,
                    CacheMode = Properties.Settings.Default.CacheImagesInMemory ? "RAM" : "Disk"
                };

                if (Properties.Settings.Default.CacheImagesInMemory)
                {
                    stats.CoversCacheCount = _ramCoversCache.Count;
                    stats.RamUsageMB = _currentRamUsageBytes / (1024.0 * 1024.0);
                    stats.RamLimitMB = TinyOPDS.Properties.Settings.Default.MaxRAMImageCacheSizeMB;
                }
                else
                {
                    try
                    {
                        if (Directory.Exists(_coversPath))
                            stats.CoversCacheCount = Directory.GetFiles(_coversPath, "*.jpg").Length;
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
    /// CoverImage implementation for cached images - FIXED: does NOT inherit from CoverImage!
    /// Simple class without the problematic base class constructor
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
                // КРИТИЧНО: Всегда создаем НОВЫЙ MemoryStream с Position = 0!
                if (_coverData != null)
                {
                    var stream = new MemoryStream(_coverData);
                    Log.WriteLine(LogLevel.Info, "Created new CoverImageStream for {0}: {1} bytes, Position: {2}",
                        _id, stream.Length, stream.Position);
                    return stream;
                }
                return null;
            }
        }

        public Stream ThumbnailImageStream
        {
            get
            {
                // КРИТИЧНО: Всегда создаем НОВЫЙ MemoryStream с Position = 0!
                if (_thumbnailData != null)
                {
                    var stream = new MemoryStream(_thumbnailData);
                    Log.WriteLine(LogLevel.Info, "Created new ThumbnailImageStream for {0}: {1} bytes, Position: {2}",
                        _id, stream.Length, stream.Position);
                    return stream;
                }
                return null;
            }
        }

        public bool HasImages => _coverData != null || _thumbnailData != null;
    }
}