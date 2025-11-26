using e621_ReBot_v3.CustomControls;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace e621_ReBot_v3.Modules.Grabber
{
    internal static partial class Module_Pixiv
    {
        internal static void Queue_Prepare(string WebAddress)
        {
            Module_CookieJar.GetCookies(WebAddress, ref Module_CookieJar.Cookies_Pixiv);
            if (WebAddress.StartsWith("https://www.pixiv.net/en/users/"))
            {
                MessageBoxResult MessageBoxResultTemp = Window_Main._RefHolder.Dispatcher.Invoke(() => { return MessageBox.Show(Window_Main._RefHolder, "Do you want to go grab all works?\nIf you chose not to, only the visible ones on this page will be grabbed.", "Grab", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes); });
                if (MessageBoxResultTemp == MessageBoxResult.Yes)
                {
                    Queue_All(WebAddress);
                }
                else
                {
                    Queue_Multi(WebAddress, Module_CefSharp.BrowserHTMLSource ?? string.Empty);
                }
            }
            else
            {
                Queue_Single(WebAddress);
            }
        }

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
                string URL_Parameters = WebAddress.Substring(WebAddress.LastIndexOf('/') + 1); ;
                Window_Main._RefHolder.Dispatcher.Invoke(() => { Module_Grabber.TreeView_GetParentItem(WebAddress, WebAddress, SkipSearch: true); });
            }
        }

        private static void Queue_Multi(string WebAddress, string HTMLSource)
        {
            if (WebAddress.Contains("&p=")) WebAddress = WebAddress.Remove(WebAddress.IndexOf("&p="));

            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(HTMLSource);

            HtmlNodeCollection ThumbNodeSelector = HtmlDocumentTemp.DocumentNode.SelectNodes(".//ul/li//a[@data-gtm-value]");
            if (ThumbNodeSelector.Count == 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            ushort SkipCounter = 0;
            List<string> Posts2Grab = new List<string>();
            foreach (HtmlNode HtmlNodeTemp in ThumbNodeSelector)
            {
                string URL2Post = $"https://www.pixiv.net{HtmlNodeTemp.Attributes["href"].Value}";

                if (Module_Grabber._GrabQueue_URLs.Contains(URL2Post))
                {
                    SkipCounter++;
                    continue;
                }
                else
                {
                    Posts2Grab.Add(URL2Post);
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
                        foreach (string URL2Post in Posts2Grab)
                        {
                            Module_Grabber._GrabQueue_URLs.Add(URL2Post);
                            Module_Grabber.TreeView_MakeChildItem(TreeViewItemParent, URL2Post, URL2Post);
                        }
                    }
                });
            }
        }

        [GeneratedRegex(@"(?<=/)\d+(?=/)?")]
        private static partial Regex Pixiv_Regex();
        private static async void Queue_All(string WebAddress)
        {
            string UserID = Pixiv_Regex().Match(WebAddress).Value;

            string? JSONSource = await Module_Grabber.GetPageSource($"https://www.pixiv.net/ajax/user/{UserID}/profile/all", Module_CookieJar.Cookies_Pixiv);
            if (string.IsNullOrEmpty(JSONSource))
            {
                Module_Grabber.Report_Info($"Error encountered in Module_Pixiv.Grab [@{WebAddress}]");
                return;
            }

            JObject JSONDictionary = JObject.Parse(JSONSource);

            List<string> WorkList = new List<string>();
            // Thanks https://stackoverflow.com/questions/16795045/accessing-all-items-in-the-jtoken-json-net/38253969
            if (JSONDictionary["body"]["illusts"].HasValues)
            {
                WorkList.AddRange(((JObject)JSONDictionary["body"]["illusts"]).Properties().Select(f => f.Name));
            }
            if (JSONDictionary["body"]["manga"].HasValues)
            {
                WorkList.AddRange(((JObject)JSONDictionary["body"]["manga"]).Properties().Select(f => f.Name));
            }

            if (WorkList.Count == 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            WorkList = WorkList.OrderByDescending(int.Parse).ToList();
            List<string> Posts2Grab = new List<string>();
            foreach (string PostID in WorkList)
            {
                string URL2Post = $"https://www.pixiv.net/en/artworks/{PostID}";
                if (Module_Grabber._GrabQueue_URLs.Contains(URL2Post))
                {
                    Module_Grabber.Report_Info($"Skipped grabbing - Already in queue [@{URL2Post}]");
                    continue;
                }
                else
                {
                    Posts2Grab.Add(URL2Post);
                }
            }
            if (Posts2Grab.Any())
            {
                TreeViewItem? TreeViewItemParent = Window_Main._RefHolder.Dispatcher.Invoke(() => { return Module_Grabber.TreeView_GetParentItem(WebAddress, WebAddress); });
                Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
                {
                    lock (Module_Grabber._GrabQueue_URLs)
                    {
                        foreach (string URL2Post in Posts2Grab)
                        {
                            Module_Grabber._GrabQueue_URLs.Add(URL2Post);
                            Module_Grabber.TreeView_MakeChildItem(TreeViewItemParent, URL2Post, URL2Post);
                        }
                    }
                });
            }
        }

        internal static async Task Grab(string WebAddress)
        {
            string Post_ID = Pixiv_Regex().Match(WebAddress).Value;

            string? JSONSource = await Module_Grabber.GetPageSource($"https://www.pixiv.net/ajax/illust/{Post_ID}", Module_CookieJar.Cookies_Pixiv);
            if (string.IsNullOrEmpty(JSONSource))
            {
                Module_Grabber.Report_Info($"Error encountered in Module_Pixiv.Grab [@{WebAddress}]");
                return;
            }

            JObject PixivJSON = JObject.Parse(JSONSource);

            if ((bool)PixivJSON["error"])
            {
                Module_Grabber.Report_Info($"Error=true in JSON encountered in Module_Pixiv.Grab [@{WebAddress}], message: {(string)PixivJSON["message"]}");

                lock (Module_Grabber._GrabQueue_WorkingOn)
                {
                    Module_Grabber._GrabQueue_WorkingOn.Remove(WebAddress);
                }
                Window_Main._RefHolder.Dispatcher.Invoke(() => { Module_Grabber.TreeView_GetParentItem(WebAddress, WebAddress, SkipSearch: true); }); //re-add it
                return;
            }

            // - - - - - - - - - - - - - - - -

            string Post_URL = WebAddress;

            DateTime Post_DateTime = (DateTime)PixivJSON["body"]["createDate"];

            string Post_Title = (string)PixivJSON["body"]["illustTitle"];
            Post_Title = WebUtility.HtmlDecode(Post_Title.Replace('[', '⟦').Replace(']', '⟧'));

            string ArtistName = (string)PixivJSON["body"]["userName"];

            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml((string)PixivJSON["body"]["illustComment"]);
            HtmlNode Post_TextNode = HtmlDocumentTemp.DocumentNode;
            string? Post_Text = Module_Html2Text.Html2Text_Pixiv(Post_TextNode);

            int PicCount = (int)PixivJSON["body"]["pageCount"];
            string Post_MediaURL;
            List<MediaItem> MediaItemList = new List<MediaItem>();
            ushort SkipCounter = 0;
            if (PicCount == 1)
            {
                Post_MediaURL = (string)PixivJSON["body"]["urls"]["original"];

                if (Post_MediaURL == null)
                {
                    Module_Grabber.Report_Info($"Grabbing skipped - Media is behind login [@{WebAddress}]");
                    return;
                }

                if (Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
                {
                    MediaItemList.Add(CreateMediaItem(Post_URL, Post_MediaURL, (string)PixivJSON["body"]["urls"]["thumb"], Post_DateTime, ArtistName, Post_Title, Post_Text, (uint)PixivJSON["body"]["width"], (uint)PixivJSON["body"]["height"]));
                }
                else
                {
                    SkipCounter++;
                    Module_Grabber.Report_Info($"Grabbing skipped - Media already grabbed or ignored [@{Post_URL}]");
                }
            }
            else
            {
                JSONSource = await Module_Grabber.GetPageSource($"https://www.pixiv.net/ajax/illust/{Post_ID}/pages", Module_CookieJar.Cookies_Pixiv);
                if (string.IsNullOrEmpty(JSONSource))
                {
                    Module_Grabber.Report_Info($"Error encountered in Module_Pixiv.Grab [@{WebAddress}]");
                    return;
                }

                JObject JSONPages = JObject.Parse(JSONSource);

                ProgressBar ProgressBarTemp = Window_Main._RefHolder.Dispatcher.Invoke(() => Module_Grabber.GetProgressBar(PicCount));
                ushort MediaCounter = 0;
                for (int p = 0; p <= PicCount - 1; p++)
                {
                    MediaCounter++;
                    Window_Main._RefHolder.Dispatcher.BeginInvoke(() => ProgressBarTemp.Value = MediaCounter);
                    Post_MediaURL = (string)JSONPages["body"][p]["urls"]["original"];

                    if (!Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
                    {
                        SkipCounter++;
                        continue;
                    }

                    string Post_ThumbnailURL = ((string)JSONPages["body"][p]["urls"]["thumb_mini"]).Replace("/128x128/", "/360x360_70/"); // /250x250_80_a2/ is cut off, that's not good.
                    MediaItemList.Add(CreateMediaItem(Post_URL, Post_MediaURL, Post_ThumbnailURL, Post_DateTime, ArtistName, Post_Title, Post_Text, (uint)JSONPages["body"][p]["width"], (uint)JSONPages["body"][p]["height"]));
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

        private static MediaItem CreateMediaItem(string Post_URL, string Post_MediaURL, string Post_ThumbnailURL, DateTime Post_DateTime, string ArtistName, string Post_Title, string? Post_Text, uint MediaWidth, uint MediaHeight)
        {
            MediaItem MediaItemTemp = new MediaItem
            {
                Grab_PageURL = Post_URL,
                Grab_MediaURL = Post_MediaURL,
                Grab_ThumbnailURL = Post_ThumbnailURL,
                Grab_DateTime = Post_DateTime,
                Grab_Artist = ArtistName,
                Grab_Title = $"⮚ {Post_Title} ⮘ by {ArtistName} on Pixiv",
                Grab_TextBody = Post_Text,
                Grid_MediaFormat = Post_MediaURL.Contains("ugoira0") ? "ugoira" : Post_MediaURL.Substring(Post_MediaURL.LastIndexOf('.') + 1),
                Grid_MediaWidth = MediaWidth,
                Grid_MediaHeight = MediaHeight,
                Grid_MediaByteLength = Module_Grabber.GetMediaSize(Post_MediaURL),
                Grid_ThumbnailFullInfo = true,
                UP_Tags = Post_DateTime.Year.ToString(),
                UP_IsWhitelisted = true
            };
            Module_Uploader.Media2BigCheck(MediaItemTemp);
            return MediaItemTemp;
        }
    }
}