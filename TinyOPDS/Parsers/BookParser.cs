/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Simple implementation of UPnP controller. Works fine with 
 * some D-Link and NetGear router models (need more tests)
 * 
 * Base class for book parsers
 *
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

using TinyOPDS.Data;

namespace TinyOPDS.Parsers
{
    public abstract class BookParser
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public abstract Book Parse(Stream stream, string fileName);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public Book Parse(string fileName)
        {
            using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                return Parse(stream, fileName);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public abstract Image GetCoverImage(Stream stream, string fileName);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public Image GetCoverImage(string fileName)
        {
            using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                return GetCoverImage(stream, fileName);
        }

    }
}
