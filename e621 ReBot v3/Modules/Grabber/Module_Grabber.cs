using e621_ReBot_v3.CustomControls;
using e621_ReBot_v3.Modules.Grabber;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace e621_ReBot_v3.Modules
{
    internal class Module_Grabber
    {
        internal static readonly ushort PauseBetweenImages = 50;
        internal static readonly List<Regex> _GrabEnabler;

        static Module_Grabber()
        {
            _GrabEnabler = new List<Regex>
            {
                new Regex(@"^\w+://www\.furaffinity\.net/((view|full|gallery|scraps|favorites)/.+/?|search/?)"),
                new Regex(@"^\w+://inkbunny\.net/(s/\d+|(gallery|scraps)/.+/\d+/\w+|submissionsviewall.php)"),
                new Regex(@"^\w+://www\.pixiv\.net/\w+/(artworks|users)/\d+"),
                new Regex(@"^\w+://www\.hiccears\.com/((contents|file).+(/.+)?|p/.+/illustrations)"),
                new Regex(@"^\w+://x\.com/.+/(media|status/\d+/?$)"),
                new Regex(@"^\w+://\w+\.newgrounds\.com/(movies/?|portal/view/\d+|art/?(view/.+|\w+)?)"),
                new Regex(@"^\w+://\w+\.sofurry\.com/(view/\d+|artwork|browse/\w+/art\?uid=\d+)"),
                new Regex(@"^\w+://www\.weasyl\.com/((~.+/)?(submissions/\w+(/.+)?)|search.+find=submit)"),
                new Regex(@"^\w+://\w+\.\w+/@.+/(\d+|media)"), //mastodon, baraag, pawoo
                new Regex(@"^\w+://www\.hentai-foundry\.com/(pictures/(user/.+|featured|popular|random|recent/)|user/.+/faves/pictures|users/FaveUsersRecentPictures)"),
                new Regex(@"^\w+://www\.plurk\.com/(p/|TimeLine/|(?!portal|login|signup|search)\w+)(.+)?"),
            };

            _GrabTimer.Tick += GrabTimer_Tick;
        }

        internal static void Start()
        {
            _GrabTimer.Start();
        }

        internal static bool DoMultiPageGrab = false;
        internal static void GrabEnabler(string WebAddress)
        {
            foreach (Regex URLTest in _GrabEnabler)
            {
                Match MatchTemp = URLTest.Match(WebAddress);
                if (MatchTemp.Success)
                {
                    Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
                    {
                        if (DoMultiPageGrab)
                        {
                            ThreadPool.QueueUserWorkItem(state => Grab_MultiPageTask(MatchTemp.Value));
                        }
                        else
                        {
                            BrowserControl._RefHolder.BB_Grab.Tag = MatchTemp.Value;
                            BrowserControl._RefHolder.BB_Grab.Visibility = Visibility.Visible;
                        }
                    });
                    return;
                }
            }
        }

        internal static void Report_Info(string InfoMessage)
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.Grab_InfoTextBox.Text = $"{DateTime.Now.ToLongTimeString()}, {InfoMessage}\n{Window_Main._RefHolder.Grab_InfoTextBox.Text}";
            });
        }

        private static void Report_Status()
        {
            string StatusMessage = GrabberActiveHandCount == 0 ? "Waiting..." : $"Grabbing - {GrabberActiveHandCount} of 4 hands active.";
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.Grab_StatusTextBlock.Text = $"Status: {StatusMessage}";
            });
        }

        internal static TreeViewItem? TreeView_GetParentItem(string IteamHeader, string ItemName, object? TagPass = null, bool SkipSearch = false)
        {
            ItemName = $"z{Convert.ToHexString(Encoding.UTF8.GetBytes(ItemName))}";

            TreeViewItem? TreeViewItemParent = SkipSearch ? null : Window_Main._RefHolder.Grab_TreeView.FindTreeViewItemByName(ItemName);
            if (TreeViewItemParent == null)
            {
                TreeViewItemParent = new TreeViewItem
                {
                    Header = IteamHeader,
                    Name = ItemName, //Can't start with number
                    Tag = TagPass,
                };
                Window_Main._RefHolder.Grab_TreeView.Items.Add(TreeViewItemParent);
            }
            return TreeViewItemParent;
        }

        internal static bool TreeView_MakeChildItem(TreeViewItem TreeViewItemParent, string IteamHeader, string ItemName, object? TagPass = null, bool SkipSearch = false)
        {
            string URL = ItemName;
            ItemName = $"z{Convert.ToHexString(Encoding.UTF8.GetBytes(ItemName))}";

            TreeViewItem? TreeViewItemChild = SkipSearch ? null : Window_Main._RefHolder.Grab_TreeView.FindTreeViewItemByName(ItemName);
            if (TreeViewItemChild == null)
            {
                TreeViewItemChild = new TreeViewItem
                {
                    Header = IteamHeader,
                    Name = ItemName, //Can't start with number
                    Tag = TagPass,
                    ToolTip = URL,
                };
                TreeViewItemParent.Items.Add(TreeViewItemChild);
                TreeViewItemParent.ToolTip = $"Pages left to grab: {TreeViewItemParent.Items.Count}";
                return true;
            }
            return false;
        }

        // - - - - - - - - - - - - - - - -

        internal static List<string> _GrabQueue_URLs = new List<string>();
        internal static void Grab_1Link(string WebAddress)
        {
            Uri TempURI = new Uri(WebAddress);
            string[] URISplit = TempURI.LocalPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            switch (TempURI.Host)
            {
                case "www.furaffinity.net":
                    {
                        if (URISplit[0].Equals("gallery") || URISplit[0].Equals("scraps"))
                        {
                            string NextPage = Module_FurAffinity.MultiPageCheck();
                            if (!string.IsNullOrEmpty(NextPage))
                            {
                                MessageBoxResult MessageBoxResultTemp = MessageBox.Show(Window_Main._RefHolder, "Would you lik to grab all pages?\nBrowser interaction will be disabled during the process.", "e621 ReBot", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                                if (MessageBoxResultTemp == MessageBoxResult.Yes)
                                {
                                    DoMultiPageGrab = true;
                                    BrowserControl._RefHolder.BrowserControls_Panel.IsEnabled = false;
                                    BrowserControl._RefHolder.IsHitTestVisible = false;

                                    string BaseAddress = $"https://www.furaffinity.net/{URISplit[0]}/{URISplit[1]}";
                                    Module_CefSharp.LoadURL(BaseAddress);
                                    return;
                                }
                            }
                        }
                        ThreadPool.QueueUserWorkItem(state => Module_FurAffinity.Queue_Prepare(WebAddress));
                        break;
                    }

                case "inkbunny.net":
                    {
                        if (URISplit[0].Equals("gallery") || URISplit[0].Equals("scraps"))
                        {
                            string NextPage = Module_Inkbunny.MultiPageCheck();
                            if (!string.IsNullOrEmpty(NextPage))
                            {
                                MessageBoxResult MessageBoxResultTemp = MessageBox.Show(Window_Main._RefHolder, "Would you lik to grab all pages?\nBrowser interaction will be disabled during the process.", "e621 ReBot", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                                if (MessageBoxResultTemp == MessageBoxResult.Yes)
                                {
                                    DoMultiPageGrab = true;
                                    BrowserControl._RefHolder.BrowserControls_Panel.IsEnabled = false;
                                    BrowserControl._RefHolder.IsHitTestVisible = false;

                                    string BaseAddress = $"https://inkbunny.net/{URISplit[0]}/{URISplit[1]}/1/{URISplit[3]}";
                                    Module_CefSharp.LoadURL(BaseAddress);
                                    return;
                                }
                            }
                        }
                        ThreadPool.QueueUserWorkItem(state => Module_Inkbunny.Queue_Prepare(WebAddress));
                        break;
                    }

                case "www.pixiv.net":
                    {
                        ThreadPool.QueueUserWorkItem(state => Module_Pixiv.Queue_Prepare(WebAddress));
                        break;

                    }

                case "www.hiccears.com":
                    {
                        ThreadPool.QueueUserWorkItem(state => Module_HicceArs.Queue_Prepare(WebAddress));
                        break;
                    }

                case "x.com": //was twitter.com before
                    {
                        ThreadPool.QueueUserWorkItem(state => Module_Twitter.Queue_Prepare(WebAddress));
                        break;
                    }

                case string Newgrounds when Newgrounds.Contains(".newgrounds.com"):
                    {
                        ThreadPool.QueueUserWorkItem(state => Module_Newgrounds.Queue_Prepare(WebAddress));
                        break;
                    }

                case string SoFurry when SoFurry.Contains(".sofurry.com"):
                    {
                        ThreadPool.QueueUserWorkItem(state => Module_SoFurry.Queue_Prepare(WebAddress));
                        break;
                    }

                case "www.weasyl.com":
                    {
                        ThreadPool.QueueUserWorkItem(state => Module_Weasyl.Queue_Prepare(WebAddress));
                        break;
                    }

                case "mastodon.social":
                    {
                        ThreadPool.QueueUserWorkItem(state => Module_Mastodons.Queue_Prepare(WebAddress, ref Module_CookieJar.Cookies_Mastodon));
                        break;
                    }

                case "baraag.net":
                    {
                        ThreadPool.QueueUserWorkItem(state => Module_Mastodons.Queue_Prepare(WebAddress, ref Module_CookieJar.Cookies_Baraag));
                        break;
                    }

                case "pawoo.net":
                    {
                        ThreadPool.QueueUserWorkItem(state => Module_Pawoo.Queue_Prepare(WebAddress));
                        break;
                    }

                case "www.hentai-foundry.com":
                    {
                        ThreadPool.QueueUserWorkItem(state => Module_HentaiFoundry.Queue_Prepare(WebAddress));
                        break;
                    }

                case "www.plurk.com":
                    {
                        ThreadPool.QueueUserWorkItem(state => Module_Plurk.Queue_Prepare(WebAddress));
                        break;
                    }
            }
        }

        private static Random RandomDelay = new Random();
        internal static void Grab_MultiPageTask(string WebAddress)
        {
            Uri TempURI = new Uri(WebAddress);
            string[] URISplit = TempURI.LocalPath.Split('/', StringSplitOptions.RemoveEmptyEntries);



            switch (TempURI.Host)
            {
                case "www.furaffinity.net":
                    {
                        if (URISplit[0].Equals("gallery") || URISplit[0].Equals("scraps"))
                        {
                            string NextPage = Module_FurAffinity.MultiPageCheck();
                            if (!string.IsNullOrEmpty(NextPage))
                            {
                                Thread.Sleep(RandomDelay.Next(100, 500));
                                Module_FurAffinity.Queue_MultiPage(WebAddress, NextPage);
                                return;
                            }


                        }
                        break;
                    }

                case "inkbunny.net":
                    {
                        if (URISplit[0].Equals("gallery") || URISplit[0].Equals("scraps"))
                        {
                            string NextPage = Module_Inkbunny.MultiPageCheck();
                            if (!string.IsNullOrEmpty(NextPage))
                            {
                                Thread.Sleep(RandomDelay.Next(4000, 5000));//Works only when going super slow,otherwise throws 429's
                                Module_Inkbunny.Queue_MultiPage(WebAddress, NextPage);
                                return;
                            }
                        }
                        break;
                    }
            }

            DoMultiPageGrab = false;
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                BrowserControl._RefHolder.BrowserControls_Panel.IsEnabled = true;
                BrowserControl._RefHolder.IsHitTestVisible = true;
            });
        }

        // - - - - - - - - - - - - - - - -

        internal readonly static ushort _GrabberMaxHandCount = 4;
        private static ushort GrabberActiveHandCount = 0;
        internal static DispatcherTimer _GrabTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private static void GrabTimer_Tick(object sender, EventArgs e)
        {
            _GrabTimer.Stop();
            if (Window_Main._RefHolder.Grab_CheckBox.IsChecked == true && Window_Main._RefHolder.Grab_TreeView.HasItems && GrabberActiveHandCount < _GrabberMaxHandCount)
            {
                TreeViewItem TreeViewItemParent = (TreeViewItem)Window_Main._RefHolder.Grab_TreeView.Items.GetItemAt(0);
                TreeViewItem TreeViewItemSender = TreeViewItemParent;
                TreeViewItem TreeViewItemDeleter = TreeViewItemSender;
                bool SelectorAtTopLevel = true;
                if (TreeViewItemParent.HasItems)
                {
                    if (TreeViewItemParent.Items.Count > 1) SelectorAtTopLevel = false;

                    TreeViewItemSender = (TreeViewItem)TreeViewItemSender.Items.GetItemAt(0);
                }

                // - - - - - - - - - - - - - - - -

                string WebAddress = Encoding.UTF8.GetString(Convert.FromHexString(TreeViewItemSender.Name.Substring(1)));

                if (DoMultiPageGrab)
                {
                    //Skip media grab if host of currently active browser Multi Page grab is the same to prevent 429's
                    Uri WebAddressURI = new Uri(WebAddress);
                    Uri BroserAddressURI = new Uri(Module_CefSharp.BrowserAddress);
                    if (WebAddressURI.Host == BroserAddressURI.Host)
                    {
                        _GrabTimer.Start();
                        return;
                    }
                }

                object? NeededData = TreeViewItemSender.Tag;
                ThreadPool.QueueUserWorkItem(state => Grab_2Media(WebAddress, NeededData));

                // - - - - - - - - - - - - - - - -

                if (SelectorAtTopLevel)
                {
                    Window_Main._RefHolder.Grab_TreeView.Items.RemoveAt(0);

                    _GrabTimer.Interval = TimeSpan.FromSeconds(1);
                }
                else
                {
                    TreeViewItemDeleter.Items.RemoveAt(0);
                    TreeViewItemDeleter.ToolTip = $"Pages left to grab: {TreeViewItemDeleter.Items.Count}";

                    if (TreeViewItemParent.HasItems && TreeViewItemParent.Header.ToString().StartsWith("https://x.com/"))
                    {
                        _GrabTimer.Interval = TimeSpan.FromMilliseconds(100);
                    }
                    else
                    {
                        _GrabTimer.Interval = TimeSpan.FromSeconds(1);
                    }
                }
            }
            _GrabTimer.Start();
        }

        internal static OrderedDictionary _GrabQueue_WorkingOn = new OrderedDictionary();
        private static void Grab_2Media(string WebAddress, object? NeededData)
        {
            GrabberActiveHandCount++;
            lock (_GrabQueue_WorkingOn)
            {
                _GrabQueue_WorkingOn.Add(WebAddress, null);
            }
            Report_Status();

            Uri TempURI = new Uri(WebAddress);
            switch (TempURI.Host)
            {
                case "www.furaffinity.net":
                    {
                        Module_FurAffinity.Grab(WebAddress, (string)NeededData);
                        break;
                    }

                case "inkbunny.net":
                    {
                        Module_Inkbunny.Grab(WebAddress, (string)NeededData);
                        break;
                    }

                case "www.pixiv.net":
                    {
                        Module_Pixiv.Grab(WebAddress);
                        break;
                    }

                case "www.hiccears.com":
                    {
                        Module_HicceArs.Grab(WebAddress, (string)NeededData);
                        break;
                    }

                case "x.com":
                    {
                        Module_Twitter.Grab(WebAddress, (string)NeededData);
                        break;
                    }

                case string Newgrounds when Newgrounds.Contains(".newgrounds.com"):
                    {
                        Module_Newgrounds.Grab(WebAddress, (string)NeededData);
                        break;
                    }

                case string SoFurry when SoFurry.Contains(".sofurry.com"):
                    {
                        Module_SoFurry.Grab(WebAddress);
                        break;
                    }

                case "www.weasyl.com":
                    {
                        Module_Weasyl.Grab(WebAddress, (string)NeededData);
                        break;
                    }

                case "mastodon.social":
                    {
                        Module_Mastodons.Grab(WebAddress, (string)NeededData, ref Module_CookieJar.Cookies_Mastodon);
                        break;
                    }

                case "baraag.net":
                    {
                        Module_Mastodons.Grab(WebAddress, (string)NeededData, ref Module_CookieJar.Cookies_Baraag);
                        break;
                    }

                case "pawoo.net":
                    {
                        Module_Pawoo.Grab(WebAddress, (string)NeededData);
                        break;
                    }

                case "www.hentai-foundry.com":
                    {
                        Module_HentaiFoundry.Grab(WebAddress, (string)NeededData);
                        break;
                    }

                case "www.plurk.com":
                    {
                        Module_Plurk.Grab(WebAddress, (string)NeededData);
                        break;
                    }
            }

            lock (_GrabQueue_URLs)
            {
                _GrabQueue_URLs.Remove(WebAddress);
            }
            Grab_3Finish();
        }

        internal static string? GetPageSource(string WebAddress, ref CookieContainer CookieRef, bool NewgroundsSpecialRequest = false)
        {
            //http client sucks, can't control cookies properly and it has bugs, fu ms, we're going using.
            using (HttpClientHandler HttpClientHandlerTemp = new HttpClientHandler() { CookieContainer = CookieRef ?? new CookieContainer(), AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate })
            {
                using (HttpClient HttpClientTemp = new HttpClient(HttpClientHandlerTemp) { Timeout = TimeSpan.FromSeconds(5) })
                {
                    HttpClientTemp.DefaultRequestHeaders.UserAgent.ParseAdd(AppSettings.GlobalUserAgent);

                    return GetPageResponseAsync(HttpClientTemp, WebAddress, NewgroundsSpecialRequest).Result;
                }
            }
        }

        internal static async Task<string?> GetPageResponseAsync(HttpClient HttpClientTemp, string WebAddress, bool NewgroundsSpecialRequest = false)
        {
            int TryCount = 0;
            do
            {
                using (HttpRequestMessage HttpRequestMessageTemp = new HttpRequestMessage(HttpMethod.Get, WebAddress))
                {
                    if (NewgroundsSpecialRequest) HttpRequestMessageTemp.Headers.Add("X-Requested-With", "XMLHttpRequest");

                    //Might need to handle timeout here, https://thomaslevesque.com/2018/02/25/better-timeout-handling-with-httpclient/
                    using (CancellationTokenSource CancellationTokenSourceTemp = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        try
                        {
                            HttpResponseMessage HttpResponseMessageTemp = await HttpClientTemp.SendAsync(HttpRequestMessageTemp, CancellationTokenSourceTemp.Token);

                            if (HttpResponseMessageTemp.IsSuccessStatusCode)
                            {
                                return await HttpResponseMessageTemp.Content.ReadAsStringAsync();
                            }
                        }
                        catch (TaskCanceledException) //handle timeout
                        {
                            HttpClientTemp.CancelPendingRequests();
                            TryCount++;
                            Thread.Sleep(500);
                        }
                    }
                }
            }
            while (TryCount < 3);

            return null;
        }

        internal static uint? GetMediaSize(string MediaURL)
        {
            HttpWebRequest GetSizeRequest = (HttpWebRequest)WebRequest.Create(MediaURL);
            GetSizeRequest.Method = "HEAD";
            GetSizeRequest.UserAgent = AppSettings.GlobalUserAgent;
            GetSizeRequest.Timeout = 3000;

            Uri TempURI = new Uri(MediaURL);
            switch (TempURI.Host)
            {
                case "i.pximg.net":
                    {
                        GetSizeRequest.Referer = "http://www.pixiv.net";
                        break;
                    }

                case "pbs.twimg.com": //Twitter sometimes not working otherwise
                    {
                        GetSizeRequest.CookieContainer = new CookieContainer();
                        break;
                    }

                    //case "www.hiccears.com":
                    //    {
                    //        GetSizeRequest.CookieContainer = Module_CookieJar.Cookies_HicceArs;
                    //        break;
                    //    }
                    //case "deviantart.com":
                    //    {
                    //        GetSizeRequest.CookieContainer = Module_CookieJar.Cookies_DeviantArt;
                    //        break;
                    //    }
            }

            try
            {
                using (HttpWebResponse GetSizeResponse = (HttpWebResponse)GetSizeRequest.GetResponse())
                {
                    if (GetSizeResponse.StatusCode == HttpStatusCode.OK)
                    {
                        return (GetSizeResponse.ContentLength < 0) ? null : (uint)GetSizeResponse.ContentLength;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (WebException)
            {
                return null;
            }
        }

        internal static ProgressBar? GetProgressBar(int SetMaxValue)
        {
            foreach (ProgressBar ProgressBarTemp in Window_Main._RefHolder.Grab_PogressBarPanel.Children)
            {
                if (!ProgressBarTemp.IsVisible)
                {
                    ProgressBarTemp.Value = 0;
                    ProgressBarTemp.Maximum = SetMaxValue;
                    ProgressBarTemp.Visibility = Visibility.Visible;
                    return ProgressBarTemp;
                }
            }
            return null;
        }

        internal static bool CheckShouldGrabConditions(string Post_MediaURL)
        {
            if (_Grabbed_MediaItems.ContainsURL(Post_MediaURL)) return false;

            if (AppSettings.MediaIgnoreList.Contains(Post_MediaURL)) return false;

            return true;
        }

        // - - - - - - - - - - - - - - - -

        internal static MediaItemList _Grabbed_MediaItems = new MediaItemList();
        private static void Grab_3Finish()
        {
            GrabberActiveHandCount--;
            Report_Status();

            lock (_GrabQueue_WorkingOn)
            {
                while (_GrabQueue_WorkingOn.Count > 0)
                {
                    if (_GrabQueue_WorkingOn[0] == null)
                    {
                        break;
                    }
                    else
                    {
                        //lock (_GrabQueue_URLs)
                        //{
                        //    _GrabQueue_URLs.Remove(_GrabQueue_WorkingOn.Cast<DictionaryEntry>().ElementAt(0).Key.ToString());
                        //}
                        lock (_Grabbed_MediaItems)
                        {
                            ushort CountBeforeCheck = (ushort)_Grabbed_MediaItems.Count;
                            if (_GrabQueue_WorkingOn[0].GetType() == typeof(MediaItem))
                            {
                                _Grabbed_MediaItems.Add((MediaItem)_GrabQueue_WorkingOn[0]);
                            }
                            else
                            {
                                _Grabbed_MediaItems.AddRange((List<MediaItem>)_GrabQueue_WorkingOn[0]);
                            }
                            for (int i = CountBeforeCheck; i < _Grabbed_MediaItems.Count; i++)
                            {
                                AppSettings.MediaRecord_Check(_Grabbed_MediaItems[i]);
                            }
                        }
                        _GrabQueue_WorkingOn.RemoveAt(0);
                    }
                }
            }
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.Grid_Populate();
                if (Window_Preview._RefHolder != null) Window_Preview._RefHolder.UpdateNavButtons();
            });
        }

        internal static GridVE? IsVisibleInGrid(MediaItem MediaItemRef)
        {
            if (_Grabbed_MediaItems.Count == 0) return null;

            int MediaItemIndex = _Grabbed_MediaItems.GetRange(Window_Main._RefHolder.Grid_ItemStartIndex, Math.Min(Window_Main._RefHolder.Grid_ItemLimit, _Grabbed_MediaItems.Count - Window_Main._RefHolder.Grid_ItemStartIndex)).IndexOf(MediaItemRef);
            if (MediaItemIndex > -1 && Window_Main._RefHolder.Grid_GridVEPanel.Children[MediaItemIndex] != null)
            {
                GridVE GridVERef = (GridVE)Window_Main._RefHolder.Grid_GridVEPanel.Children[MediaItemIndex];
                return GridVERef;
            }
            return null;
        }

        private static readonly HttpClientHandler GrabberThumbnail_HttpClientHandler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        private static readonly HttpClient GrabberThumbnail_HttpClient = new HttpClient(GrabberThumbnail_HttpClientHandler) { Timeout = TimeSpan.FromSeconds(15) };
        internal static async void Grab_Thumbnail(MediaItem MediaItemRef)
        {
            MediaItemRef.Grid_ThumbnailDLStart = true;
            using (HttpRequestMessage HttpRequestMessageTemp = new HttpRequestMessage(HttpMethod.Get, MediaItemRef.Grab_ThumbnailURL))
            {
                HttpRequestMessageTemp.Headers.UserAgent.ParseAdd(AppSettings.GlobalUserAgent);
                HttpRequestMessageTemp.Headers.Referrer = new Uri($"https://{new Uri(MediaItemRef.Grab_PageURL).Host}");

                GridVE GridVERef = IsVisibleInGrid(MediaItemRef);
                try
                {
                    using (HttpResponseMessage HttpResponseMessageTemp = await GrabberThumbnail_HttpClient.SendAsync(HttpRequestMessageTemp))
                    {
                        if (HttpResponseMessageTemp.IsSuccessStatusCode)
                        {
                            if (!_Grabbed_MediaItems.Contains(MediaItemRef)) return;

                            using (MemoryStream MemoryStreamTemp = new MemoryStream())
                            {
                                await HttpResponseMessageTemp.Content.CopyToAsync(MemoryStreamTemp);
                                MemoryStreamTemp.Seek(0, SeekOrigin.Begin);

                                BitmapImage? DownloadedImage = new BitmapImage();
                                DownloadedImage.BeginInit();
                                DownloadedImage.CacheOption = BitmapCacheOption.OnLoad;
                                DownloadedImage.StreamSource = MemoryStreamTemp;
                                DownloadedImage.EndInit();
                                //DownloadedImage.Freeze(); //Might need to be outside stream using

                                //string MediaType = MediaItemRef.Grab_MediaURL.Substring(MediaItemRef.Grab_MediaURL.LastIndexOf('.'));
                                //if (MediaType.Length > 5)
                                //{
                                //    throw new Exception($"{MediaType} is not a supported format for media.");
                                //}

                                //if (MediaItemRef.Grab_ThumbnailURL.EndsWith(".gif") || MediaItemRef.Grab_ThumbnailURL.Contains(".gif"))
                                //{
                                //    using (GifBitmapDecoder GifBitmapDecoderTemp = new GifBitmapDecoder(MemoryStreamTemp, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default))
                                //    {
                                //        Debug.WriteLine(GifBitmapDecoderTemp.Frames.Count);
                                //    }
                                //}

                                MediaItemRef.Grid_Thumbnail = Grab_ResizeThumbnail(DownloadedImage, $".{MediaItemRef.Grid_MediaFormat}");
                                Grab_MakeThumbnailInfoText(MediaItemRef);
                                DownloadedImage.StreamSource = null;
                                DownloadedImage.Freeze();
                                DownloadedImage = null;

                                if (GridVERef != null) GridVERef.LoadImage();
                            }
                        }
                    }
                }
                catch (TaskCanceledException) //Timout that was manually set
                {
                    GridVERef.IsUploaded_SetText("Timeout!");
                }
                catch (Exception)
                {
                    GridVERef.IsUploaded_SetText("Exception!");
                }
            }
        }

        internal static ImageSource Grab_ResizeThumbnail(BitmapImage BitmapImageRef, string ThumbType)
        {
            float LargerSize = Math.Max(BitmapImageRef.PixelWidth, BitmapImageRef.PixelHeight);
            float scale_factor = LargerSize > 200 ? 200 / LargerSize : 1;
            int NewImageWidth = (int)(BitmapImageRef.PixelWidth * scale_factor);
            int NewImageHeight = (int)(BitmapImageRef.PixelHeight * scale_factor);
            TransformedBitmap ScaledBitmap = new TransformedBitmap(BitmapImageRef, new ScaleTransform(scale_factor, scale_factor));

            DrawingVisual DrawingVisualTemp = new DrawingVisual();
            //RenderOptions.SetBitmapScalingMode(DrawingVisualTemp, BitmapScalingMode.HighQuality);
            using (DrawingContext DrawingContextTemp = DrawingVisualTemp.RenderOpen())
            {
                DrawingContextTemp.DrawRectangle(Brushes.Transparent, null, new Rect(new Size(200, 200)));
                Point ImageCenterPoint = new Point(100 - (NewImageWidth / 2), 100 - (NewImageHeight / 2));
                DrawingContextTemp.DrawImage(ScaledBitmap, new Rect(ImageCenterPoint, new Size(NewImageWidth, NewImageHeight)));

                FormattedText FormattedTextTemp = new FormattedText(ThumbType, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial Black"), 16, Brushes.LightSteelBlue, 96)
                {
                    TextAlignment = TextAlignment.Right
                };
                Point TextCenterPoint = new Point(176, 0);
                DrawingContextTemp.DrawGeometry(Brushes.LightSteelBlue, new Pen(Brushes.Black, 3), FormattedTextTemp.BuildGeometry(TextCenterPoint));
                DrawingContextTemp.DrawText(FormattedTextTemp, TextCenterPoint);
            }
            RenderTargetBitmap RenderTargetBitmapTemp = new RenderTargetBitmap(200, 200, 96, 96, PixelFormats.Pbgra32);
            RenderTargetBitmapTemp.Render(DrawingVisualTemp);
            RenderTargetBitmapTemp.Freeze();

            return RenderTargetBitmapTemp;
        }

        internal static void Grab_MakeThumbnailInfoText(MediaItem MediaItemRef)
        {
            if (MediaItemRef.Grid_Thumbnail == null) return;
            if (MediaItemRef.Grid_ThumbnailFullInfo != true) return;

            DrawingVisual DrawingVisualTemp = new DrawingVisual();
            using (DrawingContext DrawingContextTemp = DrawingVisualTemp.RenderOpen())
            {
                DrawingContextTemp.DrawImage(MediaItemRef.Grid_Thumbnail, new Rect(new Point(0, 0), new Size(200, 200)));
                if (MediaItemRef.Grid_MediaByteLength != null)
                {
                    FormattedText FormattedTextTemp = new FormattedText($"{MediaItemRef.Grid_MediaByteLength / 1024:N0} kB", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial Black"), 12, Brushes.LightSteelBlue, 96)
                    {
                        TextAlignment = TextAlignment.Center
                    };
                    Point TextCenterPoint = new Point(100, 182);
                    DrawingContextTemp.DrawGeometry(Brushes.LightSteelBlue, new Pen(Brushes.Black, 3), FormattedTextTemp.BuildGeometry(TextCenterPoint));
                    DrawingContextTemp.DrawText(FormattedTextTemp, TextCenterPoint);
                }
                if (MediaItemRef.Grid_MediaWidth != null)
                {
                    FormattedText FormattedTextTemp = new FormattedText($"{MediaItemRef.Grid_MediaWidth} x {MediaItemRef.Grid_MediaHeight}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Arial Black"), 12, Brushes.LightSteelBlue, 96)
                    {
                        TextAlignment = TextAlignment.Left
                    };
                    Point TextCenterPoint = new Point(20, 4);
                    DrawingContextTemp.DrawGeometry(Brushes.LightSteelBlue, new Pen(Brushes.Black, 3), FormattedTextTemp.BuildGeometry(TextCenterPoint));
                    DrawingContextTemp.DrawText(FormattedTextTemp, TextCenterPoint);
                }
            }
            RenderTargetBitmap RenderTargetBitmapTemp = new RenderTargetBitmap(200, 200, 96, 96, PixelFormats.Default);
            RenderTargetBitmapTemp.Render(DrawingVisualTemp);
            RenderTargetBitmapTemp.Freeze();

            MediaItemRef.Grid_Thumbnail = null;
            MediaItemRef.Grid_Thumbnail = RenderTargetBitmapTemp;
            MediaItemRef.Grid_ThumbnailFullInfo = null;
        }
    }
}