using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FB2Library.Elements.Poem;

namespace FB2Library.Elements
{
    public class EpigraphItem : IFb2TextItem
    {
        private readonly List<IFb2TextItem> epigraphData = new List<IFb2TextItem>();
        private readonly List<IFb2TextItem> textAuthors = new List<IFb2TextItem>();

        public List<IFb2TextItem> TextAuthors { get { return textAuthors; } }
        public List<IFb2TextItem> EpigraphData { get { return epigraphData; } }
        public string ID { get; set; }


        internal const string Fb2EpigraphElementName = "epigraph";

        internal void Load(XElement xEpigraph)
        {
            epigraphData.Clear();
            if (xEpigraph == null)
            {
                throw new ArgumentNullException("xEpigraph");
            }


            if (xEpigraph.Name.LocalName != Fb2EpigraphElementName)
            {
                throw new ArgumentException("Element of wrong type passed", "xEpigraph");
            }

            IEnumerable<XElement> xItems = xEpigraph.Elements();
            textAuthors.Clear();
            foreach (var element in xItems)
            {
                switch (element.Name.LocalName)
                {
                    case ParagraphItem.Fb2ParagraphElementName:
                        ParagraphItem paragraph = new ParagraphItem();
                        try
                        {
                            paragraph.Load(element);
                            epigraphData.Add(paragraph);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(string.Format("Failed to load paragraph: {0}.", ex.Message));
                        }
                        break;
                    case PoemItem.Fb2PoemElementName:
                        PoemItem poem = new PoemItem();
                        try
                        {
                            poem.Load(element);
                            epigraphData.Add(poem);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(string.Format("Failed to load poem: {0}.", ex.Message));
                        }
                        break;
                    case CiteItem.Fb2CiteElementName:
                        CiteItem cite = new CiteItem();
                        try
                        {
                            cite.Load(element);
                            epigraphData.Add(cite);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(string.Format("Failed to load citation: {0}.", ex.Message));
                        }
                        break;
                    case EmptyLineItem.Fb2EmptyLineElementName:
                        EmptyLineItem emptyLine = new EmptyLineItem();
                        try
                        {
                            epigraphData.Add(emptyLine);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(string.Format("Failed to load empty line: {0}.", ex.Message));
                        }
                        break;
                    case TextAuthorItem.Fb2TextAuthorElementName:
                        TextAuthorItem author = new TextAuthorItem();
                        //SimpleText author = new SimpleText();
                        try
                        {
                            author.Load(element);
                            textAuthors.Add(author);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(string.Format("Failed to load text author: {0}.", ex.Message));
                        }
                        break;
                    default:
                        Debug.WriteLine(string.Format("EpigraphItem:Load - invalid element <{0}> encountered in title ."), element.Name.LocalName);
                        break;
                }
            }

            XAttribute xID = xEpigraph.Attribute("id");
            if ((xID != null) &&(xID.Value != null))
            {
                ID = xID.Value;
            }
        }

        public XNode ToXML()
        {
            XElement xEpigraph = new XElement(Fb2Const.fb2DefaultNamespace + Fb2EpigraphElementName);
            if (!string.IsNullOrEmpty(ID))
            {
                xEpigraph.Add(new XAttribute("id", ID));
            }

            foreach (IFb2TextItem EpigrafItem in epigraphData)
            {
                xEpigraph.Add(EpigrafItem.ToXML());
            }
            foreach (IFb2TextItem EpigrafAuthor in textAuthors)
            {
                xEpigraph.Add(EpigrafAuthor.ToXML());
            }

            return xEpigraph;
        
        }
    }
}
