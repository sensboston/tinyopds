/***********************************************************
 * This file is a part of TinyOPDS server project
 * 
 * Copyright (c) 2013 SeNSSoFT
 *
 * This code is licensed under the Microsoft Public License, 
 * see http://tinyopds.codeplex.com/license for the details.
 *
 * This module defines the OPDS SequencesCatalog class (book series)
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
    /// Sequences acquisition feed class
    /// </summary>
    public class SequencesCatalog
    {
        public XDocument GetCatalog(string searchPattern, int threshold = 50)
        {
            if (!string.IsNullOrEmpty(searchPattern)) searchPattern = Uri.UnescapeDataString(searchPattern).Replace('+', ' ');

            XDocument doc = new XDocument(
                // Add root element and namespaces
                new XElement("feed", new XAttribute(XNamespace.Xmlns + "dc", Namespaces.dc), new XAttribute(XNamespace.Xmlns + "os", Namespaces.os), new XAttribute(XNamespace.Xmlns + "opds", Namespaces.opds),
                    new XElement("title", Localizer.Text("Book series")),
                    new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                    new XElement("icon", "/series.ico"),
                // Add links
                    Links.opensearch, Links.search, Links.start)
                );

            // Get all authors names starting with searchPattern
            List<string> Sequences = (from s in Library.Sequences where s.StartsWith(searchPattern) && s.Length > searchPattern.Length + 1 select s).ToList();

            if (Sequences.Count > threshold)
            {
                Dictionary<string, int> sequences = (from a in Sequences
                                                     group a by (a.Length > searchPattern.Length ? a.Substring(0, searchPattern.Length + 1) : a) into g
                                                     where g.Count() > 1
                                                     select new { Name = g, Count = g.Count() }).ToDictionary(x => x.Name.Key, y => y.Count);

                // Add catalog entries
                foreach (KeyValuePair<string, int> sequence in sequences)
                {
                    doc.Root.Add(
                        new XElement("entry",
                            new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                            new XElement("id", "tag:sequences:" + sequence.Key),
                            new XElement("title", sequence.Key),
                            new XElement("content", string.Format(Localizer.Text("Total series on {0}: {1}"), sequence.Key, sequence.Value), new XAttribute("type", "text")),
                            new XElement("link", new XAttribute("href", "/sequencesindex/" + Uri.EscapeDataString(sequence.Key)), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                        )
                    );
                }
            }
            // 
            else
            {
                List<string> sequences = (from s in Sequences where s.StartsWith(searchPattern) select s).ToList();
                // Add catalog entries
                foreach (string sequence in sequences)
                {
                    var seriesCount = Library.GetBooksBySequence(sequence).Count;

                    doc.Root.Add(
                        new XElement("entry",
                            new XElement("updated", DateTime.UtcNow.ToUniversalTime()),
                            new XElement("id", "tag:sequences:" + sequence),
                            new XElement("title", sequence),
                            new XElement("content", string.Format(Localizer.Text("{0} books in {1}"), seriesCount, sequence), new XAttribute("type", "text")),
                            new XElement("link", new XAttribute("href", "/sequence/" + Uri.EscapeDataString(sequence)), new XAttribute("type", "application/atom+xml;profile=opds-catalog"))
                        )
                    );
                }
            }
            return doc;
        }
    }
}
