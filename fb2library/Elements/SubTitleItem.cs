using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FB2Library.Elements
{
    public class SubTitleItem : ParagraphItem
    {
        protected override string GetElementName()
        {
            return Fb2SubtitleElementName;
        }

        internal const string Fb2SubtitleElementName = "subtitle";
    }
}
