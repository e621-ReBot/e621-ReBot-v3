using e621_ReBot_v3.CustomControls;
using e621_ReBot_v3.Modules;
using e621_ReBot_v3.Modules.Downloader;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace e621_ReBot_v3
{
    public partial class Window_PoolWatcher : Window
    {
        internal static Window_PoolWatcher? _RefHolder;
        public Window_PoolWatcher()
        {
            InitializeComponent();
            Owner = Window_Main._RefHolder;
            _RefHolder = this;
            App.SetWindow2Square(this);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppSettings.PoolWatcher.Any())
            {
                foreach (PoolItem PoolItemTemp in AppSettings.PoolWatcher)
                {
                    PoolVE PoolVETemp = new PoolVE() { _PoolItemRef = PoolItemTemp };
                    PoolWatcher_WrapPanel.Children.Add(PoolVETemp);
                }
            }
            SortPoolWatcher_StackPanel.IsEnabled = PoolWatcher_WrapPanel.Children.Count > 1;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _RefHolder = null;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        [GeneratedRegex(@".+e621.net/pools/\d+/?")]
        private static partial Regex PoolWatcherRegex();
        internal static readonly Regex _PoolWatcherEnabler = PoolWatcherRegex();
        internal static void PoolWatcherEnabler(string WebAddress)
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Match MatchTemp = _PoolWatcherEnabler.Match(WebAddress);
                if (MatchTemp.Success)
                {
                    string PoolID = WebAddress.Substring(WebAddress.LastIndexOf('/') + 1);
                    PoolItem? PoolItemTemp = AppSettings.PoolWatcher.Where(PoolItem => PoolItem.ID == int.Parse(PoolID)).SingleOrDefault();
                    if (PoolItemTemp == null)
                    {
                        BrowserControl._RefHolder.BB_PoolWatcher.Content = "Watch";
                        BrowserControl._RefHolder.BB_PoolWatcher.Tag = null;
                    }
                    else
                    {
                        BrowserControl._RefHolder.BB_PoolWatcher.Content = "Unwatch";
                        BrowserControl._RefHolder.BB_PoolWatcher.Tag = PoolID;
                    }
                    BrowserControl._RefHolder.BB_PoolWatcher.Visibility = Visibility.Visible;
                    return;
                }
            });
        }

        internal static void PoolWatcher_AddPool2Watch()
        {
            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(Module_CefSharp.BrowserHTMLSource);

            string PoolID = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//li[@id='subnav-show']/a").Attributes["href"].Value;
            PoolID = PoolID.Substring(PoolID.LastIndexOf('/') + 1);

            if (AppSettings.PoolWatcher.Any())
            {
                PoolItem? PoolItemTemp = AppSettings.PoolWatcher.Where(PoolItem => PoolItem.ID == int.Parse(PoolID)).SingleOrDefault();
                if (PoolItemTemp != null)
                {
                    Window_Main._RefHolder.Dispatcher.BeginInvoke(() => { MessageBox.Show(Window_Main._RefHolder, $"Pool with ID#{PoolID} is already being watched.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information); });
                    return;
                }
            }

            string ThumbnailURL = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//div[@id='posts']/section[@class='posts-container']/article[@data-md5]").Attributes["data-md5"].Value;
            ThumbnailURL = $"https://static1.e621.net/data/preview/{ThumbnailURL.Substring(0, 2)}/{ThumbnailURL.Substring(2, 2)}/{ThumbnailURL}.jpg";

            string PoolName = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//div[@id='c-pools']/div[@id='a-show']/h2/a").InnerText.Trim();
            PoolItem PoolItemRef = new PoolItem { ID = int.Parse(PoolID), Name = PoolName, ThumbnailURL = ThumbnailURL };
            lock (AppSettings.PoolWatcher)
            {
                AppSettings.PoolWatcher.Add(PoolItemRef);
            }
            GetThumbnail(PoolItemRef);
            PoolWatcher_Check4New();
        }

        internal static async void PoolWatcher_Check4New()
        {
            if (AppSettings.PoolWatcher.Any())
            {
                int PageSize = 75; //new API limit
                for (int i = 0; i < Math.Ceiling(AppSettings.PoolWatcher.Count / (double)PageSize); i++)
                {
                    string ListSlice = string.Join(',', AppSettings.PoolWatcher.Select(PoolItem => PoolItem.ID).Skip(i * PageSize).Take(PageSize));
                    Task<string?> RunTaskFirst = new Task<string?>(() => Module_e621Data.DataDownload($"https://e621.net/pools.json?search[id]={ListSlice}").GetAwaiter().GetResult());
                    lock (Module_e621APIController.BackgroundTasks)
                    {
                        Module_e621APIController.BackgroundTasks.Add(RunTaskFirst);
                    }
                    string e6JSONResult = await RunTaskFirst;

                    if (string.IsNullOrEmpty(e6JSONResult) || e6JSONResult.StartsWith('ⓔ') || e6JSONResult.Length < 32) return;

                    JArray? PoolArray = JArray.Parse(e6JSONResult);
                    Dictionary<int, PoolItem> PoolPosts2Get = new Dictionary<int, PoolItem>();
                    foreach (JToken SinglePoolData in PoolArray.Children())
                    {
                        int Pool_ID = (int)SinglePoolData["id"];
                        string Pool_Name = ((string)SinglePoolData["name"]).Replace('_', ' ').Trim();
                        List<int> Pool_IDs = SinglePoolData["post_ids"].Values<int>().ToList();
                        int Pool_PostCount = (int)SinglePoolData["post_count"];

                        PoolItem? PoolItemTemp = AppSettings.PoolWatcher.Where(PoolItem => PoolItem.ID == Pool_ID).SingleOrDefault();
                        if (PoolItemTemp != null)
                        {
                            List<int> NewPostsIfAny = Pool_IDs;
                            if (PoolItemTemp.PostIDs != null && PoolItemTemp.PostIDs.Any()) NewPostsIfAny = Pool_IDs.Except(PoolItemTemp.PostIDs).ToList();
                            if (NewPostsIfAny.Any())
                            {
                                PoolItemTemp.Name = Pool_Name;
                                PoolItemTemp.PostIDs = Pool_IDs;
                                foreach (int PostID in NewPostsIfAny)
                                {
                                    if (!PoolPosts2Get.ContainsKey(PostID)) PoolPosts2Get.Add(PostID, PoolItemTemp);
                                }
                            }
                        }
                    }

                    if (PoolPosts2Get.Keys.Any())
                    {
                        GetNewMedia(PoolPosts2Get);
                    }
                }
            }
        }

        private static async void GetNewMedia(Dictionary<int, PoolItem> PoolPosts2Get)
        {
            int ItemsAddedCount = 0;
            int PageSize = 75; //new API limit
            for (int i = 0; i < Math.Ceiling(PoolPosts2Get.Keys.Count / (double)PageSize); i++)
            {
                string ListSlice = string.Join(',', PoolPosts2Get.Keys.ToList().Skip(i * PageSize).Take(PageSize));
                Task<string?> RunTaskFirst = new Task<string?>(() => Module_e621Data.DataDownload($"https://e621.net/posts.json?tags=id:{ListSlice}").GetAwaiter().GetResult());
                lock (Module_e621APIController.BackgroundTasks)
                {
                    Module_e621APIController.BackgroundTasks.Add(RunTaskFirst);
                }
                string e6JSONResult = await RunTaskFirst;
                if (string.IsNullOrEmpty(e6JSONResult) || e6JSONResult.StartsWith('ⓔ') || e6JSONResult.Length < 32) return;

                JToken PostData = JObject.Parse(e6JSONResult)["posts"];
                foreach (JToken PostDataDetailed in PostData.Children())
                {
                    int Post_ID = (int)PostDataDetailed["id"];
                    string Pool_Name = PoolPosts2Get[Post_ID].Name;
                    Pool_Name = string.Join(null, Pool_Name.Split(Path.GetInvalidFileNameChars()));

                    string MediaURLTemp;
                    string ThumbnailURLTemp;
                    Module_DLe621.MD5_2_URL(PostDataDetailed, out MediaURLTemp, out ThumbnailURLTemp);

                    if (Module_Downloader.CheckDownloadQueue4Duplicate(MediaURLTemp, Pool_Name)) continue;

                    Module_Downloader.AddDownloadItem2Queue(
                                          PageURL: $"https://e621.net/posts/{Post_ID}",
                                          MediaURL: MediaURLTemp,
                                          ThumbnailURL: ThumbnailURLTemp,
                                          MediaFormat: (string)PostDataDetailed["file"]["ext"],
                                          e6PostID: Post_ID.ToString(),
                                          e6PoolName: Pool_Name,
                                          e6PoolPostIndex: PoolPosts2Get[Post_ID].PostIDs.IndexOf(Post_ID).ToString(),
                                          e6Download: true);
                    ItemsAddedCount++;
                }
            }
            if (ItemsAddedCount > 0)
            {
                Module_Downloader.Report_Info($"Pool Watcher >>> Started download of {ItemsAddedCount} image{(ItemsAddedCount > 1 ? "s" : null)}");
                Module_Downloader.UpdateDownloadTreeView();
            }
        }

        private void SortRadioButton_Click(object sender, RoutedEventArgs e)
        {
            int TagSender = int.Parse(((RadioButton)sender).Tag.ToString());
            List<PoolItem> PoolItemsTemp = AppSettings.PoolWatcher;
            switch (TagSender)
            {
                case 0:
                    {
                        break;
                    }
                case 1:
                    {
                        PoolItemsTemp = PoolItemsTemp.OrderBy(f => f.ID).ToList();
                        break;
                    }
                case 2:
                    {
                        PoolItemsTemp = PoolItemsTemp.OrderBy(f => f.Name).ToList();
                        break;
                    }
            }
            PoolWatcher_WrapPanel.Children.Clear();
            foreach (PoolItem PoolItemTemp in PoolItemsTemp)
            {
                PoolWatcher_WrapPanel.Children.Add(new PoolVE() { _PoolItemRef = PoolItemTemp });
            }
        }

        // - - - - - - - - - - - - - - - -

        private static HttpClientHandler Thumbnail_HttpClientHandler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
        private static HttpClient Thumbnail_HttpClient = new HttpClient(Thumbnail_HttpClientHandler) { Timeout = TimeSpan.FromSeconds(15) };
        internal static async void GetThumbnail(PoolItem PoolItemRef)
        {
            using (HttpRequestMessage HttpRequestMessageTemp = new HttpRequestMessage(HttpMethod.Get, PoolItemRef.ThumbnailURL))
            {
                HttpRequestMessageTemp.Headers.UserAgent.ParseAdd(AppSettings.GlobalUserAgent);
                using (HttpResponseMessage HttpResponseMessageTemp = await Thumbnail_HttpClient.SendAsync(HttpRequestMessageTemp))
                {
                    if (HttpResponseMessageTemp.IsSuccessStatusCode)
                    {
                        using (MemoryStream MemoryStreamTemp = new MemoryStream())
                        {
                            await HttpResponseMessageTemp.Content.CopyToAsync(MemoryStreamTemp);
                            MemoryStreamTemp.Seek(0, SeekOrigin.Begin);

                            PoolItemRef.Thumbnail = Convert.ToBase64String(MemoryStreamTemp.ToArray());
                        }
                    }
                }
            }
        }
    }
}