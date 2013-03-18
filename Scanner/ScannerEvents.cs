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

    public delegate void BookFoundEventHandler(object sender, BookFoundEventArgs e);
    public delegate void InvalidBookEventHandler(object sender, InvalidBookEventArgs e);
    public delegate void FileSkippedEventHandler(object sender, FileSkippedEventArgs e);
    public delegate void ScanCompletedEventHandler(object sender, EventArgs e);
}
