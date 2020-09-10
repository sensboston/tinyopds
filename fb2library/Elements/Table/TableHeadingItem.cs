using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements.Table
{
    public class TableHeadingItem : BaseCellElement
    {
        internal const string Fb2TableHeadingElementName = "th";

        protected override string GetElementName()
        {
            return Fb2TableHeadingElementName;
        }

        internal new void Load(XElement xHeadingCell)
        {
            if (xHeadingCell == null)
            {
                throw new ArgumentNullException("xHeadingCell");
            }

            if (xHeadingCell.Name.LocalName != Fb2TableHeadingElementName)
            {
                throw new ArgumentException("Element of wrong type passed", "xHeadingCell");
            }

            LoadData(xHeadingCell);

            LoadAlign(xHeadingCell);
            LoadVAlign(xHeadingCell);

            ColSpan = null;
            XAttribute xColSpan = xHeadingCell.Attribute("colspan");
            if ((xColSpan != null)&&(xColSpan.Value != null))
            {
                int res;
                if(int.TryParse(xColSpan.Value,out res))
                {
                    ColSpan = res;
                }
            }

            RowSpan = null;
            XAttribute xRowSpan = xHeadingCell.Attribute("rowspan");
            if ((xRowSpan != null) && (xRowSpan.Value != null))
            {
                int res;
                if (int.TryParse(xRowSpan.Value, out res))
                {
                    RowSpan = res;
                }
            }

        }



    }
}
