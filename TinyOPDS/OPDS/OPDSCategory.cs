using System.Collections.Generic;
using System.Windows.Forms;

namespace TinyOPDS.OPDS
{
    // Class to represent OPDS catalog category
    public class OPDSCategory
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string SortBy { get; set; }
        public bool Enabled { get; set; }
        public bool GroupByFirstLetter { get; set; }
        public int MaxItems { get; set; }
        public string GenreFilter { get; set; }
        public string LanguageFilter { get; set; }
        public int Order { get; set; }
        public List<OPDSCategory> SubCategories { get; set; }

        public OPDSCategory()
        {
            SubCategories = new List<OPDSCategory>();
            Enabled = true;
            MaxItems = 50;
            SortBy = "Title ascending";
            GroupByFirstLetter = false;
        }

        public TreeNode ToTreeNode()
        {
            var node = new TreeNode(Name)
            {
                Tag = this,
                Checked = Enabled
            };

            foreach (var subCategory in SubCategories)
            {
                node.Nodes.Add(subCategory.ToTreeNode());
            }

            return node;
        }
    }
}
