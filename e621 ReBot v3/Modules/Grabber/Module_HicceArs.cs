using e621_ReBot_v3.CustomControls;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace e621_ReBot_v3.Modules.Grabber
{
    internal static class Module_HicceArs
    {
        internal static void Queue_Prepare(string WebAddress)
        {
            Module_CookieJar.GetCookies(WebAddress, ref Module_CookieJar.Cookies_HicceArs);
            if (WebAddress.StartsWith("https://www.hiccears.com/p/"))
            {
                Queue_Multi(WebAddress, Module_CefSharp.BrowserHTMLSource ?? string.Empty);
            }
            else
            {
                Queue_Single(WebAddress, Module_CefSharp.BrowserHTMLSource ?? string.Empty);

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

            HtmlNodeCollection ThumbNodeSelector = HtmlDocumentTemp.DocumentNode.SelectNodes(".//section[@class='section']/div[@class='grid grid-3-3-3-3 centered']/a[@class='album-preview']");
            if (ThumbNodeSelector.Count == 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            ushort SkipCounter = 0;
            Dictionary<string, string> Posts2Grab = new Dictionary<string, string>();
            foreach (HtmlNode HtmlNodeTemp in ThumbNodeSelector)
            {
                string URL2Post = $"https://www.hiccears.com{HtmlNodeTemp.Attributes["href"].Value}";

                if (Module_Grabber._GrabQueue_URLs.Contains(URL2Post))
                {
                    SkipCounter++;
                    continue;
                }
                else
                {
                    string WorkTitle = WebUtility.HtmlDecode(HtmlNodeTemp.SelectSingleNode(".//p[@class='album-preview-title']").InnerText.Trim());
                    Posts2Grab.Add(URL2Post, WorkTitle);
                }
            }
            if (SkipCounter > 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing {SkipCounter} media containers that were already in queue [@{WebAddress}]");
            }
            if (Posts2Grab.Any())
            {
                TreeViewItem? TreeViewItemParent = Window_Main._RefHolder.Dispatcher.Invoke(() => { return Module_Grabber.TreeView_GetParentItem(WebAddress, WebAddress); });
                Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
                {
                    lock (Module_Grabber._GrabQueue_URLs)
                    {
                        foreach (string URL2Post in Posts2Grab.Keys)
                        {
                            Module_Grabber._GrabQueue_URLs.Add(URL2Post);
                            Module_Grabber.TreeView_MakeChildItem(TreeViewItemParent, Posts2Grab[URL2Post], URL2Post);
                        }
                    }
                });
            }
        }

        internal static void Grab(string WebAddress, string HTMLSource)
        {
            HTMLSource = string.IsNullOrEmpty(HTMLSource) ? Module_Grabber.GetPageSource(WebAddress, ref Module_CookieJar.Cookies_HicceArs) : HTMLSource;
            if (string.IsNullOrEmpty(HTMLSource))
            {
                Module_Grabber.Report_Info($"Error encountered in Module_HicceArs.Grab [@{WebAddress}]");
                return;
            }

            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(HTMLSource);
            HtmlNode PostNode = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//body");

            HtmlNode LoginTest = PostNode.SelectSingleNode(".//div[@class='content-grid pt-0']/div[@class='content']//div[@class='widget-box-title mb-3']");
            if (LoginTest != null)
            {
                Module_Grabber.Report_Info($"Grabbing skipped - Media is behind login [@{WebAddress}]");
                return;
            }

            PostNode = PostNode.SelectSingleNode(".//div[@class='grid grid-9-3']"); //Like maintenance 503
            if (PostNode == null)
            {
                Module_Grabber.Report_Info($"Grabbing skipped - No media container found [@{WebAddress}]");
                return;
            }

            // - - - - - - - - - - - - - - - -

            string Post_URL = WebAddress;

            string Post_DateTimeTemp = PostNode.SelectSingleNode(".//p[@class='information-line-title' and text()='Created at']").NextSibling.InnerText.Trim();
            DateTime Post_DateTime = DateTime.ParseExact(Post_DateTimeTemp, "yyyy-MM-dd HH:mm UTC", CultureInfo.InvariantCulture); ;

            string Post_Title = PostNode.SelectSingleNode(".//div[@class='section-header']//h2[@class='section-title']").InnerText.Trim();
            Post_Title = WebUtility.HtmlDecode(Post_Title.Replace('[', '⟦').Replace(']', '⟧'));

            string ArtistName = PostNode.SelectSingleNode(".//div[@class='sidebar-box-items']//p[@class='user-status-title']").InnerText.Trim();

            HtmlNode Post_TextNode = PostNode.SelectSingleNode(".//div[@class='widget-box-content']");
            string? Post_Text = Post_TextNode == null ? null : Module_Html2Text.Html2Text_Inkbunny(Post_TextNode);

            string Post_MediaURL;
            string Post_ThumbnailURL;
            List<MediaItem> MediaItemList = new List<MediaItem>();
            ushort SkipCounter = 0;
            HtmlNodeCollection ThumbNodes = PostNode.SelectNodes(".//div[@class='marketplace-content grid-column']/div[@class='grid grid-3-3-3-3 centered']/a");
            if (Post_URL.Contains("hiccears.com/file/"))
            {
                HtmlNode DownloadNode = PostNode.SelectSingleNode(".//a[@class='button secondary download-button pl-5 pr-5 mb-3']");
                Post_MediaURL = $"https://www.hiccears.com{DownloadNode.Attributes["href"].Value}";

                int NodeIndex = 0;
                if (ThumbNodes.Count != 1)
                {
                    string Post_ThumbnailURLTemp = PostNode.SelectSingleNode(".//a[@class='button primary pl-5 pr-5 mb-3 mb-3']").Attributes["href"].Value;
                    foreach (HtmlNode HtmlNodeTemp in ThumbNodes)
                    {
                        if (HtmlNodeTemp.Attributes["href"].Value.Equals(Post_ThumbnailURLTemp))
                        {
                            NodeIndex = (NodeIndex == ThumbNodes.Count - 1) ? 0 : NodeIndex + 1;
                            break;
                        }
                        NodeIndex++;
                    }
                }
                Post_ThumbnailURL = $"https://www.hiccears.com{ThumbNodes[NodeIndex].SelectSingleNode(".//img").Attributes["src"].Value}";

                if (Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
                {
                    MediaItemList.Add(CreateMediaItem(Post_URL, Post_MediaURL, Post_ThumbnailURL, Post_DateTime, ArtistName, Post_Title, Post_Text));          
                }
                else
                {
                    SkipCounter++;
                    Module_Grabber.Report_Info($"Grabbing skipped - Media already grabbed or ignored [@{Post_URL}]");
                }
            }
            else
            {
                ProgressBar ProgressBarTemp = Window_Main._RefHolder.Dispatcher.Invoke(() => Module_Grabber.GetProgressBar(ThumbNodes.Count));
                ushort MediaCounter = 0;
                foreach (HtmlNode ThumbNode in ThumbNodes)
                {
                    MediaCounter++;
                    Window_Main._RefHolder.Dispatcher.BeginInvoke(() => ProgressBarTemp.Value = MediaCounter);
                    Post_MediaURL = $"https://www.hiccears.com{ThumbNode.Attributes["href"].Value.Replace("/preview", "/download")}";
                    Post_ThumbnailURL = $"https://www.hiccears.com{ThumbNode.SelectSingleNode(".//img").Attributes["src"].Value}";

                    if (!Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
                    {
                        SkipCounter++;
                        continue;
                    }

                    MediaItemList.Add(CreateMediaItem(Post_URL, Post_MediaURL, Post_ThumbnailURL, Post_DateTime, ArtistName, Post_Title, Post_Text));
                    Thread.Sleep(Module_Grabber.PauseBetweenImages);
                }
                Window_Main._RefHolder.Dispatcher.Invoke(() => ProgressBarTemp.Visibility = Visibility.Hidden);
            }

            // - - - - - - - - - - - - - - - -

            if (MediaItemList.Count == 0)
            {
                lock (Module_Grabber._GrabQueue_WorkingOn)
                {
                    Module_Grabber._GrabQueue_WorkingOn.Remove(Post_URL);
                }
                Module_Grabber.Report_Info($"Grabbing skipped - {(SkipCounter > 1 ? "All m" : "M")}edia already grabbed [@{Post_URL}]");
                return;
            }

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
                Grab_Title = $"⮚ {Post_Title} ⮘ by {ArtistName} on HicceArs",
                Grab_TextBody = Post_Text,
                UP_Tags = Post_DateTime.Year.ToString(),
                UP_IsWhitelisted = false
            };
            CheckHicceArsMedia(ref MediaItemTemp);
            return MediaItemTemp;
        }

        private static void CheckHicceArsMedia(ref MediaItem MediaItemRef)
        {
            HttpWebRequest HicceArsMediaCheck = (HttpWebRequest)WebRequest.Create(MediaItemRef.Grab_MediaURL);
            HicceArsMediaCheck.CookieContainer = Module_CookieJar.Cookies_HicceArs;
            HicceArsMediaCheck.Method = "HEAD";
            try
            {
                using (HttpWebResponse HicceArsMediaCheckHead = (HttpWebResponse)HicceArsMediaCheck.GetResponse())
                {
                    if (HicceArsMediaCheckHead.StatusCode == HttpStatusCode.OK)
                    {
                        string contentType = HicceArsMediaCheckHead.ContentType;
                        if (contentType.Equals("text/html"))
                        {
                            //CookieError = true;
                            //TempDataRow = null;
                            //var breaker = 0;
                        }
                        else
                        {
                            contentType = contentType.Substring(contentType.IndexOf('.') + 1).Replace("jpeg", "jpg");
                            MediaItemRef.Grid_MediaFormat = contentType;
                            MediaItemRef.Grid_MediaByteLength = (uint)HicceArsMediaCheckHead.ContentLength;
                        }
                    }
                    return;
                }
            }
            catch
            {
                return;
            }
        }
    }
}