using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements
{
    public class TitleItem : IFb2TextItem
    {
        private readonly List<IFb2TextItem> titleData = new List<IFb2TextItem>();

        public List<IFb2TextItem> TitleData 
        {
            get { return titleData; }
        }

        /// <summary>
        /// Language attribute
        /// </summary>
        public string LangAttribute { get; set; }


        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var textItem in TitleData)
            {
                builder.Append(textItem.ToString());
                builder.Append(" ");
            }
            return builder.ToString();
        }


        internal const string Fb2TitleElementName = "title";

        internal void Load(XElement xTitle)
        {
            titleData.Clear();
            if (xTitle == null)
            {
                throw new ArgumentNullException("xTitle");
            }

            if (xTitle.Name.LocalName != Fb2TitleElementName)
            {
                throw new ArgumentException("Element of wrong type passed", "xTitle");
            }


            IEnumerable<XElement> subElements = xTitle.Elements();
            foreach (var element in subElements)
            {
                switch (element.Name.LocalName)
                {
                    case EmptyLineItem.Fb2EmptyLineElementName:
                        titleData.Add(new EmptyLineItem());
                        break;
                    case ParagraphItem.Fb2ParagraphElementName:
                        ParagraphItem paragraph = new ParagraphItem();
                        try
                        {
                            paragraph.Load(element);
                            titleData.Add(paragraph);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(string.Format("Failed to load paragraph: {0}.",ex.Message));
                        }
                        break;
                    default:
                        Debug.WriteLine(string.Format("TitleItem:Load - invalid element <{0}> encountered in title ."), element.Name.LocalName);
                        break;
                }

            }

            LangAttribute = null;
            XAttribute xLang = xTitle.Attribute(XNamespace.Xml + "lang");
            if ((xLang != null) && !string.IsNullOrEmpty(xLang.Value))
            {
                LangAttribute = xLang.Value;
            }
        }
        public XNode ToXML()
        {
            XElement xTitle = new XElement(Fb2Const.fb2DefaultNamespace + Fb2TitleElementName);
            if (!string.IsNullOrEmpty(LangAttribute))
            {
                xTitle.Add(new XAttribute(XNamespace.Xml + "lang", LangAttribute));
            }

            foreach (IFb2TextItem TitleItem in titleData)
            {
                xTitle.Add(TitleItem.ToXML());
            }
            return xTitle;
        }
    }
}
