/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines the Book class
 * 
 ************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Drawing;

namespace TinyOPDS.Data
{
    /// <summary>
    /// Supported book types
    /// </summary>
    public enum BookType
    {
        FB2,
        EPUB,
    }

    /// <summary>
    /// Base data class
    /// </summary>
    public class Book
    {
        public Book(string fileName = "") 
        {
            Version = 1;
            FileName = fileName;
            if (!string.IsNullOrEmpty(FileName) && FileName.IndexOf(Library.LibraryPath)==0)
            {
                FileName = FileName.Substring(Library.LibraryPath.Length+1);
            }
            Title = Sequence = Annotation = Language = string.Empty;
            HasCover = false;
            BookDate = DocumentDate = DateTime.MinValue;
            NumberInSequence = 0;
            Authors = new List<string>();
            Translators = new List<string>();
            Genres = new List<string>();
        }
        private string _id = string.Empty;
        public string ID 
        {
            get {  return _id; }
            set
            {
                // Book ID always must be in GUID form
                Guid guid;
                if (!string.IsNullOrEmpty(value) && Guid.TryParse(value, out guid)) _id = value; else _id = Utils.CreateGuid(Utils.IsoOidNamespace, FileName).ToString();
            }
        }
        public float Version { get; set; }
        public string FileName { get; private set; }
        public string FilePath { get { return Path.Combine(Library.LibraryPath, FileName); } }
        public string Title { get; set; }
        public string Language { get; set; }
        public bool HasCover { get; set; }
        public DateTime BookDate { get; set; }
        public DateTime DocumentDate { get; set; }
        public string Sequence { get; set; }
        public UInt32 NumberInSequence { get; set; }
        public string Annotation { get; set; }
        public UInt32 DocumentSize { get; set; }
        public List<string> Authors { get; set; }
        public List<string> Translators { get; set; }
        public List<string> Genres { get; set; }
        public BookType BookType { get { return Path.GetExtension(FilePath).ToLower().Contains("epub") ? BookType.EPUB : Data.BookType.FB2; } }
        public bool IsValid { get { return (!string.IsNullOrEmpty(Title) && Title.IsValidUTF() && Authors.Count > 0 && Genres.Count > 0); } }
        public DateTime AddedDate { get; set; }
    }
}
