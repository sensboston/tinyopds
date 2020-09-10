using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements.Poem
{
    public class StanzaItem : IFb2TextItem
    {
        private readonly List<VPoemParagraph> lines = new List<VPoemParagraph>();

        public TitleItem Title { get; set; }
        public SubTitleItem SubTitle { get; set; }
        public List<VPoemParagraph> Lines { get { return lines; } }
        public string Lang { get; set; }

        internal const string Fb2StanzaElementName = "stanza";

        internal void Load(XElement xStanza)
        {
            if (xStanza == null)
            {
                throw new ArgumentNullException("xStanza");
            }

            if (xStanza.Name.LocalName != Fb2StanzaElementName)
            {
                throw new ArgumentException("Element of wrong type passed", "xStanza");
            }

            Title = null;
            XElement xTitle = xStanza.Element(xStanza.Name.Namespace + TitleItem.Fb2TitleElementName);
            if (xTitle != null)
            {
                Title = new TitleItem();
                try
                {
                    Title.Load(xTitle);
                }
                catch (Exception)
                {
                }
            }

            SubTitle = null;
            XElement xSubtitle = xStanza.Element(xStanza.Name.Namespace + SubTitleItem.Fb2SubtitleElementName);
            if (xSubtitle != null)
            {
                SubTitle = new SubTitleItem();
                try
                {
                    SubTitle.Load(xSubtitle);
                }
                catch (Exception)
                {
                }
            }

            lines.Clear();
            IEnumerable<XElement> xLines = xStanza.Elements(xStanza.Name.Namespace + VPoemParagraph.Fb2VParagraphItemName);
            foreach (var xLine in xLines)
            {
                VPoemParagraph vline = new VPoemParagraph();
                try
                {
                    vline.Load(xLine);
                    lines.Add(vline);
                }
                catch (Exception)
                {
                    continue;
                }
            }

            Lang = null;
            XAttribute xLang = xStanza.Attribute(XNamespace.Xml + "lang");
            if ((xLang != null) && (xLang.Value != null))
            {
                Lang = xLang.Value;
            }

        }

        public XNode ToXML()
        {
            XElement xStanza = new XElement(Fb2Const.fb2DefaultNamespace + Fb2StanzaElementName);
            if (!string.IsNullOrEmpty(Lang))
            {
                xStanza.Add(new XAttribute(XNamespace.Xml + "lang", Lang));
            }
            if (Title != null)
            {
                xStanza.Add(Title.ToXML());
            }
            if (SubTitle != null)
            {
                xStanza.Add(SubTitle.ToXML());
            }
            foreach (VPoemParagraph PoemLine in lines)
            {
                xStanza.Add(PoemLine.ToXML());
            }
            return xStanza;
        
        }
    }
}
