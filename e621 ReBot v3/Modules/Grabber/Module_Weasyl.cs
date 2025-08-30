using e621_ReBot_v3.CustomControls;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Media;

namespace e621_ReBot_v3.Modules.Grabber
{
    internal static partial class Module_Weasyl
    {
        private static readonly ImageSource WeasylThumbDefault = new ImageSourceConverter().ConvertFrom(Properties.Resources.BrowserIcon_Weasyl) as ImageSource;

        [GeneratedRegex(@"^\w+://www.weasyl.com/~.+/submissions/\d+/.+")]
        private static partial Regex Weasyl_Regex();
        internal static void Queue_Prepare(string WebAddress)
        {
            Module_CookieJar.GetCookies(WebAddress, ref Module_CookieJar.Cookies_Weasyl);
            if (Weasyl_Regex().Match(WebAddress).Success)
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
                Window_Main._RefHolder.Dispatcher.Invoke(() => { Module_Grabber.TreeView_GetParentItem(WebAddress, WebAddress, HTMLSource, true); });
            }
        }

        private static void Queue_Multi(string WebAddress, string HTMLSource)
        {
            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(HTMLSource);

            HtmlNodeCollection HtmlNodeCollectionTemp = HtmlDocumentTemp.DocumentNode.SelectNodes(".//li[@class='item']/figure[@class='thumb']");
            if (HtmlNodeCollectionTemp.Count == 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            ushort SkipCounter = 0;
            Dictionary<string, string> Posts2Grab = new Dictionary<string, string>();
            foreach (HtmlNode HtmlNodeTemp in HtmlNodeCollectionTemp)
            {
                string URL2Post = $"https://www.weasyl.com{HtmlNodeTemp.SelectSingleNode("./a[@class='thumb-bounds']").Attributes["href"].Value}";

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
                    string WorkTitle = WebUtility.HtmlDecode(HtmlNodeTemp.SelectSingleNode("./figcaption/h6").Attributes["title"].Value);
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

        internal static void Grab(string WebAddress, string HTMLSource)
        {
            HTMLSource = string.IsNullOrEmpty(HTMLSource) ? Module_Grabber.GetPageSource(WebAddress, ref Module_CookieJar.Cookies_Weasyl) : HTMLSource;
            if (string.IsNullOrEmpty(HTMLSource))
            {
                Module_Grabber.Report_Info($"Error encountered in Module_Weasyl.Grab [@{WebAddress}]");
                return;
            }

            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(HTMLSource);
            HtmlNode PostNode = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//div[@id='page-container']");

            HtmlNode LoginTest = PostNode.SelectSingleNode(".//div[@id='error_content']");
            if (LoginTest != null)
            {
                Module_Grabber.Report_Info($"Grabbing skipped - Media is behind login [@{WebAddress}]");
                return;
            }

            // - - - - - - - - - - - - - - - -

            string Post_URL = WebAddress;

            string Post_DateTimeTemp = PostNode.SelectSingleNode(".//div[@id='db-user']/p[@class='date']/time").Attributes["datetime"].Value;
            DateTime Post_DateTime = DateTime.Parse(Post_DateTimeTemp);

            string Post_Title = PostNode.SelectSingleNode(".//div[@id='db-main']/h2[@id='detail-bar-title']").InnerText.Trim();
            Post_Title = WebUtility.HtmlDecode(Post_Title.Replace('[', '⟦').Replace(']', '⟧'));

            string ArtistName = WebUtility.HtmlDecode(PostNode.SelectSingleNode(".//div[@id='db-user']/a[@class='username']").InnerText.Trim());

            HtmlNode Post_TextNode = PostNode.SelectSingleNode(".//div[@id='detail-description']/div[@class='formatted-content']");
            string? Post_Text = Module_Html2Text.Html2Text_Weasyl(Post_TextNode);

            string? Post_MediaURL = null;
            if (PostNode.SelectSingleNode(".//div[@id='db-main']/ul/li[text()=' Download']") == null)
            {
                Post_MediaURL = PostNode.SelectSingleNode(".//div[@id='detail-art']//img").Attributes["src"].Value;
            }
            else
            {
                Post_MediaURL = PostNode.SelectSingleNode(".//div[@id='db-main']/ul/li[text()='Download']/a").Attributes["href"].Value;
            }

            // - - - - - - - - - - - - - - - -

            if (!Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
            {
                lock (Module_Grabber._GrabQueue_WorkingOn)
                {
                    Module_Grabber._GrabQueue_WorkingOn.Remove(Post_URL);
                }
                Module_Grabber.Report_Info($"Grabbing skipped - Media already grabbed or ignored [@{Post_URL}]");
                return;
            }

            MediaItem MediaItemTemp = new MediaItem
            {
                Grab_PageURL = Post_URL,
                Grab_MediaURL = Post_MediaURL,
                //Grab_ThumbnailURL = Post_ThumbnailURL,
                Grid_Thumbnail = WeasylThumbDefault,
                Grab_DateTime = Post_DateTime,
                Grab_Artist = ArtistName,
                Grab_Title = $"⮚ {Post_Title} ⮘ by {ArtistName} on Weasyl",
                Grab_TextBody = Post_Text,
                Grid_MediaFormat = Post_MediaURL.Substring(Post_MediaURL.LastIndexOf('.') + 1),
                Grid_MediaByteLength = Module_Grabber.GetMediaSize(Post_MediaURL),
                UP_Tags = Post_DateTime.Year.ToString(),
                UP_IsWhitelisted = true
            };

            lock (Module_Grabber._GrabQueue_WorkingOn)
            {
                Module_Grabber._GrabQueue_WorkingOn[Post_URL] = MediaItemTemp;
            }
            Module_Grabber.Report_Info($"Finished grabbing: {Post_URL}");
        }
    }
}