using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace FB2Library.Elements
{
    public class EmptyLineItem : IFb2TextItem
    {
        internal const string Fb2EmptyLineElementName = "empty-line";

        public override string ToString()
        {
            return "\n";
        }
        public XNode ToXML()
        {
            return new XElement(Fb2Const.fb2DefaultNamespace + Fb2EmptyLineElementName);
        }

    }
}
