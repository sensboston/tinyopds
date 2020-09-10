using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
//Добавить для <style>; 

namespace FB2Library.Elements
{
    public interface StyleType
    {
        XNode ToXML();
    }

    public class SimpleText : IFb2TextItem, StyleType
    {
        private TextStyles style = TextStyles.Normal;
        private readonly List<StyleType> subtext = new List<StyleType>();

        public string Text { get; set; }


        public List<StyleType> Children { get { return subtext; } }

        public TextStyles Style
        {
            get { return style; }

            set { style = value; }
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Text))
            {
                StringBuilder builder = new StringBuilder();
                foreach (var textItem in subtext)
                {
                    builder.Append(textItem.ToString());
                    builder.Append(" ");
                }
                return builder.ToString();
            }
            return Text;
        }


        public bool HasChildren { get { return (subtext.Count>0); }}

        public void Load(XNode xText)
        {
            subtext.Clear();
            if (xText == null)
            {
                throw new ArgumentNullException("xText");
            }
            switch (xText.NodeType)
            {
                case XmlNodeType.Text:
                    XText textNode = (XText) xText;
                    if (!string.IsNullOrEmpty(textNode.Value))
                    {
                        Text = textNode.Value;
                        style = TextStyles.Normal;
                    }
                    break;
                case XmlNodeType.Element:
                    XElement xTextElement = (XElement)xText;
                    if (xTextElement.HasElements)
                    {
                        Text = string.Empty;
                        style = GetStyle(xTextElement.Name.LocalName);
                        IEnumerable<XNode> childElements = xTextElement.Nodes();
                        foreach (var node in childElements)
                        {
                            if (node.NodeType == XmlNodeType.Element)
                            {
                                XElement element = (XElement) node;
                                switch (element.Name.LocalName)
                                {
                                    case InternalLinkItem.Fb2InternalLinkElementName:
                                        InternalLinkItem link = new InternalLinkItem();
                                        try
                                        {
                                            link.Load(element);
                                            subtext.Add(link);
                                        }
                                        catch (Exception)
                                        {
                                            continue;
                                        }
                                        break;
                                    case InlineImageItem.Fb2InlineImageElementName:
                                        InlineImageItem image = new InlineImageItem();
                                        try
                                        {
                                            image.Load(element);
                                            subtext.Add(image);
                                        }
                                        catch (Exception)
                                        {
                                            continue;
                                        }
                                        break;
                                    default:
                                        SimpleText text = new SimpleText();
                                        try
                                        {
                                            text.Load(element);
                                            subtext.Add(text);
                                        }
                                        catch (Exception)
                                        {
                                            continue;
                                        }
                                        break;
                                }
                            }
                            else
                            {
                                SimpleText text = new SimpleText();
                                try
                                {
                                    text.Load(node);
                                    subtext.Add(text);
                                }
                                catch (Exception)
                                {
                                    continue;
                                }
                            }
                        }
                    }
                    else
                    {
                        style = GetStyle(xTextElement.Name.LocalName);
                        Text = xTextElement.Value;
                        //switch (xTextElement.Name.LocalName)
                        //{
                        //    case "strong":
                        //        Text = xTextElement.Value;
                        //        break;
                        //    case "emphasis":
                        //        Text = xTextElement.Value;
                        //        break;
                        //    case "code":
                        //        Text = xTextElement.Value;
                        //        break;
                        //    case "sub":
                        //        Text = xTextElement.Value;
                        //        break;
                        //    case "sup":
                        //        Text = xTextElement.Value;
                        //        break;
                        //    case "strikethrough":
                        //        Text = xTextElement.Value;
                        //        break;
                        //    default:
                        //        Text = xTextElement.Value;                                
                        //        break;
                        //}
                    }
                    break;
            }
        }

        private static TextStyles GetStyle(string name)
        {

            switch (name)
            {
                case "strong":
                    return TextStyles.Strong;
                case "emphasis":
                    return TextStyles.Emphasis;
                case "code":
                    return TextStyles.Code;
                case "sub":
                    return TextStyles.Sub;
                case "sup":
                    return TextStyles.Sup;
                case "strikethrough":
                    return TextStyles.Strikethrough;
                default:
                    return TextStyles.Normal;
            }
        }

        private static string GetXStyle(TextStyles style)
        {

            switch (style)
            {
                case TextStyles.Strong:
                    return "strong";
                case TextStyles.Emphasis:
                    return "emphasis";
                case TextStyles.Code:
                    return "code";
                case TextStyles.Sub:
                    return "sub";
                case TextStyles.Sup:
                    return "sup";
                case TextStyles.Strikethrough:
                    return "strikethrough";
                default:
                    return "";
            }
        }

        public XNode ToXML()
        {
            if (string.IsNullOrEmpty(Text))
            {
                XElement xChildElement = new XElement(Fb2Const.fb2DefaultNamespace + GetXStyle(style));
                foreach (StyleType child in subtext)
                {
                    xChildElement.Add(child.ToXML());
                }
                return xChildElement;
            }
            if (style != TextStyles.Normal)
            {
                XElement xStyleText = new XElement(Fb2Const.fb2DefaultNamespace + GetXStyle(style), Text);
                return xStyleText;
            }
            else
            {
                XText xText = new XText(Text);
                return xText;
            }
            
        }
    }
}
