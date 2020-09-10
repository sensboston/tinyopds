using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements
{
    public class SequenceType
    {
        public const string SequenceElementName = "sequence";

        private readonly List<SequenceType> content = new List<SequenceType>();

        private XNamespace fileNameSpace = XNamespace.None;


        /// <summary>
        /// XML namespace used to read the document
        /// </summary>
        public XNamespace Namespace
        {
            set { fileNameSpace = value; }
            get { return fileNameSpace; }
        }

        /// <summary>
        /// Get/Set name of the sequence
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Get/Set sequence number
        /// </summary>
        public int? Number { get; set; }


        /// <summary>
        /// Get/Set language
        /// </summary>
        public string Language { get; set; }


        /// <summary>
        /// Get subsection list
        /// </summary>
        public List<SequenceType> SubSections { get { return content; } }

        public void Load(XElement xElement)
        {
            if (xElement == null)
            {
                throw new ArgumentNullException("xElement");
            }

            if (xElement.Name.LocalName != SequenceElementName)
            {
                throw new ArgumentException("Element of wrong type passed", "xElement");
            }

            // read all subsecquences
            content.Clear();
            IEnumerable<XElement> subElements = xElement.Elements(fileNameSpace + SequenceElementName);
            foreach (var element in subElements)
            {
                var subElement = new SequenceType();
                try
                {
                    subElement.Load(element);
                    content.Add(subElement);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error reading sequence element: {0}",ex.Message));
                    continue;
                }
            }

            // read "name" attribute
            Name = null;
            XAttribute xName = xElement.Attribute("name");
            if (xName != null && (!string.IsNullOrEmpty(xName.Value)))
            {
                Name = xName.Value;

                // read number attribute
                Number = null;
                XAttribute xNumber = xElement.Attribute("number");
                if ((xNumber != null) && (!string.IsNullOrEmpty(xNumber.Value)))
                {
                    int value;
                    if (int.TryParse(xNumber.Value, out value))
                    {
                        Number = value;
                    }
                }

                Language = null;
                XAttribute xLang = xElement.Attribute(XNamespace.Xml + "lang");
                if (xLang != null && !string.IsNullOrEmpty(xLang.Value))
                {
                    Language = xLang.Value;
                }
            }
            else
            {
                //throw new Exception("Name attribute in sequence is required!");
            }
        }

        public XElement ToXML()
        {
            XElement xSequence = new XElement(Fb2Const.fb2DefaultNamespace + SequenceElementName);
            if (!string.IsNullOrEmpty(Name))
            {
                xSequence.Add(new XAttribute("name", Name));
            }
            if (Number != null)
            { 
                xSequence.Add(new XAttribute("number",Number));
            }
            if (!string.IsNullOrEmpty(Language))
            {
                xSequence.Add(new XAttribute(XNamespace.Xml + "lang", Language));
            }
            foreach(SequenceType SubSeq in content)
            {
                xSequence.Add(SubSeq.ToXML());
            }

            return xSequence;
        }
    }
}
