using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements
{
    /// <summary>
    /// Represents FB2 stylesheet element
    /// </summary>
    public class StyleElement
    {
        public const string StyleElementName = "stylesheet";
        /// <summary>
        /// Get/Set Style Value
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Get/Set type attribute
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Load element data from the node
        /// </summary>
        /// <param name="xElement"></param>
        public void Load(XElement xElement)
        {
            if (xElement == null)
            {
                throw new ArgumentNullException("xElement");
            }
            if (xElement.Name.LocalName != StyleElementName)
            {
                throw new ArgumentException(string.Format("The element is of type {0} while StyleElement accepts only {1} types",xElement.Name.LocalName,StyleElementName));
            }

            Value = string.Empty;
            if (!string.IsNullOrEmpty(xElement.Value))
            {
                Value = xElement.Value;
            }

            Type = string.Empty;
            XAttribute xType = xElement.Attribute("type");
            if (xType == null || string.IsNullOrEmpty(xType.Value))
            {
                throw new Exception("Type attribute is rewuired by standard");
            }
            Type = xType.Value;
        }

        public XElement ToXML()
        {
            if (Type == null || string.IsNullOrEmpty(Type))
            {
                throw new Exception("Type attribute is rewuired by standard");
            }
            XElement xstyle = new XElement(Fb2Const.fb2DefaultNamespace + StyleElementName, new XAttribute("type", Type));
            if (!string.IsNullOrEmpty(Value))
            {
                xstyle.Value = Value;
            }
            return xstyle;
        }
    }
}
