/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the Genre class (book genre)
 *
 */

using System.Collections.Generic;

namespace TinyOPDS.Data
{
    public class Genre
    {
        public string Tag { get; set; }
        public string Name { get; set; }
        public string Translation { get; set; }
        public List<Genre> Subgenres = new List<Genre>();
    }
}
