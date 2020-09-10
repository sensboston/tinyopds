using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements.Table
{

    public class TableRowItem
    {

        private List<ICellElement> cells = new List<ICellElement>();


        public string ID { get; set; }
        public List<ICellElement> Cells { get { return cells; } }
        public string Align { get; set; }
        public string Style { get; set; }


        internal const string Fb2TableRowElementName = "tr";

        internal void Load(XElement xRow)
        {
            if (xRow == null)
            {
                throw new ArgumentNullException("xRow");
            }

            if (xRow.Name.LocalName != Fb2TableRowElementName)
            {
                throw new ArgumentException("Element of wrong type passed", "xRow");
            }

            IEnumerable<XElement> xCells = xRow.Elements();
            foreach (var cell in xCells)
            {
                switch (cell.Name.LocalName)
                {
                    case TableHeadingItem.Fb2TableHeadingElementName:
                        TableHeadingItem heading = new TableHeadingItem();
                        try
                        {
                            heading.Load(cell);
                            cells.Add(heading);

                        }
                        catch (Exception)
                        {

                        }
                        break;
                    case TableCellItem.Fb2TableCellElementName:
                        TableCellItem tableCell = new TableCellItem();
                        try
                        {
                            tableCell.Load(cell);
                            cells.Add(tableCell);
                        }
                        catch (Exception)
                        {

                        }
                        break;
                    default:
                        Debug.WriteLine(string.Format("TableRowItem.Load - Unknown cell type {0}"), cell.Name.LocalName);
                        break;
                }
            }


            ID = null;
            XAttribute xID = xRow.Attribute("id");
            if ((xID != null) && (xID.Value != null))
            {
                ID = xID.Value;
            }


            Style = null;
            XAttribute xStyle = xRow.Attribute("style");
            if ((xStyle != null) && (xStyle.Value != null))
            {
                Style = xStyle.Value;
            }

            Align = null;
            XAttribute xAlign = xRow.Attribute("align");
            if ((xAlign != null) && (xAlign.Value != null))
            {
                Align = xAlign.Value;
            }
        }

        public XElement ToXML()
        {
            XElement xRow = new XElement(Fb2Const.fb2DefaultNamespace + Fb2TableRowElementName);
            if (!string.IsNullOrEmpty(ID))
            {
                xRow.Add(new XAttribute("id", ID));
            }
            if (!string.IsNullOrEmpty(Style))
            {
                xRow.Add(new XAttribute("style", Style));
            }
            if (!string.IsNullOrEmpty(Align))
            {
                xRow.Add(new XAttribute("align", Align));
            }
            foreach (ICellElement cellItem in cells)
            {
                xRow.Add(((ParagraphItem)cellItem).ToXML());
            }

            return xRow;
        }
    }
}
