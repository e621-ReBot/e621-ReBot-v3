using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace e621_ReBot_v3.CustomControls
{
    public partial class DownloadVE : UserControl
    {
        internal static readonly ImageSource LoadingImage = new ImageSourceConverter().ConvertFrom(Properties.Resources.E6Image_Loading) as ImageSource;
        internal Custom_WebClient? ThumbClient;

        internal DownloadItem? _DownloadItemRef;
        internal bool _DownloadFinished = true;
        internal bool _AlreadyCopied = true;
        public DownloadVE()
        {
            InitializeComponent();
        }

        private void DownloadItem_UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (Window_Main._RefHolder != null) IndexDisplay.Text = $"#{Window_Main._RefHolder.Download_DownloadVEPanel.Children.IndexOf(this) + 1}";
        }

        internal void DownloadStartup()
        {
            cThumbnail_Image.Source = LoadingImage;
            _DownloadFinished = false;
            _AlreadyCopied = false;
        }

        internal void DownloadFinish()
        {
            _AlreadyCopied = true;
            DownloadProgress.Value = 0;
            ConversionProgress.Value = 0;
            cThumbnail_Image.Source = null;
            _DownloadItemRef = null;
            ThumbClient?.CancelAsync();
        }
    }
}