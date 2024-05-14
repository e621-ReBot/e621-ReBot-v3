using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using e621_ReBot_v3.CustomControls;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace e621_ReBot_v3.Modules.Downloader
{
    internal static class Module_DLe621
    {
        internal static string? SpecialSaveFolder;
        internal static bool CancellationPending = false;

        internal static void Grab(string WebAddress)
        {
            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(Module_CefSharp.BrowserHTMLSource);
            string[] URLParts = WebAddress.Split('/', StringSplitOptions.RemoveEmptyEntries);

            switch (URLParts[2])
            {
                case string Posts when Posts.StartsWith("posts"):
                    {
                        string? PicURL;
                        string? PostID;
                        string? ThumbURL;
                        string? Media_Format;
                        if (URLParts.Length > 3 && URLParts[3] != null && URLParts[3].All(char.IsDigit)) //single
                        {
                            PicURL = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//div[@id='image-download-link']/a").Attributes["href"].Value;
                            if (Module_Downloader._2Download_DownloadItems.ContainsURL(PicURL) || Module_Downloader.Download_AlreadyDownloaded.Contains(PicURL))
                            {
                                return;
                            }

                            PostID = WebAddress;
                            if (PostID.Contains('?')) PostID = PostID.Substring(0, PostID.IndexOf('?'));
                            PostID = PostID.Substring(PostID.LastIndexOf('/') + 1);
                            ThumbURL = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//section[@id='image-container']").Attributes["data-preview-file-url"].Value;
                            Media_Format = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//section[@id='image-container']").Attributes["data-file-ext"].Value;

                            Module_Downloader.AddDownloadItem2Queue(
                                PageURL: WebAddress,
                                MediaURL: PicURL,
                                ThumbnailURL: ThumbURL,
                                MediaFormat: Media_Format,
                                e6PostID: PostID,
                                e6Download: true);
                        }
                        else //multi
                        {
                            SpecialSaveFolder = Module_Downloader.SelectFolderPopup(SpecialSaveFolder);
                            if (WebAddress.Contains("/posts?tags="))
                            {
                                if (string.IsNullOrEmpty(AppSettings.APIKey) || HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//div[@class='paginator']/menu").ChildNodes.Count <= 3)
                                {
                                    goto GrabPageOnly_Tags;
                                }

                                MessageBoxResult MessageBoxResultTemp = Window_Main._RefHolder.Dispatcher.Invoke(() => { return MessageBox.Show(Window_Main._RefHolder, "Do you want to download all images with current tags?\nPress no if you want current page only.", "Download", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes); });
                                if (MessageBoxResultTemp == MessageBoxResult.Yes)
                                {
                                    string TagQuery = WebAddress;
                                    TagQuery = TagQuery.Substring(TagQuery.IndexOf("tags=") + 5);

                                    Grab_MediaWithTags(TagQuery, SpecialSaveFolder);
                                    return;
                                }
                                else
                                {
                                    goto GrabPageOnly_Tags;
                                }
                            }

                        GrabPageOnly_Tags:
                            HtmlNodeCollection NodeSelector = HtmlDocumentTemp.DocumentNode.SelectNodes(".//div[@id='posts-container']/article");
                            if (NodeSelector != null)
                            {
                                foreach (HtmlNode Post in NodeSelector)
                                {
                                    if (!Post.Attributes["class"].Value.Contains("blacklisted"))
                                    {
                                        PicURL = Post.Attributes["data-file-url"].Value;
                                        if (Module_Downloader._2Download_DownloadItems.ContainsURL(PicURL) || Module_Downloader.Download_AlreadyDownloaded.Contains(PicURL))
                                        {
                                            continue;
                                        }

                                        Module_Downloader.AddDownloadItem2Queue(
                                            PageURL: WebAddress,
                                            MediaURL: PicURL,
                                            ThumbnailURL: Post.Attributes["data-preview-file-url"].Value,
                                            MediaFormat: Post.Attributes["data-file-ext"].Value,
                                            e6PostID: Post.Attributes["data-id"].Value,
                                            e6PoolName: SpecialSaveFolder,
                                            e6Download: true);
                                    }
                                }
                            }
                        }
                        break;
                    }

                case "pools":
                    {
                        HtmlNode BottomMenuHolder = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//div[@class='paginator']/menu");
                        if (string.IsNullOrEmpty(AppSettings.APIKey) || BottomMenuHolder.SelectSingleNode(".//div[@class='paginator']/menu").ChildNodes.Count <= 3)
                        {
                            goto GrabPageOnly_Pools;
                        }

                        MessageBoxResult MessageBoxResultTemp = Window_Main._RefHolder.Dispatcher.Invoke(() => { return MessageBox.Show(Window_Main._RefHolder, "Do you want to download the whole pool?\nPress no if you want current page only.", "Download", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes); });
                        if (MessageBoxResultTemp == MessageBoxResult.OK)
                        {
                            string PoolID = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//li[@id='subnav-show']/a").Attributes["href"].Value;
                            PoolID = PoolID.Substring(PoolID.LastIndexOf('/') + 1);

                            Grab_Pool(PoolID);
                            return;
                        }

                    GrabPageOnly_Pools:
                        HtmlNodeCollection NodeSelector = HtmlDocumentTemp.DocumentNode.SelectNodes(".//div[@id='a-show']//article");
                        if (NodeSelector != null)
                        {
                            int GetCurrentPage = int.Parse(BottomMenuHolder.SelectSingleNode(".//li[@class='current-page']").InnerText);

                            var PoolPages = new List<string>();
                            if (GetCurrentPage > 1)
                            {
                                string PoolID = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//div[@id='a-show']//a").Attributes["href"].Value.Replace("/posts?tags=pool%3A", "");

                                string? JSON_PoolData = Module_e621Data.DataDownload($"https://e621.net/pools/{PoolID}.json");
                                if (string.IsNullOrEmpty(JSON_PoolData) || JSON_PoolData.StartsWith('ⓔ') || JSON_PoolData.Length < 32) return;

                                PoolPages = JObject.Parse(Module_e621Data.DataDownload($"https://e621.net/pools/{PoolID}.json"))["post_ids"].Values<string>().ToList();
                            }

                            int PoolIndex = 0;
                            foreach (HtmlNode Post in NodeSelector)
                            {
                                if (Post.Attributes["data-flags"].Value.Equals("deleted")) continue;

                                string PicURL = Post.Attributes["data-file-url"].Value;
                                if (Module_Downloader._2Download_DownloadItems.ContainsURL(PicURL) || Module_Downloader.Download_AlreadyDownloaded.Contains(PicURL))
                                {
                                    continue;
                                }

                                string PostID = Post.Attributes["data-id"].Value;
                                string PoolName = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//div[@id='a-show']//a").InnerText;
                                PoolName = string.Join(null, PoolName.Split(Path.GetInvalidFileNameChars()));
                                string PoolPostIndex = GetCurrentPage > 1 ? PoolPages.IndexOf(PostID).ToString() : PoolIndex.ToString();

                                Module_Downloader.AddDownloadItem2Queue(
                                           PageURL: WebAddress,
                                           MediaURL: PicURL,
                                           ThumbnailURL: Post.Attributes["data-preview-file-url"].Value,
                                           MediaFormat: Post.Attributes["data-file-ext"].Value,
                                           e6PostID: Post.Attributes["data-id"].Value,
                                           e6PoolName: PoolName,
                                           e6PoolPostIndex: PoolPostIndex,
                                           e6Download: true);

                                PoolIndex += 1;
                            }
                        }
                        break;
                    }

                case "popular":
                    {
                        HtmlNodeCollection NodeSelector = HtmlDocumentTemp.DocumentNode.SelectNodes(".//div[@id='posts-container']/article");
                        if (NodeSelector != null)
                        {
                            SpecialSaveFolder = Module_Downloader.SelectFolderPopup(SpecialSaveFolder);
                            foreach (HtmlNode Post in NodeSelector)
                            {
                                if (!Post.Attributes["class"].Value.Contains("blacklisted"))
                                {
                                    string PicURL = Post.Attributes["data-file-url"].Value;
                                    if (Module_Downloader._2Download_DownloadItems.ContainsURL(PicURL) || Module_Downloader.Download_AlreadyDownloaded.Contains(PicURL))
                                    {
                                        continue;
                                    }

                                    Module_Downloader.AddDownloadItem2Queue(
                                          PageURL: WebAddress,
                                          MediaURL: PicURL,
                                          ThumbnailURL: Post.Attributes["data-preview-file-url"].Value,
                                          MediaFormat: Post.Attributes["data-file-ext"].Value,
                                          e6PostID: Post.Attributes["data-id"].Value,
                                          e6PoolName: SpecialSaveFolder,
                                          e6Download: true);
                                }
                            }
                        }
                        break;
                    }

                case "favorites":
                    {
                        SpecialSaveFolder = Module_Downloader.SelectFolderPopup(SpecialSaveFolder);
                        if (string.IsNullOrEmpty(AppSettings.APIKey) || HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//div[@class='paginator']/menu").ChildNodes.Count <= 3)
                        {
                            goto GrabPageOnly_Favorites;
                        }

                        MessageBoxResult MessageBoxResultTemp = Window_Main._RefHolder.Dispatcher.Invoke(() => { return MessageBox.Show(Window_Main._RefHolder, "Do you want to download all favorites?\nPress no if you want current page only.", "Download", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes); });
                        if (MessageBoxResultTemp == MessageBoxResult.Yes)
                        {
                            string TagQuery = WebAddress;
                            if (TagQuery.Contains("user_id"))
                            {
                                TagQuery = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//input[@id='tags']").Attributes["value"].Value;
                            }
                            else
                            {
                                if (TagQuery.Contains('?'))
                                {
                                    TagQuery = TagQuery.Substring(TagQuery.IndexOf("?") + 1);
                                }
                                else
                                {
                                    TagQuery = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//input[@id='tags']").Attributes["value"].Value;
                                }
                            }

                            Grab_MediaWithTags(TagQuery, SpecialSaveFolder);
                            return;
                        }

                    GrabPageOnly_Favorites:
                        HtmlNodeCollection NodeSelector = HtmlDocumentTemp.DocumentNode.SelectNodes(".//div[@id='posts-container']/article");
                        if (NodeSelector != null)
                        {
                            foreach (HtmlNode Post in NodeSelector)
                            {
                                if (!Post.Attributes["class"].Value.Contains("blacklisted"))
                                {
                                    string PicURL = Post.Attributes["data-file-url"].Value;
                                    if (Module_Downloader._2Download_DownloadItems.ContainsURL(PicURL) || Module_Downloader.Download_AlreadyDownloaded.Contains(PicURL))
                                    {
                                        continue;
                                    }

                                    Module_Downloader.AddDownloadItem2Queue(
                                       PageURL: WebAddress,
                                       MediaURL: PicURL,
                                       ThumbnailURL: Post.Attributes["data-preview-file-url"].Value,
                                       MediaFormat: Post.Attributes["data-file-ext"].Value,
                                       e6PostID: Post.Attributes["data-id"].Value,
                                       e6PoolName: SpecialSaveFolder,
                                       e6Download: true);
                                }
                            }
                        }
                        break;
                    }
            }
        }

        private static List<string> CreateTagList(JToken PostTags, string RatingTag)
        {
            List<string> TempList = new List<string>();
            foreach (JProperty TagCategory in PostTags.Children())
            {
                TempList.AddRange(TagCategory.First.ToObject<string[]>());
            }
            TempList.Add("rating:" + RatingTag);
            return TempList;
        }

        internal static void MD5_2_URL(JToken cPost, out string MediaURL, out string ThumbnailURL)
        {
            string MD5 = cPost["file"]["md5"].Value<string>();
            MediaURL = $"https://static1.e621.net/data/{MD5.Substring(0, 2)}/{MD5.Substring(2, 2)}/{MD5}.{cPost["file"]["ext"].Value<string>()}";
            ThumbnailURL = $"https://static1.e621.net/data/preview/{MD5.Substring(0, 2)}/{MD5.Substring(2, 2)}/{MD5}.jpg";
        }

        private static void Update_APIStatus(string StatusMessage, bool ButtonEnable)
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.DownloadQueue_StatusTextBlock.Text = $"API DL Status: {StatusMessage}";
                Window_Main._RefHolder.DownloadQueue_CancelAPIDL.IsEnabled = ButtonEnable;
            });
        }

        internal async static void Grab_MediaWithTags(string TagQuery, string FolderName)
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() => { Window_Main._RefHolder.DownloadQueue_CancelAPIDL.IsEnabled = true; });

            string PostRequestString = $"https://e621.net/posts.json?limit=320&tags={TagQuery}";
            int PageCounter = 1;

        GrabAnotherAPIPage:
            Update_APIStatus($"Working on Tags - Page {PageCounter}", true);
            Task<string?> RunTaskFirst = new Task<string?>(() => Module_e621Data.DataDownload($"{PostRequestString}{(PageCounter > 1 ? $"&page={PageCounter}" : null)}"));
            lock (Module_e621APIController.UserTasks)
            {
                Module_e621APIController.UserTasks.Add(RunTaskFirst);
            }

            string? e6JSONResult = await RunTaskFirst;
            if (string.IsNullOrEmpty(e6JSONResult) || e6JSONResult.StartsWith('ⓔ') || e6JSONResult.Length < 32) return;

            JToken? JSON_Object = JObject.Parse(e6JSONResult)["posts"];
            foreach (JToken cPost in JSON_Object.Children())
            {
                List<string> TempTagList = CreateTagList(cPost["tags"], cPost["rating"].Value<string>());
                if (!Blacklist_Check(TempTagList))
                {
                    string MediaURLTemp;
                    string ThumbnailURLTemp;
                    MD5_2_URL(cPost, out MediaURLTemp, out ThumbnailURLTemp);
                    string PostID = cPost["id"].Value<string>();
                    Module_Downloader.AddDownloadItem2Queue(
                        PageURL: $"https://e621.net/posts/{PostID}",
                        MediaURL: MediaURLTemp,
                        ThumbnailURL: ThumbnailURLTemp,
                        MediaFormat: cPost["file"]["ext"].Value<string>(),
                        e6PostID: PostID,
                        e6PoolName: FolderName,
                        e6Download: true
                        );
                }
            }
            PageCounter += 1;
            Module_Downloader.UpdateDownloadTreeView();

            if (CancellationPending)
            {
                CancellationPending = false;
                return;
            }
            if (JSON_Object.Children().Count() == 320)
            {
                goto GrabAnotherAPIPage;
            }
        }

        internal async static void Grab_Pool(string PoolID)
        {
            string? JSON_PoolData = Module_e621Data.DataDownload($"https://e621.net/pools/{PoolID}.json");
            if (string.IsNullOrEmpty(JSON_PoolData) || JSON_PoolData.StartsWith('ⓔ') || JSON_PoolData.Length < 32) return;

            JToken PoolJSON = JObject.Parse(JSON_PoolData);
            string PoolName = PoolJSON["name"].Value<string>().Replace('_', ' ').Trim();
            PoolName = string.Join(null, PoolName.Split(Path.GetInvalidFileNameChars()));
            string FolderPath = Path.Combine(AppSettings.Download_FolderLocation, @"e621\", PoolName);

            List<string> FoundComicPosts = new List<string>();
            if (Directory.Exists(FolderPath))
            {
                foreach (string FileFound in Directory.GetFiles(FolderPath))
                {
                    string CutPageName = FileFound.Substring(FileFound.LastIndexOf('_') + 1);
                    FoundComicPosts.Add(CutPageName);
                }
            }

            Window_Main._RefHolder.Dispatcher.BeginInvoke(() => { Window_Main._RefHolder.DownloadQueue_CancelAPIDL.IsEnabled = true; });

            List<string> PoolPages = PoolJSON["post_ids"].Values<string>().ToList();
            int SkippedPostsCounter = 0;
            string PoolRequestString = $"https://e621.net/posts.json?limit=320&tags=pool:{PoolID}";
            int PageCounter = 1;

        GrabAnotherAPIPage:
            Update_APIStatus($"Working on Pool#{PoolID} - Page {PageCounter}", true);

            Task<string?> RunTaskFirst = new Task<string?>(() => Module_e621Data.DataDownload($"{PoolRequestString}{(PageCounter > 1 ? $"&page={PageCounter}" : null)}"));
            lock (Module_e621APIController.UserTasks)
            {
                Module_e621APIController.UserTasks.Add(RunTaskFirst);
            }

            string? e6JSONResult = await RunTaskFirst;
            if (string.IsNullOrEmpty(e6JSONResult) || e6JSONResult.StartsWith('ⓔ') || e6JSONResult.Length < 32) return;

            JToken JSON_Object = JObject.Parse(e6JSONResult)["posts"];
            foreach (JToken cPost in JSON_Object)
            {
                string MediaURLTemp;
                string ThumbnailURLTemp;
                MD5_2_URL(cPost, out MediaURLTemp, out ThumbnailURLTemp);

                string PicName = MediaURLTemp.Substring(MediaURLTemp.LastIndexOf('/') + 1);
                if (FoundComicPosts.Contains(PicName))
                {
                    SkippedPostsCounter += 1;
                    continue;
                }

                string? PostID = cPost["id"].Value<string>();
                Module_Downloader.AddDownloadItem2Queue(
                    PageURL: $"https://e621.net/posts/{PostID}",
                    MediaURL: MediaURLTemp,
                    ThumbnailURL: ThumbnailURLTemp,
                    MediaFormat: cPost["file"]["ext"].Value<string>(),
                    e6PostID: PostID,
                    e6PoolName: PoolName,
                    e6PoolPostIndex: PoolPages.IndexOf(PostID).ToString(),
                    e6Download: true
                    );
            }
            Module_Downloader.UpdateDownloadTreeView();

            if (CancellationPending)
            {
                CancellationPending = false;
                goto ExitFromStuff;
            }
            if (JSON_Object.Children().Count() == 320)
            {
                goto GrabAnotherAPIPage;
            }

        ExitFromStuff:
            if (SkippedPostsCounter > 0)
            {
                Module_Downloader.Report_Info($"{PoolName}: {SkippedPostsCounter} page{(SkippedPostsCounter > 1 ? "s" : null)} skipped as they already exist");
            }
        }

        private static bool Blacklist_Check(List<string> PostTags)
        {
            if (AppSettings.Blacklist.Any())
            {
                foreach (string BlacklistLine in AppSettings.Blacklist)
                {
                    if (BlacklistLine.Contains('-'))
                    {
                        List<string> BlacklistLineList = new List<string>();
                        BlacklistLineList.AddRange(BlacklistLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                        int HitCounter = 0;
                        foreach (string BlacklistTag in BlacklistLineList)
                        {
                            string BlacklistTagTemp = BlacklistTag;
                            if (BlacklistTag.StartsWith('-'))
                            {
                                BlacklistTagTemp = BlacklistTag.Substring(1);
                                if (!PostTags.Contains(BlacklistTagTemp)) HitCounter++;
                                continue;
                            }

                            if (PostTags.Contains(BlacklistTagTemp)) HitCounter++;
                        }

                        if (HitCounter == BlacklistLineList.Count) return true;
                        continue;
                    }

                    if (BlacklistLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(tag => PostTags.Contains(tag)))
                    {
                        return true;
                    }
                }
                return false;
            }
            return false;
        }
    }
}