using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows.Controls;
using e621_ReBot_v3.CustomControls;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace e621_ReBot_v3.Modules.Grabber
{
    internal static class Module_SoFurry
    {
        internal static void Queue_Prepare(string WebAddress)
        {
            Module_CookieJar.GetCookies(WebAddress, ref Module_CookieJar.Cookies_SoFurry);
            if (WebAddress.StartsWith("https://www.sofurry.com/view/"))
            {
                Queue_Single(WebAddress);
            }
            else
            {
                Queue_Multi(WebAddress, Module_CefSharp.BrowserHTMLSource ?? string.Empty);
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
                Window_Main._RefHolder.Dispatcher.Invoke(() => { Module_Grabber.TreeView_GetParentItem(WebAddress, WebAddress, true); });
            }
        }

        private static void Queue_Multi(string WebAddress, string HTMLSource)
        {
            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(HTMLSource);

            HtmlNodeCollection HtmlNodeCollectionTemp = HtmlDocumentTemp.DocumentNode.SelectNodes(".//div[@id=('yw0' or 'yw1')]//a[@class='sfArtworkSmallInner']");
            if (HtmlNodeCollectionTemp.Count == 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            ushort SkipCounter = 0;
            Dictionary<string, string> Posts2Grab = new Dictionary<string, string>();
            foreach (HtmlNode HtmlNodeTemp in HtmlNodeCollectionTemp)
            {
                string URL2Post = $"https://www.sofurry.com{HtmlNodeTemp.Attributes["href"].Value}";

                if (Module_Grabber._GrabQueue_URLs.Contains(URL2Post))
                {
                    Module_Grabber.Report_Info($"Skipped grabbing - Already in queue [@{URL2Post}]");
                    continue;
                }
                else
                {
                    lock (Module_Grabber._GrabQueue_URLs)
                    {
                        Module_Grabber._GrabQueue_URLs.Add(URL2Post);
                    }
                    string WorkTitle = WebUtility.HtmlDecode(HtmlNodeTemp.SelectSingleNode("./img").Attributes["alt"].Value);
                    WorkTitle = WorkTitle.Substring(0, WorkTitle.LastIndexOf('|'));
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
                foreach (string URL2Post in Posts2Grab.Keys)
                {
                    Window_Main._RefHolder.Dispatcher.BeginInvoke(() => { Module_Grabber.TreeView_MakeChildItem(TreeViewItemParent, Posts2Grab[URL2Post], URL2Post); });
                }
            }
        }

        internal static void Grab(string WebAddress)
        {
            string Post_ID = WebAddress.Substring(WebAddress.LastIndexOf('/') + 1);

            string JSONSourceTest = Module_Grabber.GetPageSource($"https://api2.sofurry.com/std/getSubmissionDetails?id={Post_ID}", ref Module_CookieJar.Cookies_SoFurry);
            if (string.IsNullOrEmpty(JSONSourceTest))
            {
                Module_Grabber.Report_Info($"Error encountered in Module_SoFurry.Grab [@{WebAddress}]");
                return;
            }

            JObject SoFurryJSON = JObject.Parse(JSONSourceTest);

            if (SoFurryJSON["contentType"].Value<uint>() != 1) // (0=story, 1=art, 2=music, 3=journal, 4=photo)
            {
                Module_Grabber.Report_Info($"Skipped grabbing - Unsupported submission type [@{WebAddress}]");
                return;
            }

            // - - - - - - - - - - - - - - - -

            string Post_URL = WebAddress;

            DateTime Post_DateTime = DateTime.UtcNow;

            string Post_Title = SoFurryJSON["title"].Value<string>();
            Post_Title = Post_Title.Replace('[', '⟦').Replace(']', '⟧');

            string ArtistName = SoFurryJSON["author"].Value<string>();

            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(SoFurryJSON["description"].Value<string>());
            HtmlNode Post_TextNode = HtmlDocumentTemp.DocumentNode;
            string? Post_Text = Module_Html2Text.Html2Text_SoFurry(Post_TextNode);

            // contentSourceUrl is enough but combine with FileName
            string Post_MediaURL = $"{SoFurryJSON["contentSourceUrl"].Value<string>()}&{SoFurryJSON["fileName"].Value<string>()}";

            // - - - - - - - - - - - - - - - -

            if (Module_Grabber._Grabbed_MediaItems.ContainsURL(Post_MediaURL))
            {
                lock (Module_Grabber._GrabQueue_WorkingOn)
                {
                    Module_Grabber._GrabQueue_WorkingOn.Remove(Post_URL);
                }
                Module_Grabber.Report_Info($"Grabbing skipped - Media already grabbed [@{Post_URL}]");
                return;
            }

            string Post_ThumbnailURL = SoFurryJSON["thumbnailSourceUrl"].Value<string>();
            MediaItem MediaItemTemp = new MediaItem
            {
                Grab_PageURL = Post_URL,
                Grab_MediaURL = Post_MediaURL,
                Grab_ThumbnailURL = Post_ThumbnailURL,
                Grab_DateTime = Post_DateTime,
                Grab_Artist = ArtistName,
                Grab_Title = $"⮚ {Post_Title} ⮘ by {ArtistName} on SoFurry",
                Grab_TextBody = Post_Text,
                Grid_MediaFormat = Post_MediaURL.Substring(Post_MediaURL.LastIndexOf('.') + 1),
                //Grid_MediaByteLength = Module_Grabber.GetMediaSize(Post_MediaURL), //doesn't have contentLength but has MD5
                //UP_Tags = Post_DateTime.Year.ToString(),
                UP_IsWhitelisted = true
            };
            if (Post_MediaURL.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || Post_MediaURL.EndsWith(".swf", StringComparison.OrdinalIgnoreCase))
            {
                MediaItemTemp.Grid_MediaWidth = SoFurryJSON["width"].Value<uint>();
                MediaItemTemp.Grid_MediaHeight = SoFurryJSON["height"].Value<uint>();
                //MediaItemTemp.Grid_ThumbnailFullInfo = true;
            }

            lock (Module_Grabber._GrabQueue_WorkingOn)
            {
                Module_Grabber._GrabQueue_WorkingOn[Post_URL] = MediaItemTemp;
            }
            Module_Grabber.Report_Info($"Finished grabbing: {Post_URL}");
        }
    }
}