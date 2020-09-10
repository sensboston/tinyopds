using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FB2Library.Elements.Poem;
using FB2Library.Elements.Table;

namespace FB2Library.Elements
{
    public class AnnotationType : IFb2TextItem
    {
        private readonly List<IFb2TextItem> content = new List<IFb2TextItem>();

        protected string GetElementName()
        {
            return ElementName;
        }

        public string ElementName { get; set; }

        public List<IFb2TextItem> Content { get { return content; } }

        public string ID { set; get; }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var textItem in content)
            {
                builder.Append(textItem.ToString());
                builder.Append(" ");
            }
            return builder.ToString();

        }



        internal void Load(XElement xAnnotation)
        {
            if (xAnnotation == null)
            {
                throw new ArgumentNullException("xAnnotation");
            }

            if (xAnnotation.Name.LocalName != GetElementName())
            {
                throw new ArgumentException("Element of wrong type passed", "xAnnotation");
            }

            content.Clear();
            IEnumerable<XElement> xItems = xAnnotation.Elements();
            foreach (var xItem in xItems)
            {
                switch (xItem.Name.LocalName)
                {
                    case ParagraphItem.Fb2ParagraphElementName:
                        ParagraphItem paragraph = new ParagraphItem();
                        try
                        {
                            paragraph.Load(xItem);
                            content.Add(paragraph);
                        }
                        catch (Exception)
                        {
                        }
                        break;
                    case PoemItem.Fb2PoemElementName:
                        PoemItem poem = new PoemItem();
                        try
                        {
                            poem.Load(xItem);
                            content.Add(poem);
                        }
                        catch (Exception)
                        {
                        }
                        break;
                    case CiteItem.Fb2CiteElementName:
                        CiteItem cite = new CiteItem();
                        try
                        {
                            cite.Load(xItem);
                            content.Add(cite);
                        }
                        catch (Exception)
                        {
                        }
                        break;
                    case SubTitleItem.Fb2SubtitleElementName:
                        SubTitleItem subtitle = new SubTitleItem();
                        try
                        {
                            subtitle.Load(xItem);
                            content.Add(subtitle);
                        }
                        catch (Exception)
                        {
                        }
                        break;
                    case TableItem.Fb2TableElementName:
                        TableItem table = new TableItem();
                        try
                        {
                            table.Load(xItem);
                            content.Add(table);
                        }
                        catch (Exception)
                        {
                        }
                        break;
                    case EmptyLineItem.Fb2EmptyLineElementName:
                        EmptyLineItem eline = new EmptyLineItem();
                        content.Add(eline);
                        break;
                    default:
                        Debug.WriteLine(string.Format("AnnotationItem:Load - invalid element <{0}> encountered in annotation ."), xItem.Name.LocalName);
                        break;
                }
            }

            ID = null;
            XAttribute xID = xAnnotation.Attribute("id");
            if ((xID != null) && (xID.Value != null))
            {
                ID = xID.Value;
            }
        }

        public XNode ToXML()
        {
            XElement xAnnotation = new XElement(Fb2Const.fb2DefaultNamespace+ ElementName);
            if (ID != null)
            {
                xAnnotation.Add(new XAttribute("id",ID));
            }
            foreach (IFb2TextItem Item in content)
            {
                xAnnotation.Add(Item.ToXML());
            }

            return xAnnotation;
        }
    }
}
