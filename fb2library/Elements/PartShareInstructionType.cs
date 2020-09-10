using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements
{
    /// <summary>
    /// Pointer to cpecific document section, explaining how to deal with it
    /// </summary>
    public class PartShareInstructionType : IShareInstructionElement
    {
        private readonly XNamespace lNamespace = @"http://www.w3.org/1999/xlink";

        public const string PartElementName = "part";

        public string Type { get; set; }

        public string HRef { get; set; }


        public GenerationInstructionEnum Instruction { get; set; }


        private XNamespace fileNameSpace = XNamespace.None;

        /// <summary>
        /// XML namespace used to read the document
        /// </summary>
        public XNamespace Namespace
        {
            set { fileNameSpace = value; }
            get { return fileNameSpace; }
        }

        public void Load(XElement xElement)
        {
            if (xElement == null)
            {
                throw new ArgumentNullException("xElement");
            }

            Type = null;
            XAttribute xType = xElement.Attribute(lNamespace +  "type");
            if (xType != null)
            {
                Type = xType.Value;
            }

            HRef = null;
            XAttribute xHRef = xElement.Attribute(lNamespace + "href");
            if (xHRef == null)
            {
                throw new Exception("href is required attribute - absent");
            }
            HRef = xHRef.Value;


            XAttribute xIncude = xElement.Attribute("include");
            if (xIncude == null)
            {
                throw new Exception("include is required attribute - absent");
            }
            switch (xIncude.Value)
            {
                case "require":
                    Instruction = GenerationInstructionEnum.Require;
                    break;
                case "allow":
                    Instruction = GenerationInstructionEnum.Allow;
                    break;
                case "deny":
                    Instruction = GenerationInstructionEnum.Deny;
                    break;
                default:
                    Debug.WriteLine(string.Format("Invalid instruction type : {0}", xIncude.Value));
                    break;
            }

        }

        private string GetXInclude()
        {
            switch (Instruction)
            {
                case GenerationInstructionEnum.Require:
                    return "require";
                case GenerationInstructionEnum.Allow:
                    return "allow";
                case GenerationInstructionEnum.Deny:
                    return "deny";
                default:
                    return "";
                //Debug.WriteLine(string.Format("Invalid instruction type : {0}", xIncludeAll.Value));
                //break;
            }
        }
        public XElement ToXML()
        {
            XElement xPart = new XElement(Fb2Const.fb2DefaultNamespace + PartElementName);
            if (!string.IsNullOrEmpty(Type))
            {
                xPart.Add(new XAttribute(lNamespace + "type", Type));
            }
            xPart.Add(new XAttribute(lNamespace + "href", HRef));
            xPart.Add(new XAttribute("include", GetXInclude()));

            return xPart;
        }
    }
}
