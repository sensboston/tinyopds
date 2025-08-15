/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines the OPDS GenresCatalog class with SQLite support
 * 
 ************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using TinyOPDS.Data;

namespace TinyOPDS.OPDS
{
    /// <summary>
    /// Genres acquisition feed class
    /// </summary>
    public class GenresCatalog
    {
        public XDocument GetCatalog(string searchPattern, int threshold = 100)
        {
            if (!string.IsNullOrEmpty(searchPattern)) searchPattern = Uri.UnescapeDataString(searchPattern).Replace('+', ' ');

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                new XElement("id", "tag:genres"),
                new XElement("title", Localizer.Text("Books by genres")),
                new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                new XElement("icon", "/genres.ico"),
                // Add links
                Links.opensearch, Links.search, Links.start)
            );

            bool topLevel = true;
            bool useCyrillic = TinyOPDS.Properties.Settings.Default.SortOrder > 0;

            // Use SQLite version for genres
            List<Genre> libGenres = Library.Genres;
            List<Genre> genres = null;

            // Is it top level (main genres)?
            if (string.IsNullOrEmpty(searchPattern))
            {
                genres = (from g in Library.FB2Genres from sg in g.Subgenres where libGenres.Contains(sg) select g).Distinct().ToList();
            }
            // Is it a second level (subgenres)?
            else
            {
                topLevel = false;
                var parent = Library.FB2Genres.Where(g => g.Name.ToLower().Equals(searchPattern.ToLower()) || g.Translation.ToLower().Equals(searchPattern.ToLower())).FirstOrDefault();
                if (parent != null) genres = parent.Subgenres.Where(sg => libGenres.Contains(sg)).ToList();
            }

            if (genres != null)
            {
                var comparer = new OPDSComparer(useCyrillic);
                genres = genres.OrderBy(g => useCyrillic ? g.Translation : g.Name, comparer).ToList();

                foreach (Genre genre in genres)
                {
                    // Use SQLite version to count books in genre
                    var genreCount = Library.GetBooksByGenre(genre.Tag).Count;

                    if (genreCount > 0)
                    {
                        doc.Root.Add(
                            new XElement("entry",
                                new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                                new XElement("id", "tag:genres:" + genre.Tag),
                                new XElement("title", useCyrillic ? genre.Translation : genre.Name),
                                new XElement("content", string.Format(Localizer.Text("Books: {0}"), genreCount), new XAttribute("type", "text")),
                                new XElement("link", new XAttribute("href", topLevel ? "/genres/" + Uri.EscapeDataString(useCyrillic ? genre.Translation : genre.Name) : "/genre/" + Uri.EscapeDataString(genre.Tag)), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                            )
                        );
                    }
                }
            }

            return doc;
        }
    }
}