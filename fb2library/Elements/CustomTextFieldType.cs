using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements
{
    public class CustomTextFieldType : TextFieldType 
    {
        /// <summary>
        /// Get/Set info-type attribute
        /// </summary>
        public string InfoType { get; set; }


        public override void Load(XElement xElement)
        {
            base.Load(xElement);

            InfoType = null;
            XAttribute xInfoType = xElement.Attribute("info-type");
            if (xInfoType == null)
            {
                //throw new Exception("info-type attribute required for custom info element");
            }
            else
            {
                InfoType = xInfoType.Value;
            }
            
        }

        
    }
}
