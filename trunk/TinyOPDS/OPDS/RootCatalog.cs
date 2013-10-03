/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines the OPDS RootCatalog class
 * 
 ************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using TinyOPDS.Data;

namespace TinyOPDS.OPDS
{
    /// <summary>
    /// Root catalog class
    /// </summary>
    class RootCatalog
    {
        public XDocument GetCatalog(bool newBooksOnly = false)
        {
            return new XDocument(
                // Add root element with namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc),
                                     new XAttribute(XNamespace.Xmlns + "os", Namespaces.os),
                                     new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),

                      new XElement("id", "tag:root"),
                      new XElement("title", TinyOPDS.Properties.Settings.Default.ServerName),
                      new XElement("subtitle", Utils.ServerVersionName),
                      new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                      new XElement("icon", "/favicon.ico"),

                      // Add links
                      Links.opensearch,
                      Links.search,
                      Links.start,
                      Links.self,

                      // Add new books entry (if we have a new books of course!)
                      (newBooksOnly || Library.NewBooksCount == 0) ? null :
                      new XElement("entry",
                          new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                          new XElement("id", "tag:root:new"),
                          new XElement("title", Localizer.Text("New books"), new XAttribute("type", "text")),
                          new XElement("content", string.Format(Localizer.Text("{0} new books"), Library.NewBooksCount), new XAttribute("type", "text")),
                          new XElement("link", new XAttribute("href", "/new"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                          ),

                      // Add catalog entries
                      new XElement("entry",
                          new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                          new XElement("id", "tag:root:authors"),
                          new XElement("title", Localizer.Text("By authors"), new XAttribute("type", "text")),
                          new XElement("content", string.Format(Localizer.Text("{0} books by {1} authors"), newBooksOnly ? Library.NewBooksCount : Library.Count, newBooksOnly ? Library.NewBookAuthors.Count : Library.Authors.Count), new XAttribute("type", "text")),
                          new XElement("link", new XAttribute("href", (newBooksOnly ? "/new" : "") + "/authorsindex"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                          ),
                      new XElement("entry",
                          new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                          new XElement("id", "tag:root:sequences"),
                          new XElement("title", Localizer.Text("By series"), new XAttribute("type", "text")),
                          new XElement("content", string.Format(Localizer.Text("{0} books by {1} series"), newBooksOnly ? Library.NewBooksCount : Library.Count, newBooksOnly ? Library.NewBookSequences.Count : Library.Sequences.Count), new XAttribute("type", "text")),
                          new XElement("link", new XAttribute("href", (newBooksOnly ? "/new" : "") + "/sequencesindex"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                          ),
                      new XElement("entry",
                          new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                          new XElement("id", "tag:root:genre"),
                          new XElement("title", Localizer.Text("By genres"), new XAttribute("type", "text")),
                          new XElement("content", Localizer.Text("Books grouped by genres"), new XAttribute("type", "text")),
                          new XElement("link", new XAttribute("href", (newBooksOnly ? "/new" : "") + "/genres"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                      )
                  )
              );
        }
    }
}
