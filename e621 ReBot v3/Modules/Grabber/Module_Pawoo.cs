using e621_ReBot_v3.CustomControls;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Controls;

namespace e621_ReBot_v3.Modules.Grabber
{
    internal static class Module_Pawoo
    {
        internal static void Queue_Prepare(string WebAddress)
        {
            Module_CookieJar.GetCookies(WebAddress, ref Module_CookieJar.Cookies_Pawoo);
            string NumericPartCheck = WebAddress.Substring(WebAddress.LastIndexOf('/') + 1);
            if (NumericPartCheck.All(char.IsDigit))
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
                lock (Module_Grabber._GrabQueue_URLs)
                {
                    Module_Grabber._GrabQueue_URLs.Add(WebAddress);
                }
                HtmlDocument HtmlDocumentTemp = new HtmlDocument();
                HtmlDocumentTemp.LoadHtml(HTMLSource);
                Window_Main._RefHolder.Dispatcher.Invoke(() => { Module_Grabber.TreeView_GetParentItem(WebAddress, WebAddress, HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//div[@class='entry entry-center']").OuterHtml, true); });
            }
        }

        private static void Queue_Multi(string WebAddress, string HTMLSource)
        {
            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(HTMLSource);

            HtmlNodeCollection MediaNodeSelector = HtmlDocumentTemp.DocumentNode.SelectNodes(".//div[@class='activity-stream activity-stream--under-tabs']/div[@class='entry h-entry']//div[@data-component='MediaGallery']");
            if (MediaNodeSelector.Count == 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            ushort SkipCounter = 0;
            Dictionary<string, string> Posts2Grab = new Dictionary<string, string>();
            foreach (HtmlNode HtmlNodeTemp in MediaNodeSelector)
            {
                HtmlNode MastodonNodeHolder = HtmlNodeTemp.ParentNode;

                string URL2Post = MastodonNodeHolder.SelectSingleNode(".//div[@class='status__info']/a").Attributes["href"].Value;

                if (Module_Grabber._GrabQueue_URLs.Contains(URL2Post))
                {
                    SkipCounter++;
                    continue;
                }
                else
                {
                    Posts2Grab.Add(URL2Post, MastodonNodeHolder.OuterHtml);
                }
            }
            if (SkipCounter > 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing {SkipCounter} media containers that were already in queue [@{WebAddress}]");
            }
            if (Posts2Grab.Any())
            {
                TreeViewItem? TreeViewItemParent = Window_Main._RefHolder.Dispatcher.Invoke(() => { return Module_Grabber.TreeView_GetParentItem(WebAddress, WebAddress); });
                foreach (string URL2Post in Posts2Grab.Keys)
                {
                    Window_Main._RefHolder.Dispatcher.BeginInvoke(() => { Module_Grabber.TreeView_MakeChildItem(TreeViewItemParent, URL2Post, URL2Post, Posts2Grab[URL2Post]); });
                }
            }
        }

        internal static void Grab(string WebAddress, string HTMLSource)
        {
            HTMLSource = string.IsNullOrEmpty(HTMLSource) ? Module_Grabber.GetPageSource(WebAddress, ref Module_CookieJar.Cookies_Pawoo) : HTMLSource;
            if (string.IsNullOrEmpty(HTMLSource))
            {
                Module_Grabber.Report_Info($"Error encountered in Module_Mastodons.Grab_vHTML [@{WebAddress}]");
                return;
            }

            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(HTMLSource);
            HtmlNode PostNode = HtmlDocumentTemp.DocumentNode;

            HtmlNodeCollection ThumbNodeSelector = PostNode.SelectNodes(".//div[@data-component='MediaGallery']//a[@class='media-gallery__item-thumbnail']");
            if (ThumbNodeSelector.Count == 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            // - - - - - - - - - - - - - - - -

            string Post_URL = WebAddress;

            DateTime Post_DateTime = DateTime.Parse(PostNode.SelectSingleNode(".//data[@class='dt-published']").Attributes["value"].Value);

            string ArtistName = PostNode.SelectSingleNode(".//span[@class='display-name__account']").InnerText.Trim();

            HtmlNode Post_TextNode = PostNode.SelectSingleNode(".//div[@class='e-content']");
            string? Post_Text = Module_Html2Text.Html2Text_Mastodon(Post_TextNode);

            string? Post_MediaURL;
            string? Post_ThumbnailURL;
            List<MediaItem> MediaItemList = new List<MediaItem>();
            ushort SkipCounter = 0;

            HtmlNode MediaNodeHitTest = PostNode.SelectSingleNode(".//div[@class='video-player inline']");
            if (MediaNodeHitTest != null)
            {
                HtmlNode VideoNodeHitTest = MediaNodeHitTest.SelectSingleNode(".//video");
                if (VideoNodeHitTest != null)
                {
                    Post_MediaURL = VideoNodeHitTest.Attributes["src"].Value;
                    if (Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
                    {
                        Post_ThumbnailURL = VideoNodeHitTest.Attributes["poster"].Value;
                        MediaItemList.Add(CreateMediaItem(Post_URL, Post_MediaURL, Post_ThumbnailURL, Post_DateTime, ArtistName, Post_Text));

                    }
                    else
                    {
                        SkipCounter++;
                    }
                }
            }
            MediaNodeHitTest = PostNode.SelectSingleNode(".//div[@class='media-gallery']");
            if (MediaNodeHitTest != null)
            {
                //remove retweets
                HtmlNode RTHitTest = PostNode.SelectSingleNode(".//div[@class='status__prepend']");
                if (RTHitTest == null)
                {
                    foreach (HtmlNode MediaNode in MediaNodeHitTest.SelectNodes(".//img"))
                    {
                        Post_MediaURL = MediaNode.ParentNode.Attributes["href"].Value;
                        if (!Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
                        {
                            SkipCounter += 1;
                            continue;
                        }
                        Post_ThumbnailURL = MediaNode.Attributes["src"].Value;
                        MediaItemList.Add(CreateMediaItem(Post_URL, Post_MediaURL, Post_ThumbnailURL, Post_DateTime, ArtistName, Post_Text));
                        Thread.Sleep(Module_Grabber.PauseBetweenImages);
                    }
                }
            }

            // - - - - - - - - - - - - - - - -

            if (MediaItemList.Count == 0)
            {
                lock (Module_Grabber._GrabQueue_WorkingOn)
                {
                    Module_Grabber._GrabQueue_WorkingOn.Remove(Post_URL);
                }
                Module_Grabber.Report_Info($"Grabbing skipped - {(SkipCounter > 1 ? "All m" : "M")}edia already grabbed or ignored [@{Post_URL}]");
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

        private static MediaItem CreateMediaItem(string Post_URL, string Post_MediaURL, string Post_ThumbnailURL, DateTime Post_DateTime, string ArtistName, string? Post_Text)
        {
            MediaItem MediaItemTemp = new MediaItem
            {
                Grab_PageURL = Post_URL,
                Grab_MediaURL = Post_MediaURL,
                Grab_ThumbnailURL = Post_ThumbnailURL,
                Grab_DateTime = Post_DateTime,
                Grab_Artist = ArtistName,
                Grab_Title = $"Created by {ArtistName} on Pawoo",
                Grab_TextBody = Post_Text,
                Grid_MediaFormat = Post_MediaURL.Substring(Post_MediaURL.LastIndexOf('.') + 1),
                Grid_MediaByteLength = Module_Grabber.GetMediaSize(Post_MediaURL),
                UP_Tags = Post_DateTime.Year.ToString(),
                UP_IsWhitelisted = false
            };
            return MediaItemTemp;
        }
    }
}