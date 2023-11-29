using System.Windows.Controls;

namespace e621_ReBot_v3.CustomControls
{
    internal class Custom_TreeView : TreeView
    {
        internal TreeViewItem? FindTreeViewItemByHeader(string Header)
        {
            foreach (TreeViewItem TreeViewItemTemp in Items)
            {
                if (TreeViewItemTemp.Header.Equals(Header))
                {
                    return TreeViewItemTemp;
                }

                if (TreeViewItemTemp.HasItems)
                {
                    FindTreeViewItemChildByHeader(TreeViewItemTemp, Header);
                }
            }
            return null;
        }

        private TreeViewItem? FindTreeViewItemChildByHeader(TreeViewItem TreeViewItemParent, string Header)
        {
            foreach (TreeViewItem TreeViewItemTemp in TreeViewItemParent.Items)
            {
                if (TreeViewItemTemp.Header.Equals(Header))
                {
                    return TreeViewItemTemp;
                }

                if (TreeViewItemTemp.HasItems)
                {
                    FindTreeViewItemChildByHeader(TreeViewItemTemp, Header);
                }
            }
            return null;
        }

        internal TreeViewItem? FindTreeViewItemByName(string Name)
        {
            foreach (TreeViewItem TreeViewItemTemp in Items)
            {
                if (TreeViewItemTemp.Name.Equals(Name))
                {
                    return TreeViewItemTemp;
                }

                if (TreeViewItemTemp.HasItems)
                {
                    FindTreeViewItemChildByName(TreeViewItemTemp, Name);
                }
            }
            return null;
        }

        private TreeViewItem? FindTreeViewItemChildByName(TreeViewItem TreeViewItemParent, string Name)
        {
            foreach (TreeViewItem TreeViewItemTemp in TreeViewItemParent.Items)
            {
                if (TreeViewItemTemp.Name.Equals(Name))
                {
                    return TreeViewItemTemp;
                }

                if (TreeViewItemTemp.HasItems)
                {
                    FindTreeViewItemChildByName(TreeViewItemTemp, Name);
                }
            }
            return null;
        }
    }
}
