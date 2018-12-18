/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * Events and handlers classes for scanners
 * 
 ************************************************************/

using System;
using TinyOPDS.Data;

namespace TinyOPDS.Scanner
{
    /// <summary>
    /// Scanner delegated events and arguments declarations
    /// </summary>

    public class BookFoundEventArgs : EventArgs
    {
        public Book Book;
        public BookFoundEventArgs(Book book) { Book = book; }
    }

    public class InvalidBookEventArgs : EventArgs
    {
        public string BookName;
        public InvalidBookEventArgs(string bookName) { BookName = bookName; }
    }

    public class FileSkippedEventArgs : EventArgs
    {
        public int Count;
        public FileSkippedEventArgs(int count) { Count = count; }
    }

    public class BookAddedEventArgs : EventArgs
    {
        public string BookPath;
        public BookType BookType;
        public BookAddedEventArgs(string bookPath) 
        { 
            BookPath = bookPath;
            BookType = BookPath.ToLower().Contains(".epub") ? BookType.EPUB : BookType.FB2;
        }
    }

    public class BookDeletedEventArgs : BookAddedEventArgs 
    {
        public BookDeletedEventArgs(string bookPath) : base(bookPath) {}
    }

    public delegate void BookFoundEventHandler(object sender, BookFoundEventArgs e);
    public delegate void InvalidBookEventHandler(object sender, InvalidBookEventArgs e);
    public delegate void FileSkippedEventHandler(object sender, FileSkippedEventArgs e);
    public delegate void ScanCompletedEventHandler(object sender, EventArgs e);
    public delegate void BookAddedEventHandler(object sender, BookAddedEventArgs e);
    public delegate void BookDeletedEventHandler(object sender, BookDeletedEventArgs e);
}
