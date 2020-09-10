using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FB2Library.Elements.Poem
{
    public class VPoemParagraph : ParagraphItem
    {
        protected override string GetElementName()
        {
            return Fb2VParagraphItemName;
        }


        internal const string Fb2VParagraphItemName = "v";
    }
}
