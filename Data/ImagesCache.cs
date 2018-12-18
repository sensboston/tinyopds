/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * Simple image caching class
 * 
 * TODO: add disk caching
 * 
 ************************************************************/

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Threading;
using System.Drawing;

namespace TinyOPDS.Data
{
    public static class ImagesCache
    {
        private static Dictionary<string, CoverImage> _cache;

        static ImagesCache()
        {
            _cache = new Dictionary<string, CoverImage>();
        }

        public static void Add(CoverImage image)
        {
            if (!_cache.ContainsKey(image.ID))
            {
                if (_cache.Count >= 1000) _cache.Remove(_cache.First().Key);
                _cache[image.ID] = image;
            }
        }

        public static bool HasImage(string id) { return _cache.ContainsKey(id); }

        public static CoverImage GetImage(string id) { return _cache[id];  }
    }
}
