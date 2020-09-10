using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FB2Library.Elements
{
    public class TextAuthorItem : ParagraphItem
    {
        protected override string GetElementName()
        {
            return Fb2TextAuthorElementName;
        }

        internal const string Fb2TextAuthorElementName = "text-author";
    }
}