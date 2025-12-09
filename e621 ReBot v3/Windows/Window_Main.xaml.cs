using CefSharp;
using e621_ReBot_v3.CustomControls;
using e621_ReBot_v3.Modules;
using e621_ReBot_v3.Modules.Converter;
using e621_ReBot_v3.Modules.Downloader;
using e621_ReBot_v3.Modules.Uploader;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace e621_ReBot_v3
{
    public partial class Window_Main : Window
    {
        internal static Window_Main? _RefHolder;
        public Window_Main()
        {
            InitializeComponent();
            _RefHolder = this;
            App.SetWindow2Square(this);

#if DEBUG
            AppSettings.DevMode = true;
#endif

            Credit_StackPanel.Visibility = Visibility.Hidden;
            ReBot_Menu_ListBox.Visibility = Visibility.Hidden;
            MakeUpdate_WButton.Visibility = Visibility.Hidden;
            Update_TextBlock.Visibility = Visibility.Hidden;
            Tooltip_Border.Visibility = Visibility.Hidden;
            GB_Left.Visibility = Visibility.Hidden;
            GB_Right.Visibility = Visibility.Hidden;
            GBTB_Center.Visibility = Visibility.Hidden;
            GBTB_Left.Visibility = Visibility.Hidden;
            GBTB_Right.Visibility = Visibility.Hidden;
            Upload_ProgressCanvas.Visibility = Visibility.Hidden;
            SettingsButton_DLGenders.Visibility = Visibility.Hidden;
            SettingsButton_DLDNPs.Visibility = Visibility.Hidden;

            DateTime HolidaysStart = new DateTime(DateTime.UtcNow.Year, 12, 24);
            DateTime HolidaysEnd = HolidaysStart.AddDays(13);
            if (DateTime.UtcNow > HolidaysStart && DateTime.UtcNow < HolidaysEnd)
            {
                SantaHat1.Visibility = Visibility.Visible;
                SantaHat2.Visibility = Visibility.Visible;
            }
            ScrollDisableTimer.Tick += ScrollDisableTimer_Tick;

            AppSettings.LoadSettings();

            if (AppSettings.BigMode)
            {
                Width = 1920;
                Height = 1032;
                Grid_ItemLimit = 32;
                //Grid_GridVEPanel.Margin = new Thickness(8, 16, 0, 16);
            }
        }

        #region "Window"

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ReBot_Title.Text = $"e621 ReBot v{Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version} Beta";

            ReBot_Menu_ListBox.SelectionChanged += ReBot_Menu_SelectionChanged;
            GBU_Upload.IsEnabledChanged += GBU_Upload_IsEnabledChanged;
            GBD_Download.IsEnabledChanged += GBD_Download_IsEnabledChanged;
            GB_Clear.IsEnabledChanged += GB_Clear_IsEnabledChanged;

            AppSettings.SetLoadedSettings();
        }

        //Like Shown
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            if (AppSettings.FirstRun)
            {
                ReBot_Menu_ListBox.Visibility = Visibility.Visible;
                Module_Tutorial.Step_0();
            }
            else
            {
                if (!string.IsNullOrEmpty(AppSettings.APIKey)) ThreadPool.QueueUserWorkItem(state => Module_Credit.Credit_CheckAll());
                Module_Updater.PreUpdateCheck();
            }

            ModuleEnabler();
            ErrorReporter();

            if (!string.IsNullOrEmpty(AppSettings.Note)) new Window_Notes().ShowDialog();
            //ReBot_HttpClient.CreateClient();
            //ReBot_HttpClient.CreateClient1();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            MoverRectangle.Focus();
        }

        private void ModuleEnabler()
        {
            Module_Grabber.Start(); //Make it load on main thread, bug fix.
            Module_Downloader.Start(); //Make it load on main thread, bug fix.
            if (AppSettings.APIKey != null)
            {
                Module_e621APIController.ToggleStatus();
                ThreadPool.QueueUserWorkItem(state => Window_PoolWatcher.PoolWatcher_Check4New());
            }
        }

        private bool DeleteLogAfterCopy = false;
        private void ErrorReporter()
        {
            if (File.Exists("ReBotErrorLog.txt"))
            {
                MessageBoxResult MessageBoxResultTemp = MessageBox.Show(this, "I found an error log! You should report it to my maker. Do you want me to copy it to your clipboard?", "e621 ReBot", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes);
                if (MessageBoxResultTemp == MessageBoxResult.Yes)
                {
                    Clipboard.SetText(File.ReadAllText("ReBotErrorLog.txt"));
                    DeleteLogAfterCopy = true;
                }
            }
        }

        private void MoverRectangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void FormButton_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        internal Process? UploadQueueProcess;
        internal Process? ConversionQueueProcess;
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (Module_Uploader._2Upload_MediaItems.Count > 0 || Module_Downloader._2Download_DownloadItems.Count > 0) //cTreeView_ConversionQueue.Nodes.Count > 0
            {
                MessageBoxResult MessageBoxResultTemp = MessageBox.Show(this, "There are currently some jobs active, are you sure you want to close me?", "e621 ReBot", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (MessageBoxResultTemp == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            if (Module_CefSharp.CefSharpBrowser != null)
            {
                Module_CefSharp.CefSharpBrowser.Dispose();
                Cef.Shutdown();
            }

            AppSettings.SaveSettings();

            if (UploadQueueProcess != null) UploadQueueProcess.Kill();
            if (ConversionQueueProcess != null) ConversionQueueProcess.Kill();

            if (DeleteLogAfterCopy && File.Exists("ReBotErrorLog.txt")) File.Delete("ReBotErrorLog.txt");
        }

        private void FormButton_Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ButtonNavTag_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(((Button)sender).Tag.ToString()) { UseShellExecute = true });
        }

        private void ReBot_e6Button_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Module_CefSharp.CefSharpBrowser != null)
            {
                Module_CefSharp.CefSharpBrowser.LoadUrl("https://e621.net/");
                ReBot_Menu_ListBox.SelectedIndex = 1;
            }
            else
            {
                Process.Start(new ProcessStartInfo("https://e621.net/") { UseShellExecute = true });
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (ReBot_TabControl.SelectedIndex == 2)
            {
                switch (e.Key)
                {
                    case Key.Left:
                        {
                            if (GB_Left.IsVisible) GB_Left.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            break;
                        }

                    case Key.Right:
                        {
                            if (GB_Right.IsVisible) GB_Right.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            break;
                        }
                }
                if (_SelectedGridVE != null)
                {
                    switch (e.Key)
                    {
                        case Key.E:
                        case Key.Q:
                        case Key.S:
                            {
                                _SelectedGridVE.ChangeRating(e.Key.ToString());
                                if (Window_Preview._RefHolder != null && ReferenceEquals(_SelectedGridVE._MediaItemRef, Window_Preview._RefHolder.MediaItemHolder))
                                {
                                    Window_Preview._RefHolder.SetRatingColour();
                                }
                                break;
                            }

                        case Key.Delete:
                            {
                                //if (cTreeView_UploadQueue.Nodes.ContainsKey((string)_Selected_e6GridVE._DataRowReference["Grab_MediaURL"]))
                                //{
                                //    MessageBox.Show("Image can't be removed while it is queued for upload.", "e621 ReBot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                //}
                                _SelectedGridVE.AnimateScaleOut();
                                _SelectedGridVE = null;
                                break;
                            }

                        case Key.OemPlus:
                        case Key.Add:
                            {
                                if (_SelectedGridVE.cUpload_CheckBox.IsVisible) _SelectedGridVE.cUpload_CheckBox.IsChecked = true;
                                break;
                            }

                        case Key.NumPad1:
                        case Key.D1:
                            {
                                _SelectedGridVE.cDL_CheckBox.IsChecked = true;
                                break;
                            }

                        case Key.OemMinus:
                        case Key.Subtract:
                            {
                                if (_SelectedGridVE.cUpload_CheckBox.IsVisible) _SelectedGridVE.cUpload_CheckBox.IsChecked = false;
                                break;
                            }

                        case Key.NumPad0:
                        case Key.D0:
                            {
                                _SelectedGridVE.cDL_CheckBox.IsChecked = false;
                                break;
                            }

                        case Key.T:
                            {
                                if (_SelectedGridVE != null)
                                {
                                    if (Window_Tagger._RefHolder != null) Window_Tagger._RefHolder.Close();
                                    Point GridPoint = _SelectedGridVE.PointToScreen(new Point(0, 0));
                                    Window_Tagger.OpenTagger(this, _SelectedGridVE._MediaItemRef, new Point(GridPoint.X + 200, GridPoint.Y), true, true);
                                }
                                break;
                            }
                    }
                }
            }
        }

        #endregion

        // - - - - - - - - - - - - - - - -

        #region "Menu"

        private void ReBot_Menu_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ReBot_TabControl.SelectedIndex = ReBot_Menu_ListBox.SelectedIndex;
            TooltipSetter();
        }

        private void TooltipSetter()
        {
            switch (ReBot_TabControl.SelectedIndex)
            {
                case 2:
                    {
                        Tooltip_Border.ToolTip = "Scroll the mouse or press Left/Right arrows to scroll through pages.\n----------------\nMedia controls:\n* Click on media to de/select.\n* Double-click to view.\n* Ctrl+click to navigate to Source.\n* Alt+click to navigate to Source with default browser.\n* Right click for more options.\nWhile selected:\n* Press Q, S or E to change rating.\n* +, - to toggle upload selection.\n* 1, 0 to toggle download selection.\n* T to open Tagger.\n* Del to remove media.";
                        Tooltip_Border.Visibility = Visibility.Visible;
                        break;
                    }

                default:
                    {
                        Tooltip_Border.Visibility = Visibility.Hidden;
                        break;
                    }
            }
        }

        internal bool InitStarted = false;

        internal void ListBoxItem_Browser_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (InitStarted == false && Module_CefSharp.CefSharpBrowser == null)
            {
                InitStarted = true;
                if (BrowserControl._RefHolder != null) BrowserControl._RefHolder.InitilizeBrowser();
            }
        }

        #endregion

        // - - - - - - - - - - - - - - - -

        #region "Welcome"

        private void Notes_WButton_Click(object sender, RoutedEventArgs e)
        {
            new Window_Notes().ShowDialog();
        }

        private void MakeUpdate_WButton_Click(object sender, RoutedEventArgs e)
        {
            Module_Updater.CreateUpdateZip();
        }

        #endregion

        // - - - - - - - - - - - - - - - -


        #region "Grid"

        internal readonly int Grid_ItemLimit = 18;
        internal int Grid_ItemStartIndex = 0;
        internal int Grid_LastIndexCheck = 0;
        internal void Grid_Populate(bool SkipAnimation = false)
        {
            int Items2Show = Math.Min(Module_Grabber._Grabbed_MediaItems.Count - Grid_ItemStartIndex, Grid_ItemLimit);
            if (Items2Show == 0 || Module_Grabber._Grabbed_MediaItems.Count == 0)
            {
                Grid_Paginator();
                return;
            }
            if (Items2Show == Grid_GridVEPanel.Children.Count)
            {
                if (Grid_LastIndexCheck == Grid_ItemStartIndex)
                {
                    Grid_Paginator();
                    return;
                }
                Grid_LastIndexCheck = Grid_ItemStartIndex;
            }
            else
            {
                if (Items2Show > Grid_GridVEPanel.Children.Count)
                {
                    int Items2Create = Math.Min(Module_Grabber._Grabbed_MediaItems.Count - Grid_ItemStartIndex - Grid_GridVEPanel.Children.Count, Grid_ItemLimit - Grid_GridVEPanel.Children.Count);
                    for (int x = 0; x < Items2Create; x++)
                    {
                        int ScaleValue = SkipAnimation ? 1 : 0;
                        GridVE GridVETemp = new GridVE(ScaleValue);

                        if (Grid_ItemLimit > 20) //big mode
                        {
                            GridVETemp.Margin = new Thickness(6, 8, 6, 8);
                            GridVETemp.Width = 220;
                            GridVETemp.Height = 220;
                        }

                        Grid_GridVEPanel.Children.Add(GridVETemp);
                        if (!SkipAnimation) GridVETemp.AnimateScaleIn();
                    }
                }
                else
                {
                    Grid_GridVEPanel.Children.RemoveRange(Items2Show - 1, Grid_GridVEPanel.Children.Count - Items2Show);
                }
            }
            if (Grid_GridVEPanel.Children.Count > 0)
            {
                int MediaItemIndexTracker = Grid_ItemStartIndex;
                MediaItem? MediaItemTemp;
                foreach (GridVE GridVETemp in Grid_GridVEPanel.Children)
                {
                    MediaItemTemp = Module_Grabber._Grabbed_MediaItems[MediaItemIndexTracker];
                    if (!ReferenceEquals(GridVETemp._MediaItemRef, MediaItemTemp))
                    {
                        GridVETemp._MediaItemRef = MediaItemTemp;
                        GridVETemp.LoadMediaItem(); //Will change selection
                        if (ReferenceEquals(_SelectedGridVE, GridVETemp))
                        {
                            //ChangeGridVESelection(_SelectedGridVE);
                            _SelectedGridVE = null;
                        }
                    }
                    MediaItemIndexTracker++;
                }
            }
            Grid_Paginator();
        }

        internal void Grid_Paginator()
        {
            int CurrentPage = Grid_ItemStartIndex / Grid_ItemLimit + 1;
            int TotalPages = (int)Math.Ceiling((float)Module_Grabber._Grabbed_MediaItems.Count / Grid_ItemLimit);
            GBTB_Center.Text = $"{CurrentPage} / {TotalPages}";
            GBTB_Center.Visibility = TotalPages > 1 ? Visibility.Visible : Visibility.Hidden;
            GBTB_Left.Text = (CurrentPage - 1).ToString();
            GBTB_Right.Text = (CurrentPage + 1).ToString();
            GB_Left.Visibility = CurrentPage > 1 ? Visibility.Visible : Visibility.Hidden;
            GB_Right.Visibility = CurrentPage < TotalPages ? Visibility.Visible : Visibility.Hidden;
            GB_Clear.IsEnabled = Grid_GridVEPanel.Children.Count != 0;
            Grid_GridVEPanel.Opacity = Grid_GridVEPanel.Children.Count == 0 ? 0 : 1;
        }

        private void GB_Left_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            GBTB_Left.Visibility = GB_Left.Visibility;
        }

        private void GB_Right_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            GBTB_Right.Visibility = GB_Right.Visibility;
        }

        private readonly DispatcherTimer ScrollDisableTimer = new DispatcherTimer { Interval = TimeSpan.FromMicroseconds(250) };
        private void ScrollDisableTimer_Tick(object? sender, EventArgs e)
        {
            ScrollDisableTimer.Stop();
            ReBot_GridTab.MouseWheel += ReBot_GridTab_MouseWheel;
        }

        private void ReBot_GridTab_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            ReBot_GridTab.MouseWheel -= ReBot_GridTab_MouseWheel;
            if (e.Delta > 0) // Up / Left
            {
                if (GB_Left.IsVisible) GB_Left.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            else
            {
                if (GB_Right.IsVisible) GB_Right.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            ScrollDisableTimer.Start();
        }

        private void GB_Left_Click(object sender, RoutedEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                Grid_ItemStartIndex = 0;
            }
            else
            {
                Grid_ItemStartIndex -= Grid_ItemLimit;
            }
            if (_SelectedGridVE != null) _SelectedGridVE.ToggleSelection();
            Grid_Populate(true);
        }

        private void GB_Right_Click(object sender, RoutedEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                int LastPageItems = Module_Grabber._Grabbed_MediaItems.Count % Grid_ItemLimit;
                Grid_ItemStartIndex = Module_Grabber._Grabbed_MediaItems.Count - (LastPageItems == 0 ? Grid_ItemLimit : LastPageItems); //In C#, integer division is truncated before any multiplication happens which is what is needed but not entirely clear from single line.
            }
            else
            {
                Grid_ItemStartIndex += Grid_ItemLimit;
            }
            if (_SelectedGridVE != null) _SelectedGridVE.ToggleSelection();
            Grid_Populate(true);
        }

        private void GB_Clear_Click(object sender, RoutedEventArgs e)
        {
            bool ClearAll = true;

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                lock (Module_Grabber._Grabbed_MediaItems)
                {
                    for (int i = Module_Grabber._Grabbed_MediaItems.Count - 1; i >= 0; i--)
                    {
                        MediaItem? MediaItemTemp = Module_Grabber._Grabbed_MediaItems[i];
                        DownloadCounter += MediaItemTemp.DL_Queued ? -1 : 0;
                        if (MediaItemTemp.UP_UploadedID != null)
                        {
                            Module_Grabber._Grabbed_MediaItems.RemoveAt(i);
                        }
                    }
                }
                ClearAll = false;
            }

            if (ClearAll && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                lock (Module_Grabber._Grabbed_MediaItems)
                {
                    for (int i = Grid_ItemStartIndex + Grid_GridVEPanel.Children.Count - 1; i >= Grid_ItemStartIndex; i--)
                    {
                        MediaItem? MediaItemTemp = Module_Grabber._Grabbed_MediaItems[i];
                        DownloadCounter += MediaItemTemp.DL_Queued ? -1 : 0;
                        UploadCounter += MediaItemTemp.UP_Queued ? -1 : 0;
                        Module_Grabber._Grabbed_MediaItems.RemoveAt(i);
                    }
                }
                ClearAll = false;
            }

            if (ClearAll)
            {
                lock (Module_Grabber._Grabbed_MediaItems)
                {
                    Module_Grabber._Grabbed_MediaItems.Clear();
                }
                Grid_GridVEPanel.Children.Clear();
                Grid_ItemStartIndex = 0;
                UploadCounter = 0;
                DownloadCounter = 0;
                Grid_Paginator();
            }
            else
            {
                int CurrentPage = Grid_ItemStartIndex / Grid_ItemLimit;
                int NewEndPage = (int)Math.Floor((float)Module_Grabber._Grabbed_MediaItems.Count / Grid_ItemLimit - 0.01);
                NewEndPage = NewEndPage < CurrentPage ? NewEndPage : CurrentPage;
                Grid_ItemStartIndex = NewEndPage * Grid_ItemLimit;
                Grid_LastIndexCheck = -1;
                Grid_Populate();
            }

            if (Window_Preview._RefHolder != null && Module_Grabber._Grabbed_MediaItems.Count > 0 && !Module_Grabber._Grabbed_MediaItems.Contains(Window_Preview._RefHolder.MediaItemHolder)) Window_Preview._RefHolder.Close();

            UploadCounterChange(0);
            DownloadCounterChange(0);
            //GB_Clear.IsEnabled = Grid_GridVEPanel.Children.Count != 0;     

            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private void GB_Clear_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            GBD_Select.IsEnabled = GB_Clear.IsEnabled;
            GBD_Inverse.IsEnabled = GB_Clear.IsEnabled;
            GBU_Select.IsEnabled = GB_Clear.IsEnabled;
            GBU_Inverse.IsEnabled = GB_Clear.IsEnabled;
        }

        internal GridVE? _SelectedGridVE;
        internal void ChangeGridVESelection(GridVE GridVERef)
        {
            if (GridVERef.IsSelected)
            {
                if (_SelectedGridVE != null && !ReferenceEquals(_SelectedGridVE, GridVERef)) _SelectedGridVE.ToggleSelection();
                _SelectedGridVE = GridVERef;
            }
            else
            {
                _SelectedGridVE = null;
            }
        }

        internal int UploadCounter = 0;
        internal void UploadCounterChange(int byValue)
        {
            UploadCounter += byValue;
            GBU_Upload.IsEnabled = UploadCounter > 0 && Module_e621APIController.APIEnabled;
        }

        private void GBU_Upload_Click(object sender, RoutedEventArgs e)
        {
            Module_Uploader.UploadButtonClick(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        }

        private void GBU_Upload_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            GBU_Unselect.IsEnabled = GBU_Upload.IsEnabled;
        }

        private void GBU_Select_Click(object sender, RoutedEventArgs e)
        {
            if (Module_Grabber._Grabbed_MediaItems.Count == 0) return;
            foreach (GridVE GridVETemp in Grid_GridVEPanel.Children)
            {
                if (GridVETemp.cUpload_CheckBox.IsEnabled) GridVETemp.cUpload_CheckBox.IsChecked = true;
            }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                UploadCounter = 0;
                foreach (MediaItem MediaItemTemp in Module_Grabber._Grabbed_MediaItems)
                {
                    if (MediaItemTemp.UP_UploadedID == null)
                    {
                        MediaItemTemp.UP_Queued = true;
                        UploadCounter++;
                    }
                    else
                    {
                        MediaItemTemp.UP_Queued = false;
                    }
                }
                UploadCounterChange(0);
            }
        }

        private void GBU_Inverse_Click(object sender, RoutedEventArgs e)
        {
            if (Module_Grabber._Grabbed_MediaItems.Count == 0) return;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                foreach (MediaItem MediaItemTemp in Module_Grabber._Grabbed_MediaItems)
                {
                    if (MediaItemTemp.UP_UploadedID == null)
                    {
                        MediaItemTemp.UP_Queued = !MediaItemTemp.UP_Queued;
                        UploadCounter += MediaItemTemp.UP_Queued ? 1 : -1;
                    }
                }
                foreach (GridVE GridVETemp in Grid_GridVEPanel.Children)
                {
                    if (GridVETemp.cUpload_CheckBox.IsEnabled)
                    {
                        GridVETemp.GridVEIsLoading = true;
                        GridVETemp.cUpload_CheckBox.IsChecked = GridVETemp._MediaItemRef.UP_Queued;
                        GridVETemp.GridVEIsLoading = false;
                    }
                }
                UploadCounterChange(0);
            }
            else
            {
                foreach (GridVE GridVETemp in Grid_GridVEPanel.Children)
                {
                    if (GridVETemp.cUpload_CheckBox.IsEnabled) GridVETemp.cUpload_CheckBox.IsChecked = !GridVETemp.cUpload_CheckBox.IsChecked;
                }
            }
        }

        private void GBU_Unselect_Click(object sender, RoutedEventArgs e)
        {
            if (Module_Grabber._Grabbed_MediaItems.Count == 0) return;
            foreach (GridVE GridVETemp in Grid_GridVEPanel.Children)
            {
                GridVETemp.cUpload_CheckBox.IsChecked = false;
            }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                foreach (MediaItem MediaItemTemp in Module_Grabber._Grabbed_MediaItems)
                {
                    MediaItemTemp.UP_Queued = false;
                }
                UploadCounter = 0;
                GBU_Upload.IsEnabled = false;
            }
        }

        internal int DownloadCounter = 0;
        internal void DownloadCounterChange(int byValue)
        {
            DownloadCounter += byValue;
            GBD_Download.IsEnabled = DownloadCounter > 0;
        }

        private void GBD_Download_Click(object sender, RoutedEventArgs e)
        {
            int DownloadAdditionCounter = 0;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                foreach (GridVE GridVETemp in Grid_GridVEPanel.Children)
                {
                    MediaItem? MediaItemTemp = GridVETemp._MediaItemRef;
                    if (MediaItemTemp.DL_Queued)
                    {
                        if (Module_Downloader.CheckDownloadQueue4Duplicate(MediaItemTemp.Grab_MediaURL)) continue;

                        Module_Downloader.AddDownloadItem2Queue(
                            PageURL: MediaItemTemp.Grab_PageURL,
                            MediaURL: MediaItemTemp.Grab_MediaURL,
                            ThumbnailURL: MediaItemTemp.Grab_ThumbnailURL,
                            Artist: MediaItemTemp.Grab_Artist,
                            Title: MediaItemTemp.Grab_Title,
                            MediaFormat: MediaItemTemp.Grid_MediaFormat,
                            MediaItemRef: MediaItemTemp);
                        DownloadAdditionCounter++;
                    }
                }
            }
            else
            {
                foreach (MediaItem MediaItemTemp in Module_Grabber._Grabbed_MediaItems)
                {
                    if (MediaItemTemp.DL_Queued)
                    {
                        if (Module_Downloader.CheckDownloadQueue4Duplicate(MediaItemTemp.Grab_MediaURL)) continue;

                        Module_Downloader.AddDownloadItem2Queue(
                            PageURL: MediaItemTemp.Grab_PageURL,
                            MediaURL: MediaItemTemp.Grab_MediaURL,
                            ThumbnailURL: MediaItemTemp.Grab_ThumbnailURL,
                            Artist: MediaItemTemp.Grab_Artist,
                            Title: MediaItemTemp.Grab_Title,
                            MediaFormat: MediaItemTemp.Grid_MediaFormat,
                            MediaItemRef: MediaItemTemp);
                        DownloadAdditionCounter++;
                    }
                }
            }
            Module_Downloader.UpdateDownloadTreeView();
            GBD_Change.Text = $"+{DownloadAdditionCounter}";
            GBD_Change.IsEnabled = true; //Makes it local, so animation no longer work becase it takes priority over style
            GBD_Change.IsEnabled = false;
        }

        private void GBD_Download_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            GBD_Unselect.IsEnabled = GBD_Download.IsEnabled;
        }

        private void GBD_Select_Click(object sender, RoutedEventArgs e)
        {
            if (Module_Grabber._Grabbed_MediaItems.Count == 0) return;
            foreach (GridVE GridVETemp in Grid_GridVEPanel.Children)
            {
                GridVETemp.cDL_CheckBox.IsChecked = true;
            }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                foreach (MediaItem MediaItemTemp in Module_Grabber._Grabbed_MediaItems)
                {
                    MediaItemTemp.DL_Queued = true;
                }
                DownloadCounter = Module_Grabber._Grabbed_MediaItems.Count;
                GBD_Download.IsEnabled = true;
            }
        }

        private void GBD_Inverse_Click(object sender, RoutedEventArgs e)
        {
            if (Module_Grabber._Grabbed_MediaItems.Count == 0) return;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                foreach (MediaItem MediaItemTemp in Module_Grabber._Grabbed_MediaItems)
                {
                    MediaItemTemp.DL_Queued = !MediaItemTemp.DL_Queued;
                    DownloadCounter += MediaItemTemp.DL_Queued ? 1 : -1;
                }
                foreach (GridVE GridVETemp in Grid_GridVEPanel.Children)
                {
                    GridVETemp.GridVEIsLoading = true;
                    GridVETemp.cDL_CheckBox.IsChecked = GridVETemp._MediaItemRef.DL_Queued;
                    GridVETemp.GridVEIsLoading = false;
                }
                DownloadCounterChange(0);
            }
            else
            {
                foreach (GridVE GridVETemp in Grid_GridVEPanel.Children)
                {
                    GridVETemp.cDL_CheckBox.IsChecked = !GridVETemp.cDL_CheckBox.IsChecked;
                }
            }
        }

        private void GBD_Unselect_Click(object sender, RoutedEventArgs e)
        {
            if (Module_Grabber._Grabbed_MediaItems.Count == 0) return;
            foreach (GridVE GridVETemp in Grid_GridVEPanel.Children)
            {
                GridVETemp.cDL_CheckBox.IsChecked = false;
            }
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                foreach (MediaItem MediaItemTemp in Module_Grabber._Grabbed_MediaItems)
                {
                    MediaItemTemp.DL_Queued = false;
                }
                DownloadCounter = 0;
                GBD_Download.IsEnabled = false;
            }
        }

        #endregion

        // - - - - - - - - - - - - - - - -

        #region "Downloads"

        private void Download_DownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(AppSettings.Download_FolderLocation);
            Process.Start("explorer.exe", AppSettings.Download_FolderLocation);
        }

        private void RadionButton_DLTX_Click(object sender, RoutedEventArgs e)
        {
            RadioButton WhichRadioButton = (RadioButton)sender;
            if (WhichRadioButton.IsChecked == true)
            {
                ushort NewValue = ushort.Parse(WhichRadioButton.Content.ToString());
                AppSettings.Download_ThreadsCount = NewValue;
            }

            if (AppSettings.Download_ThreadsCount != Download_DownloadVEPanel.Children.Count)
            {
                int DifferenceRequired = AppSettings.Download_ThreadsCount - Download_DownloadVEPanel.Children.Count;
                if (AppSettings.Download_ThreadsCount > Download_DownloadVEPanel.Children.Count)
                {
                    for (int i = 0; i < DifferenceRequired; i++)
                    {
                        DownloadVE DownloadVETemp = new DownloadVE();
                        lock (_DownloadVEList)
                        {
                            _DownloadVEList.Add(DownloadVETemp);
                        }
                        Download_DownloadVEPanel.Children.Add(DownloadVETemp);
                        Module_Downloader.DLThreadsWaiting++;
                    }
                }
                else
                {
                    if (DownloadQueue_CheckBox.IsChecked == true)
                    {
                        if (Module_Downloader._2Download_DownloadItems.Count == 0 && Download_DownloadVEPanel.Children.Count == Module_Downloader.DLThreadsWaiting)
                        {
                            for (int i = Download_DownloadVEPanel.Children.Count - 1; i >= 0; i--)
                            {
                                DownloadVE DownloadVETemp = (DownloadVE)Download_DownloadVEPanel.Children[i];
                                lock (_DownloadVEList)
                                {
                                    _DownloadVEList.Remove(DownloadVETemp);
                                }
                                Download_DownloadVEPanel.Children.Remove(DownloadVETemp);
                                Module_Downloader.DLThreadsWaiting--;
                                DifferenceRequired++;
                                if (DifferenceRequired == 0) break;
                            }
                        }
                    }
                    else
                    {
                        if (Download_DownloadVEPanel.Children.Count == Module_Downloader.DLThreadsWaiting)
                        {
                            for (int i = Download_DownloadVEPanel.Children.Count - 1; i >= 0; i--)
                            {
                                DownloadVE DownloadVETemp = (DownloadVE)Download_DownloadVEPanel.Children[i];
                                lock (_DownloadVEList)
                                {
                                    _DownloadVEList.Remove(DownloadVETemp);
                                }
                                Download_DownloadVEPanel.Children.Remove(DownloadVETemp);
                                Module_Downloader.DLThreadsWaiting--;
                                DifferenceRequired++;
                                if (DifferenceRequired == 0) break;
                            }
                        }
                    }
                }
            }
        }

        internal List<DownloadVE> _DownloadVEList = new List<DownloadVE>();
        private void Download_DownloadVEPanel_LayoutUpdated(object sender, EventArgs e)
        {
            for (int i = 0; i < Download_DownloadVEPanel.Children.Count; i++)
            {
                ((DownloadVE)Download_DownloadVEPanel.Children[i]).IndexDisplay.Text = $"#{i + 1}";
            }
        }

        private void DownloadQueue_CancelAPIDL_Click(object sender, RoutedEventArgs e)
        {
            Module_DLe621.CancellationPending = true;
            DownloadQueue_CancelAPIDL.IsEnabled = false;
        }

        private void DownloadQueue_DownloadPageUp_Click(object sender, RoutedEventArgs e)
        {
            Module_Downloader.DownloadTreeViewPage -= 1;
            Module_Downloader.UpdateDownloadTreeView();
        }

        private void DownloadQueue_DownloadPageDown_Click(object sender, RoutedEventArgs e)
        {
            Module_Downloader.DownloadTreeViewPage += 1;
            Module_Downloader.UpdateDownloadTreeView();
        }

        internal ContextMenu? DownloadTreeViewContextMenuHolder;
        internal string? DownloadTreeViewContextMenuHolderTarget;
        private void DownloadTreeViewContextMenu_Opening(object sender, ContextMenuEventArgs e)
        {
            if (DownloadTreeViewContextMenuHolder != null)
            {
                DownloadTreeViewContextMenu_Closed(DownloadTreeViewContextMenuHolder, null);
            }
        }

        private void DownloadTreeViewContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            ContextMenu ContextMenuParent = (ContextMenu)sender;
            TreeViewItem TreeViewItemTarget = (TreeViewItem)ContextMenuParent.PlacementTarget;
            TreeViewItemTarget.Foreground = (SolidColorBrush)FindResource("ThemeFocus");
            DownloadTreeViewContextMenuHolder = ContextMenuParent;
            DownloadTreeViewContextMenuHolderTarget = TreeViewItemTarget.Header.ToString();
        }

        private void DownloadTreeViewContextMenu_Closed(object sender, RoutedEventArgs? e)
        {
            DownloadTreeViewContextMenuHolder = null;
            DownloadTreeViewContextMenuHolderTarget = null;
            Module_Downloader.UpdateDownloadTreeView();
        }

        private void DownloadTreeViewContextMenu_Remove(object sender, RoutedEventArgs e)
        {
            lock (Module_Downloader._2Download_DownloadItems)
            {
                Module_Downloader._2Download_DownloadItems.RemoveURL(DownloadTreeViewContextMenuHolderTarget);
            }
            Module_Downloader.UpdateDownloadTreeView();
        }

        private void DownloadTreeViewContextMenu_RemoveAll(object sender, RoutedEventArgs e)
        {
            lock (Module_Downloader._2Download_DownloadItems)
            {
                Module_Downloader._2Download_DownloadItems.Clear();
            }
            Module_Downloader.UpdateDownloadTreeView();
        }

        private void TreeViewContextMenu_ExpandAll(object sender, RoutedEventArgs e)
        {
            MenuItem MenuItemClicked = (MenuItem)sender;
            ContextMenu ContextMenuParent = (ContextMenu)MenuItemClicked.Parent;
            TreeViewItem TreeViewItemTarget = (TreeViewItem)ContextMenuParent.PlacementTarget;
            Custom_TreeView? TreeViewItemParentTree;
            if (TreeViewItemTarget.Parent.GetType() == typeof(Custom_TreeView))
            {
                TreeViewItemParentTree = (Custom_TreeView?)TreeViewItemTarget.Parent;
            }
            else
            {
                TreeViewItemParentTree = (Custom_TreeView?)((TreeViewItem)TreeViewItemTarget.Parent).Parent;
            }

            foreach (TreeViewItem TreeViewItemTemp in TreeViewItemParentTree.Items)
            {
                TreeViewItemTemp.ExpandSubtree();
            }
        }

        private void TreeViewContextMenu_CollapseAll(object sender, RoutedEventArgs e)
        {
            MenuItem MenuItemClicked = (MenuItem)sender;
            ContextMenu ContextMenuParent = (ContextMenu)MenuItemClicked.Parent;
            TreeViewItem TreeViewItemTarget = (TreeViewItem)ContextMenuParent.PlacementTarget;
            Custom_TreeView? TreeViewItemParentTree;
            if (TreeViewItemTarget.Parent.GetType() == typeof(Custom_TreeView))
            {
                TreeViewItemParentTree = (Custom_TreeView?)TreeViewItemTarget.Parent;
            }
            else
            {
                TreeViewItemParentTree = (Custom_TreeView?)((TreeViewItem)TreeViewItemTarget.Parent).Parent;
            }

            foreach (TreeViewItem TreeViewItemTemp in TreeViewItemParentTree.Items)
            {
                TreeViewItemTemp.IsExpanded = false;
            }
        }

        private void RadionButton_Naminge6_Click(object sender, RoutedEventArgs e)
        {
            RadioButton WhichRadioButton = (RadioButton)sender;
            if (WhichRadioButton.IsChecked == true)
            {
                ushort NewValue = ushort.Parse(WhichRadioButton.Name.Substring(WhichRadioButton.Name.Length - 1));
                AppSettings.NamingPattern_e6 = NewValue;
            }
        }

        private void RadionButton_NamingWeb_Click(object sender, RoutedEventArgs e)
        {
            RadioButton WhichRadioButton = (RadioButton)sender;
            if (WhichRadioButton.IsChecked == true)
            {
                ushort NewValue = ushort.Parse(WhichRadioButton.Name.Substring(WhichRadioButton.Name.Length - 1));
                AppSettings.NamingPattern_Web = NewValue;
            }
        }

        private void Download_PoolWatcher_Click(object sender, RoutedEventArgs e)
        {
            new Window_PoolWatcher().ShowDialog();
        }

        private void Download_DownloadFolderLocation_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog OpenFolderDialogTemp = new OpenFolderDialog()
            {
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory
            };

            if (OpenFolderDialogTemp.ShowDialog() == true)
            {
                string DLFolderPath = $"{OpenFolderDialogTemp.FolderName}";
                Download_DownloadFolderLocation.ToolTip = $"Current path: {DLFolderPath}";
                AppSettings.Download_FolderLocation = DLFolderPath;
                AppSettings.SaveSettings();
            }
        }

        #endregion

        // - - - - - - - - - - - - - - - -

        #region "Jobs"

        private void Upload_CheckBox_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (Module_Uploader._UploadDisableTimer.IsEnabled)
            {
                Upload_CheckBox.Checked -= Upload_CheckBox_CheckChanged;
                Upload_CheckBox.Unchecked -= Upload_CheckBox_CheckChanged;
                Upload_CheckBox.IsChecked = false;
                Upload_CheckBox.Checked += Upload_CheckBox_CheckChanged;
                Upload_CheckBox.Unchecked += Upload_CheckBox_CheckChanged;
                MessageBox.Show(this, "There is no upload credit remaining, please wait for more hourly or total credit.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                Upload_CheckBox.Content = $"Uploader{(Upload_TreeView.Items.Count > 0 ? $" ({Upload_TreeView.Items.Count})" : null)}";
            }
        }

        private void GrabTreeViewContextMenu_Remove(object sender, RoutedEventArgs e)
        {
            MenuItem MenuItemClicked = (MenuItem)sender;
            ContextMenu ContextMenuParent = (ContextMenu)MenuItemClicked.Parent;
            TreeViewItem TreeViewItemTarget = (TreeViewItem)ContextMenuParent.PlacementTarget;

            string WebAddress = Encoding.UTF8.GetString(Convert.FromHexString(TreeViewItemTarget.Name.Substring(1)));
            if (TreeViewItemTarget.Parent.GetType() == typeof(Custom_TreeView))
            {
                if (TreeViewItemTarget.Items.Count == 0)
                {
                    lock (Module_Grabber._GrabQueue_URLs)
                    {
                        Module_Grabber._GrabQueue_URLs.Remove(WebAddress);
                    }
                }
                else
                {
                    lock (Module_Grabber._GrabQueue_URLs)
                    {
                        foreach (TreeViewItem TreeViewItemTemp in TreeViewItemTarget.Items)
                        {
                            WebAddress = Encoding.UTF8.GetString(Convert.FromHexString(TreeViewItemTemp.Name.Substring(1)));
                            Module_Grabber._GrabQueue_URLs.Remove(WebAddress);
                        }
                    }
                }
                Grab_TreeView.Items.Remove(TreeViewItemTarget);
            }
            else //Children Nodes
            {
                TreeViewItem TreeViewItemParent = (TreeViewItem)TreeViewItemTarget.Parent;
                if (TreeViewItemParent.Items.Count == 1)
                {
                    Grab_TreeView.Items.Remove(TreeViewItemParent);
                }
                else
                {
                    TreeViewItemParent.Items.Remove(TreeViewItemTarget);
                    TreeViewItemParent.ToolTip = $"Pages left to grab: {TreeViewItemParent.Items.Count}";
                }
                lock (Module_Grabber._GrabQueue_URLs)
                {
                    Module_Grabber._GrabQueue_URLs.Remove(WebAddress);
                }
            }
        }

        private void GrabTreeViewContextMenu_RemoveAll(object sender, RoutedEventArgs e)
        {
            lock (Module_Grabber._GrabQueue_URLs)
            {
                Module_Grabber._GrabQueue_URLs.Clear();
            }
            Grab_TreeView.Items.Clear();
        }

        private void UploadTreeViewContextMenu_Remove(object sender, RoutedEventArgs e)
        {
            MenuItem MenuItemClicked = (MenuItem)sender;
            ContextMenu ContextMenuParent = (ContextMenu)MenuItemClicked.Parent;
            TreeViewItem TreeViewItemTarget = (TreeViewItem)ContextMenuParent.PlacementTarget;

            lock (Module_Uploader._2Upload_MediaItems)
            {
                Module_Uploader._2Upload_MediaItems.RemoveURL(TreeViewItemTarget.Header.ToString());
            }
            Upload_TreeView.Items.Remove(TreeViewItemTarget);
            Upload_CheckBox.Content = $"Uploader{(Upload_TreeView.Items.Count > 0 ? $" ({Upload_TreeView.Items.Count})" : null)}";
        }

        private void UploadTreeViewContextMenu_RemoveAll(object sender, RoutedEventArgs e)
        {
            lock (Module_Uploader._2Upload_MediaItems)
            {
                Module_Uploader._2Upload_MediaItems.Clear();
            }
            Upload_TreeView.Items.Clear();
            Upload_CheckBox.Content = $"Uploader{(Upload_TreeView.Items.Count > 0 ? $" ({Upload_TreeView.Items.Count})" : null)}";
        }

        private void Upload_ReverseUploadQueue_Click(object sender, RoutedEventArgs e)
        {
            if (Upload_CheckBox.IsChecked == true | Module_Uploader.Upload_BGW.IsBusy)
            {
                MessageBox.Show(this, "Upload queue and API should be stopped before attempting to reverse the order.", "e621 ReBot Upload", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Module_Uploader.ReverseUploadTreeView();
        }

        private void RetryTreeViewContextMenu_Retry(object sender, RoutedEventArgs e)
        {
            MenuItem MenuItemClicked = (MenuItem)sender;
            ContextMenu ContextMenuParent = (ContextMenu)MenuItemClicked.Parent;
            TreeViewItem TreeViewItemTarget = (TreeViewItem)ContextMenuParent.PlacementTarget;
            MediaItem MediaItemTarget = (MediaItem)TreeViewItemTarget.Tag;

            Module_RetryQueue.MoveItem2UploadQueue(MediaItemTarget);
            Retry_TreeView.Items.Remove(TreeViewItemTarget);
            Retry_TextBlock.Text = $"Retry Queue{(Retry_TreeView.Items.Count > 0 ? $" ({Retry_TreeView.Items.Count})" : null)}";
        }

        private void RetryTreeViewContextMenu_Remove(object sender, RoutedEventArgs e)
        {
            MenuItem MenuItemClicked = (MenuItem)sender;
            ContextMenu ContextMenuParent = (ContextMenu)MenuItemClicked.Parent;
            TreeViewItem TreeViewItemTarget = (TreeViewItem)ContextMenuParent.PlacementTarget;

            lock (Module_RetryQueue._2Retry_MediaItems)
            {
                Module_RetryQueue._2Retry_MediaItems.RemoveURL(TreeViewItemTarget.Header.ToString());
            }
            Retry_TreeView.Items.Remove(TreeViewItemTarget);
            Retry_TextBlock.Text = $"Retry Queue{(Retry_TreeView.Items.Count > 0 ? $" ({Retry_TreeView.Items.Count})" : null)}";
        }

        private void RetryTreeViewContextMenu_RemoveAll(object sender, RoutedEventArgs e)
        {
            lock (Module_RetryQueue._2Retry_MediaItems)
            {
                Module_RetryQueue._2Retry_MediaItems.Clear();
            }
            Retry_TreeView.Items.Clear();
            Retry_TextBlock.Text = $"Retry Queue{(Retry_TreeView.Items.Count > 0 ? $" ({Retry_TreeView.Items.Count})" : null)}";
        }

        private readonly string[] ValidExtensions = { ".mp4", ".swf" };
        private void DragDropConver_Label_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void DragDropConver_Label_Drop(object sender, DragEventArgs e)
        {
            List<string> FileDropList = new List<string>();

            string[] DropList = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            foreach (string FilePath in DropList)
            {
                if (ValidExtensions.Any(ext => FilePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                {
                    FileDropList.Add(FilePath);
                }
            }

            switch (FileDropList.Count)
            {
                case 0:
                    {
                        MessageBox.Show("No supported video files detected.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    }

                case 1:
                    {
                        Module_FFMpeg.DragDropConvert(FileDropList[0]);
                        break;
                    }

                default:
                    {
                        MessageBox.Show("Can only convert one video at a time.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    }
            }
        }

        #endregion

        // - - - - - - - - - - - - - - - -

        #region "Settings"

        private void SB_APIKey_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(AppSettings.APIKey))
            {
                if (string.IsNullOrEmpty(AppSettings.UserName))
                {
                    MessageBox.Show(this, "I still don't know your name so you will have to introduce yourself first.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information);
                    AppSettings.FirstRun = true;
                    ReBot_Menu_ListBox.SelectedIndex = 1;
                    ListBoxItem_Browser_PreviewMouseLeftButtonDown(null, null);
                }
                else
                {
                    if (Window_APIKey._RefHolder == null) new Window_APIKey().Show();
                }
            }
            else
            {
                if (MessageBox.Show(this, "Are you sure you want to remove the API key?", "e621 ReBot", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    AppSettings.APIKey = null;
                    AppSettings.SaveSettings();
                    SB_APIKey.Content = "Add API key";
                    Module_e621APIController.ToggleStatus();
                    //BU_CancelAPIDL_Click(null, null);
                    MessageBox.Show(this, "Some functions will remain disabled until you add the API key again.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void RadionButton_UI_Click(object sender, RoutedEventArgs e)
        {
            RadioButton WhichRadioButton = (RadioButton)sender;
            if (WhichRadioButton.IsChecked == true)
            {
                ushort NewValue = ushort.Parse(WhichRadioButton.Content.ToString());
                AppSettings.Update_Interval = NewValue;
            }
        }

        private void SettingsCheckBox_BigMode_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.BigMode = ((CheckBox)sender).IsChecked ?? false;
        }

        private void SettingsCheckBox_GridSaveSession_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Grid_SaveSession = ((CheckBox)sender).IsChecked ?? false;
        }

        private void SettingsCheckBox_BrowserClearCache_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Browser_ClearCache = ((CheckBox)sender).IsChecked ?? false;
        }

        private void SettingsCheckBox_MediaSaveManualInferiorRecord_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.MediaSaveManualInferiorRecord = ((CheckBox)sender).IsChecked ?? false;
        }

        private void SettingsCheckBox_DownloadSaveTags_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Download_SaveTags = ((CheckBox)sender).IsChecked ?? false;
        }

        private void SettingsCheckBox_IgnoreErrors_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Download_IgnoreErrors = ((CheckBox)sender).IsChecked ?? false;
        }

        private void SettingsButton_Blacklist_Click(object sender, RoutedEventArgs e)
        {
            new Window_Blacklist().ShowDialog();
        }

        private void ColorBox_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox WhichColorBox = (TextBox)sender;
            switch (e.Key)
            {
                case Key.Enter:
                    {
                        if (WhichColorBox.Text.Length == 6)
                        {
                            byte R = Convert.ToByte(WhichColorBox.Text.Substring(0, 2), 16);
                            byte G = Convert.ToByte(WhichColorBox.Text.Substring(2, 2), 16);
                            byte B = Convert.ToByte(WhichColorBox.Text.Substring(4, 2), 16);
                            SolidColorBrush SolidColorBrushTemp = new SolidColorBrush(Color.FromRgb(R, G, B));
                            switch (ushort.Parse(WhichColorBox.Tag.ToString()))
                            {
                                case 0:
                                    {
                                        Application.Current.Resources["ThemeBackground"] = SolidColorBrushTemp;
                                        break;
                                    }
                                case 1:
                                    {
                                        Application.Current.Resources["ThemeForeground"] = SolidColorBrushTemp;
                                        break;
                                    }
                                case 2:
                                    {
                                        Application.Current.Resources["ThemeFocus"] = SolidColorBrushTemp;
                                        break;
                                    }
                            }
                            Keyboard.ClearFocus();
                        }
                        //MessageBox.Show(this, "That is not a valid color code.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    }

                case Key.Escape:
                    {
                        break;
                    }

                case Key.Space:
                    {
                        e.Handled = true;
                        break;
                    }
            }
        }

        [GeneratedRegex(@"^[a-fA-F0-9]+$")]
        private static partial Regex ColorBoxRegex();
        private void ColorBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !ColorBoxRegex().IsMatch(e.Text);
        }

        private void ResetTheme_Button_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Resources["ThemeBackground"] = (SolidColorBrush)(new BrushConverter().ConvertFrom("#012E56")); //#FF012E56
            Application.Current.Resources["ThemeForeground"] = new SolidColorBrush(Colors.LightSteelBlue);
            Application.Current.Resources["ThemeFocus"] = new SolidColorBrush(Colors.Orange); ;
        }

        private void SettingsButton_DLSuggestions_Click(object sender, RoutedEventArgs e)
        {
            SettingsButton_DLSuggestions.IsEnabled = false;
            ThreadPool.QueueUserWorkItem(state => Module_e621Data.DLSuggestions());
        }

        private void SettingsButton_DLGenders_Click(object sender, RoutedEventArgs e)
        {
            SettingsButton_DLGenders.IsEnabled = false;
            ThreadPool.QueueUserWorkItem(state => Module_e621Data.DLGenders());
        }

        private void SettingsButton_DLDNPs_Click(object sender, RoutedEventArgs e)
        {
            SettingsButton_DLDNPs.IsEnabled = false;
            ThreadPool.QueueUserWorkItem(state => Module_e621Data.DLDNPs());
        }



        #endregion

        // - - - - - - - - - - - - - - - -
    }
}