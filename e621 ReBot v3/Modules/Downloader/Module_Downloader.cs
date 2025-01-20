using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CefSharp;
using e621_ReBot_v3.CustomControls;
using e621_ReBot_v3.Modules.Converter;
using e621_ReBot_v3.Modules.Downloader;
using DownloadItem = e621_ReBot_v3.CustomControls.DownloadItem;

namespace e621_ReBot_v3.Modules
{
    internal enum ActionType
    {
        Upload,
        Download,
        Conversion,
        Manual,
        Update
    }

    internal static class Module_Downloader
    {
        internal static readonly List<Regex> _DownloadEnabler;
        static Module_Downloader()
        {
            _DownloadTimer.Tick += DownloadTimer_Tick;
            Download_BGW.DoWork += DownloadBGW_Start;
            _DownloadVEFinisherTimer.Tick += DownloadVEFinisherTimer_Tick;

            _DownloadEnabler = new List<Regex>
            {
                new Regex(@".+e621.net/(posts(/\d+|\?.+)?|pools/\d+|favorites|popular)"),
                new Regex(@".+www.furaffinity.net/view/\d+/"),
                new Regex(@".+inkbunny.net/s/\d+"),
                //new Regex(@".+www.furaffinity.net/(view|full|gallery|scraps|favorites)/.+/"),
                //new Regex(@".+www.furaffinity.net/search/"),
                //new Regex(@".+inkbunny.net/(s|gallery|scraps)/\w+"),
                //new Regex(@".+inkbunny.net/submissionsviewall.php"),
                //new Regex(@".+pixiv.net/\w+/(artworks|users)/\d+"),
                //new Regex(@".+www.hiccears.com/(contents|file)/.+"),
                //new Regex(@".+www.hiccears.com/p/.+/illustrations"),
                //new Regex(@".+x.com/.+/(media|status/\d+/?)"),
                //new Regex(@".+.newgrounds.com/(movies/?|portal/view/\d+|art/?(view/.+|\w+)?)"),
                //new Regex(@".+.sofurry.com/(view/\d+|browse/\w+/art?uid=\d+|artwork|photos)"),
                //new Regex(@".+www.weasyl.com/((~.+/)?submissions(/\d+/)?|collections).+"),
                //new Regex(@".+www.weasyl.com/search\?find=submit"),
                //new Regex(@".+www.hentai-foundry.com/(user/.+/faves/pictures|users/.+)"),
                //new Regex(@".+www.hentai-foundry.com/pictures/(user/.+|featured|popular|random|recent/)"),
                //new Regex(@".+pawoo.net/@.+/(\d+|media)"),
                //new Regex(@".+www.plurk.com/(p/)?\w+"),
                //new Regex(@".+mastodon.social/@.+/(\d+|media)"),
                //new Regex(@".+baraag.net/@.+(\d+|media)"),

                //- - - Download only

                new Regex(@".+derpibooru.org/(images/?|search\?|galleries/)(\d+)?"),
                new Regex(@".+itaku.ee/((images|posts)/\d+|profile/\w+/gallery)")

            };

            for (int i = 0; i < 8; i++)
            {
                Custom_WebClient ThumbClient = new Custom_WebClient();
                ThumbClient.DownloadDataCompleted += Download_ThumbnailDLFinished;
                Holder_ThumbClient.Add(ThumbClient);
                Custom_WebClient FileClient = new Custom_WebClient();
                FileClient.DownloadProgressChanged += Download_FileDLProgressReport;
                FileClient.DownloadFileCompleted += Download_FileDLFinished;
                Holder_FileClient.Add(FileClient);
            }
        }

        internal static void Start()
        {
            _DownloadTimer.Start();
        }

        internal static void DownloadEnabler(string WebAddress)
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                foreach (Regex URLTest in _DownloadEnabler)
                {
                    Match MatchTemp = URLTest.Match(WebAddress);
                    if (MatchTemp.Success)
                    {
                        BrowserControl._RefHolder.BB_Download.Tag = MatchTemp.Value;
                        BrowserControl._RefHolder.BB_Download.Visibility = Visibility.Visible;
                        return;
                    }
                }
            });
        }

        internal static Dictionary<string, string> MediaBrowser_MediaCache = new Dictionary<string, string>();
        internal static List<string> Download_AlreadyDownloaded = new List<string>();
        internal static DownloadItemList _2Download_DownloadItems = new DownloadItemList();

        // - - - - - - - - - - - - - - - -

        internal static string MediaFile_GetFileNameOnly(string FullNamePath, string? MediaFormat = null)
        {
            if (FullNamePath.Contains("?token="))
            {
                FullNamePath = FullNamePath.Substring(0, FullNamePath.IndexOf("?token="));
            }
            if (FullNamePath.Contains("sofurryfiles.com"))
            {
                FullNamePath = FullNamePath.Substring(FullNamePath.LastIndexOf('&') + 1);
            }
            if (FullNamePath.Contains("pbs.twimg.com"))
            {
                FullNamePath = FullNamePath.Replace(":orig", "");
            }
            if (FullNamePath.EndsWith("/download", StringComparison.OrdinalIgnoreCase))
            {
                FullNamePath = $"{FullNamePath.Substring(0, FullNamePath.LastIndexOf("/download"))}.{MediaFormat}";
            }

            FullNamePath = HttpUtility.UrlDecode(FullNamePath).Substring(FullNamePath.LastIndexOf('/') + 1);

            return FullNamePath;
        }

        internal static string MediaFile_RenameFileName(string FileName, DownloadItem DownloadItemRef)
        {
            string NewFileName = FileName;

            switch (AppSettings.NamingPattern_Web)
            {
                case 0: //Original
                    {
                        //NewFileName = FileName;
                        break;
                    }

                case 1: //Artist_Title_....
                    {
                        if (DownloadItemRef.Grab_Title.Contains("Created by") || DownloadItemRef.Grab_Title.Contains("Plurk by"))
                        {
                            goto case 2;
                        }
                        string TitleSubstring = DownloadItemRef.Grab_Title;
                        TitleSubstring = TitleSubstring.Substring(0, TitleSubstring.IndexOf(" ⮘ by ")).Substring(2);
                        NewFileName = $"{DownloadItemRef.Grab_Artist}_{TitleSubstring}_{FileName.Substring(0, 4)}.{DownloadItemRef.Grab_MediaFormat}";
                        NewFileName = string.Join(null, NewFileName.Split(Path.GetInvalidFileNameChars()));
                        break;
                    }

                case 2: //Artist_Original
                    {
                        NewFileName = $"{DownloadItemRef.Grab_Artist}_{FileName}";
                        NewFileName = string.Join(null, NewFileName.Split(Path.GetInvalidFileNameChars()));
                        break;
                    }
            }

            return NewFileName;
        }

        internal static bool ReSaveMedia(DownloadItem DownloadItemRef)
        {
            Uri DomainURL = new Uri(DownloadItemRef.Grab_PageURL);
            string HostString = DomainURL.Host.Remove(DomainURL.Host.LastIndexOf('.')).Replace("www.", "");
            HostString = $"{new CultureInfo("en-US", false).TextInfo.ToTitleCase(HostString)}\\";

            string PurgeArtistName = DownloadItemRef.Grab_Artist.Replace('/', '-');
            PurgeArtistName = Path.GetInvalidFileNameChars().Aggregate(PurgeArtistName, (current, c) => current.Replace(c.ToString(), string.Empty));
            string FolderPath = Path.Combine(AppSettings.Download_FolderLocation, HostString, PurgeArtistName);
            Directory.CreateDirectory(FolderPath);

            //string GetFileNameOnly = MediaFile_GetFileNameOnly(DownloadItemRef.Grab_MediaURL, DownloadItemRef.Grab_MediaFormat);
            //if (GetFileNameOnly.EndsWith(".", StringComparison.Ordinal))
            //{
            //    GetFileNameOnly += Module_HicceArs.GetHicceArsMediaType((string)DataRowRef["Grab_MediaURL"]);
            //}

            string MediaName = MediaFile_GetFileNameOnly(DownloadItemRef.Grab_MediaURL);
            string MediaRename = MediaFile_RenameFileName(MediaName, DownloadItemRef);
            string FullFilePath = Path.Combine(FolderPath, MediaRename);
            DownloadItemRef.MediaItemRef.DL_FilePath = FullFilePath;

            if (!Download_AlreadyDownloaded.Contains(DownloadItemRef.Grab_MediaURL))
            {
                lock (Download_AlreadyDownloaded)
                {
                    Download_AlreadyDownloaded.Add(DownloadItemRef.Grab_MediaURL);
                }
            }

            if (!File.Exists(FullFilePath) && MediaBrowser_MediaCache.ContainsKey(MediaName))
            {
                File.Copy(MediaBrowser_MediaCache[MediaName], FullFilePath, true);
                return true;
            }

            return false;
        }

        // - - - - - - - - - - - - - - - -

        internal static byte[] DownloadFileBytes(string DownloadURL, ActionType ActionTypeEnum, ProgressBar? ProgressBarRef = null)
        {
            HttpWebRequest FileDownloader = (HttpWebRequest)WebRequest.Create(DownloadURL);
            FileDownloader.Timeout = 5000;
            switch (DownloadURL)
            {
                case string Ugoira when Ugoira.Contains("ugoira"):
                    {
                        FileDownloader.Referer = "https://www.pixiv.net/";
                        break;
                    }
                case string Ugoira when Ugoira.StartsWith("https://www.hiccears.com/file/"):
                    {
                        FileDownloader.CookieContainer = Module_CookieJar.Cookies_HicceArs;
                        break;
                    }
            }

            using (MemoryStream DownloadedBytes = new MemoryStream())
            {
                using (WebResponse DownloaderReponse = FileDownloader.GetResponse())
                {
                    using (Stream DownloadStream = DownloaderReponse.GetResponseStream())
                    {
                        byte[] DownloadBuffer = new byte[65536]; // 64 kB buffer
                        while (DownloadedBytes.Length < DownloaderReponse.ContentLength)
                        {
                            int DownloadStreamPartLength = DownloadStream.Read(DownloadBuffer, 0, DownloadBuffer.Length);
                            if (DownloadStreamPartLength > 0)
                            {
                                DownloadedBytes.Write(DownloadBuffer, 0, DownloadStreamPartLength);
                                float ReportPercentage = (float)DownloadedBytes.Length / DownloaderReponse.ContentLength;

                                switch (ActionTypeEnum)
                                {
                                    case ActionType.Upload:
                                        {
                                            Module_Uploader.Report_Status($"Downloading Media...{ReportPercentage:P0}");
                                            break;
                                        }
                                    case ActionType.Download:
                                        {
                                            ProgressBarRef.Dispatcher.BeginInvoke(() => { ProgressBarRef.Value = (int)(ReportPercentage * 100); });
                                            break;
                                        }

                                    case ActionType.Conversion:
                                        {

                                            //string ReportType = isUgoira ? "CDU" : "CDV";
                                            //Module_FFmpeg.ReportConversionProgress(ReportType, ReportPercentage, in DataRowRef);
                                            break;
                                        }

                                    case ActionType.Manual:
                                        {
                                            //if (Form_Preview._FormReference != null && Form_Preview._FormReference.IsHandleCreated && ReferenceEquals(Form_Preview._FormReference.Preview_RowHolder, DataRowRef))
                                            //{
                                            //    Form_Preview._FormReference.BeginInvoke(new Action(() =>
                                            //    {
                                            //        if (Form_Preview._FormReference != null && Form_Preview._FormReference.IsHandleCreated)
                                            //        {
                                            //            Form_Preview._FormReference.Label_Download.Text = $"{ReportPercentage:P0}";
                                            //            Form_Preview._FormReference.Label_Download.Visible = true;
                                            //            Form_Preview._FormReference.PB_Download.Visible = false;
                                            //            Form_Preview._FormReference.Label_DownloadWarning.Visible = false;
                                            //        }
                                            //    }));
                                            //}
                                            break;
                                        }

                                    case ActionType.Update:
                                        {

                                            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
                                            {
                                                Window_Main._RefHolder.Update_TextBlock.Text = $"Downloading Update...{ReportPercentage:P0}";
                                            });
                                            break;
                                        }
                                }
                            }
                        }
                    }
                }

                DownloadedBytes.Seek(0, SeekOrigin.Begin);
                return DownloadedBytes.ToArray();
            }
        }

        internal static void SaveFileBytes(ActionType ActionTypeEnum, in byte[] bytes2Save, string FileName, string? DownloadFolder = null)
        {
            //HicceArs filename fix for extension is not needed, e621 detects it.

            string SavePath = $"FFMpegTemp\\{(ActionTypeEnum == ActionType.Upload ? "Upload\\" : null)}{FileName}";
            using (MemoryStream bytes2Stream = new MemoryStream(bytes2Save))
            {
                if (FileName.Contains("ugoira"))
                {
                    using (ZipArchive UgoiraZip = new ZipArchive(bytes2Stream, ZipArchiveMode.Read))
                    {
                        UgoiraZip.ExtractToDirectory(SavePath);

                        //if (DownloadFolder != null && Properties.Settings.Default.Converter_KeepOriginal)
                        //{
                        //    Directory.CreateDirectory(DownloadFolder);
                        //    DownloadedBytes.Seek(0, SeekOrigin.Begin);
                        //    using (FileStream TempFileStream = new FileStream($"{DownloadFolder}\\{FileName}", FileMode.Create))
                        //    {
                        //        DownloadedBytes.WriteTo(TempFileStream);
                        //    }
                        //}
                    }
                }
                else
                {
                    using (FileStream FileStreamTemp = new FileStream(SavePath, FileMode.Create))
                    {
                        bytes2Stream.WriteTo(FileStreamTemp);
                    }
                    //if (DownloadFolder != null && (Properties.Settings.Default.Converter_KeepOriginal || ActionType.Manual))
                    //{
                    //    Directory.CreateDirectory(DownloadFolder);
                    //    File.Copy($"{TempFolder}\\{FileName}", $"{DownloadFolder}\\{FileName}", true);
                    //}
                }
            }
        }

        // - - - - - - - - - - - - - - - -

        internal static void UpdateDownloadTreeViewText()
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() => { Window_Main._RefHolder.DownloadQueue_CheckBox.Content = $"Download Queue ({_2Download_DownloadItems.Count})"; });
        }

        internal static int DownloadNodeMax = 28;
        internal static int DownloadTreeViewPage = 0;

        internal static void UpdateDownloadTreeView()
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                TreeViewItem? TreeViewItemTemp;
                if (_2Download_DownloadItems.Count > 0)
                {
                    int StartIndex = DownloadTreeViewPage * DownloadNodeMax;
                    if (StartIndex > (_2Download_DownloadItems.Count - 1))
                    {
                        DownloadTreeViewPage = (int)Math.Floor((_2Download_DownloadItems.Count - 1) / (float)DownloadNodeMax);
                        StartIndex = DownloadTreeViewPage * DownloadNodeMax;
                    }

                    int NodesOnPage = Math.Min(DownloadNodeMax, _2Download_DownloadItems.Count - StartIndex);
                    int TreeNodeCount = Window_Main._RefHolder.DownloadQueue_TreeView.Items.Count;
                    if (TreeNodeCount != NodesOnPage)
                    {
                        for (int i = 0; i < Math.Abs(TreeNodeCount - NodesOnPage); i++)
                        {
                            if (TreeNodeCount < NodesOnPage)
                            {
                                Window_Main._RefHolder.DownloadQueue_TreeView.Items.Add(new TreeViewItem());
                                //TreeViewItemTemp.ContextMenu.Opened += ContextMenu_Opened;
                                //TreeViewItemTemp.ContextMenu.Closed += ContextMenu_Closed;
                            }
                            else
                            {
                                Window_Main._RefHolder.DownloadQueue_TreeView.Items.RemoveAt(0);
                            }
                        }
                    }

                    for (int i = 0; i < NodesOnPage; i++)
                    {
                        TreeViewItemTemp = (TreeViewItem)Window_Main._RefHolder.DownloadQueue_TreeView.Items[i];
                        TreeViewItemTemp.Header = _2Download_DownloadItems[i + StartIndex].Grab_MediaURL;
                        TreeViewItemTemp.Foreground = (SolidColorBrush)Application.Current.FindResource("ThemeForeground");
                    }
                    Window_Main._RefHolder.DownloadQueue_DownloadPageUp.IsEnabled = DownloadTreeViewPage != 0;
                    Window_Main._RefHolder.DownloadQueue_DownloadPageDown.IsEnabled = _2Download_DownloadItems.Count > (StartIndex + DownloadNodeMax);
                }
                else
                {
                    DownloadTreeViewPage = 0;
                    Window_Main._RefHolder.DownloadQueue_TreeView.Items.Clear();
                    Window_Main._RefHolder.DownloadQueue_DownloadPageUp.IsEnabled = false;
                    Window_Main._RefHolder.DownloadQueue_DownloadPageDown.IsEnabled = false;
                }
                Window_Main._RefHolder.DownloadQueue_CheckBox.Content = $"Download Queue ({_2Download_DownloadItems.Count})";

                if (Window_Main._RefHolder.DownloadTreeViewContextMenuHolderTarget != null)
                {
                    TreeViewItemTemp = (TreeViewItem)Window_Main._RefHolder.DownloadTreeViewContextMenuHolder.PlacementTarget;
                    if (TreeViewItemTemp.Parent != null)
                    {
                        if (!TreeViewItemTemp.Header.ToString().Equals(Window_Main._RefHolder.DownloadTreeViewContextMenuHolderTarget))
                        {
                            TreeViewItemTemp = Window_Main._RefHolder.DownloadQueue_TreeView.FindTreeViewItemByHeader(Window_Main._RefHolder.DownloadTreeViewContextMenuHolderTarget);
                        }

                        if (TreeViewItemTemp == null)
                        {
                            ((TreeViewItem)Window_Main._RefHolder.DownloadTreeViewContextMenuHolder.PlacementTarget).ContextMenu.IsOpen = false;
                        }
                        else
                        {
                            TreeViewItemTemp.Foreground = (SolidColorBrush)Application.Current.FindResource("ThemeFocus");
                        }
                    }
                }
                Window_Main._RefHolder.Download_SessionDownloadsTextBlock.Text = $"Media Downloaded: {SessionDownloads}";
            });
        }

        internal static void Report_Info(string InfoMessage)
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.Download_InfoTextBox.Text = $"{DateTime.Now.ToLongTimeString()}, {InfoMessage}\n{Window_Main._RefHolder.Download_InfoTextBox.Text}";
            });
        }

        // - - - - - - - - - - - - - - - -
        internal static void AddDownloadItem2Queue(string PageURL, string MediaURL, string? ThumbnailURL = null, string? Artist = null, string? Title = null, string? MediaFormat = null, string? e6PostID = null, string? e6PoolName = null, string? e6PoolPostIndex = null, bool e6Download = false, MediaItem? MediaItemRef = null)
        {
            DownloadItem DownloadItemTemp = new DownloadItem()
            {
                Grab_PageURL = PageURL,
                Grab_MediaURL = MediaURL,
                Grab_ThumbnailURL = ThumbnailURL,
                Grab_Artist = Artist,
                Grab_Title = Title,
                Grab_MediaFormat = MediaFormat,
                e6_PostID = e6PostID,
                e6_PoolName = e6PoolName ?? string.Empty,
                e6_PoolPostIndex = e6PoolPostIndex,
                Is_e6Download = e6Download,
                MediaItemRef = MediaItemRef
            };
            lock (_2Download_DownloadItems)
            {
                _2Download_DownloadItems.Add(DownloadItemTemp);
            }
        }

        private static DownloadVE? FindDownloadVE()
        {
            foreach (DownloadVE DownloadVETemp in Window_Main._RefHolder.Download_DownloadVEPanel.Children)
            {
                if (DownloadVETemp._DownloadFinished && DownloadVETemp._AlreadyCopied)
                {
                    return DownloadVETemp;
                }
            }
            return null;
        }

        // - - - - - - - - - - - - - - - -

        internal static void Grab_DownloadMedia(string WebAddress)
        {
            Uri TempURI = new Uri(WebAddress);
            switch (TempURI.Host)
            {
                case "e621.net":
                    {
                        Module_DLe621.GrabMediaLinks(WebAddress);
                        break;
                    }

                case "www.furaffinity.net":
                    {
                        Module_FurAffinity.GrabMediaLinks(WebAddress);
                        break;
                    }

                case "inkbunny.net":
                    {
                        Module_Inkbunny.GrabMediaLinks(WebAddress);
                        break;
                    }

                case "www.pixiv.net":
                    {
                        Module_Pixiv.GrabMediaLinks(WebAddress);
                        break;
                    }

                //case "www.hiccears.com":
                //    {
                //        Module_HicceArs.Grab(WebAddress, (string)NeededData);
                //        break;
                //    }

                //case "x.com":
                //    {
                //        Module_Twitter.Grab(WebAddress, (string)NeededData);
                //        break;
                //    }

                //case string Newgrounds when Newgrounds.Contains(".newgrounds.com"):
                //    {
                //        Module_Newgrounds.Grab(WebAddress, (string)NeededData);
                //        break;
                //    }

                //case string SoFurry when SoFurry.Contains(".sofurry.com"):
                //    {
                //        Module_SoFurry.Grab(WebAddress);
                //        break;
                //    }

                //case "www.weasyl.com":
                //    {
                //        Module_Weasyl.Grab(WebAddress, (string)NeededData);
                //        break;
                //    }

                //case "mastodon.social":
                //    {
                //        Module_Mastodons.Grab(WebAddress, (string)NeededData, ref Module_CookieJar.Cookies_Mastodon);
                //        break;
                //    }

                //case "baraag.net":
                //    {
                //        Module_Mastodons.Grab(WebAddress, (string)NeededData, ref Module_CookieJar.Cookies_Baraag);
                //        break;
                //    }

                //case "pawoo.net":
                //    {
                //        Module_Pawoo.Grab(WebAddress, (string)NeededData);
                //        break;
                //    }

                //case "www.hentai-foundry.com":
                //    {
                //        Module_HentaiFoundry.Grab(WebAddress, (string)NeededData);
                //        break;
                //    }

                //case "www.plurk.com":
                //    {
                //        Module_Plurk.Grab(WebAddress, (string)NeededData);
                //        break;
                //    }

                case "derpibooru.org":
                    {
                        Module_Derpibooru.GrabMediaLinks(WebAddress);
                        break;
                    }

                case "itaku.ee":
                    {
                        Module_Itaku.GrabMediaLinks(WebAddress);
                        break;
                    }
            }
            UpdateDownloadTreeView();
        }

        internal static string? SelectFolderPopup(string? LastValue)
        {
            string? InputedText = Window_Main._RefHolder.Dispatcher.Invoke(() => { return Custom_InputBox.ShowInputBox(Window_Main._RefHolder, "e621 ReBot", "If you want to download media to a separate folder, enter a folder name below.", BrowserControl._RefHolder.BQB_Start.PointToScreen(new Point(0, 0)), LastValue ?? string.Empty); });
            if (InputedText.Equals("☠") || string.IsNullOrEmpty(InputedText))
            {
                InputedText = null;
            }
            else
            {
                InputedText = string.Join(null, InputedText.Split(Path.GetInvalidFileNameChars()));
                InputedText = InputedText.Trim();
            }
            return InputedText;
        }

        // - - - - - - - - - - - - - - - -

        private static readonly List<Custom_WebClient> Holder_ThumbClient = new List<Custom_WebClient>();
        private static readonly List<Custom_WebClient> Holder_FileClient = new List<Custom_WebClient>();
        private static void Download_StartDLClient(DownloadVE DownloadVERef, string DowwnloadType)
        {
            string SiteReferer = $"https://{new Uri(DownloadVERef._DownloadItemRef.Grab_MediaURL).Host}";
            Custom_WebClient? WebClientSelected = null;
            foreach (Custom_WebClient WebClientTemp in (DowwnloadType.Equals("Thumb") ? Holder_ThumbClient : Holder_FileClient))
            {
                if (!WebClientTemp.IsBusy)
                {
                    WebClientSelected = WebClientTemp;
                    break;
                }
            }
            WebClientSelected.Headers.Add(HttpRequestHeader.Referer, SiteReferer);

            switch (SiteReferer)
            {
                case "https://e621.net":
                    {
                        WebClientSelected.Headers.Add(HttpRequestHeader.UserAgent, AppSettings.AppName);
                        break;
                    }

                case "https://www.hiccears.com":
                    {
                        string BaseURL = "https://www.hiccears.com";
                        List<CefSharp.Cookie> CookieList = Cef.GetGlobalCookieManager().VisitUrlCookiesAsync(BaseURL, true).Result;
                        foreach (CefSharp.Cookie CookieHolder in CookieList)
                        {
                            if (CookieHolder.Name.Equals("hiccears"))
                            {
                                WebClientSelected.Headers.Add(HttpRequestHeader.Cookie, $"{CookieHolder.Name}={CookieHolder.Value}");
                                break;
                            }
                        }
                        break;
                    }
            }

            if (DowwnloadType.Equals("Thumb"))
            {
                WebClientSelected.DownloadDataAsync(new Uri(DownloadVERef._DownloadItemRef.Grab_ThumbnailURL), DownloadVERef);
            }
            else
            {
                //DownloadVERef.ToolTip = (DownloadVERef._DownloadItemRef.Grab_MediaURL);
                WebClientSelected.DownloadFileAsync(new Uri(DownloadVERef._DownloadItemRef.Grab_MediaURL), $"{DownloadVERef.FolderIcon.Tag}.dlpart", DownloadVERef);
            }
        }

        private static void Download_ThumbnailDLFinished(object sender, DownloadDataCompletedEventArgs e)
        {
            DownloadVE DownloadVETemp = (DownloadVE)e.UserState;
            if (DownloadVETemp._DownloadItemRef == null) return; //file download finished before thumb?
            if (e.Error != null)
            {
                Report_Info($"Thumb DL Error @{DownloadVETemp._DownloadItemRef.Grab_ThumbnailURL}, Msg:{e.Error.Message}");
                return;
            } 
            if (e.Result == null) return; // same?

            using (MemoryStream MemoryStreamTemp = new MemoryStream(e.Result))
            {
                MemoryStreamTemp.Seek(0, SeekOrigin.Begin);

                BitmapImage? DownloadedImage = new BitmapImage();
                DownloadedImage.BeginInit();
                DownloadedImage.CacheOption = BitmapCacheOption.OnLoad;
                DownloadedImage.StreamSource = MemoryStreamTemp;
                DownloadedImage.EndInit();
                DownloadedImage.StreamSource = null;

                if (DownloadVETemp._DownloadItemRef.MediaItemRef == null)
                {
                    DownloadVETemp.cThumbnail_Image.Source = DownloadedImage;
                    DownloadedImage.Freeze();
                }
                else
                {
                    DownloadVETemp._DownloadItemRef.MediaItemRef.Grid_Thumbnail = Module_Grabber.Grab_ResizeThumbnail(DownloadedImage, $".{DownloadVETemp._DownloadItemRef.Grab_MediaFormat}");
                    Module_Grabber.Grab_MakeThumbnailInfoText(DownloadVETemp._DownloadItemRef.MediaItemRef);
                    DownloadVETemp.cThumbnail_Image.Source = DownloadVETemp._DownloadItemRef.MediaItemRef.Grid_Thumbnail;
                    DownloadedImage.Freeze();
                    DownloadedImage = null;
                }
            }
        }

        private static void Download_FileDLProgressReport(object sender, DownloadProgressChangedEventArgs e)
        {
            ((DownloadVE)e.UserState).DownloadProgress.Value = e.ProgressPercentage;
        }

        private static uint SessionDownloads = 0;
        private static void Download_FileDLFinished(object? sender, AsyncCompletedEventArgs e)
        {
            DownloadVE DownloadVETemp = (DownloadVE)e.UserState;

            string PicURL = DownloadVETemp._DownloadItemRef.Grab_MediaURL;
            if (e.Cancelled) //timeout detected, cancelled it;
            {
                Window_Main._RefHolder.DownloadQueue_CheckBox.IsChecked = false;
                MessageBox.Show(Window_Main._RefHolder, "Timeout has been detected, further downloads have been paused!", "e621 ReBot Downloader", MessageBoxButton.OK, MessageBoxImage.Warning);

                if (!_2Download_DownloadItems.ContainsURL(PicURL) && !Download_AlreadyDownloaded.Contains(PicURL))
                {
                    lock (_2Download_DownloadItems)
                    {
                        _2Download_DownloadItems.Insert(0, DownloadVETemp._DownloadItemRef);
                    }
                }
                DLThreadsWaiting++;
                DownloadVETemp.DownloadFinish();
                UpdateDownloadTreeView();
            }
            else
            {
                if (e.Error != null)
                {
                    string ErrorMsg = e.Error.InnerException == null ? e.Error.Message : e.Error.InnerException.Message;
                    if (ErrorMsg.Contains("An existing connection was forcibly closed by the remote host."))
                    {

                        if (!_2Download_DownloadItems.ContainsURL(PicURL) && !Download_AlreadyDownloaded.Contains(PicURL))
                        {
                            lock (_2Download_DownloadItems)
                            {
                                _2Download_DownloadItems.Insert(0, DownloadVETemp._DownloadItemRef);
                            }
                        }
                        DLThreadsWaiting++;
                        DownloadVETemp.DownloadFinish();
                        UpdateDownloadTreeView();
                    }
                    else
                    {
                        MessageBox.Show(Window_Main._RefHolder, $"{PicURL}\n{ErrorMsg}", "e621 ReBot Downloader", MessageBoxButton.OK, MessageBoxImage.Error);
                        throw e.Error;
                    }
                }
            }

            string TempFilePath = $"{DownloadVETemp.FolderIcon.Tag}.dlpart";
            FileInfo FileInfoTemp = new FileInfo(TempFilePath);
            if (FileInfoTemp.Exists)
            {
                FileInfoTemp.MoveTo(Path.ChangeExtension(TempFilePath, null)); //Change back to normal name
            }

            SessionDownloads++;
            DownloadVETemp._DownloadFinished = true;
            _DownloadVEFinisherTimer.Stop();
            _DownloadVEFinisherTimer.Start();
        }

        internal static DispatcherTimer _DownloadVEFinisherTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        private static void DownloadVEFinisherTimer_Tick(object? sender, EventArgs e)
        {
            _DownloadVEFinisherTimer.Stop();
            foreach (DownloadVE DownloadVETemp in Window_Main._RefHolder.Download_DownloadVEPanel.Children)
            {
                if (DownloadVETemp._DownloadFinished && !DownloadVETemp._AlreadyCopied)
                {
                    //if (!DownloadVETemp._AlreadyCopied)
                    //{
                    //    DataRow DataRowTemp = (DataRow)e6_DownloadItemTemp.Tag;

                    lock (Download_AlreadyDownloaded)
                    {
                        Download_AlreadyDownloaded.Add(DownloadVETemp._DownloadItemRef.Grab_MediaURL);
                    }
                    //    Image ImageHolder = e6_DownloadItemTemp.picBox_ImageHolder.Tag == null ? e6_DownloadItemTemp.picBox_ImageHolder.BackgroundImage : null;

                    //    AddPic2FLP((string)DataRowTemp["Grab_MediaURL"], e6_DownloadItemTemp.DL_FolderIcon.Tag.ToString(), ImageHolder);
                    //    e6_DownloadItemTemp.picBox_ImageHolder.Tag = null;
                    //    e6_DownloadItemTemp._AlreadyCopied = true;

                    //    if (e6_DownloadItemTemp.DataRow4Grid != null)
                    //    {
                    //        e6_DownloadItemTemp.DataRow4Grid["DL_FilePath"] = e6_DownloadItemTemp.DL_FolderIcon.Tag.ToString();
                    //    }
                    //}
                    DownloadVETemp.DownloadFinish();
                    DLThreadsWaiting++;
                }
            }

            if (Window_Main._RefHolder.Download_DownloadVEPanel.Children.Count > AppSettings.Download_ThreadsCount)
            {
                int DifferenceRequired = AppSettings.Download_ThreadsCount - Window_Main._RefHolder.Download_DownloadVEPanel.Children.Count;
                DownloadVE? DownloadVETemp;
                for (int i = Window_Main._RefHolder.Download_DownloadVEPanel.Children.Count - 1; i >= 0; i--)
                {
                    DownloadVETemp = (DownloadVE?)Window_Main._RefHolder.Download_DownloadVEPanel.Children[i];
                    if (DownloadVETemp._DownloadFinished && DownloadVETemp._AlreadyCopied)
                    {
                        Window_Main._RefHolder.Download_DownloadVEPanel.Children.RemoveAt(i);
                        DLThreadsWaiting--;
                        DifferenceRequired++;
                        if (DifferenceRequired == 0) break;
                    }
                }
            }

            if (_2Download_DownloadItems.Count == 0)
            {
                UpdateDownloadTreeView();
            }

            if (Download_AlreadyDownloaded.Count % 1000 == 0)
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
            }
        }

        // - - - - - - - - - - - - - - - -

        internal static DispatcherTimer _DownloadTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private static void DownloadTimer_Tick(object? sender, EventArgs e)
        {
            if (Window_Main._RefHolder.DownloadQueue_CheckBox.IsChecked == true && _2Download_DownloadItems.Count > 0 && !Download_BGW.IsBusy)
            {
                Download_BGW.RunWorkerAsync();
            }
        }

        internal static ushort DLThreadsWaiting = 4;
        private static readonly BackgroundWorker Download_BGW = new BackgroundWorker();
        private static void DownloadBGW_Start(object? sender, DoWorkEventArgs e)
        {
            DownloadItem? DownloadItemTemp;
            while (DLThreadsWaiting > 0 && _2Download_DownloadItems.Count > 0)
            {
                DownloadItemTemp = _2Download_DownloadItems[0];
                lock (_2Download_DownloadItems)
                {
                    _2Download_DownloadItems.RemoveAt(0);
                }

                DLThreadsWaiting--;
                if (DownloadItemTemp.Is_e6Download)
                {
                    DownloadFrom_e6URL(DownloadItemTemp);
                }
                else
                {
                    DownloadFrom_URL(DownloadItemTemp);
                }
            }
            UpdateDownloadTreeView();
        }

        private static void DownloadFrom_e6URL(DownloadItem DownloadItemRef)
        {
            string PicURL = DownloadItemRef.Grab_MediaURL;

            string GetFileNameOnly = MediaFile_GetFileNameOnly(PicURL);
            GetFileNameOnly = GetFileNameOnly.Substring(0, GetFileNameOnly.LastIndexOf('.'));

            string DownloadPath = Path.Combine(AppSettings.Download_FolderLocation, @"e621\");
            string PoolName = DownloadItemRef.e6_PoolName;
            if (PoolName != null) DownloadPath += $"{PoolName}\\";
            Directory.CreateDirectory(DownloadPath);

            switch (AppSettings.NamingPattern_e6)
            {
                case 0:
                    {
                        GetFileNameOnly = $"{GetFileNameOnly}.{DownloadItemRef.Grab_MediaFormat}";
                        break;
                    }
                case 1:
                    {
                        GetFileNameOnly = $"{DownloadItemRef.e6_PostID}.{DownloadItemRef.Grab_MediaFormat}";
                        break;
                    }
                case 2:
                    {
                        GetFileNameOnly = $"{GetFileNameOnly}_{DownloadItemRef.e6_PostID}.{DownloadItemRef.Grab_MediaFormat}";
                        break;
                    }
                case 3:
                    {
                        GetFileNameOnly = $"{DownloadItemRef.e6_PostID}_{GetFileNameOnly}.{DownloadItemRef.Grab_MediaFormat}";
                        break;
                    }
            }
            if (DownloadItemRef.e6_PoolPostIndex != null)
            {
                GetFileNameOnly = $"{DownloadItemRef.e6_PoolPostIndex}_{GetFileNameOnly}.{DownloadItemRef.Grab_MediaFormat}";
            }

            string FilePath = Path.Combine(DownloadPath, GetFileNameOnly);
            if (File.Exists(FilePath))
            {
                    DLThreadsWaiting++;
                    return; // Don't need duplicates
            }

            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                DownloadVE DownloadVETemp = FindDownloadVE();
                DownloadVETemp._DownloadItemRef = DownloadItemRef;
                DownloadVETemp.FolderIcon.Tag = FilePath;
                DownloadVETemp.DownloadStartup();
                if (DownloadItemRef.Grab_MediaFormat.Equals("swf"))
                {
                    DownloadVETemp.cThumbnail_Image.Source = new ImageSourceConverter().ConvertFrom(Properties.Resources.E6Image_Flash) as ImageSource;
                }
                else
                {
                    Download_StartDLClient(DownloadVETemp, "Thumb");
                }
                Download_StartDLClient(DownloadVETemp, "File");
            });
        }

        private static void DownloadFrom_URL(DownloadItem DownloadItemRef)
        {
            Uri DomainURL = new Uri(DownloadItemRef.Grab_PageURL);
            string HostString = DomainURL.Host.Remove(DomainURL.Host.LastIndexOf('.')).Replace("www.", "");
            HostString = $"{new CultureInfo("en-US", false).TextInfo.ToTitleCase(HostString)}\\";

            string PurgeArtistName = DownloadItemRef.Grab_Artist.Replace('/', '-');
            PurgeArtistName = Path.GetInvalidFileNameChars().Aggregate(PurgeArtistName, (current, c) => current.Replace(c.ToString(), string.Empty));
            string FolderPath = Path.Combine(AppSettings.Download_FolderLocation, HostString, PurgeArtistName, DownloadItemRef.e6_PoolName);
            Directory.CreateDirectory(FolderPath);

            string GetFileNameOnly = MediaFile_GetFileNameOnly(DownloadItemRef.Grab_MediaURL, DownloadItemRef.Grab_MediaFormat);
            //if (GetFileNameOnly.EndsWith(".", StringComparison.Ordinal))
            //{
            //    GetFileNameOnly += Module_HicceArs.GetHicceArsMediaType((string)DataRowRef["Grab_MediaURL"]);
            //}

            switch (GetFileNameOnly)
            {
                case string UgoiraTest when UgoiraTest.Contains("ugoira"):
                    {
                        string WebMName = $"{GetFileNameOnly.Substring(0, GetFileNameOnly.IndexOf("_ugoira0"))}_ugoira1920x1080.webm";
                        string ImageRename = MediaFile_RenameFileName(WebMName, DownloadItemRef);

                        string FilePath = Path.Combine(FolderPath, ImageRename);
                        if (File.Exists(FilePath))
                        {
                            Report_Info($"Ugoira WebM already exists, skipped coverting {DownloadItemRef.Grab_MediaURL}");
                            //Form_Loader._FormReference.BeginInvoke(new Action(() =>
                            //{
                            //    Form_Loader._FormReference.DownloadFLP_Downloaded.SuspendLayout();
                            //    UIDrawController.SuspendDrawing(Form_Loader._FormReference.DownloadFLP_Downloaded);
                            //    AddPic2FLP((string)DataRowRef["Grab_ThumbnailURL"], FilePath);
                            //    Form_Loader._FormReference.DownloadFLP_Downloaded.ResumeLayout();
                            //    UIDrawController.ResumeDrawing(Form_Loader._FormReference.DownloadFLP_Downloaded);
                            //}));
                            DLThreadsWaiting++;
                            return;
                        }

                        Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
                        {
                            DownloadVE DownloadVETemp = FindDownloadVE();
                            DownloadVETemp._DownloadItemRef = DownloadItemRef;
                            DownloadVETemp.FolderIcon.Tag = FilePath;
                            DownloadVETemp.DownloadStartup();

                            if (DownloadVETemp._DownloadItemRef.MediaItemRef == null)
                            {
                                Download_StartDLClient(DownloadVETemp, "Thumb");
                            }
                            else
                            {
                                if (DownloadVETemp._DownloadItemRef.MediaItemRef.Grid_Thumbnail != null)
                                {
                                    DownloadVETemp.cThumbnail_Image.Source = DownloadVETemp._DownloadItemRef.MediaItemRef.Grid_Thumbnail;
                                }
                                else
                                {
                                    if (!DownloadVETemp._DownloadItemRef.MediaItemRef.Grid_ThumbnailDLStart)
                                    {
                                        DownloadVETemp._DownloadItemRef.MediaItemRef.Grid_ThumbnailDLStart = true;
                                        Download_StartDLClient(DownloadVETemp, "Thumb");
                                    }
                                }
                            }
                            ThreadPool.QueueUserWorkItem(state => Module_FFMpeg.DownloadQueue_Ugoira2WebM(DownloadVETemp));
                        });
                        break;
                    }

                default:
                    {
                        string ImageRename = MediaFile_RenameFileName(GetFileNameOnly, DownloadItemRef);

                        string FilePath = Path.Combine(FolderPath, ImageRename);
                        if (File.Exists(FilePath) || (MediaBrowser_MediaCache.Keys.Contains(GetFileNameOnly) && ReSaveMedia(DownloadItemRef)))
                        {
                            //Form_Loader._FormReference.BeginInvoke(new Action(() =>
                            //{
                            //    Form_Loader._FormReference.DownloadFLP_Downloaded.SuspendLayout();
                            //    UIDrawController.SuspendDrawing(Form_Loader._FormReference.DownloadFLP_Downloaded);
                            //    AddPic2FLP((string)DataRowRef["Grab_ThumbnailURL"], FilePath);
                            //    Form_Loader._FormReference.DownloadFLP_Downloaded.ResumeLayout();
                            //    UIDrawController.ResumeDrawing(Form_Loader._FormReference.DownloadFLP_Downloaded);
                            //}));
                            DLThreadsWaiting++;
                            return;
                        }

                        Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
                        {
                            DownloadVE DownloadVETemp = FindDownloadVE();
                            DownloadVETemp._DownloadItemRef = DownloadItemRef;
                            DownloadVETemp.FolderIcon.Tag = FilePath;
                            DownloadVETemp.DownloadStartup();

                            //Weasyl special
                            if (string.IsNullOrEmpty(DownloadVETemp._DownloadItemRef.Grab_ThumbnailURL) || DownloadVETemp._DownloadItemRef.Grab_ThumbnailURL.Contains("cdn.weasyl.com"))
                            {
                                DownloadVETemp.cThumbnail_Image.Source = new ImageSourceConverter().ConvertFrom(Properties.Resources.BrowserIcon_Weasyl) as ImageSource;
                            }
                            else
                            {
                                if (DownloadVETemp._DownloadItemRef.MediaItemRef == null)
                                {
                                    Download_StartDLClient(DownloadVETemp, "Thumb");
                                }
                                else
                                {
                                    if (DownloadVETemp._DownloadItemRef.MediaItemRef.Grid_Thumbnail != null)
                                    {
                                        DownloadVETemp.cThumbnail_Image.Source = DownloadVETemp._DownloadItemRef.MediaItemRef.Grid_Thumbnail;
                                    }
                                    else
                                    {
                                        if (!DownloadVETemp._DownloadItemRef.MediaItemRef.Grid_ThumbnailDLStart)
                                        {
                                            DownloadVETemp._DownloadItemRef.MediaItemRef.Grid_ThumbnailDLStart = true;
                                            Download_StartDLClient(DownloadVETemp, "Thumb");
                                        }
                                    }
                                }
                            }

                            switch (DownloadVETemp._DownloadItemRef.Grab_MediaFormat)
                            {
                                //case "mp4":
                                case "swf":
                                    {
                                        if (AppSettings.Converter_DontConvertVideos)
                                        {
                                            Download_StartDLClient(DownloadVETemp, "File");
                                        }
                                        else
                                        {
                                            ThreadPool.QueueUserWorkItem(state => Module_FFMpeg.DownloadQueue_Video2WebM(DownloadVETemp));
                                        }
                                        break;
                                    }

                                default:
                                    {
                                        Download_StartDLClient(DownloadVETemp, "File");
                                        break;
                                    }
                            }
                        });
                        break;
                    }
            }
        }
    }
}