using CefSharp;
using CefSharp.Wpf;
using e621_ReBot_v3.CustomControls;
using e621_ReBot_v3.Modules;
using e621_ReBot_v3.Modules.Converter;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DownloadItem = e621_ReBot_v3.CustomControls.DownloadItem;

namespace e621_ReBot_v3
{

    public partial class Window_Preview : Window
    {
        internal static Window_Preview? _RefHolder;
        private readonly ChromiumWebBrowser MediaBrowser;
        public Window_Preview()
        {
            InitializeComponent();
            _RefHolder = this;
            //Owner = Window_Main._RefHolder;
            Top = Window_Main._RefHolder.Top;
            Left = Window_Main._RefHolder.Left;
            App.SetWindow2Square(this);

            MediaBrowser = new ChromiumWebBrowser("about:blank")
            {
                RequestHandler = new MediaBrowser_RequestHandler(),
                MenuHandler = new MediaBrowser_MenuHandler(),
                //FocusHandler = new MediaBrowser_FocusHandler()
                Focusable = false
            };
            MediaBrowser.TitleChanged += MediaBrowser_TitleChanged;
            MediaBrowser.LoadingStateChanged += MediaBrowser_LoadingStateChanged;

            if (!Directory.Exists("CefSharp Cache\\Media Cache")) Directory.CreateDirectory("CefSharp Cache\\Media Cache");
            Preview_Grid.Children.Add(MediaBrowser);
            Grid.SetRow(MediaBrowser, 2);

            ActionAfterDelayTimer.Tick += ActionAfterDelayTimer_Tick;

            if (Module_CookieJar.Cookies_Pixiv == null) Module_CookieJar.GetCookies("https://www.pixiv.net/", ref Module_CookieJar.Cookies_Pixiv);
            HttpClientHandler HttpClientHandlerTemp = new HttpClientHandler
            {
                CookieContainer = Module_CookieJar.Cookies_Pixiv,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };
            UgoiraHttpClient = new HttpClient(HttpClientHandlerTemp);
            UgoiraHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AppSettings.GlobalUserAgent);
            UgoiraHttpClient.DefaultRequestHeaders.Referrer = new Uri("https://www.pixiv.net/");

            AlreadyUploaded_Label.Text = string.Empty;
            MediaUgoiraPlayer.Visibility = Visibility.Hidden;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (MediaUgoiraPlayer._Loaded) MediaUgoiraPlayer.UnloadUgoira();
            Preview_Grid.Children.Remove(MediaBrowser);
            MediaBrowser.Dispose();
            _RefHolder = null;
            Window_Main._RefHolder.Activate();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left:
                case Key.Up:
                    {
                        if (PB_Previous.IsEnabled) PB_Previous.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        break;
                    }
                case Key.Right:
                case Key.Down:
                    {
                        if (PB_Next.IsEnabled) PB_Next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        break;
                    }

                case Key.E:
                    {
                        if (PB_Explicit.IsEnabled) PB_Explicit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        break;
                    }
                case Key.Q:
                    {
                        if (PB_Questionable.IsEnabled) PB_Questionable.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        break;
                    }
                case Key.S:
                    {
                        if (PB_Safe.IsEnabled) PB_Safe.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        break;
                    }

                case Key.T:
                    {
                        if (PB_Tagger.IsEnabled)
                        {
                            e.Handled = true;
                            PB_Tagger.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        }
                        break;
                    }

                case Key.U:
                    {
                        if (PB_Upload.IsEnabled) UploadChange(null);
                        break;
                    }
                case Key.OemPlus:
                case Key.Add:

                    {
                        if (PB_Upload.IsEnabled) UploadChange(true);
                        break;
                    }
                case Key.OemMinus:
                case Key.Subtract:
                    {
                        if (PB_Upload.IsEnabled) UploadChange(false);
                        break;
                    }

                case Key.D:
                    {
                        if (PB_Download.IsEnabled) PB_Download.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        break;
                    }

                case Key.V:
                    {
                        if (PB_ViewFile.IsEnabled) PB_ViewFile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        break;
                    }
                //case Key.NumPad1:
                //case Key.D1:
                //    {
                //        break;
                //    }
                //case Key.NumPad0:
                //case Key.D0:
                //    {
                //        break;
                //    }
                case Key.F:
                    {
                        PB_SauceNao.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        break;
                    }

                case Key.I:
                    {
                        PB_IQDBQ.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        break;
                    }

            }
        }

        // - - - - - - - - - - - - - - - -

        internal MediaItem? MediaItemHolder;
        internal void Nav2URL(MediaItem MediaItemRef)
        {


            if (MediaBrowser.IsBrowserInitialized) MediaBrowser.Stop(); //Error: "IBrowser instance is null" Null sometimes?
            if (MediaUgoiraPlayer._Loaded)
            {
                MediaUgoiraPlayer.Visibility = Visibility.Hidden;
                MediaUgoiraPlayer.UnloadUgoira();
            }

            MediaItemHolder = MediaItemRef;
            UpdateNavButtons();
            SetRatingColour();
            SetUPColour();

            Title = $"Preview ({MediaItemIndexHolder + 1})";
            if (Window_Tagger._RefHolder != null) Window_Tagger._RefHolder.Close();

            PB_Upload.IsEnabled = true;
            panel_Search.IsEnabled = false;
            AlreadyUploaded_Label.Text = string.Empty;
            Tags_TextBlock.Text = MediaItemHolder.UP_Tags;
            if (MediaItemHolder.UP_UploadedID != null)
            {
                AlreadyUploaded_Label.Text = $"#{MediaItemHolder.UP_UploadedID}";
                PB_Upload.IsEnabled = false;
            }

            string MediaURL = MediaItemHolder.Grab_MediaURL;
            string MediaName = Module_Downloader.MediaFile_GetFileNameOnly(MediaURL, MediaItemHolder.Grid_MediaFormat);

            //if (Form_Loader._FormReference.cTreeView_ConversionQueue.Nodes.ContainsKey(ImageURL))
            //{
            //    if (Form_Loader._FormReference.cTreeView_ConversionQueue.Nodes[0].Name.Equals(ImageURL))
            //    {
            //        string StatusLabelText = Form_Loader._FormReference.label_ConversionStatus.Text;
            //        Label_Download.ForeColor = StatusLabelText.Contains("Downloading") ? Color.DarkOrange : Color.DarkOrchid;
            //        Label_Download.Text = StatusLabelText.Substring(StatusLabelText.LastIndexOf("...") + 3);
            //    }
            //    else
            //    {
            //        Label_Download.Text = "0%";
            //        Label_Download.ForeColor = Color.DarkOrange;
            //    }
            //    Label_Download.Visible = true;
            //}
            //else
            //{

            PB_Download.IsEnabled = false;
            PB_ViewFile.IsEnabled = false;
            if (MediaItemHolder.DL_FilePath != null && File.Exists(MediaItemHolder.DL_FilePath))
            {
                if (MediaName.Contains("ugoira")
                || MediaName.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                || MediaName.EndsWith(".swf", StringComparison.OrdinalIgnoreCase))
                {
                    PB_ViewFile.Content = "▶";
                }
                else
                {
                    PB_ViewFile.Content = "🔍";
                }
                PB_ViewFile.IsEnabled = true;
            }
            else
            {
                MediaItemHolder.DL_FilePath = null;
                PB_Download.IsEnabled = true;
            }
            PB_PlayUgoira.IsEnabled = MediaName.Contains("ugoira");

            //if (MediaName.Contains("ugoira"))
            //{
            //    //Label_DownloadWarning.Visible = true;
            //    //toolTip_Display.SetToolTip(Label_DownloadWarning, $"This is an Ugoira, you need to download it in order to view the animated version.");
            //}

            PB_LoadAllMedia.IsEnabled = !(MediaItemIndexHolder == Module_Grabber._Grabbed_MediaItems.Count - 1);
            MediaBrowser.LoadUrl(MediaURL);
        }

        private string? DocumentTitle;
        private void MediaBrowser_TitleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            DocumentTitle = e.NewValue.ToString();
        }

        DateTime LoadFinishedDateTime;
        private void MediaBrowser_LoadingStateChanged(object? sender, LoadingStateChangedEventArgs e)
        {
            //if (MediaBrowser.Address.Equals("about:blank")) return;

            if (e.IsLoading)
            {
                ActionAfterDelayTimer.Stop();
            }
            else
            {
                LoadFinishedDateTime = DateTime.Now;
                Dispatcher.Invoke(BrowserLoadedActions);
            }
        }

        private DispatcherTimer ActionAfterDelayTimer = new DispatcherTimer();
        private void BrowserLoadedActions()
        {
            if (MediaItemHolder.Preview_DontDelay)
            {
                MediaLoadedActions();
                return;
            }

            TimeSpan TimePassed = DateTime.Now - LoadFinishedDateTime;
            if (TimePassed > TimeSpan.FromMilliseconds(500))
            {
                MediaLoadedActions();
            }
            else
            {
                ActionAfterDelayTimer.Interval = TimeSpan.FromMilliseconds(500 - TimePassed.Milliseconds);
                ActionAfterDelayTimer.Start();
            }
        }

        private void ActionAfterDelayTimer_Tick(object? sender, EventArgs e)
        {
            ActionAfterDelayTimer.Stop();
            if (_RefHolder == null) return;

            MediaLoadedActions();
        }

        [GeneratedRegex(@"[(](\d+)×(\d+)[)]")]
        private static partial Regex Preview_Regex();
        private void MediaLoadedActions()
        {
            switch (MediaItemHolder.Grid_MediaFormat)
            {
                case "ugoira":
                    {
                        AutoTags();
                        Title = $"Preview ({MediaItemIndexHolder + 1}) - Ugoira";
                        break;
                    }

                case "mp4":
                case "swf":
                    {

                        if (MediaItemHolder.Grid_MediaMD5 == null && MediaItemHolder.DL_FilePath != null && File.Exists(MediaItemHolder.DL_FilePath))
                        {
                            GetCachedMedia(MediaItemHolder.DL_FilePath);
                        }
                        if (MediaItemHolder.Grid_MediaMD5 != null && MediaItemHolder.UP_UploadedID == null) Check4MD5On621();
                        AutoTags();
                        Title = string.Format("Preview ({0}) - .{1} ({2:N2} kB)   ", MediaItemIndexHolder + 1, MediaItemHolder.Grid_MediaFormat, MediaItemHolder.Grid_MediaByteLength / 1024f);
                        break;
                    }

                default:
                    {
                        if (MediaItemHolder.Grid_MediaMD5 == null)
                        {
                            MatchCollection ImageResolution = Preview_Regex().Matches(DocumentTitle);

                            if (ImageResolution.Count == 0) return; //Protect against 404

                            MediaItemHolder.Grid_MediaWidth = ushort.Parse(ImageResolution[0].Groups[1].Value);
                            MediaItemHolder.Grid_MediaHeight = ushort.Parse(ImageResolution[0].Groups[2].Value);

                            GetCachedMedia();
                            if (MediaItemHolder.UP_UploadedID == null)
                            {
                                Check4MD5On621();
                                Module_Uploader.Media2BigCheck(MediaItemHolder);
                            }
                            AutoTags();

                            //load newgrounds image from cache
                            if (MediaItemHolder.Grab_PageURL.Contains(".newgrounds.com"))
                            {
                                string CachedImagePath = Module_Downloader.MediaBrowser_MediaCache[Module_Downloader.MediaFile_GetFileNameOnly(MediaItemHolder.Grab_MediaURL)];
                                MediaItemHolder.Grid_Thumbnail = Module_Grabber.Grab_ResizeThumbnail(new BitmapImage(new Uri(CachedImagePath, UriKind.Relative)), MediaItemHolder.Grid_MediaFormat);
                            }
                            MediaItemHolder.Grid_MediaMD5Checked = true;
                        }

                        //Also check if uploaded after loading saved grid
                        if (!MediaItemHolder.Grid_MediaMD5Checked && MediaItemHolder.UP_UploadedID == null)
                        {
                            Check4MD5On621();
                        }

                        if (MediaItemHolder.Grid_ThumbnailFullInfo == false)
                        {
                            MediaItemHolder.Grid_ThumbnailFullInfo = true;
                            GridVE? GridVETemp = Module_Grabber.IsVisibleInGrid(MediaItemHolder);
                            if (GridVETemp == null)
                            {
                                if (MediaItemHolder.Grid_Thumbnail == null && MediaItemHolder.Grid_ThumbnailDLStart == false)
                                {
                                    string CachedImagePath = Module_Downloader.MediaBrowser_MediaCache[Module_Downloader.MediaFile_GetFileNameOnly(MediaItemHolder.Grab_MediaURL)];
                                    MediaItemHolder.Grid_Thumbnail = Module_Grabber.Grab_ResizeThumbnail(new BitmapImage(new Uri(CachedImagePath, UriKind.Relative)), MediaItemHolder.Grid_MediaFormat);
                                }
                                Module_Grabber.Grab_MakeThumbnailInfoText(MediaItemHolder);
                            }
                            else
                            {
                                GridVETemp.LoadImage();
                            }
                        }
                        Title = string.Format("Preview ({0}) - {1}×{2}.{3} ({4:N2} kB)   [MD5: {5}]", MediaItemIndexHolder + 1, MediaItemHolder.Grid_MediaWidth, MediaItemHolder.Grid_MediaHeight, MediaItemHolder.Grid_MediaFormat, MediaItemHolder.Grid_MediaByteLength / 1024f, MediaItemHolder.Grid_MediaMD5);
                        break;
                    }
            }
            if (MediaItemHolder.UP_UploadedID != null)
            {
                PB_Upload.IsEnabled = false;
                panel_Search.IsEnabled = false;
                AlreadyUploaded_Label.Text = $"#{MediaItemHolder.UP_UploadedID}";
            }
            else
            {
                panel_Search.IsEnabled = Module_e621APIController.APIEnabled;
            }
            Tags_TextBlock.Text = MediaItemHolder.UP_Tags;

            if (LoadAllImagesMod)
            {
                if (MediaItemIndexHolder == Module_Grabber._Grabbed_MediaItems.Count - 1)
                {
                    PB_LoadAllMedia.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    return;
                }
                else
                {
                    PB_Navigate_Click(PB_Next, null);
                }
            }
            else
            {
                panel_Navigation.IsEnabled = true;
            }
        }

        private void GetCachedMedia(string? FilePath = null)
        {
            string MediaName = FilePath ?? Module_Downloader.MediaFile_GetFileNameOnly(MediaItemHolder.Grab_MediaURL, MediaItemHolder.Grid_MediaFormat);
            string? EitherFilePath = FilePath ?? Module_Downloader.MediaBrowser_MediaCache[MediaName];

            if (EitherFilePath != null)
            {
                byte[]? MediaBytes = null;
                for (int numTries = 0; numTries < 10; numTries++)
                {
                    try
                    {
                        MediaBytes = File.ReadAllBytes(EitherFilePath);
                        break;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(500);
                    }
                }
                if (MediaBytes == null || MediaBytes.Length == 0)
                {
                    return;
                }

                // Get MD5
                using (MD5 MD5Provider = MD5.Create())
                {
                    byte[] hashBytes = MD5Provider.ComputeHash(MediaBytes);
                    MediaItemHolder.Grid_MediaMD5 = Convert.ToHexString(hashBytes).ToLower();
                }
            }
        }

        private void Check4MD5On621()
        {
            if (MediaItemHolder.Grid_MediaMD5 == null) return;
            MediaItemHolder.Preview_DontDelay = true;

            string MD5Check = Module_e621Data.DataDownload($"https://e621.net/posts.json?md5={MediaItemHolder.Grid_MediaMD5}");
            if (string.IsNullOrEmpty(MD5Check) || MD5Check.StartsWith('ⓔ') || MD5Check.Length < 32) return;

            JObject MD5CheckJSON = JObject.Parse(MD5Check);
            MediaItemHolder.UP_UploadedID = (string)MD5CheckJSON["post"]["id"];
            AppSettings.MediaRecord_Add(MediaItemHolder);

            MediaItemHolder.UP_Rating = ((string)MD5CheckJSON["post"]["rating"]).ToUpper();
            List<string> TagList = new List<string>();
            foreach (JProperty pTag in MD5CheckJSON["post"]["tags"].Children())
            {
                foreach (JToken cTag in pTag.First)
                {
                    TagList.Add((string)cTag);
                }
            }
            ;
            MediaItemHolder.UP_Tags = string.Join(' ', TagList);
            GridVE? GridVETemp = Module_Grabber.IsVisibleInGrid(MediaItemHolder);
            if (GridVETemp != null)
            {
                GridVETemp.IsUploaded_SetText(MediaItemHolder.UP_UploadedID);
            }
        }

        private void AutoTags()
        {
            List<string> CurrentTags = new List<string>();
            CurrentTags.AddRange(MediaItemHolder.UP_Tags.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            CurrentTags = CurrentTags.Distinct().ToList();
            string? animated_tag = null;

            // /// = = = = = Check if GIF is animated
            if (CurrentTags.Contains("animated"))
            {
                animated_tag = string.Empty;
            }
            else
            {
                switch (MediaItemHolder.Grid_MediaFormat)
                {
                    case "ugoira":
                        {
                            animated_tag = " animated  animated_png";
                            break;
                        }
                    case "gif":
                        {

                            //byte[] bytes = File.ReadAllBytes(Module_Downloader.MediaBrowser_MediaCache[Module_Downloader.GetMediasFileNameOnly((string)Preview_RowHolder["Grab_MediaURL"])]); // File.ReadAllBytes(Preview_RowHolder("Image_FilePath"))
                            //using (MemoryStream TempStream = new MemoryStream(bytes))
                            //{
                            //    using (Image gif = Image.FromStream(TempStream))
                            //    {
                            //        int frameCount = gif.GetFrameCount(new FrameDimension(gif.FrameDimensionsList[0]));
                            //        if (frameCount > 1)
                            //        {
                            //            animated_tag = " animated no_sound";
                            //        }
                            //    }
                            //}
                            animated_tag = " animated no_sound";
                            break;
                        }
                    case "swf":
                    case "mp4":
                        {
                            animated_tag = " animated webm";
                            break;
                        }
                }
            }

            string? ratio_tag = null;
            string? resolution_tag = null;
            if (animated_tag == null)
            {
                // = = = = = Add tags regarding image size
                int ImageWidth = (int)MediaItemHolder.Grid_MediaWidth;
                int ImageHeight = (int)MediaItemHolder.Grid_MediaHeight;

                int size_bigger = Math.Max(ImageWidth, ImageHeight);
                if (size_bigger > 15000) return;

                int size_smaller = Math.Min(ImageWidth, ImageHeight);
                float size_ratio = (float)size_bigger / size_smaller;
                bool ReverseRatio = ImageWidth < ImageHeight;

                if (ImageWidth == ImageHeight)
                {
                    ratio_tag = " 1:1";
                }
                else
                {
                    switch (size_ratio)
                    {
                        case 4f / 3f:
                            {
                                ratio_tag = ReverseRatio ? " 3:4" : " 4:3";
                                break;
                            }
                        case 16f / 9f:
                            {
                                ratio_tag = ReverseRatio ? " 9:16" : " 16:9";
                                break;
                            }
                        case 16f / 10f:
                            {
                                ratio_tag = ReverseRatio ? " 10:16 5:8" : " 16:10 8:5";
                                break;
                            }
                        case 18f / 9f:
                            {
                                ratio_tag = ReverseRatio ? " 9:18 1:2" : " 18:9 2:1";
                                break;
                            }
                        case 21f / 9f:
                            {
                                ratio_tag = ReverseRatio ? " 9:21 3:7" : " 21:9 7:3";
                                break;
                            }
                        case 36f / 10f:
                            {
                                ratio_tag = ReverseRatio ? " 10:36" : " 36:10";
                                break;
                            }
                        case 5f / 4f:
                            {
                                ratio_tag = ReverseRatio ? " 4:5" : " 5:4";
                                break;
                            }
                        case 3f / 2f:
                            {
                                ratio_tag = ReverseRatio ? " 2:3" : " 3:2";
                                break;
                            }
                        case 3f / 1f:
                            {
                                ratio_tag = ReverseRatio ? " 1:3" : " 3:1";
                                break;
                            }
                    }
                }

                switch (size_bigger)
                {
                    case int case0 when case0 >= 10000:
                        {
                            resolution_tag = " superabsurd_res";
                            break;
                        }
                    case int case1 when case1 >= (ReverseRatio ? 2400 : 3200):
                        {
                            resolution_tag = " absurd_res";
                            break;
                        }
                    case int case2 when case2 >= (ReverseRatio ? 1200 : 1600):
                        {
                            resolution_tag = " hi_res";
                            break;
                        }
                    case int case3 when case3 <= 500:
                        {
                            resolution_tag = " low_res";
                            break;
                        }
                }
            }

            if (!string.IsNullOrEmpty(animated_tag)) MediaItemHolder.UP_Tags += animated_tag;
            if (!string.IsNullOrEmpty(ratio_tag)) MediaItemHolder.UP_Tags += ratio_tag;
            if (!string.IsNullOrEmpty(resolution_tag)) MediaItemHolder.UP_Tags += resolution_tag;
            if (Window_Tagger._RefHolder != null)
            {
                TextBox TextBoxTemp = Window_Tagger._RefHolder.Tags_TextBox;

                List<string> SortTags = string.Format("{0}{1}{2} {3} {4}", ratio_tag, animated_tag, resolution_tag, TextBoxTemp.Text, MediaItemHolder.UP_Tags).Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();

                int WordStartIndex = TextBoxTemp.Text.Substring(0, TextBoxTemp.SelectionStart).LastIndexOf(' ');
                if (WordStartIndex != -1)
                {
                    int WordEndIndex = TextBoxTemp.Text.IndexOf(' ', WordStartIndex);
                    if (WordEndIndex == -1) WordEndIndex = TextBoxTemp.Text.Length;
                    string SelectedWord = TextBoxTemp.Text.Substring(WordStartIndex, WordEndIndex - WordStartIndex);
                    SortTags.Remove(SelectedWord);
                    SortTags.Add(SelectedWord); //"move" to end
                }

                TextBoxTemp.Text = string.Join(' ', SortTags);
                TextBoxTemp.SelectionStart = TextBoxTemp.Text.Length;
            }
        }

        // - - - - - - - - - - - - - - - -

        private void SetBrowserColour(object sender, RoutedEventArgs e)
        {
            if (!MediaBrowser.IsLoading)
            {
                MediaBrowser.ExecuteScriptAsyncWhenPageLoaded($"document.body.style.background = '{((Button)sender).Tag}';");
            }
        }

        private void PB_Navigate_Click(object sender, RoutedEventArgs e)
        {
            if (Window_Tagger._RefHolder != null) Window_Tagger._RefHolder.Close();

            int WantedChange = short.Parse(((Button)sender).Tag.ToString());
            int WouldBeNewIndex = Module_Grabber._Grabbed_MediaItems.FindIndex(MediaItemHolder) + WantedChange;
            if (WouldBeNewIndex == -1 || WouldBeNewIndex == Module_Grabber._Grabbed_MediaItems.Count) return;

            Nav2URL(Module_Grabber._Grabbed_MediaItems[WouldBeNewIndex]);
        }

        private int MediaItemIndexHolder = 0;
        internal void UpdateNavButtons()
        {
            MediaItemIndexHolder = Module_Grabber._Grabbed_MediaItems.FindIndex(MediaItemHolder);

            PB_Previous.IsEnabled = MediaItemIndexHolder != 0;
            PB_Next.IsEnabled = MediaItemIndexHolder != Module_Grabber._Grabbed_MediaItems.Count - 1;
        }

        internal void SetRatingColour()
        {
            foreach (Button ButtonTemp in panel_Rating.Children)
            {
                if (MediaItemHolder.UP_Rating.Equals(ButtonTemp.Content))
                {
                    ButtonTemp.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ButtonTemp.Tag.ToString()));
                    ButtonTemp.Foreground = new SolidColorBrush(Colors.Black);
                    ButtonTemp.IsEnabled = false;
                }
                else
                {
                    ButtonTemp.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(ButtonTemp.Tag.ToString()));
                    ButtonTemp.Background = Window_Main._RefHolder.Background;
                    ButtonTemp.IsEnabled = true;
                }
            }
        }

        private void PB_Rating_Click(object sender, RoutedEventArgs e)
        {
            string NewRating = ((Button)sender).Content.ToString();
            GridVE GridVETemp = Module_Grabber.IsVisibleInGrid(MediaItemHolder);
            if (GridVETemp != null) GridVETemp.ChangeRating(NewRating);
            MediaItemHolder.UP_Rating = NewRating;
            SetRatingColour();
        }

        internal Point TaggerLocation;
        private void PB_Tagger_Click(object sender, RoutedEventArgs e)
        {
            if (TaggerLocation.X == 0 && TaggerLocation.Y == 0)
            {
                TaggerLocation = new Point(Left + Width / 2 - 240, Top + Height / 2 - 120);
            }
            Window_Tagger.OpenTagger(this, MediaItemHolder, TaggerLocation, !Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
        }

        private void PB_Upload_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (PB_Upload.IsEnabled)
            {
                SetUPColour();
            }
            else
            {
                PB_Upload.Background = new SolidColorBrush(Colors.Transparent);
                PB_Upload.Foreground = new SolidColorBrush(Colors.Black);
            }
        }

        private void PB_Upload_Click(object sender, RoutedEventArgs e)
        {
            UploadChange(null);
        }

        private void UploadChange(bool? NewStatus)
        {
            GridVE? GridVETemp = Module_Grabber.IsVisibleInGrid(MediaItemHolder);
            if (GridVETemp != null)
            {
                GridVETemp.cUpload_CheckBox.IsChecked = NewStatus ?? !GridVETemp.cUpload_CheckBox.IsChecked;
            }
            else
            {
                bool PreviousState = MediaItemHolder.UP_Queued;
                MediaItemHolder.UP_Queued = NewStatus ?? !MediaItemHolder.UP_Queued;
                if (MediaItemHolder.Grid_MediaTooBig != null && Module_Uploader.Media2Big4User(MediaItemHolder, MediaItemHolder.UP_Queued))
                {
                    MediaItemHolder.UP_Queued = false;
                }
                else
                {
                    if (PreviousState != MediaItemHolder.UP_Queued) Window_Main._RefHolder.UploadCounterChange(MediaItemHolder.UP_Queued ? 1 : -1);
                }
                SetUPColour();
            }
        }

        internal void SetUPColour()
        {
            PB_Upload.Background = MediaItemHolder.UP_Queued ? new SolidColorBrush(Colors.LimeGreen) : ((SolidColorBrush)FindResource("ThemeBackground"));
            PB_Upload.Foreground = MediaItemHolder.UP_Queued ? new SolidColorBrush(Colors.Black) : ((SolidColorBrush)FindResource("ThemeForeground"));
        }

        private void PB_Download_Click(object sender, RoutedEventArgs e)
        {
            if (MediaItemHolder.Grid_MediaFormat.Equals("ugoira"))
            {
                goto DownloadInstead;
            }

            string MediaName = Module_Downloader.MediaFile_GetFileNameOnly(MediaItemHolder.Grab_MediaURL, MediaItemHolder.Grid_MediaFormat);
            if (Module_Downloader.MediaBrowser_MediaCache.ContainsKey(MediaName))
            {
                DownloadItem DownloadItemTemp = new DownloadItem()
                {
                    Grab_PageURL = MediaItemHolder.Grab_PageURL,
                    Grab_MediaURL = MediaItemHolder.Grab_MediaURL,
                    Grab_ThumbnailURL = MediaItemHolder.Grab_ThumbnailURL,
                    Grab_Artist = MediaItemHolder.Grab_Artist,
                    Grab_Title = MediaItemHolder.Grab_Title,
                    Grab_MediaFormat = MediaItemHolder.Grid_MediaFormat,
                    MediaItemRef = MediaItemHolder
                };
                Module_Downloader.ReSaveMedia(DownloadItemTemp);
                PB_ViewFile.IsEnabled = true;
                return;
            }

        DownloadInstead:

            PB_Download.IsEnabled = false;
            if (Module_Downloader.CheckDownloadQueue4Duplicate(MediaItemHolder.Grab_MediaURL)) return;

            Module_Downloader.AddDownloadItem2Queue(
               PageURL: MediaItemHolder.Grab_PageURL,
               MediaURL: MediaItemHolder.Grab_MediaURL,
               ThumbnailURL: MediaItemHolder.Grab_ThumbnailURL,
               Artist: MediaItemHolder.Grab_Artist,
               Title: MediaItemHolder.Grab_Title,
               MediaFormat: MediaItemHolder.Grid_MediaFormat,
               MediaItemRef: MediaItemHolder);
            Module_Downloader.UpdateDownloadTreeView();
        }

        private void PB_ViewFile_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(MediaItemHolder.DL_FilePath))
            {
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    Process.Start("explorer.exe", $"/select,{MediaItemHolder.DL_FilePath}");
                }
                else
                {
                    Process.Start("explorer.exe", MediaItemHolder.DL_FilePath);
                }
            }
            else
            {
                MediaItemHolder.DL_FilePath = null;
                PB_ViewFile.IsEnabled = false;
                PB_Download.IsEnabled = true;
                //if (FilePath.Contains("ugoira"))
                //{
                //    Label_DownloadWarning.Visible = true;
                //}
            }
        }

        private void PB_SimilarSearch_Click(object sender, RoutedEventArgs e)
        {
            Button SenderButton = (Button)sender;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                string? PostIDReturned = Custom_IDBox.ShowIDBox(this, SenderButton.PointToScreen(new Point(0, 0)), "Enter Post ID", new SolidColorBrush(Colors.DarkOrange));
                if (PostIDReturned != null) InferiorSub(PostIDReturned);
                return;
            }

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                string? PostIDReturned = Custom_IDBox.ShowIDBox(this, SenderButton.PointToScreen(new Point(0, 0)), "Enter Post ID");
                if (PostIDReturned != null) SuperiorSub(PostIDReturned, MediaItemHolder);
                return;
            }

            Window_MediaSelect.Show_SimilarSearch(SenderButton.PointToScreen(new Point(0, 0)), SenderButton.Content.ToString());
        }

        private async void InferiorSub(string PostID)
        {
            Task<string?> RunTaskFirst = new Task<string?>(() => Module_e621Data.DataDownload($"https://e621.net/posts/{PostID}.json"));
            lock (Module_e621APIController.UserTasks)
            {
                Module_e621APIController.UserTasks.Add(RunTaskFirst);
            }

            string? PostTest = await RunTaskFirst;
            if (string.IsNullOrEmpty(PostTest) || PostTest.Length < 16 || PostTest.StartsWith('ⓔ'))
            {
                MessageBox.Show(this, $"Post with ID#{PostID} does not exist.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (PostTest.StartsWith('ⓔ'))
            {
                MessageBox.Show(this, $"{PostTest}", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            JToken PostData = JObject.Parse(PostTest)["post"];
            MediaItemHolder.UP_Rating = ((string)PostData["rating"]).ToUpper();
            MediaItemHolder.UP_UploadedID = PostID;

            List<string> SortTags = new List<string>();
            foreach (JProperty pTag in PostData["tags"].Children())
            {
                foreach (JToken cTag in pTag.First)
                {
                    SortTags.Add((string)cTag);
                }
            }
            SortTags.Sort();
            if (PostData["pools"].Children().Any())
            {
                foreach (JToken pPool in PostData["pools"].Children())
                {
                    SortTags.Add($"pool:{(string)pPool}");
                }
            }
            MediaItemHolder.UP_Tags = string.Join(' ', SortTags);
            Tags_TextBlock.Text = MediaItemHolder.UP_Tags;

            GridVE GridVETemp = Module_Grabber.IsVisibleInGrid(MediaItemHolder);
            if (GridVETemp != null)
            {
                GridVETemp.ChangeRating(MediaItemHolder.UP_Rating);
                GridVETemp.IsUploaded_SetText(PostID);
            }
            AlreadyUploaded_Label.Text = $"#{PostID}";
            //if (Properties.Settings.Default.ManualInferiorSave)
            //{
            //    Module_DB.DB_Media_CreateRecord(RowRefference);
            //}
            SetRatingColour();
        }

        internal static async void SuperiorSub(string PostID, MediaItem MediaItemRef)
        {
            Task<string?> RunTaskFirst = new Task<string?>(() => Module_e621Data.DataDownload($"https://e621.net/posts/{PostID}.json"));
            lock (Module_e621APIController.UserTasks)
            {
                Module_e621APIController.UserTasks.Add(RunTaskFirst);
            }

            string? PostTest = await RunTaskFirst;
            if (string.IsNullOrEmpty(PostTest) || PostTest.Length < 16)
            {
                MessageBox.Show(_RefHolder, $"Post with ID#{PostID} does not exist.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (PostTest.StartsWith('ⓔ'))
            {
                MessageBox.Show(_RefHolder, $"{PostTest}", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            JToken PostData = JObject.Parse(PostTest)["post"];
            _RefHolder.MediaItemHolder.UP_Rating = ((string)PostData["rating"]).ToUpper();
            List<string> SortTags = new List<string>();
            foreach (JProperty pTag in PostData["tags"].Children())
            {
                foreach (JToken cTag in pTag.First)
                {
                    SortTags.Add((string)cTag);
                }
            }
            SortTags.Sort();
            if (PostData["pools"].Children().Any())
            {
                foreach (JToken pPool in PostData["pools"].Children())
                {
                    SortTags.Add($"pool:{(string)pPool}");
                }
            }
            string InferiorParentID = (string)PostData["relationships"]["parent_id"];
            if (InferiorParentID != null)
            {
                SortTags.Add($"parent:{InferiorParentID}");
                MediaItemRef.UP_Inferior_ParentID = InferiorParentID;
            }
            MediaItemRef.UP_Tags = string.Join(' ', SortTags);

            MediaItemRef.UP_Inferior_ID = PostID;
            string InferiorDescription = (string)PostData["description"];
            string CurrentDescriptionConstruct = MediaItemRef.Grab_TextBody == null ? $"[code]{MediaItemRef.Grab_Title}[/code]" : $"[section,expanded={MediaItemRef.Grab_Title}]\n{MediaItemRef.Grab_TextBody}\n[/section]"; ;
            if (!string.IsNullOrEmpty(InferiorDescription) && !CurrentDescriptionConstruct.Contains(InferiorDescription))
            {
                MediaItemRef.UP_Inferior_Description = InferiorDescription;
            }

            if (PostData["sources"].Children().Any())
            {
                List<string> SourceList = new List<string>();
                foreach (JToken cChild in PostData["sources"])
                {
                    SourceList.Add((string)cChild);
                }
                MediaItemRef.UP_Inferior_Sources = SourceList;
            }

            if ((bool)PostData["relationships"]["has_children"])
            {
                List<string> ChildList = new List<string>();
                foreach (JToken cChild in PostData["relationships"]["children"])
                {
                    ChildList.Add((string)cChild);
                }
                MediaItemRef.UP_Inferior_Children = ChildList;
            }

            if ((bool)PostData["has_notes"])
            {
                // when they fix api this should no longer take 2 requests to get notes
                RunTaskFirst = new Task<string?>(() => Module_e621Data.DataDownload($"https://e621.net/notes.json?search[post_id]={PostID}", true));
                lock (Module_e621APIController.UserTasks)
                {
                    Module_e621APIController.UserTasks.Insert(0, RunTaskFirst);
                }

                PostTest = await RunTaskFirst;
                if (!string.IsNullOrEmpty(PostTest) && !PostTest.StartsWith('ⓔ') && !PostTest.StartsWith('{'))
                {
                    MediaItemRef.UP_Inferior_HasNotes = true;
                    float NewNoteSizeRatio = Math.Max((float)MediaItemRef.Grid_MediaWidth, (float)MediaItemRef.Grid_MediaHeight) / Math.Max((uint)PostData["file"]["width"], (uint)PostData["file"]["height"]);
                    MediaItemRef.UP_Inferior_NoteSizeRatio = NewNoteSizeRatio;
                }
            }
            MediaItemRef.UP_Tags = MediaItemRef.UP_Tags.Replace("better_version_at_source", null);
            _RefHolder.Tags_TextBlock.Text = MediaItemRef.UP_Tags;

            GridVE GridVETemp = Module_Grabber.IsVisibleInGrid(MediaItemRef);
            if (GridVETemp != null)
            {
                GridVETemp.ChangeRating(MediaItemRef.UP_Rating);
                GridVETemp.cUpload_CheckBox.IsEnabled = false; //to trigger tag warning update
                GridVETemp.cUpload_CheckBox.IsEnabled = true;
                GridVETemp.cUpload_CheckBox.IsChecked = true;
                GridVETemp.cIsSuperior_Polygon.Visibility = Visibility.Visible;
                GridVETemp.cIsSuperior_Polygon.ToolTip = $"Media will be uploaded as superior of #{PostID}\n\nClick to navigate to post.\nAlt+Click to open in your default browser.";
            }
            else
            {

                if (MediaItemRef.Grid_MediaTooBig != null && Module_Uploader.Media2Big4User(MediaItemRef, true))
                {
                    //GridVETemp.cUpload_CheckBox.IsChecked = false;
                    //GridVETemp.cUpload_CheckBox.Visibility = Visibility.Hidden;
                    //GridVETemp.cIsSuperior_Polygon.Visibility = Visibility.Hidden;
                }
                else
                {
                    Window_Main._RefHolder.UploadCounterChange(MediaItemRef.UP_Queued ? 0 : 1);
                    MediaItemRef.UP_Queued = true;
                }
            }
            if (MediaItemRef.UP_UploadedID != null)
            {
                AppSettings.MediaRecord_Remove(MediaItemRef);
                MediaItemRef.UP_UploadedID = null;
                if (GridVETemp != null) GridVETemp.IsUploaded_SetText(null);
                _RefHolder.AlreadyUploaded_Label.Text = string.Empty;
            }
            _RefHolder.SetRatingColour();
            _RefHolder.SetUPColour();
        }

        internal bool LoadAllImagesMod = false;
        private void PB_LoadAllMedia_Click(object sender, RoutedEventArgs e)
        {
            panel_Rating.Focus();
            if (LoadAllImagesMod)
            {
                if (PB_LoadAllMedia.IsEnabled) PB_LoadAllMedia.Foreground = new SolidColorBrush(Colors.LightSteelBlue);
                panel_Navigation.IsEnabled = true;
                PB_Tagger.IsEnabled = true;
                UpdateNavButtons(); // color doesn't change automatically when panel is enabled.
                LoadAllImagesMod = false;
            }
            else
            {
                PB_LoadAllMedia.Foreground = new SolidColorBrush(Colors.Red);
                panel_Navigation.IsEnabled = false;
                PB_Tagger.IsEnabled = false;
                LoadAllImagesMod = true;
                if (MediaBrowser.IsLoading)
                {
                    LoadAllImagesMod = true;
                }
                else
                {
                    PB_Navigate_Click(PB_Next, null);
                }
            }
        }

        private void PB_LoadAllMedia_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            PB_LoadAllMedia.Foreground = new SolidColorBrush(PB_LoadAllMedia.IsEnabled ? Colors.LightSteelBlue : Colors.Black);
        }

        private readonly HttpClient UgoiraHttpClient;
        private async void PB_PlayUgoira_Click(object sender, RoutedEventArgs e)
        {
            PB_PlayUgoira.IsEnabled = false;
            panel_Navigation.IsEnabled = false;

            //Get Ugoira Data
            string? UgoiraJSON = await Module_FFMpeg.UgoiraJSONResponse(MediaItemHolder.Grab_PageURL);
            JToken UgoiraJObject = JObject.Parse(UgoiraJSON)["body"];

            //Extract JSON Data
            List<WriteableBitmap> FrameFiles = new List<WriteableBitmap>();
            List<uint> FrameTimes = new List<uint>();
            foreach (JToken UgoiraFrame in UgoiraJObject["frames"])
            {
                FrameTimes.Add((uint)UgoiraFrame["delay"]);
            }

            //Get Images
            byte[] UgoiraBytes = await UgoiraHttpClient.GetByteArrayAsync((string)UgoiraJObject["src"]); //originalSrc
            using (MemoryStream bytes2Stream = new MemoryStream(UgoiraBytes))
            {
                using (ZipArchive UgoiraZip = new ZipArchive(bytes2Stream, ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in UgoiraZip.Entries)
                    {
                        using (Stream entryStream = entry.Open())
                        {
                            // Copy to MemoryStream to ensure seekable stream
                            using (var ms = new MemoryStream())
                            {
                                entryStream.CopyTo(ms);
                                ms.Position = 0;

                                BitmapDecoder decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                                BitmapSource source = decoder.Frames[0];

                                WriteableBitmap wb = new WriteableBitmap(source);
                                wb.Freeze();

                                FrameFiles.Add(wb);
                            }
                        }
                    }
                }
            }

            MediaUgoiraPlayer.LoadUgoira(FrameFiles, FrameTimes);
            Panel.SetZIndex(MediaUgoiraPlayer, 6969); //bring to front
            MediaUgoiraPlayer.Visibility = Visibility.Visible;
            panel_Navigation.IsEnabled = true;
        }

        private void AlreadyUploaded_Label_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            string e6Post = $"https://e621.net/post/show/{MediaItemHolder.UP_UploadedID}";
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                Process.Start(new ProcessStartInfo(e6Post) { UseShellExecute = true });
            }
            else
            {
                Window_Main._RefHolder.Activate();
                Window_Main._RefHolder.ReBot_Menu_ListBox.SelectedIndex = 1;
                if (Module_CefSharp.BrowserAddress != null && !Module_CefSharp.BrowserAddress.Equals(e6Post)) Module_CefSharp.LoadURL(e6Post);
            }
        }
    }
}