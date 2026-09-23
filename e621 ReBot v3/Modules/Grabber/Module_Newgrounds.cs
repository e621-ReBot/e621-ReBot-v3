using e621_ReBot_v3.CustomControls;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace e621_ReBot_v3.Modules.Grabber
{
    internal static class Module_Newgrounds
    {
        internal static void Queue_Prepare(string WebAddress)
        {
            if (WebAddress.Contains("newgrounds.com/art/view/")
            || WebAddress.Contains("newgrounds.com/portal/view/")) //movies
            {
                Queue_Single(WebAddress, Module_CefSharp.BrowserHTMLSource ?? string.Empty);

            }
            else
            {
                Queue_Multi(WebAddress, Module_CefSharp.BrowserHTMLSource ?? string.Empty);
            }
        }

        private static void Queue_Single(string WebAddress, string HTMLSource)
        {
            if (Module_Grabber._GrabQueue_URLs.Contains(WebAddress))
            {
                Module_Grabber.Report_Info($"Skipped grabbing - Already in queue [@{WebAddress}]");
                return;
            }
            else
            {
                //lock should already be in background thread as the task is done in background
                lock (Module_Grabber._GrabQueue_URLs)
                {
                    Module_Grabber._GrabQueue_URLs.Add(WebAddress);
                }
                Window_Main._RefHolder.Dispatcher.Invoke(() => { Module_Grabber.TreeView_GetParentItem(WebAddress, WebAddress, HTMLSource, true); });
            }
        }

        private static void Queue_Multi(string WebAddress, string HTMLSource)
        {
            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(HTMLSource);

            HtmlNodeCollection ThumbNodeSelector = HtmlDocumentTemp.DocumentNode.SelectNodes(".//div[@class='userpage-browse-content']//div[@class='span-1 align-center' and not(@id)]");
            if (ThumbNodeSelector.Count == 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            ushort SkipCounter = 0;
            Dictionary<string, string> Posts2Grab = new Dictionary<string, string>();
            foreach (HtmlNode HtmlNodeTemp in ThumbNodeSelector)
            {
                string URL2Post = $"{HtmlNodeTemp.SelectSingleNode(".//a").Attributes["href"].Value}";

                if (Module_Grabber._GrabQueue_URLs.Contains(URL2Post))
                {
                    SkipCounter++;
                    continue;
                }
                else
                {
                    string WorkTitle = WebUtility.HtmlDecode(HtmlNodeTemp.SelectSingleNode(".//h4").InnerText.Trim());
                    Posts2Grab.Add(URL2Post, WorkTitle);
                }
            }
            if (SkipCounter > 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing {SkipCounter} media containers that were already in queue [@{WebAddress}]");
            }
            if (Posts2Grab.Any())
            {
                //lock should already be in background thread as the task is done in background
                lock (Module_Grabber._GrabQueue_URLs)
                {
                    foreach (string URL2Post in Posts2Grab.Keys)
                    {
                        Module_Grabber._GrabQueue_URLs.Add(URL2Post);
                    }
                }

                Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
                {
                    TreeViewItem? TreeViewItemParent = Module_Grabber.TreeView_GetParentItem(WebAddress, WebAddress);
                    if (TreeViewItemParent != null)
                    {
                        foreach (string URL2Post in Posts2Grab.Keys)
                        {
                            Module_Grabber.TreeView_MakeChildItem(TreeViewItemParent, Posts2Grab[URL2Post], URL2Post);
                        }
                    }
                });
            }
        }

        internal static async Task Grab(string WebAddress, string HTMLSource)
        {
            if (string.IsNullOrEmpty(HTMLSource))
            {
                DownloadResult DownloadResultTemp = await ReBot_HttpClient.DownloadStringAsync(WebAddress);
                if (!DownloadResultTemp.IsSuccessStatusCode)
                {
                    Module_Grabber.Report_Info($"Error encountered in Module_Newgrounds.Grab [@{WebAddress}]");
                    return;
                }
                HTMLSource = DownloadResultTemp.TextData;
            }

            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(HTMLSource);
            HtmlNode PostNode = HtmlDocumentTemp.DocumentNode.SelectSingleNode("html");

            HtmlNode LoginTest = PostNode.SelectSingleNode(".//div[@id='adults_only']");
            if (LoginTest != null)
            {
                Module_Grabber.Report_Info($"Grabbing skipped - Media is behind login [@{WebAddress}]");
                return;
            }

            // - - - - - - - - - - - - - - - -

            string Post_URL = WebAddress;

            string Post_DateTimeTemp = PostNode.SelectSingleNode(".//div[@id='sidestats']//dl[@class='sidestats' and not(@data-statistics)]/dd[@class='multivalue']/span").InnerText;
            DateTime Post_DateTime = DateTime.ParseExact(Post_DateTimeTemp, "MMM d, yyyy", CultureInfo.InvariantCulture);

            string Post_Title = PostNode.SelectSingleNode(".//div[@class='pod']//h2[@itemprop='name']").InnerText.Trim();
            Post_Title = WebUtility.HtmlDecode(Post_Title.Replace('[', '⟦').Replace(']', '⟧'));

            string ArtistName = PostNode.SelectSingleNode(".//div[@class='body-center']//div[@class='item-details-main']").InnerText.Trim();

            HtmlNode Post_TextNode = PostNode.SelectSingleNode(".//div[@class='body-center']//div[@id='author_comments']");
            string? Post_Text = Module_Html2Text.Html2Text_Newgrounds(Post_TextNode);

            string Post_MediaURL;
            string Post_ThumbnailURL = PostNode.SelectSingleNode(".//meta[@property='og:image']").Attributes["content"].Value;
            Post_ThumbnailURL = Post_ThumbnailURL.Substring(0, Post_ThumbnailURL.IndexOf('?'));
            List<MediaItem> MediaItemList = new List<MediaItem>();
            ushort SkipCounter = 0;
            ushort MediaCounter = 0;

            HtmlNode ContainerNode = PostNode.SelectSingleNode(".//div[@itemtype='https://schema.org/MediaObject']/div[@class='pod-body']");
            if (ContainerNode != null) //Images
            {
                if (ContainerNode.SelectSingleNode(".//div[@class='art-view-gallery-nav']") != null) //clickable gallery navigation
                {
                    //string ScriptWithData = HTMLSource.Substring(HTMLSource.IndexOf("ngImageViewer.addToGallery"));
                    //ScriptWithData = ScriptWithData.Substring(0, ScriptWithData.IndexOf("</script>"));
                    //string[] ImageNodes = ScriptWithData.Split("ngImageViewer.addToGallery(", StringSplitOptions.RemoveEmptyEntries);
                    string ScriptWithData = HTMLSource.Substring(HTMLSource.IndexOf("let imageData = ") + 16);
                    ScriptWithData = ScriptWithData.Substring(0, ScriptWithData.IndexOf("new ngImageGallery(")).Trim();
                    ScriptWithData = ScriptWithData.Remove(ScriptWithData.Length - 1);

                    JArray ImageData = JArray.Parse(ScriptWithData);

                    if (ImageData.Count == 0)
                    {
                        Module_Grabber.Report_Info($"Error encountered in Module_Newgrounds.Grab GalleryNav [@{WebAddress}]");
                        return;
                    }

                    ProgressBar ProgressBarTemp = Window_Main._RefHolder.Dispatcher.Invoke(() => Module_Grabber.GetProgressBar(ImageData.Count));

                    foreach (JToken ImageNode in ImageData)
                    {
                        MediaCounter++;
                        Window_Main._RefHolder.Dispatcher.BeginInvoke(() => { if (ProgressBarTemp != null) ProgressBarTemp.Value = MediaCounter; });

                        Post_MediaURL = (string)ImageNode["image"];
                        if (Post_MediaURL.Contains('?')) Post_MediaURL = Post_MediaURL.Substring(0, Post_MediaURL.IndexOf('?'));

                        if (!Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
                        {
                            SkipCounter++;
                            continue;
                        }

                        Post_ThumbnailURL = (string)ImageNode["icon"];
                        if (Post_ThumbnailURL.Contains('?')) Post_ThumbnailURL = Post_ThumbnailURL.Substring(0, Post_ThumbnailURL.IndexOf('?'));

                        MediaItemList.Add(CreateMediaItem(Post_URL, Post_MediaURL, Post_ThumbnailURL, Post_DateTime, ArtistName, Post_Title, Post_Text));
                        MediaItem MediaItemTemp = MediaItemList.Last();
                        MediaItemTemp.Grid_MediaWidth = (uint)ImageNode["width"];
                        MediaItemTemp.Grid_MediaHeight = (uint)ImageNode["height"];
                        MediaItemTemp.Grid_ThumbnailFullInfo = true;

                        await Task.Delay(Module_Grabber.PauseBetweenImages);
                    }
                    Window_Main._RefHolder.Dispatcher.Invoke(() => ProgressBarTemp.Visibility = Visibility.Hidden);
                    var breaker = 0;
                }
                else
                {
                    HtmlNodeCollection ImageNodes = ContainerNode.SelectNodes(".//img[@itemprop='image']");
                    ProgressBar ProgressBarTemp = Window_Main._RefHolder.Dispatcher.Invoke(() => Module_Grabber.GetProgressBar(ImageNodes.Count));

                    foreach (HtmlNode ImageNode in ImageNodes)
                    {
                        MediaCounter++;
                        Window_Main._RefHolder.Dispatcher.BeginInvoke(() => { if (ProgressBarTemp != null) ProgressBarTemp.Value = MediaCounter; });

                        //parent /a node has partial url as link so use that instead
                        Post_MediaURL = ImageNode.ParentNode.Attributes["href"].Value;
                        if (Post_MediaURL.Contains('?')) Post_MediaURL = Post_MediaURL.Substring(0, Post_MediaURL.IndexOf('?'));

                        if (!Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
                        {
                            SkipCounter++;
                            continue;
                        }

                        MediaItemList.Add(CreateMediaItem(Post_URL, Post_MediaURL, Post_ThumbnailURL, Post_DateTime, ArtistName, Post_Title, Post_Text));
                        if (MediaCounter == 1)
                        {
                            HtmlNode ResolutionNodeHolder = PostNode.SelectSingleNode(".//div[@id='sidestats']//dl[@class='sidestats' and not(@data-statistics)]/dd[4]");
                            if (ResolutionNodeHolder != null)
                            {
                                string ResolutionStringHolder = ResolutionNodeHolder.InnerText.Trim();
                                ResolutionStringHolder = ResolutionStringHolder.Substring(0, ResolutionStringHolder.Length - 3);
                                string[] ResolutionHolder = ResolutionStringHolder.Split(" x ", StringSplitOptions.RemoveEmptyEntries);
                                MediaItem MediaItemTemp = MediaItemList.Last();
                                MediaItemTemp.Grid_MediaWidth = uint.Parse(ResolutionHolder[0]);
                                MediaItemTemp.Grid_MediaHeight = uint.Parse(ResolutionHolder[1]);
                                MediaItemTemp.Grid_ThumbnailFullInfo = true;
                            }
                        }
                        await Task.Delay(Module_Grabber.PauseBetweenImages);
                    }
                    Window_Main._RefHolder.Dispatcher.Invoke(() => ProgressBarTemp.Visibility = Visibility.Hidden);
                }
            }
            else //Video, maybe
            {
                DownloadResult DownloadResultTemp = await ReBot_HttpClient.DownloadStringAsync(Post_URL.Replace("/view/", "/video/"));
                if (!DownloadResultTemp.IsSuccessStatusCode)
                {
                    Module_Grabber.Report_Info($"Error encountered in Module_Newgrounds.Grab [@{WebAddress}]");
                    return;
                }
                string VideoJSONString = DownloadResultTemp.TextData;
                if (string.IsNullOrEmpty(VideoJSONString))
                {
                    Module_Grabber.Report_Info($"Grabbing skipped - No media container found [@{Post_URL}]");
                    return;
                }
                JObject VideoJSON = JObject.Parse(VideoJSONString);

                Post_MediaURL = (string)VideoJSON["sources"].First.First.First["src"];
                if (Post_MediaURL.Contains('?')) Post_MediaURL = Post_MediaURL.Remove(Post_MediaURL.IndexOf('?'));

                if (Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
                {
                    MediaItemList.Add(CreateMediaItem(Post_URL, Post_MediaURL, Post_ThumbnailURL, Post_DateTime, ArtistName, Post_Title, Post_Text));
                }
                else
                {
                    //lock should already be in background thread as the task is done in background
                    lock (Module_Grabber._GrabQueue_WorkingOn)
                    {
                        Module_Grabber._GrabQueue_WorkingOn.Remove(Post_URL);
                    }
                    Module_Grabber.Report_Info($"Grabbing skipped - Media already grabbed or ignored [@{Post_URL}]");
                    return;
                }
            }

            // - - - - - - - - - - - - - - - -

            if (MediaItemList.Count == 0)
            {
                //lock should already be in background thread as the task is done in background
                lock (Module_Grabber._GrabQueue_WorkingOn)
                {
                    Module_Grabber._GrabQueue_WorkingOn.Remove(Post_URL);
                }
                Module_Grabber.Report_Info($"Grabbing skipped - {(SkipCounter > 1 ? "All m" : "M")}edia already grabbed or ignored [@{Post_URL}]");
                return;
            }

            //lock should already be in background thread as the task is done in background
            lock (Module_Grabber._GrabQueue_WorkingOn)
            {
                Module_Grabber._GrabQueue_WorkingOn[Post_URL] = MediaItemList.Count == 1 ? MediaItemList.First() : MediaItemList;
            }
            string PrintText = $"Finished grabbing: {Post_URL}";
            if (SkipCounter > 0)
            {
                PrintText += $", {SkipCounter} media container{(SkipCounter > 1 ? "s have" : " has")} been skipped";
            }
            Module_Grabber.Report_Info(PrintText);
        }

        private static MediaItem CreateMediaItem(string Post_URL, string Post_MediaURL, string Post_ThumbnailURL, DateTime Post_DateTime, string ArtistName, string Post_Title, string? Post_Text)
        {

            MediaItem MediaItemTemp = new MediaItem
            {
                Grab_PageURL = Post_URL,
                Grab_MediaURL = Post_MediaURL,
                Grab_ThumbnailURL = Post_ThumbnailURL,
                Grab_DateTime = Post_DateTime,
                Grab_Artist = ArtistName,
                Grab_Title = $"⮚ {Post_Title} ⮘ by {ArtistName} on Newgrounds",
                Grab_TextBody = Post_Text,
                Grid_MediaFormat = Post_MediaURL.Substring(Post_MediaURL.LastIndexOf('.') + 1),
                Grid_MediaByteLength = Module_Grabber.GetMediaSize(Post_MediaURL),
                UP_Tags = Post_DateTime.Year.ToString(),
                UP_IsWhitelisted = true
            };
            return MediaItemTemp;
        }
    }
}