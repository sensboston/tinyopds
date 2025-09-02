/*
 * This file is part of TinyOPDS server project
 * https://github.com/sensboston/tinyopds
 *
 * Copyright (c) 2013-2025 SeNSSoFT
 * SPDX-License-Identifier: MIT
 *
 * This module defines the OPDS RootCatalog class
 * OPTIMIZED: Now uses cached count properties instead of loading full lists
 *
 */

using System;
using System.Xml.Linq;

using TinyOPDS.Data;

namespace TinyOPDS.OPDS
{
    /// <summary>
    /// Root catalog class
    /// </summary>
    class RootCatalog
    {
        public XDocument GetCatalog()
        {
            // Use cached counts instead of loading full lists
            int authorsCount = Library.AuthorsCount;
            int sequencesCount = Library.SequencesCount;
            int totalBooksCount = Library.Count;
            int newBooksCount = Library.NewBooksCount;

            return new XDocument(
                // Add root element with namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc),
                                     new XAttribute(XNamespace.Xmlns + "os", Namespaces.os),
                                     new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),

                      new XElement("id", "tag:root"),
                      new XElement("title", Properties.Settings.Default.ServerName),
                      new XElement("subtitle", Utils.ServerVersionName),
                      new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                      new XElement("icon", "/favicon.ico"),

                      // Add links
                      Links.opensearch,
                      Links.search,
                      Links.start,
                      Links.self,

                      // Add new books entry (if we have a new books of course!)
                      newBooksCount == 0 ? null :
                      new XElement("entry",
                          new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                          new XElement("id", "tag:root:new"),
                          new XElement("title", Localizer.Text("New books (by date added)"), new XAttribute("type", "text")),
                          new XElement("content", string.Format(Localizer.Text("{0} new books ordered by date"), newBooksCount), new XAttribute("type", "text")),
                          new XElement("link", new XAttribute("href", "/newdate"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                          ),

                      newBooksCount == 0 ? null :
                      new XElement("entry",
                          new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                          new XElement("id", "tag:root:new"),
                          new XElement("title", Localizer.Text("New books (alphabetically)"), new XAttribute("type", "text")),
                          new XElement("content", string.Format(Localizer.Text("{0} new books ordered alphabetically"), newBooksCount), new XAttribute("type", "text")),
                          new XElement("link", new XAttribute("href", "/newtitle"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                          ),

                      // Add catalog entries
                      new XElement("entry",
                          new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                          new XElement("id", "tag:root:authors"),
                          new XElement("title", Localizer.Text("By authors"), new XAttribute("type", "text")),
                          new XElement("content", string.Format(Localizer.Text("{0} books by {1} authors"), totalBooksCount, authorsCount), new XAttribute("type", "text")),
                          new XElement("link", new XAttribute("href", "/authorsindex"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                          ),

                      // Show series entry only if we have series
                      sequencesCount == 0 ? null :
                      new XElement("entry",
                          new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                          new XElement("id", "tag:root:sequences"),
                          new XElement("title", Localizer.Text("By series"), new XAttribute("type", "text")),
                          new XElement("content", string.Format(Localizer.Text("{0} series"), sequencesCount), new XAttribute("type", "text")),
                          new XElement("link", new XAttribute("href", "/sequencesindex"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                          ),

                      new XElement("entry",
                          new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                          new XElement("id", "tag:root:genre"),
                          new XElement("title", Localizer.Text("By genres"), new XAttribute("type", "text")),
                          new XElement("content", Localizer.Text("Books grouped by genres"), new XAttribute("type", "text")),
                          new XElement("link", new XAttribute("href", "/genres"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                      )
                  )
              );
        }
    }
}