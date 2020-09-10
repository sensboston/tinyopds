using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements
{
    public class InlineImageItem : StyleType
    {
        private readonly XNamespace xLinkNamespace = @"http://www.w3.org/1999/xlink";

        protected virtual string GetElementName()
        {
            return Fb2InlineImageElementName;
        }


        public string ImageType { get; set;}
        public string HRef { get; set;}
        public string AltText { get; set;}

        internal const string Fb2InlineImageElementName = "image";

        public override string ToString()
        {
            return HRef;
        }


        internal void Load(XElement xImage)
        {
            if (xImage == null)
            {
                throw new ArgumentNullException("xImage");
            }

            if (xImage.Name.LocalName != GetElementName())
            {
                throw new ArgumentException("Element of wrong type passed", "xImage");
            }

            XAttribute xType = xImage.Attribute(xLinkNamespace + "type");
            if ((xType != null) && (xType.Value != null))
            {
                ImageType = xType.Value;
            }
            else  // to fix badly formated FB2s
            {
                xType = xImage.Attribute("type"); 
                if ((xType != null) && (xType.Value != null))
                {
                    ImageType = xType.Value;
                }
            }


            XAttribute xHRef = xImage.Attribute(xLinkNamespace + "href");
            if ((xHRef != null) && (xHRef.Value != null))
            {
                HRef = xHRef.Value;
            }
            else // to fix badly formated FB2s
            {
                xHRef = xImage.Attribute(xLinkNamespace + "href");
                if ((xHRef != null) && (xHRef.Value != null))
                {
                    HRef = xHRef.Value;
                }
            }

            XAttribute xAlt = xImage.Attribute(xLinkNamespace + "alt");
            if ((xAlt != null) &&(xAlt.Value != null))
            {
                AltText = xAlt.Value;
            }
            else // to fix badly formated FB2s
            {
                xAlt = xImage.Attribute(xLinkNamespace + "alt");
                if ((xAlt != null) && (xAlt.Value != null))
                {
                    AltText = xAlt.Value;
                }
            }
        }

        public XNode ToXML()
        {
            XElement xImage = new XElement(Fb2Const.fb2DefaultNamespace + Fb2InlineImageElementName);
            if (!string.IsNullOrEmpty(ImageType))
            { 
                  xImage.Add(new XAttribute(xLinkNamespace + "type",ImageType));          
            }
            if (!string.IsNullOrEmpty(HRef))
            { 
                  xImage.Add(new XAttribute(xLinkNamespace + "href",HRef));          
            }
            if (!string.IsNullOrEmpty(AltText))
            {
                xImage.Add(new XAttribute("alt", AltText));
            }
            return xImage;
        }

    }
}
