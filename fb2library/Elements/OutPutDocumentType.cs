using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements
{
    public class OutPutDocumentType : IShareInstructionElement
    {
        private readonly List<ShareInstructionType> parts = new List<ShareInstructionType>();

        public const string OutputDocumentElementName = "output-document-class";

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
        /// Get list of the instruction parts
        /// </summary>
        public List<ShareInstructionType> Parts { get { return parts; } }

        /// <summary>
        /// Get/Set Name attribute
        /// </summary>
        public string Name { get; set;}

        /// <summary>
        /// Get/Set document part creation type
        /// </summary>
        public GenerationInstructionEnum Create { get; set; }


        /// <summary>
        /// Get/Set price
        /// </summary>
        public float? Price { get; set; }

        public void Load(XElement xElement)
        {
            if (xElement == null)
            {
                throw new ArgumentNullException("xElement");
            }

            parts.Clear();
            IEnumerable<XElement> xParts = xElement.Elements(fileNameSpace + "part");
            foreach (var xPart in xParts)
            {
                ShareInstructionType part = new ShareInstructionType {Namespace = fileNameSpace};
                try
                {
                    part.Load(xPart);
                    parts.Add(part);
                }
                catch (Exception)
                {
                    continue;
                }
            }

            Name = null;
            XAttribute xName = xElement.Attribute("name");
            if (xName == null)
            {
                throw new Exception("name is required attribute - absent");
            }
            Name = xName.Value;


            Create = GenerationInstructionEnum.Unknown;
            XAttribute xCreate = xElement.Attribute("create");
            if (xCreate != null)
            {
                switch (xCreate.Value)
                {
                    case "require":
                        Create = GenerationInstructionEnum.Require;
                        break;
                    case "allow":
                        Create = GenerationInstructionEnum.Allow;
                        break;
                    case "deny":
                        Create = GenerationInstructionEnum.Deny;
                        break;
                    default:
                        Debug.WriteLine(string.Format("Invalid instruction type : {0}", xCreate.Value));
                        break;
                }                
            }

            Price = null;
            XAttribute xPrice = xElement.Attribute("price");
            if ((xPrice != null) && !string.IsNullOrEmpty(xPrice.Value))
            {
                float val;
                if (float.TryParse(xPrice.Value, out val))
                {
                    Price = val;
                }
            }

        }
        private string GetXCreate()
        {
            switch (Create)
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
            XElement xOutputDocumentClass = new XElement(Fb2Const.fb2DefaultNamespace + OutputDocumentElementName);
            xOutputDocumentClass.Add(new XAttribute("name", Name));
            if (Create != GenerationInstructionEnum.Unknown)
            {
                xOutputDocumentClass.Add(new XAttribute("create", GetXCreate()));
            }
            if (Price != null)
            {
                xOutputDocumentClass.Add(new XAttribute("price", Price.ToString()));
            }
            foreach (ShareInstructionType PartElement in parts)
            {
                xOutputDocumentClass.Add(PartElement.ToXML());
            }
            return xOutputDocumentClass;
        }
    }
}
