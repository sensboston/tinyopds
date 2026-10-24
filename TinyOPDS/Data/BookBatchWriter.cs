/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Saves the books found by the scanner in batches
 *
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TinyOPDS.Data
{
    /// <summary>
    /// Collects the books found by the scanner and saves them to the library in batches of BatchSize books.
    /// Batches are saved one by one in the order they were collected, the scanner doesn't wait for them.
    /// Also counts the found books and duplicates: a found book is counted when added, the ones not
    /// saved (duplicates) are moved from found to duplicates when their batch is saved
    /// </summary>
    public class BookBatchWriter
    {
        private readonly object sync = new object();
        private readonly List<Book> pendingBooks = new List<Book>();
        private Task savingTask = Task.CompletedTask;
        private int fb2Found, epubFound, duplicates, flushing;
        private bool changed;

        public BookBatchWriter(int batchSize)
        {
            BatchSize = Math.Max(1, batchSize);
        }

        public int BatchSize { get; }

        public int FB2Found { get { lock (sync) return fb2Found; } }
        public int EPUBFound { get { lock (sync) return epubFound; } }
        public int Duplicates { get { lock (sync) return duplicates; } }

        /// <summary>
        /// Found books and duplicates counted at once
        /// </summary>
        public (int FB2, int EPUB, int Duplicates) GetCounters()
        {
            lock (sync) return (fb2Found, epubFound, duplicates);
        }

        /// <summary>
        /// True while FlushAsync waits for the books to be saved
        /// </summary>
        public bool IsFlushing => Volatile.Read(ref flushing) > 0;

        /// <summary>
        /// Adds the book to the batch, a full batch is queued for saving
        /// </summary>
        /// <returns>Number of books added so far</returns>
        public int Add(Book book)
        {
            lock (sync)
            {
                CountFound(book.BookType);
                pendingBooks.Add(book);
                if (pendingBooks.Count >= BatchSize) QueuePendingBooks();
                return fb2Found + epubFound + duplicates;
            }
        }

        /// <summary>
        /// Counts the books saved by another writer (the folder watcher)
        /// </summary>
        public void CountFound(BookType bookType, int count = 1)
        {
            lock (sync)
            {
                if (bookType == BookType.FB2) fb2Found += count; else epubFound += count;
            }
        }

        /// <summary>
        /// Saves the books not saved yet, completes when all the batches are saved
        /// and the library statistics are refreshed
        /// </summary>
        public async Task FlushAsync()
        {
            Task saving;
            lock (sync)
            {
                if (pendingBooks.Count > 0)
                    Log.WriteLine("Flushing {0} remaining books at scan completion", pendingBooks.Count);
                saving = QueuePendingBooks();
            }

            Interlocked.Increment(ref flushing);
            try
            {
                await saving;

                // Batches refresh books counts only, the rest of the statistics is refreshed once here
                bool refresh;
                lock (sync)
                {
                    refresh = changed;
                    changed = false;
                }
                if (refresh) await Task.Run(() => Library.RefreshStatistics());
            }
            finally
            {
                Interlocked.Decrement(ref flushing);
            }
        }

        // The batch is taken right away: taken when the previous batches are saved,
        // the books found meanwhile would be split into many small batches. Called under sync
        private Task QueuePendingBooks()
        {
            if (pendingBooks.Count > 0)
            {
                var books = new List<Book>(pendingBooks);
                pendingBooks.Clear();
                savingTask = SaveAfterAsync(savingTask, books);
            }
            return savingTask;
        }

        private async Task SaveAfterAsync(Task previous, List<Book> books)
        {
            await previous;
            await SaveAsync(books);
        }

        private async Task SaveAsync(List<Book> books)
        {
            try
            {
                var batchResult = await Library.AddBatchAsync(books);

                // A saved book that replaced a book already in the database doesn't add a book either
                int fb2NotAdded = books.Count(b => b.BookType == BookType.FB2) - batchResult.FB2Count + batchResult.FB2Replaced;
                int epubNotAdded = books.Count(b => b.BookType != BookType.FB2) - batchResult.EPUBCount + batchResult.EPUBReplaced;

                lock (sync)
                {
                    duplicates += fb2NotAdded + epubNotAdded;
                    fb2Found -= fb2NotAdded;
                    epubFound -= epubNotAdded;
                    if (batchResult.Added > 0 || batchResult.Replaced > 0) changed = true;
                }

                if (batchResult.Duplicates > 0 || batchResult.Errors > 0)
                {
                    Log.WriteLine("Batch flush completed: {0} processed, {1} added, {2} duplicates, {3} errors",
                        batchResult.TotalProcessed, batchResult.Added, batchResult.Duplicates, batchResult.Errors);
                }
                else
                {
                    Log.WriteLine("Batch flush completed: {0} books successfully added", batchResult.Added);
                }
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "Error flushing books batch: {0}", ex.Message);

                // Fallback: try to add books individually
                foreach (var book in books)
                {
                    try
                    {
                        if (!Library.Add(book))
                        {
                            lock (sync)
                            {
                                duplicates++;
                                if (book.BookType == BookType.FB2) fb2Found = Math.Max(0, fb2Found - 1);
                                else epubFound = Math.Max(0, epubFound - 1);
                            }
                        }
                    }
                    catch (Exception ex2)
                    {
                        Log.WriteLine(LogLevel.Error, "Error adding book {0}: {1}", book.FileName, ex2.Message);
                    }
                }
            }
        }
    }
}
