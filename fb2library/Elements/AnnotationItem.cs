using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FB2Library.Elements.Poem;
using FB2Library.Elements.Table;

namespace FB2Library.Elements
{
    public class AnnotationItem : AnnotationType
    {
        public AnnotationItem()
        {
            ElementName = Fb2AnnotationItemName;
        }
        internal const string Fb2AnnotationItemName = "annotation";

    }
}
