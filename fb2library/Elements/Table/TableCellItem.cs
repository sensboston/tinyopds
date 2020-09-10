using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements.Table
{
    public class TableCellItem : BaseCellElement
    {
        internal const string Fb2TableCellElementName = "td";

        protected override string GetElementName()
        {
            return Fb2TableCellElementName;
        }

        internal new string Fb2ParagraphElementName = Fb2TableCellElementName;

        internal new void Load(XElement xCell)
        {
            if (xCell == null)
            {
                throw new ArgumentNullException("xCell");
            }

            if (xCell.Name.LocalName != Fb2TableCellElementName)
            {
                throw new ArgumentException("Element of wrong type passed", "xCell");
            }

            LoadData(xCell);
            
            LoadAlign(xCell);
            LoadVAlign(xCell);

            ColSpan = null;
            XAttribute xColSpan = xCell.Attribute("colspan");
            if ((xColSpan != null) && (xColSpan.Value != null))
            {
                int res;
                if (int.TryParse(xColSpan.Value, out res))
                {
                    ColSpan = res;
                }
            }

            RowSpan = null;
            XAttribute xRowSpan = xCell.Attribute("rowspan");
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
