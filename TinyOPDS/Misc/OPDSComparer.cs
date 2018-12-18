/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines the custom comparer class
 * 
 * TODO: should sort down some rare used chars
 * 
 ************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using TinyOPDS.Data;

namespace TinyOPDS
{
    public class OPDSComparer : IComparer<object>
    {
        private bool _cyrillcFirst;

        public OPDSComparer(bool cyrillicFirst = true)
        {
            _cyrillcFirst = cyrillicFirst;
        }

        public int Compare(object x, object y)
        {
            string x1 = string.Empty, y1 = string.Empty;
            if (x is string)
            {
                x1 = x as string;
                y1 = y as string;
            }
            else if (x is Genre)
            {
                x1 = _cyrillcFirst ? (x as Genre).Translation : (x as Genre).Name;
                y1 = _cyrillcFirst ? (y as Genre).Translation : (y as Genre).Name;
            }
            // Shift "garbage" characters and digits to the end
            if (x1.Length > 0 && y1.Length > 0)
            {
                if (char.IsLetter(x1[0]) && !char.IsLetter(y1[0])) return -1;
                else if (!char.IsLetter(x1[0]) && char.IsLetter(y1[0])) return 1;
                else if (char.IsLetterOrDigit(x1[0]) && !char.IsLetterOrDigit(y1[0])) return -1;
                else if (!char.IsLetterOrDigit(x1[0]) && char.IsLetterOrDigit(y1[0])) return 1;
            }
            if (_cyrillcFirst && x1.Length > 0 && y1.Length > 0)
            {
                // Cyrillic letter came first
                if (x1[0] > 400 && y1[0] < 400) return -1;
                if (x1[0] < 400 && y1[0] > 400) return 1;
            }
            return string.Compare(x1, y1, true);
        }
    }

}
