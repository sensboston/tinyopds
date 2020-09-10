using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements
{
    public class InternalLinkItem : StyleType
    {
        private readonly XNamespace lNamespace = @"http://www.w3.org/1999/xlink";
        
        public string Type { get; set; }

        public string HRef { get; set; }

        public SimpleText LinkText { get; set; }

        internal const string Fb2InternalLinkElementName = "a";


        public override string ToString()
        {
            return LinkText.ToString();
        }

        internal void Load(XElement xLink)
        {
            if (xLink == null)
            {
                throw new ArgumentNullException("xLink");
            }

            if (xLink.Name.LocalName != Fb2InternalLinkElementName)
            {
                throw new ArgumentException("Element of wrong type passed", "xLink");
            }

            LinkText = null;
            //if (xLink.Value != null)
            {
                LinkText = new SimpleText();
                try
                {
                    LinkText.Load(xLink);
                }
                catch (Exception)
                {
                    LinkText = null;
                }
            }

            XAttribute xTypeAttr = xLink.Attribute("type");
            if ((xTypeAttr != null)&& (xTypeAttr.Value != null))
            {
                Type = xTypeAttr.Value;
            }

            XAttribute xHRefAttr = xLink.Attribute(lNamespace + "href");
            if ((xHRefAttr != null) && (xHRefAttr.Value != null))
            {
                HRef = xHRefAttr.Value;
            }

        }

        public XNode ToXML()
        {
            XElement xLink = new XElement(Fb2Const.fb2DefaultNamespace + Fb2InternalLinkElementName);
            if (!string.IsNullOrEmpty(Type))
            { 
                xLink.Add(new XAttribute("type",Type));
            }
            if(!string.IsNullOrEmpty(HRef))
            {
                xLink.Add(new XAttribute(lNamespace + "href",HRef));
            }
            if (LinkText != null)
            {
                xLink.Add(LinkText.ToXML());
            }
            return xLink;
        }

    }
}
