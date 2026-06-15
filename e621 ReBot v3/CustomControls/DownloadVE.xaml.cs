using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace e621_ReBot_v3.CustomControls
{
    public partial class DownloadVE : UserControl
    {
        internal static readonly ImageSource LoadingImage = CreateLoadingImage();
        private static ImageSource CreateLoadingImage()
        {
            BitmapImage BitmapImageTemp = new BitmapImage();

            using (MemoryStream MemoryStreamTemp = new MemoryStream(Properties.Resources.E6Image_Loading))
            {
                BitmapImageTemp.BeginInit();
                BitmapImageTemp.CacheOption = BitmapCacheOption.OnLoad;
                BitmapImageTemp.StreamSource = MemoryStreamTemp;
                BitmapImageTemp.EndInit();
            }

            BitmapImageTemp.Freeze(); // important for performance + thread safety
            return BitmapImageTemp;
        }

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

        private static readonly SolidColorBrush DownloadBrush = new SolidColorBrush(Colors.Orange);

        private static readonly SolidColorBrush ConversionBrush = new SolidColorBrush(Colors.DarkOrchid);
        internal void DownloadFinish()
        {
            _AlreadyCopied = true;
            DownloadProgress.Value = 0;
            DownloadProgress.Foreground = DownloadBrush;
            ConversionProgress.Value = 0;
            ConversionProgress.Foreground = ConversionBrush;
            cThumbnail_Image.Source = null;
            _DownloadItemRef = null;
            ThumbClient?.CancelAsync();
        }
    }
}