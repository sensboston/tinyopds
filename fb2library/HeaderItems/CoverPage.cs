using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FB2Library.Elements;

namespace FB2Library.HeaderItems
{
    public class CoverPage
    {
        private readonly List<InlineImageItem> coverimages = new List<InlineImageItem>();

        private XNamespace fileNameSpace = XNamespace.None;

        internal const string Fb2CoverpageImageElementName = "coverpage";

        protected string GetElementName()
        {
            return Fb2CoverpageImageElementName;
        }


        public List<InlineImageItem> CoverpageImages { get { return coverimages; } }

        public bool HasImages() {  return (coverimages.Count > 0); } 

        /// <summary>
        /// XML namespace used to read the document
        /// </summary>
        public XNamespace Namespace
        {
            set { fileNameSpace = value; }
            get { return fileNameSpace; }
        }


        internal void Load(XElement xCoverpage)
        {
            if (xCoverpage == null)
            {
                throw new ArgumentNullException("xCoverpage");
            }

            if (xCoverpage.Name.LocalName != GetElementName())
            {
                throw new ArgumentException("Element of wrong type passed", "xCoverpage");
            }

            coverimages.Clear();
            IEnumerable<XElement> xImages = xCoverpage.Elements(fileNameSpace +InlineImageItem.Fb2InlineImageElementName);
            foreach (var xImage in xImages)
            {
                InlineImageItem image = new InlineImageItem();
                try
                {
                    image.Load(xImage);
                    coverimages.Add(image);
                }
                catch (Exception)
                {
                }
                
            }
        }
        
        public XElement ToXML()
        {
            XElement xCover = new XElement(Fb2Const.fb2DefaultNamespace + Fb2CoverpageImageElementName);
            foreach (InlineImageItem ImageItem in coverimages)
            {
                xCover.Add(ImageItem.ToXML());
            }

            return xCover;
        }
    }
}