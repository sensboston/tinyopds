using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements
{
    /// <summary>
    /// A human readable date, maybe not exact, with an optional computer readable variant
    /// </summary>
    public class DateItem : IFb2TextItem
    {
        /// <summary>
        /// Get/Set Date as Text
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Get/Set Date in computer readable form
        /// </summary>
        public DateTime DateValue{get;set;}

        /// <summary>
        /// Get/Set language
        /// </summary>
        public string Language { get; set; }


        internal const string Fb2DateElementName = "date";

        internal void Load(XElement xDate)
        {
            if (xDate == null)
            {
                throw new ArgumentNullException("xDate");
            }

            if (xDate.Name.LocalName != Fb2DateElementName)
            {
                throw new ArgumentException("Element of wrong type passed", "xDate");
            }

            if (xDate.Value != null)
            {
                Text = xDate.Value;
            }

            XAttribute xDateValue = xDate.Attribute("value");
            if ((xDateValue != null)&&(!string.IsNullOrEmpty(xDateValue.Value)))
            {
                try
                {
                    DateValue = DateTime.Parse(xDateValue.Value);
                }
                catch (Exception)
                {
                                     
                }
            }

            Language = null;
            XAttribute xLang = xDate.Attribute(XNamespace.Xml + "lang");
            if (xLang != null && string.IsNullOrEmpty(xLang.Value))
            {
                Language = xLang.Value;
            }
        }

        public XNode ToXML()
        {
            XElement xDate = new XElement(Fb2Const.fb2DefaultNamespace + Fb2DateElementName);
            if (!string.IsNullOrEmpty(Text))
            {
                xDate.Value = Text;
            }
            if (!string.IsNullOrEmpty(Language))
            {
                xDate.Add(new XAttribute(XNamespace.Xml + "lang", Language));
            }
            if (!DateValue.Equals(DateTime.MinValue))
            {
                xDate.Add(new XAttribute("value", DateValue.ToShortDateString()));
            }
            return xDate;
        }
    }
}
