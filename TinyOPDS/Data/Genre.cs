/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines the Genre class (book genre)
 * 
 ************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
