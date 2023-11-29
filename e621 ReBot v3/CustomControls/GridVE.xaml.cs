using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using e621_ReBot_v3.Modules;

namespace e621_ReBot_v3.CustomControls
{
    public partial class GridVE : UserControl
    {
        private static readonly ImageSource LoadingImage = new ImageSourceConverter().ConvertFrom(Properties.Resources.E6Image_Loading) as ImageSource;
        internal static ushort _Width = 200;
        internal static ushort _Height = 200;
        internal static Thickness MarginSize = new Thickness(2, 4, 2, 4);

        internal bool _isSelected = false;
        internal MediaItem? _MediaItemRef;
        public GridVE()
        {
            InitializeComponent();
            Width = _Width;
            Height = _Height;
            Margin = MarginSize;
            IsUploaded_DockPanel.Visibility = Visibility.Hidden;
        }

        internal GridVE(int ScaleSize) : this()
        {
            ((ScaleTransform)((TransformGroup)RenderTransform).Children[0]).ScaleX = ScaleSize;
            ((ScaleTransform)((TransformGroup)RenderTransform).Children[0]).ScaleY = ScaleSize;
        }

        internal void LoadMediaItem()
        {
            if (_MediaItemRef != null)
            {
                cIsSuperior_Polygon.Visibility = _MediaItemRef.UP_Inferior_ID == null ? Visibility.Hidden : Visibility.Visible;
                if (_MediaItemRef.Grid_Thumbnail == null)
                {
                    if (_MediaItemRef.Grid_ThumbnailDLStart == false) Module_Grabber.Grab_Thumbnail(_MediaItemRef);
                    cThumbnail_Image.Source = LoadingImage;
                }
                else
                {
                    Module_Grabber.Grab_MakeThumbnailInfoText(_MediaItemRef);
                    cThumbnail_Image.Source = _MediaItemRef.Grid_Thumbnail;
                }
                GridVEIsLoading = true;
                cDL_CheckBox.IsChecked = _MediaItemRef.DL_Queued;
                cUpload_CheckBox.IsChecked = _MediaItemRef.UP_Queued;
                cUpload_CheckBox.IsEnabled = _MediaItemRef.UP_UploadedID == null || _MediaItemRef.Grid_MediaTooBig == true;
                IsUploaded_SetText(_MediaItemRef.UP_UploadedID);
                ChangeRating(_MediaItemRef.UP_Rating);
                GridVEIsLoading = false;
            }
        }

        internal void LoadImage()
        {
            Module_Grabber.Grab_MakeThumbnailInfoText(_MediaItemRef);
            cThumbnail_Image.Source = _MediaItemRef.Grid_Thumbnail;
        }

        private void RemoveControl(object sender, EventArgs e)
        {
            Window_Main._RefHolder.DownloadCounterChange(_MediaItemRef.DL_Queued ? -1 : 0);
            Window_Main._RefHolder.UploadCounterChange(_MediaItemRef.UP_Queued ? -1 : 0);
            lock (Module_Grabber._Grabbed_MediaURLs)
            {
                Module_Grabber._Grabbed_MediaURLs.Remove(_MediaItemRef.Grab_MediaURL);
            }
            lock (Module_Grabber._Grabbed_MediaItems)
            {
                Module_Grabber._Grabbed_MediaItems.Remove(_MediaItemRef);
            }
            Window_Main._RefHolder.Grid_GridVEPanel.Children.Remove(this);

            bool NoAnimationPopulation = false;
            if (Module_Grabber._Grabbed_MediaItems.Count <= Window_Main._RefHolder.Grid_ItemStartIndex)
            {
                if (Module_Grabber._Grabbed_MediaItems.Count > 0) Window_Main._RefHolder.Grid_ItemStartIndex -= Window_Main._RefHolder.Grid_ItemLimit;
                NoAnimationPopulation = true;
            }

            Window_Main._RefHolder.Grid_Populate(NoAnimationPopulation);
            if (Window_Preview._RefHolder != null)
            {
                if (ReferenceEquals(_MediaItemRef, Window_Preview._RefHolder.MediaItemHolder))
                {
                    Window_Preview._RefHolder.Close();
                }
                else
                {
                    if (!Window_Preview._RefHolder.LoadAllImagesMod) Window_Preview._RefHolder.UpdateNavButtons();
                }
            }
        }

        // - - - - - - - - - - - - - - - -

        private void GridVE_UserControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                _isSelected = true;
                BorderHighlight.BorderBrush = new SolidColorBrush(Colors.Orange);
                BorderHighlight.BorderThickness = new Thickness(2);
                if (Window_Preview._RefHolder == null)
                {
                    if (Window_Main._RefHolder.InitStarted)
                    {
                        new Window_Preview().Show();
                        Window_Preview._RefHolder.Nav2URL(_MediaItemRef);
                        Window_Preview._RefHolder.Activate();
                    }
                    else
                    {
                        MessageBox.Show(Window_Main._RefHolder, "Please initialize the browser first.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    Window_Preview._RefHolder.Nav2URL(_MediaItemRef);
                    Window_Preview._RefHolder.Activate();
                }
            }
            else
            {
                _isSelected = !_isSelected;
                BorderHighlight.BorderBrush = new SolidColorBrush(_isSelected ? Colors.Orange : Colors.RoyalBlue);
                BorderHighlight.BorderThickness = new Thickness(_isSelected ? 2 : 1);
            }
            Window_Main._RefHolder.ChangeGridVESelection(this);
        }

        internal void ToggleSelection()
        {
            MouseButtonEventArgs MouseButtonEventArgsTemp = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = Mouse.MouseDownEvent,
                Source = this
            };
            RaiseEvent(MouseButtonEventArgsTemp);
        }

        internal void ChangeRating(string NewRating)
        {
            _MediaItemRef.UP_Rating = NewRating;
            switch (NewRating)
            {
                case "S":
                    {
                        cRating_Polygon.Fill = new SolidColorBrush(Colors.LimeGreen);
                        break;
                    }
                case "Q":
                    {
                        cRating_Polygon.Fill = new SolidColorBrush(Colors.Yellow);
                        break;
                    }
                default:
                    {
                        cRating_Polygon.Fill = new SolidColorBrush(Colors.Red);
                        break;
                    }
            }
        }

        // - - - - - - - - - - - - - - - -

        internal bool GridVEIsLoading = true;
        private void CDL_CheckBox_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (!GridVEIsLoading)
            {
                _MediaItemRef.DL_Queued = cDL_CheckBox.IsChecked ?? false;
                Window_Main._RefHolder.DownloadCounterChange(_MediaItemRef.DL_Queued ? 1 : -1);
            }
        }

        private void CUpload_CheckBox_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            cUpload_CheckBox.Visibility = cUpload_CheckBox.IsEnabled ? Visibility.Visible : Visibility.Hidden;
            cTagWarning_TextBlock.Visibility = cUpload_CheckBox.IsEnabled && _MediaItemRef.UP_Tags.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().Count() < 8 ? Visibility.Visible : Visibility.Hidden;
            cIsSuperior_Polygon.Visibility = cUpload_CheckBox.IsEnabled && _MediaItemRef.UP_Inferior_ID != null ? Visibility.Visible : Visibility.Hidden;
            cRating_Polygon.Visibility = cUpload_CheckBox.Visibility;
        }

        private void CUpload_CheckBox_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (!GridVEIsLoading)
            {
                _MediaItemRef.UP_Queued = cUpload_CheckBox.IsChecked ?? false;
                if (_MediaItemRef.Grid_MediaTooBig != null && Module_Uploader.Media2Big4User(_MediaItemRef, _MediaItemRef.UP_Queued))
                {
                    cUpload_CheckBox.IsChecked = false;
                }
                else
                {
                    Window_Main._RefHolder.UploadCounterChange(_MediaItemRef.UP_Queued ? 1 : -1);
                }
                if (Window_Preview._RefHolder != null && ReferenceEquals(_MediaItemRef, Window_Preview._RefHolder.MediaItemHolder)) Window_Preview._RefHolder.SetUPColour();
            }
        }

        // - - - - - - - - - - - - - - - -

        internal void IsUploaded_SetText(string? PostID)
        {
            if (PostID == null)
            {
                cIsUploaded_TextBlock.Text = string.Empty;
                IsUploaded_DockPanel.Visibility = Visibility.Hidden;
            }
            else
            {
                cIsUploaded_TextBlock.Text = $"#{PostID}";
                IsUploaded_DockPanel.Visibility = Visibility.Visible;
                cUpload_CheckBox.IsChecked = false;
                cUpload_CheckBox.IsEnabled = false;
            }
        }

        private void IsUploaded_DockPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            string e6Post = $"https://e621.net/post/show/{_MediaItemRef.UP_UploadedID}";
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                Process.Start(new ProcessStartInfo(e6Post) { UseShellExecute = true });
            }
            else
            {
                if (Window_Main._RefHolder.InitStarted)
                {
                    if (Module_CefSharp.BrowserAddress == null || !Module_CefSharp.BrowserAddress.Equals(e6Post)) Module_CefSharp.LoadURL(e6Post);
                    Window_Main._RefHolder.Activate();
                    Window_Main._RefHolder.ReBot_Menu_ListBox.SelectedIndex = 1;
                }
                else
                {
                    MessageBox.Show(Window_Main._RefHolder, "Please initialize the browser first.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            e.Handled = true;
        }

        private void CIsSuperior_Polygon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            string e6Post = $"https://e621.net/post/show/{_MediaItemRef.UP_Inferior_ID}";
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                Process.Start(new ProcessStartInfo(e6Post) { UseShellExecute = true });
            }
            else
            {
                Window_Main._RefHolder.Activate();
                Window_Main._RefHolder.ReBot_Menu_ListBox.SelectedIndex = 1;
                if (!Module_CefSharp.BrowserAddress.Equals(e6Post)) Module_CefSharp.LoadURL(e6Post);
            }
            e.Handled = true;
        }

        // - - - - - - - - - - - - - - - -

        private int MediaItemIndex = 0;
        private void GridVE_UserControl_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            MediaItemIndex = Module_Grabber._Grabbed_MediaItems.FindIndex(_MediaItemRef);
            if (MediaItemIndex == -1) return;

            MoveUp.IsEnabled = MediaItemIndex > 0;
            MoveDown.IsEnabled = MediaItemIndex < Module_Grabber._Grabbed_MediaItems.Count - 1;
        }

        private void MenuItem_Click_Source(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(_MediaItemRef.Grab_PageURL);
        }

        private void MenuItem_Click_Media(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(_MediaItemRef.Grab_MediaURL);
        }

        private void MenuItem_Click_Move(object sender, RoutedEventArgs e)
        {
            int RequestedindexChange = int.Parse(((MenuItem)sender).Tag.ToString());
            lock (Module_Grabber._Grabbed_MediaItems)
            {
                MediaItem MediaItemTemp = Module_Grabber._Grabbed_MediaItems[MediaItemIndex];
                Module_Grabber._Grabbed_MediaItems.RemoveAt(MediaItemIndex);
                Module_Grabber._Grabbed_MediaItems.Insert(MediaItemIndex + RequestedindexChange, MediaItemTemp);
            }
            if (Window_Main._RefHolder._SelectedGridVE != null) Window_Main._RefHolder._SelectedGridVE.ToggleSelection();
            Window_Main._RefHolder.Grid_LastIndexCheck = -1;
            Window_Main._RefHolder.Grid_Populate(true);
        }

        // - - - - - - - - - - - - - - - -

        internal void AnimateScaleIn()
        {
            ((Storyboard)FindResource("ScaleIn")).Begin(this);
        }

        internal void AnimateScaleOut()
        {
            RenderTransformOrigin = new Point(0.5, 0.5);
            ((Storyboard)FindResource("ScaleOut")).Begin(this);
        }
    }
}