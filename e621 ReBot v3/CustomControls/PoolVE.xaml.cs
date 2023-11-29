using System.IO;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace e621_ReBot_v3.CustomControls
{
    public partial class PoolVE : UserControl
    {
        private PoolItem? PoolItemRef;

        internal PoolItem? _PoolItemRef
        {
            get 
            { 
                return PoolItemRef; 
            }   
            set 
            { 
                PoolItemRef = value;
                if (PoolItemRef != null ) 
                {
                    if (PoolItemRef.Name != null) PVE_Title.Text = PoolItemRef.Name;
                    if (PoolItemRef.Thumbnail != null) 
                    {
                        using (MemoryStream MemoryStreamTemp = new MemoryStream(Convert.FromBase64String(PoolItemRef.Thumbnail)))
                        {
                            BitmapImage? DownloadedImage = new BitmapImage();
                            DownloadedImage.BeginInit();
                            DownloadedImage.CacheOption = BitmapCacheOption.OnLoad;
                            DownloadedImage.StreamSource = MemoryStreamTemp;
                            DownloadedImage.EndInit();
                            DownloadedImage.StreamSource = null;

                            PVE_CoverImage.Source = DownloadedImage;
                            DownloadedImage.Freeze();
                        }
                    }
                }
            }  
        }

        public PoolVE()
        {
            InitializeComponent();
        }

        private void PoolWatcher_RemovePool_Click(object sender, RoutedEventArgs e)
        {
            MenuItem MenuItemClicked = (MenuItem)sender;
            ContextMenu ContextMenuParent = (ContextMenu)MenuItemClicked.Parent;
            PoolVE PoolVETarget = (PoolVE)ContextMenuParent.PlacementTarget;

            lock (AppSettings.PoolWatcher)
            {
                AppSettings.PoolWatcher.Remove(PoolVETarget._PoolItemRef);
            }

            WrapPanel WrapPanelParent = (WrapPanel)PoolVETarget.Parent;
            WrapPanelParent.Children.Remove(PoolVETarget);
            if (WrapPanelParent.Children.Count == 0)
            {
                Window_PoolWatcher._RefHolder.Close();
                Window_Main._RefHolder.SettingsButton_PoolWatcher.IsEnabled = false;
                return;
            }
            Window_PoolWatcher._RefHolder.SortPoolWatcher_StackPanel.IsEnabled = WrapPanelParent.Children.Count > 1;
        }
    }
}
