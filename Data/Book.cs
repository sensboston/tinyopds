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
        public Book(string filePath = "") 
        {
            Version = 1;
            FilePath = filePath;
            Title = Sequence = Annotation = Language = "";
            HasCover = false;
            BookDate = DocumentDate = DateTime.MinValue;
            NumberInSequence = 0;
            Authors = new List<string>();
            Translators = new List<string>();
            Genres = new List<string>();
        }
        private string id = "";
        public string ID 
        {
            get {  return id; }
            set
            {
                // Book ID always must be in GUID form
                Guid guid;
                if (Guid.TryParse(value, out guid)) id = value; else id = Utils.Create(Utils.IsoOidNamespace, value).ToString();
            }
        }
        public float Version { get; set; }
        public string FilePath { get; set; }
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
    }
}
