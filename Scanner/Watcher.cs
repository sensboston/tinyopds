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

        public Watcher(string path)
        {
            _fileWatcher = new FileSystemWatcher(path, "*");
            _fileWatcher.Created += new FileSystemEventHandler(_fileWatcher_Created);
            _fileWatcher.Deleted += new FileSystemEventHandler(_fileWatcher_Deleted);
            _fileWatcher.Renamed += new RenamedEventHandler(_fileWatcher_Renamed);
        }

        public bool IsEnabled
        {
            get
            {
                return _fileWatcher.EnableRaisingEvents;
            }
            set
            {
                _fileWatcher.EnableRaisingEvents = value;
            }
        }

        /// <summary>
        /// New file (book or zip archive) added to the library
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _fileWatcher_Created(object sender, FileSystemEventArgs e)
        {
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
