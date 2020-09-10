using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace FB2Library.Elements
{
    //public class StyledCharacter : IFb2TextItem
    //{
    //    public char Character { get; set; }
    //    public TextStylesFlags Style { get; set; }
    //}


    public class ParagraphItem : IFb2TextItem
    {
        private readonly List<StyleType> paragraphData = new List<StyleType>();

        protected virtual string GetElementName()
        {
            return Fb2ParagraphElementName;
        }
        
        internal const string Fb2ParagraphElementName = "p";
        

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var textItem in paragraphData)
            {
                builder.Append(textItem.ToString());
                builder.Append(" ");
            }
            return builder.ToString();
        }


        public string ID { get; set; }
        public string Style { get; set; }
        public string Lang { get; set; }

        public List<StyleType> ParagraphData { get { return paragraphData; } }

        protected void LoadData(XElement xParagraph)
        {
            if (xParagraph.HasElements)
            {
                IEnumerable<XNode> childElements = xParagraph.Nodes();
                foreach (var element in childElements)
                {
                    if ((element.NodeType == XmlNodeType.Element) && !IsSimpleText(element))
                    {
                        XElement xElement = (XElement) element;
                        if (xElement.Name.LocalName == InlineImageItem.Fb2InlineImageElementName)
                        {
                            InlineImageItem image = new InlineImageItem();
                            try
                            {
                                image.Load(xElement);
                                paragraphData.Add(image);

                            }
                            catch (Exception)
                            {
                            }
                        }
                        else if (xElement.Name.LocalName == InternalLinkItem.Fb2InternalLinkElementName)
                        {
                            InternalLinkItem linkItem = new InternalLinkItem();
                            try
                            {
                                linkItem.Load(xElement);
                                paragraphData.Add(linkItem);

                            }
                            catch (Exception)
                            {
                            }                            
                        }
                    }
                    else //if ( element.NodeType != XmlNodeType.Whitespace)
                    {
                            SimpleText text = new SimpleText();
                            try
                            {
                                text.Load(element);
                                paragraphData.Add(text);
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                    }
                }
               
            }
            else if (!string.IsNullOrEmpty(xParagraph.Value))
            {
                SimpleText text = new SimpleText();
                text.Load(xParagraph);
                paragraphData.Add(text);
            }

            XAttribute xID = xParagraph.Attribute("id");
            if ((xID != null) && (xID.Value != null))
            {
                ID = xID.Value;
            }

            XAttribute xStyle = xParagraph.Attribute("style");
            if ((xStyle != null) && (xStyle.Value != null))
            {
                Style = xStyle.Value;
            }

            Lang = null;
            XAttribute xLang = xParagraph.Attribute(XNamespace.Xml + "lang");
            if ((xLang != null) && (xLang.Value != null))
            {
                Lang = xLang.Value;
            }
            
        }

        internal virtual void Load(XElement xParagraph)
        {
            paragraphData.Clear();
            if (xParagraph == null)
            {
                throw new ArgumentNullException("xParagraph");
            }

            if (xParagraph.Name.LocalName != GetElementName())
            {
                throw new ArgumentException("Element of wrong type passed", "xParagraph");
            }
            LoadData(xParagraph);

        } // Load

        private bool IsSimpleText(XNode element)
        {
            // if not element than we assume simple text
            if (element.NodeType != XmlNodeType.Element)
            {
                return true;
            }
            XElement xElement = (XElement) element;
            switch (xElement.Name.LocalName)
            {
                case InternalLinkItem.Fb2InternalLinkElementName:
                case InlineImageItem.Fb2InlineImageElementName:
                    return false;
                    
            }
            return true;
        }

        public XNode ToXML()
        {
            XElement xParagraph = new XElement(Fb2Const.fb2DefaultNamespace + GetElementName());
            if (!string.IsNullOrEmpty(ID))
            {
                xParagraph.Add(new XAttribute("id", ID));
            }
            if (!string.IsNullOrEmpty(Style))
            {
                xParagraph.Add(new XAttribute("style", Style));
            }
            if (!string.IsNullOrEmpty(Lang))
            {
                xParagraph.Add(new XAttribute(XNamespace.Xml + "lang", Lang));
            }

            foreach (StyleType childElements in paragraphData)
            {
                xParagraph.Add(childElements.ToXML());
            }
            return xParagraph;
        }

    } //class 


}// namespace
