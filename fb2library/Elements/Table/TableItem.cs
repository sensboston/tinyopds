using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements.Table
{
    public class TableItem : IFb2TextItem
    {
        private List<TableRowItem> rows = new List<TableRowItem>();

        public string ID { get; set; }
        public string Style { get; set; }
        public List<TableRowItem> Rows { get { return rows; } }



        internal const string Fb2TableElementName = "table";

        internal void Load(XElement xTable)
        {
            rows.Clear();
            if (xTable == null)
            {
                throw new ArgumentNullException("xTable");
            }

            if (xTable.Name.LocalName != Fb2TableElementName)
            {
                throw new ArgumentException("Element of wrong type passed", "xTable");
            }

            IEnumerable<XElement> xRows = xTable.Elements(xTable.Name.Namespace + TableRowItem.Fb2TableRowElementName);
            foreach (var row in xRows)
            {
                TableRowItem rowItem = new TableRowItem();
                try
                {
                    rowItem.Load(row);
                    Rows.Add(rowItem);
                }
                catch (Exception)
                {
                    continue;
                }
            }

            Style = null;
            XAttribute xStyle = xTable.Attribute("style");
            if ((xStyle != null) && (xStyle.Value != null))
            {
                Style = xStyle.Value;
            }

            ID = null;
            XAttribute xID = xTable.Attribute("id");
            if ((xID != null) && (xID.Value != null))
            {
                ID = xID.Value;
            }

            

        }

        public XNode ToXML()
        {
            XElement xTable = new XElement(Fb2Const.fb2DefaultNamespace + Fb2TableElementName);
            if (!string.IsNullOrEmpty(ID))
            {
                xTable.Add(new XAttribute("id", ID));
            }
            if (!string.IsNullOrEmpty(Style))
            {
                xTable.Add(new XAttribute("style", Style));
            }
            foreach (TableRowItem RowItem in rows)
            {
                xTable.Add(RowItem.ToXML());
            }
            return xTable;
        }
    }
}
