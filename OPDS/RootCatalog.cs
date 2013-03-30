/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 * All rights reserved.
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
        public XDocument Catalog
        {
            get
            {
                return new XDocument(
                    // Add root element with namespaces
                    new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), 
                                         new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),

                          new XElement("id", "tag:root"),
                          new XElement("title", Properties.Settings.Default.ServerName),
                          new XElement("subtitle", Utils.ServerVersionName),
                          new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                          new XElement("icon", "http://{$HOST}/favicon.ico"),

                          // Add links
                          Links.opensearch,
                          Links.search,
                          Links.start,
                          Links.self,

                          // Add catalog entries
                          new XElement("entry",
                              new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                              new XElement("id", "tag:root:authors"),
                              new XElement("title", Localizer.Text("By authors"), new XAttribute("type", "text")),
                              new XElement("content", string.Format(Localizer.Text("{0} books by {1} authors"), Library.Count, Library.Authors.Count), new XAttribute("type", "text")),
                              new XElement("link", new XAttribute("href", "http://{$HOST}/authorsindex"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                              ),
                          new XElement("entry",
                              new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                              new XElement("id", "tag:root:sequences"),
                              new XElement("title", Localizer.Text("По сериям"), new XAttribute("type", "text")),
                              new XElement("content", string.Format("{0} книг в {1} сериях", Library.Count, Library.Sequences.Count), new XAttribute("type", "text")),
                              new XElement("link", new XAttribute("href", "http://{$HOST}/sequencesindex"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                              ),
                          new XElement("entry",
                              new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                              new XElement("id", "tag:root:genre"),
                              new XElement("title", Localizer.Text("По жанрам"), new XAttribute("type", "text")),
                              new XElement("content", Localizer.Text("поиск книг по жанрам"), new XAttribute("type", "text")),
                              new XElement("link", new XAttribute("href", "http://{$HOST}/genres"), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                          )
                      )
                  );
            }
        }
    }
}
