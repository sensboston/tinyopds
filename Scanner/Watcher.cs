using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using TinyOPDS.Data;

namespace TinyOPDS.Scanner
{
    public class Watcher
    {
        private FileSystemWatcher _fileWatcher;

        public event EventHandler OnLibraryChanged;

        public Watcher(string path = "")
        {
            _fileWatcher = new FileSystemWatcher(path, "*");
            _fileWatcher.Created += new FileSystemEventHandler(_fileWatcher_Created);
            _fileWatcher.Deleted += new FileSystemEventHandler(_fileWatcher_Deleted);
            _fileWatcher.Renamed += new RenamedEventHandler(_fileWatcher_Renamed);
            _fileWatcher.IncludeSubdirectories = true;
        }

        public string PathToWatch
        {
            get { return _fileWatcher.Path; }
            set { _fileWatcher.Path = value; }
        }

        public bool IsEnabled
        {
            get { return _fileWatcher.EnableRaisingEvents; }
            set { _fileWatcher.EnableRaisingEvents = value; }
        }

        /// <summary>
        /// New file (book or zip archive) added to the library
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _fileWatcher_Created(object sender, FileSystemEventArgs e)
        {
            FileScanner scanner = new FileScanner(false);
            scanner.OnBookFound += (object s, BookFoundEventArgs be) =>
                {
                    if (Library.Add(be.Book))
                    {
                        if (OnLibraryChanged != null) OnLibraryChanged(this, new EventArgs());
                    }
                };
            scanner.ScanFile(e.FullPath);

            Log.WriteLine("Directory scanner started");
        }

        /// <summary>
        /// Library file (book or zip archive) is renamed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _fileWatcher_Renamed(object sender, RenamedEventArgs e)
        {
        }

        /// <summary>
        /// Library file (book or zip archive) deleted from the library
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _fileWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
        }
    }
}
