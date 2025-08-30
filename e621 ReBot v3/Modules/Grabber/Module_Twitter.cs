using e621_ReBot_v3.CustomControls;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Controls;

namespace e621_ReBot_v3.Modules.Grabber
{
    internal static partial class Module_Twitter
    {
        internal static void Queue_Prepare(string WebAddress)
        {
            Module_CookieJar.GetCookies(WebAddress, ref Module_CookieJar.Cookies_Twitter);
            if (WebAddress.Contains("/status/"))
            {
                Queue_Single(WebAddress);
            }
            else
            {
                Queue_Multi(WebAddress);
            }
        }

        internal static JArray? TwitterJSONHolder;
        [GeneratedRegex(@"(?<=status/)\d+(?=/)?")]
        private static partial Regex Twitter_Regex();
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
                //Isn't guaranteed to be first token, have to search for it.
                string TweetID = Twitter_Regex().Match(WebAddress).Value;
                Window_Main._RefHolder.Dispatcher.Invoke(() => { Module_Grabber.TreeView_GetParentItem(WebAddress, WebAddress, TwitterJSONHolder.SelectToken($"$.[?(@.id_str == '{TweetID}')]").ToString(), true); });
            }
        }

        private static void Queue_Multi(string WebAddress)
        {
            if (TwitterJSONHolder == null || !TwitterJSONHolder.Any())
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            ushort SkipCounter = 0;
            Dictionary<string, string> Posts2Grab = new Dictionary<string, string>();
            foreach (JToken JTokenTemp in TwitterJSONHolder.Children())
            {
                JToken ExtendedContainer = JTokenTemp["extended_entities"];

                string URL2Post = ExtendedContainer["media"][0]["expanded_url"].Value<string>();
                URL2Post = $"{URL2Post.Substring(0, URL2Post.IndexOf("/status/") + 8)}{JTokenTemp["id_str"].Value<string>()}";

                URL2Post = URL2Post.Replace("//twitter.com", "//x.com"); //They still have twitter in api.

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

        internal static void Grab(string WebAddress, string JSONSource)
        {
            JSONSource = string.IsNullOrEmpty(JSONSource) ? Module_Grabber.GetPageSource(WebAddress, ref Module_CookieJar.Cookies_Twitter) : JSONSource;
            if (string.IsNullOrEmpty(JSONSource))
            {
                Module_Grabber.Report_Info($"Error encountered in Module_Twitter.Grab [@{WebAddress}]");
                return;
            }

            JObject TweeterJSON = JObject.Parse(JSONSource);

            if (!TweeterJSON["extended_entities"]["media"].Any())
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            string Post_URL = WebAddress;

            DateTime Post_DateTime = DateTime.ParseExact(TweeterJSON["created_at"].Value<string>(), "ddd MMM dd HH:mm:ss K yyyy", CultureInfo.InvariantCulture);

            string ArtistName = TweeterJSON["extended_entities"]["media"][0]["expanded_url"].Value<string>();
            ArtistName = ArtistName.Split('/', StringSplitOptions.RemoveEmptyEntries)[2];

            string Post_Text = TweeterJSON["full_text"].Value<string>();

            string? Post_MediaURL;
            string? Post_ThumbnailURL;
            List<MediaItem> MediaItemList = new List<MediaItem>();
            ushort SkipCounter = 0;
            foreach (JToken MediaNode in TweeterJSON["extended_entities"]["media"])
            {
                if (MediaNode["video_info"] != null)
                {
                    JToken? BestVideo = null;
                    foreach (JToken VideoCheck in MediaNode["video_info"]["variants"])
                    {
                        if (VideoCheck["bitrate"] != null)
                        {
                            if (BestVideo == null)
                            {
                                BestVideo = VideoCheck;
                                continue;
                            }
                            if (VideoCheck["bitrate"].Value<int>() > BestVideo["bitrate"].Value<int>())
                            {
                                BestVideo = VideoCheck;
                            }
                        }
                    }
                    Post_MediaURL = BestVideo["url"].Value<string>();
                    if (Post_MediaURL.Contains('?')) Post_MediaURL = Post_MediaURL.Substring(0, Post_MediaURL.IndexOf('?'));

                    if (!Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
                    {
                        SkipCounter += 1;
                        continue;
                    }
                }
                else
                {
                    Post_MediaURL = MediaNode["media_url_https"].Value<string>();
                    string TempMediaURL = $"{Post_MediaURL}{(Post_MediaURL.EndsWith(".mp4") ? null : ":orig")}";

                    if (!Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
                    {
                        SkipCounter += 1;
                        continue;
                    }
                }
                Post_ThumbnailURL = MediaNode["media_url_https"].Value<string>();
                string FormatHolder = Post_MediaURL.Substring(Post_MediaURL.LastIndexOf('.') + 1);
                Post_MediaURL = $"{Post_MediaURL}{(Post_MediaURL.EndsWith(".mp4") ? null : ":orig")}";

                MediaItem MediaItemTemp = new MediaItem
                {
                    Grab_PageURL = Post_URL,
                    Grab_MediaURL = Post_MediaURL,
                    Grab_ThumbnailURL = $"{Post_ThumbnailURL}:small",
                    Grab_DateTime = Post_DateTime,
                    Grab_Artist = ArtistName,
                    Grab_Title = $"Tweet by @{ArtistName}",
                    Grab_TextBody = Post_Text,
                    Grid_MediaFormat = FormatHolder,
                    Grid_MediaWidth = MediaNode["original_info"]["width"].Value<uint>(),
                    Grid_MediaHeight = MediaNode["original_info"]["height"].Value<uint>(),
                    Grid_MediaByteLength = Module_Grabber.GetMediaSize(Post_MediaURL),
                    Grid_ThumbnailFullInfo = true,
                    UP_Tags = Post_DateTime.Year.ToString(),
                    UP_IsWhitelisted = false //is giving errors so turn it into byte upload
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

        //internal static string? TwitterAuthorizationHolder;
    }
}