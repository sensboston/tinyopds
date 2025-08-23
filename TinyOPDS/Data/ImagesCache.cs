/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * File-based image caching class with lazy write support
 *
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;

namespace TinyOPDS.Data
{
    public static class ImagesCache
    {
        private static readonly object _lockObject = new object();
        private static readonly Dictionary<string, CoverImage> _memoryCache;
        private static readonly ConcurrentQueue<SaveTask> _saveQueue;
        private static readonly Timer _saveTimer;
        private static volatile bool _isProcessing;

        private static readonly string _cacheBasePath;
        private static readonly string _coversPath;
        private static readonly string _thumbsPath;

        static ImagesCache()
        {
            _memoryCache = new Dictionary<string, CoverImage>();
            _saveQueue = new ConcurrentQueue<SaveTask>();

            // Cache paths relative to application directory
            _cacheBasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cached_pics");
            _coversPath = Path.Combine(_cacheBasePath, "covers");
            _thumbsPath = Path.Combine(_cacheBasePath, "thumbs");

            EnsureCacheDirectories();

            // Timer for lazy write operations (flush every 5 seconds)
            _saveTimer = new Timer(ProcessSaveQueue, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        private struct SaveTask
        {
            public string Id;
            public Image CoverImage;
            public Image ThumbnailImage;
        }

        /// <summary>
        /// Ensure cache directories exist
        /// </summary>
        private static void EnsureCacheDirectories()
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

        /// <summary>
        /// Add image to cache (immediate memory cache + lazy disk write)
        /// </summary>
        public static void Add(CoverImage image)
        {
            if (image?.ID == null || !image.HasImages) return;

            lock (_lockObject)
            {
                // Always add to memory cache if not in low memory mode
                if (!_memoryCache.ContainsKey(image.ID))
                {
                    // Remove oldest entry if cache is full
                    if (_memoryCache.Count >= 1000)
                    {
                        var firstKey = GetOldestCacheKey();
                        if (firstKey != null)
                            _memoryCache.Remove(firstKey);
                    }
                    _memoryCache[image.ID] = image;
                }

                // Queue for lazy disk save if files don't exist
                string coverPath = Path.Combine(_coversPath, image.ID + ".jpg");
                string thumbPath = Path.Combine(_thumbsPath, image.ID + ".jpg");

                if (!File.Exists(coverPath) || !File.Exists(thumbPath))
                {
                    var saveTask = new SaveTask
                    {
                        Id = image.ID,
                        CoverImage = CloneImage(image.CoverImageStream),
                        ThumbnailImage = CloneImage(image.ThumbnailImageStream)
                    };

                    _saveQueue.Enqueue(saveTask);
                }
            }
        }

        /// <summary>
        /// Check if image exists in cache (memory or disk)
        /// </summary>
        public static bool HasImage(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            // Check memory cache first
            lock (_lockObject)
            {
                if (_memoryCache.ContainsKey(id))
                    return true;
            }

            // Check disk cache
            string coverPath = Path.Combine(_coversPath, id + ".jpg");
            return File.Exists(coverPath);
        }

        /// <summary>
        /// Get image from cache (memory first, then disk)
        /// </summary>
        public static CoverImage GetImage(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            // Try memory cache first
            lock (_lockObject)
            {
                if (_memoryCache.ContainsKey(id))
                    return _memoryCache[id];
            }

            // Try loading from disk - create a wrapper that behaves like CoverImage
            return CreateCoverImageFromDisk(id);
        }

        /// <summary>
        /// Create a CoverImage-compatible object from disk cache
        /// </summary>
        private static CoverImage CreateCoverImageFromDisk(string id)
        {
            try
            {
                string coverPath = Path.Combine(_coversPath, id + ".jpg");

                if (!File.Exists(coverPath)) return null;

                // Load image from disk and create a temporary book object for CoverImage constructor
                var tempImage = Image.FromFile(coverPath);
                var dummyBook = new Book { ID = id };

                // Create a new CoverImage and manually set its internal image
                var coverImage = new CoverImageFromDisk(dummyBook, tempImage);

                // Add back to memory cache if not in low memory mode
                lock (_lockObject)
                {
                    if (!_memoryCache.ContainsKey(id))
                    {
                        if (_memoryCache.Count >= 1000)
                        {
                            var firstKey = GetOldestCacheKey();
                            if (firstKey != null)
                                _memoryCache.Remove(firstKey);
                        }
                        _memoryCache[id] = coverImage;
                    }
                }


                return coverImage;
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Failed to load image from disk cache for ID {0}: {1}", id, ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Process lazy save queue
        /// </summary>
        private static void ProcessSaveQueue(object state)
        {
            if (_isProcessing || _saveQueue.IsEmpty) return;

            _isProcessing = true;

            Task.Run(() =>
            {
                try
                {
                    var processed = 0;
                    while (_saveQueue.TryDequeue(out SaveTask task) && processed < 10) // Process max 10 per batch
                    {
                        SaveImageToDisk(task);
                        processed++;
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteLine(LogLevel.Error, "Error in lazy save queue processing: {0}", ex.Message);
                }
                finally
                {
                    _isProcessing = false;
                }
            });
        }

        /// <summary>
        /// Save single image to disk
        /// </summary>
        private static void SaveImageToDisk(SaveTask task)
        {
            try
            {
                string coverPath = Path.Combine(_coversPath, task.Id + ".jpg");
                string thumbPath = Path.Combine(_thumbsPath, task.Id + ".jpg");

                // Save cover image
                if (task.CoverImage != null && !File.Exists(coverPath))
                {
                    task.CoverImage.Save(coverPath, ImageFormat.Jpeg);
                }

                // Save thumbnail
                if (task.ThumbnailImage != null && !File.Exists(thumbPath))
                {
                    task.ThumbnailImage.Save(thumbPath, ImageFormat.Jpeg);
                }

                // Dispose temporary images
                task.CoverImage?.Dispose();
                task.ThumbnailImage?.Dispose();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Failed to save image to disk for ID {0}: {1}", task.Id, ex.Message);
            }
        }

        /// <summary>
        /// Clone image from stream to avoid disposal issues
        /// </summary>
        private static Image CloneImage(Stream imageStream)
        {
            if (imageStream == null) return null;

            try
            {
                imageStream.Position = 0;
                return Image.FromStream(imageStream);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get oldest cache key for removal
        /// </summary>
        private static string GetOldestCacheKey()
        {
            // Simple FIFO - return first key
            foreach (var key in _memoryCache.Keys)
                return key;
            return null;
        }

        /// <summary>
        /// Clear all cache (memory and disk)
        /// </summary>
        public static void ClearCache()
        {
            lock (_lockObject)
            {
                _memoryCache.Clear();

                // Clear save queue
                while (_saveQueue.TryDequeue(out _)) { }
            }

            // Clear disk cache
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
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public static CacheStats GetStats()
        {
            lock (_lockObject)
            {
                int diskCovers = 0, diskThumbs = 0;

                try
                {
                    if (Directory.Exists(_coversPath))
                        diskCovers = Directory.GetFiles(_coversPath, "*.jpg").Length;
                    if (Directory.Exists(_thumbsPath))
                        diskThumbs = Directory.GetFiles(_thumbsPath, "*.jpg").Length;
                }
                catch { }

                return new CacheStats
                {
                    MemoryCacheCount = _memoryCache.Count,
                    DiskCacheCovers = diskCovers,
                    DiskCacheThumbnails = diskThumbs,
                    PendingSaves = _saveQueue.Count
                };
            }
        }

        public struct CacheStats
        {
            public int MemoryCacheCount;
            public int DiskCacheCovers;
            public int DiskCacheThumbnails;
            public int PendingSaves;
        }
    }

    /// <summary>
    /// CoverImage that loads from pre-existing disk file
    /// </summary>
    internal class CoverImageFromDisk : CoverImage
    {
        public CoverImageFromDisk(Book book, Image loadedImage) : base(book)
        {
            // Use reflection to set the private _cover field
            var field = typeof(CoverImage).GetField("_cover", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(this, loadedImage);
        }
    }
}