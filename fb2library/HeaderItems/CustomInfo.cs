using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FB2Library.Elements;

namespace FB2Library.HeaderItems
{
    public class ItemCustomInfo : CustomTextFieldType
    {
        public const string CustomInfoElementName = "custom-info";

        private XNamespace fileNameSpace = XNamespace.None;

        /// <summary>
        /// XML namespace used to read the document
        /// </summary>
        public XNamespace Namespace
        {
            set { fileNameSpace = value; }
            get { return fileNameSpace; }
        }


        public override void Load(XElement xElement)
        {
            if (xElement == null)
            {
                throw new ArgumentNullException("xElement");
            }
            base.Load(xElement);
        }

        public XElement ToXML()
        {
            XElement xCustomInfo = new XElement(Fb2Const.fb2DefaultNamespace + CustomInfoElementName);
            xCustomInfo.Add(new XAttribute("info-type",InfoType));
            xCustomInfo.Value = Text;
            return xCustomInfo;
        }
    }
}
