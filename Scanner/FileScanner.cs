using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using System.Threading;

using TinyOPDS.Data;
using TinyOPDS.Parsers;

namespace TinyOPDS.Scanner
{
    public enum FileScannerStatus
    {
        SCANNING,
        STOPPED,
    }

    public class FileScanner
    {
        public int SkippedFiles { get; set; }
        public FileScannerStatus Status { get; set; }

        public event BookFoundEventHandler OnBookFound;
        private IEnumerable<BookFoundEventHandler> BookFoundEventHandlers() { return from d in OnBookFound.GetInvocationList() select (BookFoundEventHandler)d; }

        public event InvalidBookEventHandler OnInvalidBook;
        private IEnumerable<InvalidBookEventHandler> InvalidBookEventHandlers() { return from d in OnInvalidBook.GetInvocationList() select (InvalidBookEventHandler)d; }

        public event FileSkippedEventHandler OnFileSkipped;
        private IEnumerable<FileSkippedEventHandler> FileSkippedEventHandlers() { return from d in OnFileSkipped.GetInvocationList() select (FileSkippedEventHandler)d; }

        public event ScanCompletedEventHandler OnScanCompleted;
        private IEnumerable<ScanCompletedEventHandler> ScanCompletedEventHandlers() { return from d in OnScanCompleted.GetInvocationList() select (ScanCompletedEventHandler)d; }

        private bool _isRecursive;
        private List<ZipScanner> _zipScanners = new List<ZipScanner>();

        public FileScanner(bool IsRecursive = true)
        {
            Status = FileScannerStatus.STOPPED;
            _isRecursive = IsRecursive;
        }

        public void Stop()
        {
            Status = FileScannerStatus.STOPPED;

            if (OnBookFound != null) OnBookFound -= BookFoundEventHandlers().Last();
            if (OnInvalidBook != null) OnInvalidBook -= InvalidBookEventHandlers().Last();
            if (OnFileSkipped != null) OnFileSkipped -= FileSkippedEventHandlers().Last();
            if (OnScanCompleted != null) OnScanCompleted -= ScanCompletedEventHandlers().Last();

            for (int i = 0; i < _zipScanners.Count; i++)
            {
                if (_zipScanners[i] != null && _zipScanners[i].Status == FileScannerStatus.SCANNING) _zipScanners[i].Stop();
            }
            _zipScanners.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Path"></param>
        public void ScanDirectory(string Path)
        {
            SkippedFiles = 0;
            BackgroundWorker scanner = new BackgroundWorker();
            scanner.DoWork += (__, ___) => { ScanDirectory(new DirectoryInfo(Path)); };
            scanner.RunWorkerCompleted += (__, ___) =>
            {
                Status = FileScannerStatus.STOPPED;
                if (OnScanCompleted != null) OnScanCompleted(this, new EventArgs());
                scanner.Dispose();
            };
            Status = FileScannerStatus.SCANNING;
            scanner.RunWorkerAsync();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="directory"></param>
        /// <param name="IsRoot"></param>
        private void ScanDirectory(DirectoryInfo directory)
        {
            foreach (FileInfo file in directory.GetFiles())
            {
                if (Status != FileScannerStatus.SCANNING) break;
                ScanFile(directory.FullName + "\\" + file.Name);
            }

            // Recursively scan all subdirectories
            DirectoryInfo[] subDirectories = directory.GetDirectories();
            if (_isRecursive)
                foreach (DirectoryInfo subDirectory in subDirectories)
                    if (Status != FileScannerStatus.STOPPED)
                        ScanDirectory(subDirectory.FullName);

            // Wait for all zip scanners
            while (_zipScanners.Count > 0 && Status != FileScannerStatus.STOPPED) Thread.Sleep(100);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        public void ScanFile(string fullName)
        {
            Book book = null;
            string ext = Path.GetExtension(fullName).ToLower();

            // Process accepted files
            try
            {
                if (Library.Contains(fullName.Substring(Library.LibraryPath.Length + 1)))
                {
                    SkippedFiles++;
                    if (OnFileSkipped != null) OnFileSkipped(this, new FileSkippedEventArgs(SkippedFiles));
                }
                else if (ext.Contains(".epub"))
                {
                    book = new ePubParser().Parse(fullName);
                }
                else if (ext.Contains(".fb2"))
                {
                    book = new FB2Parser().Parse(fullName);
                }
                else if (ext.Contains(".zip"))
                {
                    ZipScanner zipScan = new ZipScanner(fullName);
                    zipScan.OnBookFound += ((object sender, BookFoundEventArgs e) => { if (e.Book.IsValid && OnBookFound != null) OnBookFound(sender, e); });
                    zipScan.OnInvalidBook += (object sender, InvalidBookEventArgs e) => { if (OnInvalidBook != null) OnInvalidBook(sender, e); };
                    zipScan.OnFileSkipped += (object sender, FileSkippedEventArgs e) =>
                    {
                        SkippedFiles++;
                        if (OnFileSkipped != null) OnFileSkipped(sender, new FileSkippedEventArgs(SkippedFiles));
                        book = null;
                    };
                    zipScan.OnScanCompleted += (object sender, EventArgs e) =>
                    {
                        if (_zipScanners.Contains((sender as ZipScanner)))
                        {
                            _zipScanners.Remove((sender as ZipScanner));
                            sender = null;
                        }
                        if (_zipScanners.Count > 0) _zipScanners.First().Scan();
                        else if (Status == FileScannerStatus.STOPPED && (OnScanCompleted != null)) OnScanCompleted(this, e);
                    };
                    if (_zipScanners.Count == 0) zipScan.Scan();
                    _zipScanners.Add(zipScan);
                }

                // Inform caller
                if (book != null)
                {
                    if (book.IsValid && OnBookFound != null) OnBookFound(this, new BookFoundEventArgs(book));
                    else if (!book.IsValid && OnInvalidBook != null) OnInvalidBook(this, new InvalidBookEventArgs(fullName));
                }

            }
            catch (Exception e)
            {
                Log.WriteLine(".ScanFile: exception {0} on file: {1}", e.Message, fullName);
                if (OnInvalidBook != null) OnInvalidBook(this, new InvalidBookEventArgs(fullName));
            }
        }
    }
}
