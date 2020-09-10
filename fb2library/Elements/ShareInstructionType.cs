using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements
{
    public interface IShareInstructionElement
    {
        void Load(XElement xElement);
        XElement ToXML();
    }


    public enum GenerationInstructionEnum
    {
        Unknown,
        Require,
        Allow,
        Deny,
    }

    /// <summary>
    /// In-document instruction for generating output free and payed documents
    /// </summary>
    public class ShareInstructionType
    {
        public enum ShareModeEnum
        {
            Unknown,
            Free,
            Paid,
        }

        
        private List<IShareInstructionElement> content = new List<IShareInstructionElement>();

        /// <summary>
        /// Get/Set Modes for document sharing
        /// </summary>
        public ShareModeEnum SharedMode { get; set; }


        /// <summary>
        /// Get/Set instructions to process sections
        /// </summary>
        public GenerationInstructionEnum Instruction { get; set; }

        /// <summary>
        /// Get/Set price
        /// </summary>
        public float? Price { get; set; }

        /// <summary>
        /// Get/Set currency
        /// </summary>
        public string Currency { get; set; }

        public const string ShareInstructionElementName = "output";

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
        /// Get list of content elements
        /// </summary>
        public List<IShareInstructionElement> Content { get { return content; } }

        public void Load(XElement xElement)
        {
            if (xElement == null)
            {
                throw new ArgumentNullException("xElement");
            }
            if (xElement.Name.LocalName != ShareInstructionElementName)
            {
                throw new ArgumentException(string.Format("Wrong element name: {0} instead of {1}",xElement.Name.LocalName,ShareInstructionElementName));
            }

            content.Clear();
            IEnumerable<XElement> xElements = xElement.Elements();
            foreach (var element in xElements)
            {   
                if (element.Name.LocalName == PartShareInstructionType.PartElementName)
                {
                    PartShareInstructionType part = new PartShareInstructionType{Namespace = fileNameSpace};
                    try
                    {
                        part.Load(element);
                        content.Add(part);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(string.Format("Error loading part type: {0}",ex.Message));
                    }
                }
                else if (element.Name.LocalName == OutPutDocumentType.OutputDocumentElementName)
                {
                    OutPutDocumentType doc = new OutPutDocumentType{Namespace = fileNameSpace};
                    try
                    {
                        doc.Load(element);
                        content.Add(doc);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(string.Format("Error loading output document type: {0}", ex.Message));
                    }
                }
                else
                {
                    Debug.WriteLine(string.Format("Invalid element type encoutered {0}", element.Name.LocalName));
                }
            }

            XAttribute xSharedMode = xElement.Attribute("mode");
            if ((xSharedMode == null) || string.IsNullOrEmpty(xSharedMode.Value) )
            {
                Debug.WriteLine("mode attribute is required attribute");
            }
            else
            {
                switch (xSharedMode.Value)
                {
                    case "free":
                        SharedMode = ShareModeEnum.Free;
                        break;
                    case "paid":
                        SharedMode = ShareModeEnum.Paid;
                        break;
                    default:
                        Debug.WriteLine(string.Format("Invalid shared mode type : {0}", xSharedMode.Value));
                        break;
                }
            }


            XAttribute xIncludeAll = xElement.Attribute("include-all");
            if ((xIncludeAll == null) || string.IsNullOrEmpty(xIncludeAll.Value))
            {
                Debug.WriteLine("mode attribute is required attribute");
            }
            else
            {
                switch (xIncludeAll.Value)
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
                        Debug.WriteLine(string.Format("Invalid instruction type : {0}", xIncludeAll.Value));
                        break;
                }
            }

            Price = null;
            XAttribute xPrice = xElement.Attribute("price");
            if ((xPrice != null) && !string.IsNullOrEmpty(xPrice.Value))
            {
                float val;
                if (float.TryParse(xPrice.Value,out val))
                {
                    Price = val;
                }
            }


            Currency = null;
            XAttribute xCurrency = xElement.Attribute("currency");
            if (xCurrency != null) 
            {
                Currency = xCurrency.Value;
            }
        }

        private string GetXSharedMode()
        {
            switch (SharedMode)
            {
                case ShareModeEnum.Free:
                    return "free";
                case ShareModeEnum.Paid:
                    return "paid";
                default:
                    return "";
                //    Debug.WriteLine(string.Format("Invalid shared mode type : {0}", xSharedMode.Value));
                //    break;
            }
        }

        private string GetXIncludeAll()
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
            XElement xShareInstruction = new XElement(Fb2Const.fb2DefaultNamespace + ShareInstructionElementName);
            
            xShareInstruction.Add(new XAttribute("mode", GetXSharedMode()));
            xShareInstruction.Add(new XAttribute("include-all", GetXIncludeAll()));
            if (Price != null)
            {
                xShareInstruction.Add(new XAttribute("price", Price.ToString()));
            }
            if (Currency != null)
            {
                xShareInstruction.Add(new XAttribute("currency", Currency));
            }
            foreach (IShareInstructionElement ShareElement in content)
            {
                xShareInstruction.Add(ShareElement.ToXML());
            }


            return xShareInstruction;
        }
    }
}
