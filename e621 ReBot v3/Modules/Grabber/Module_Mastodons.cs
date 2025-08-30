using e621_ReBot_v3.CustomControls;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Controls;

namespace e621_ReBot_v3.Modules.Grabber
{
    internal static class Module_Mastodons
    {
        internal static void Queue_Prepare(string WebAddress, ref CookieContainer CookieRef)
        {
            Module_CookieJar.GetCookies(WebAddress, ref CookieRef);
            string NumericPartCheck = WebAddress.Substring(WebAddress.LastIndexOf('/') + 1);
            if (NumericPartCheck.All(char.IsDigit))
            {
                Queue_Single(WebAddress);
            }
            else
            {
                Queue_Multi(WebAddress);
            }
        }

        internal static JArray? MastodonsJSONHolder;
        private static void Queue_Single(string WebAddress)
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
                Window_Main._RefHolder.Dispatcher.Invoke(() => { Module_Grabber.TreeView_GetParentItem(WebAddress, WebAddress, MastodonsJSONHolder[0].ToString(), true); });
            }
        }

        private static void Queue_Multi(string WebAddress)
        {
            if (MastodonsJSONHolder == null || !MastodonsJSONHolder.Any())
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            ushort SkipCounter = 0;
            Dictionary<string, string> Posts2Grab = new Dictionary<string, string>();
            foreach (JToken JTokenTemp in MastodonsJSONHolder.Children())
            {
                string URL2Post = JTokenTemp["url"].Value<string>();

                if (Module_Grabber._GrabQueue_URLs.Contains(URL2Post))
                {
                    SkipCounter++;
                    continue;
                }
                else
                {
                    lock (Module_Grabber._GrabQueue_URLs)
                    {
                        Module_Grabber._GrabQueue_URLs.Add(URL2Post);
                    }
                    Posts2Grab.Add(URL2Post, JTokenTemp.ToString());
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

        internal static void Grab(string WebAddress, string JSONSource, ref CookieContainer CookieRef)
        {
            JSONSource = string.IsNullOrEmpty(JSONSource) ? Module_Grabber.GetPageSource(WebAddress, ref CookieRef) : JSONSource;
            if (string.IsNullOrEmpty(JSONSource))
            {
                Module_Grabber.Report_Info($"Error encountered in Module_Mastodons.Grab [@{WebAddress}]");
                return;
            }


            JObject MastodonsJSON = JObject.Parse(JSONSource);

            if (!MastodonsJSON["media_attachments"].Any())
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            string Post_URL = WebAddress;

            DateTime Post_DateTime = MastodonsJSON["created_at"].Value<DateTime>();

            string ArtistName = MastodonsJSON["account"]["username"].Value<string>();

            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(MastodonsJSON["content"].Value<string>());
            HtmlNode Post_TextNode = HtmlDocumentTemp.DocumentNode;
            string? Post_Text = Module_Html2Text.Html2Text_Mastodon(Post_TextNode);

            string? Post_MediaURL;
            string? Post_ThumbnailURL;
            List<MediaItem> MediaItemList = new List<MediaItem>();
            ushort SkipCounter = 0;
            foreach (JToken MediaNode in MastodonsJSON["media_attachments"])
            {
                Post_MediaURL = MediaNode["url"].Value<string>();

                if (!Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
                {
                    SkipCounter += 1;
                    continue;
                }

                Post_ThumbnailURL = MediaNode["preview_url"].Value<string>();

                MediaItem MediaItemTemp = new MediaItem
                {
                    Grab_PageURL = Post_URL,
                    Grab_MediaURL = Post_MediaURL,
                    Grab_ThumbnailURL = Post_ThumbnailURL,
                    Grab_DateTime = Post_DateTime,
                    Grab_Artist = ArtistName,
                    Grab_Title = $"Created by @{ArtistName}",
                    Grab_TextBody = Post_Text,
                    Grid_MediaFormat = Post_MediaURL.Substring(Post_MediaURL.LastIndexOf('.') + 1),
                    Grid_MediaWidth = MediaNode["meta"]["original"]["width"].Value<uint>(),
                    Grid_MediaHeight = MediaNode["meta"]["original"]["height"].Value<uint>(),
                    Grid_MediaByteLength = Module_Grabber.GetMediaSize(Post_MediaURL),
                    Grid_ThumbnailFullInfo = true,
                    UP_Tags = Post_DateTime.Year.ToString(),
                    UP_IsWhitelisted = true
                };
                Module_Uploader.Media2BigCheck(MediaItemTemp);
                MediaItemList.Add(MediaItemTemp);
                Thread.Sleep(Module_Grabber.PauseBetweenImages);
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
    }
}