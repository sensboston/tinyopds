/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * Database lookups for duplicate detection
 *
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace TinyOPDS.Data
{
    /// <summary>
    /// Finds the copies of a book already saved in the database.
    /// Copies of the same book have the same duplicate key (see Book.CreateDuplicateKey),
    /// the greatest copy (see Book.CompareTo) is kept active, the other ones are marked as replaced by it
    /// </summary>
    public class DuplicateDetector
    {
        private readonly DatabaseManager db;

        public DuplicateDetector(DatabaseManager database)
        {
            db = database;
        }

        /// <summary>
        /// Active (not replaced) books with the given duplicate key
        /// </summary>
        public List<Book> FindActiveByDuplicateKey(string duplicateKey)
        {
            if (string.IsNullOrEmpty(duplicateKey)) return new List<Book>();

            try
            {
                return db.ExecuteQuery<Book>(
                    @"SELECT ID, Version, FileName, DocumentDate, DocumentSize, DocumentIDTrusted, DuplicateKey
                      FROM Books
                      WHERE DuplicateKey = @DuplicateKey AND ReplacedByID IS NULL",
                    MapBook,
                    DatabaseManager.CreateParameter("@DuplicateKey", duplicateKey));
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "FindActiveByDuplicateKey error: {0}", ex.Message);
                return new List<Book>();
            }
        }

        /// <summary>
        /// Book saved with the given ID (active or replaced), null if there is none
        /// </summary>
        public Book FindByID(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            try
            {
                return db.ExecuteQuery<Book>(
                    @"SELECT ID, Version, FileName, DocumentDate, DocumentSize, DocumentIDTrusted, DuplicateKey
                      FROM Books
                      WHERE ID = @ID",
                    MapBook,
                    DatabaseManager.CreateParameter("@ID", id)).FirstOrDefault();
            }
            catch (Exception ex)
            {
                Log.WriteLine(LogLevel.Error, "FindByID error: {0}", ex.Message);
                return null;
            }
        }

        // Only the fields used to find and compare copies
        private Book MapBook(IDataReader reader)
        {
            var book = new Book(DatabaseManager.GetString(reader, "FileName"))
            {
                ID = DatabaseManager.GetString(reader, "ID"),
                Version = DatabaseManager.GetFloat(reader, "Version"),
                DocumentSize = DatabaseManager.GetUInt32(reader, "DocumentSize"),
                DocumentIDTrusted = DatabaseManager.GetBoolean(reader, "DocumentIDTrusted"),
                DuplicateKey = DatabaseManager.GetString(reader, "DuplicateKey")
            };

            var docDate = DatabaseManager.GetDateTime(reader, "DocumentDate");
            if (docDate.HasValue) book.DocumentDate = docDate.Value;

            return book;
        }
    }
}
