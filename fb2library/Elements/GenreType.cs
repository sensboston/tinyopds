using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements
{
    /// <summary>
    /// Holds genre element
    /// </summary>
    public class GenreType
    {
        /// <summary>
        /// Get/Set genre
        /// </summary>
        public string Genre { get; set; }
    }

    /// <summary>
    /// Extends  GenreType class to support match 
    /// match from 1 to 100%
    /// </summary>
    public class TitleGenreType : GenreType
    {
        private int match = 100;

        /// <summary>
        /// Get/Set matching value , can be from 1 to 100%
        /// </summary>
        public int Match
        {
            get { return match; }
            set 
            {
                if (value > 0 && value <= 100)
                {
                    match = value;
                }
                else
                {
                    match = 100;
                }
            }
        }
        
        public XElement ToXML()
        { 
            if (string.IsNullOrEmpty(Genre))
            {
                 throw new ArgumentException("Genre is empty");
            }
            XElement xGenre = new XElement(Fb2Const.fb2DefaultNamespace + "genre", Genre);
            if (match > 0 && match < 100)
            {
                xGenre.Add(new XAttribute("match",match.ToString()));
            }
            return xGenre;
        }
        
    }
}
