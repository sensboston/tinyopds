/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines the OPDS GenresCatalog class
 * 
 ************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Web;

using TinyOPDS.Data;

namespace TinyOPDS.OPDS
{
    /// <summary>
    /// Genres acquisition feed class
    /// </summary>
    public class GenresCatalog
    {
        public XDocument GetCatalog(string searchPattern, int threshold = 50)
        {
            if (!string.IsNullOrEmpty(searchPattern)) searchPattern = Uri.UnescapeDataString(searchPattern).Replace('+', ' ');

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                new XElement("title", Localizer.Text("Books by genres")),
                new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                new XElement("icon", "/genres.ico"),
                // Add links
                Links.opensearch, Links.search, Links.start)
            );

            bool topLevel = true;
            bool useCyrillic = Localizer.Language.Equals("ru");

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
                Genre genre = Library.FB2Genres.Where(g => g.Name.Equals(searchPattern) || g.Translation.Equals(searchPattern)).FirstOrDefault();
                if (genre != null)
                {
                    genres = (from g in libGenres where genre.Subgenres.Contains(g) select g).Distinct().ToList();
                    topLevel = false;
                }
            }

            if (genres != null)
            {
                genres.Sort(new OPDSComparer(useCyrillic));

                // Add catalog entries
                foreach (Genre genre in genres)
                {
                    doc.Root.Add(
                        new XElement("entry",
                            new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                            new XElement("id", "tag:root:genre:" + (useCyrillic ? genre.Translation : genre.Name)),
                            new XElement("title", (useCyrillic ? genre.Translation : genre.Name)),
                            new XElement("content", string.Format(Localizer.Text("Books in genre «{0}»"), (useCyrillic ? genre.Translation : genre.Name)), new XAttribute("type", "text")),
                            new XElement("link", new XAttribute("href", "/" + (topLevel ? "genres/" : "genre/") + (topLevel ? Uri.EscapeDataString((useCyrillic ? genre.Translation : genre.Name)) : genre.Tag)), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                        )
                    );
                }
            }

            return doc;
        }
    }
}
