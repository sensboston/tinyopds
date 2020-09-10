using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FB2Library.Elements;

// TODO: sequence not loaded 

namespace FB2Library.HeaderItems
{
    /// <summary>
    /// Information about some paper/outher published document, that was used as a source of this xml document
    /// </summary>
    public class ItemPublishInfo
    {
        private const string BookNameElementName = "book-name";
        private const string PublisherElementName = "publisher";
        private const string CityElementName = "city";
        private const string YearElementName = "year";
        private const string ISBNElementName = "isbn";

        public const string PublishInfoElementName = "publish-info";


        private XNamespace fileNameSpace = XNamespace.None;

        private List<SequenceType> sequences = new List<SequenceType>();

        /// <summary>
        /// Get list of sequences
        /// </summary>
        public List<SequenceType> Sequences { get { return sequences; } }

        /// <summary>
        /// XML namespace used to read the document
        /// </summary>
        public XNamespace Namespace
        {
            set { fileNameSpace = value; }
            get { return fileNameSpace; }
        }


        /// <summary>
        /// ISBN of original book
        /// </summary>
        public TextFieldType ISBN { get; set; }

        /// <summary>
        /// Year of the original (paper) publication
        /// </summary>
        public int? Year { get; set; }

        /// <summary>
        /// City where the original (paper) book was published
        /// </summary>
        public TextFieldType City { get; set; }

        /// <summary>
        /// Original (paper) book publisher
        /// </summary>
        public TextFieldType Publisher { get; set; }

        /// <summary>
        /// Original (paper) book name
        /// </summary>
        public TextFieldType BookName { get; set; }


        internal void Load(XElement xPublishInfo)
        {
            if (xPublishInfo == null)
            {
                throw new ArgumentNullException("xPublishInfo");
            }

            // Load book name
            BookName = null;
            XElement xBookName = xPublishInfo.Element(fileNameSpace + BookNameElementName);
            if ( xBookName != null)  
            {
                BookName = new TextFieldType();
                try
                {
                    BookName.Load(xBookName);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error reading publisher book name : {0}", ex.Message));
                }
            }

            // Load publisher
            Publisher = null;
            XElement xPublisher = xPublishInfo.Element(fileNameSpace + PublisherElementName);
            if (xPublisher != null)
            {
                Publisher = new TextFieldType();
                try
                {
                    Publisher.Load(xPublisher);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error reading publishers : {0}", ex.Message));
                }
            }

            // Load city 
            City = null;
            XElement xCity = xPublishInfo.Element(fileNameSpace + CityElementName);
            if (xCity != null)
            {
                City = new TextFieldType();
                try
                {
                    City.Load(xCity);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error reading publishers' City: {0}", ex.Message));
                }
            }

            // Load year 
            Year = null;
            XElement xYear = xPublishInfo.Element(fileNameSpace + YearElementName);
            if ( (xYear != null) && (xYear.Value != null))
            {
                int year;
                if ( int.TryParse( xYear.Value,out year) )
                {
                    Year = year;
                }

            }

            // Load ISBN
            ISBN = null;
            XElement xISBN = xPublishInfo.Element(fileNameSpace + ISBNElementName);
            if (xISBN != null) 
            {
                ISBN = new TextFieldType();
                try
                {
                    ISBN.Load(xISBN);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error reading publishers' ISBN: {0}", ex.Message));
                }
            }

            // Load sequence here
            sequences.Clear();
            IEnumerable<XElement> xSequences = xPublishInfo.Elements(fileNameSpace + SequenceType.SequenceElementName);
            foreach (var xSequence in xSequences)
            {
                SequenceType sec = new SequenceType();
                try
                {
                    sec.Load(xSequence);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error reading publisher sequence data: {0}", ex.Message));
                    //Debug.WriteLine(string.Format("Error reading publisher sequence data: {0}", ex.Message));
                    continue;
                }
            }

        }

        public XElement ToXML()
        {
            XElement xPublishInfo = new XElement(Fb2Const.fb2DefaultNamespace + PublishInfoElementName);

            if (BookName != null)
            {
                xPublishInfo.Add(BookName.ToXML(BookNameElementName));
            }
            if (Publisher != null)
            {
                xPublishInfo.Add(Publisher.ToXML(PublisherElementName));
            }
            if (City != null)
            {
                xPublishInfo.Add(City.ToXML(CityElementName));
            }
            if (Year != null)
            {
                xPublishInfo.Add(new XElement(Fb2Const.fb2DefaultNamespace + YearElementName, Year.ToString()));
            }
            if (ISBN != null)
            {
                xPublishInfo.Add(ISBN.ToXML(ISBNElementName));
            }
            foreach (SequenceType sec in sequences)
            {
                xPublishInfo.Add(sec.ToXML());
            }

            return xPublishInfo;
        }

    }// class
}
