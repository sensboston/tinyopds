using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements
{
    public enum TextStyles
    {
        Normal = 0,
        Strong, // <strong>
        Emphasis, // <emphasis>
        Code, // <code>
        Sub, // <sub>
        Sup, // <sup>
        Strikethrough, //<strikethrough>
    }


    public interface  IFb2TextItem
    {
        XNode ToXML();
    }
}
