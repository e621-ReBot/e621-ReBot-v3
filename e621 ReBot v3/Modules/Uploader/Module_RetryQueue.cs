using System.Windows.Controls;
using e621_ReBot_v3.CustomControls;

namespace e621_ReBot_v3.Modules.Uploader
{
    internal class Module_RetryQueue
    {
        internal static MediaItemList _2Retry_MediaItems = new MediaItemList();
        internal static void MoveItem2RetryQueue(MediaItem MediaItem2Move)
        {
            lock (Module_Uploader._2Upload_MediaItems)
            {
                lock (_2Retry_MediaItems)
                {
                    Module_Uploader._2Upload_MediaItems.Remove(MediaItem2Move);
                    _2Retry_MediaItems.Add(MediaItem2Move);
                    Window_Main._RefHolder.Dispatcher.Invoke(() => { RetryTreeView_CreateJob(MediaItem2Move); });
                }
            }
        }

        private static void RetryTreeView_CreateJob(MediaItem MediaItemRef)
        {
            TreeViewItem? TreeViewItemParent = new TreeViewItem
            {
                Header = MediaItemRef.Grab_MediaURL,
                Tag = MediaItemRef,
            };

            if (Module_Credit.CanReplace && MediaItemRef.UP_Inferior_ID != null)
            {
                TreeViewItemParent.Items.Add(new TreeViewItem { Header = "Replace Inferior" });
            }
            else
            {
                TreeViewItemParent.Items.Add(new TreeViewItem { Header = "Upload" });
                if (MediaItemRef.UP_Inferior_HasNotes != null) TreeViewItemParent.Items.Add(new TreeViewItem { Header = "Copy Notes" });
                if (MediaItemRef.UP_Inferior_Children != null) TreeViewItemParent.Items.Add(new TreeViewItem { Header = "Move Children" });
                if (MediaItemRef.UP_Inferior_ID != null) TreeViewItemParent.Items.Add(new TreeViewItem { Header = "Flag Inferior" });
            }
            Window_Main._RefHolder.Retry_TreeView.Items.Add(TreeViewItemParent);
            Window_Main._RefHolder.Retry_TextBlock.Text = $"Retry Queue{(Window_Main._RefHolder.Retry_TreeView.Items.Count > 0 ? $" ({Window_Main._RefHolder.Retry_TreeView.Items.Count})" : null)}";
        }

        internal static void MoveItem2UploadQueue(MediaItem MediaItem2Move)
        {
            lock (_2Retry_MediaItems)
            {
                lock (Module_Uploader._2Upload_MediaItems)
                {
                    _2Retry_MediaItems.Remove(MediaItem2Move);
                    Module_Uploader._2Upload_MediaItems.Add(MediaItem2Move);
                    Window_Main._RefHolder.Dispatcher.Invoke(() => { Module_Uploader.UploadTreeView_CreateJob(MediaItem2Move); });
                }
            }
        }
    }
}