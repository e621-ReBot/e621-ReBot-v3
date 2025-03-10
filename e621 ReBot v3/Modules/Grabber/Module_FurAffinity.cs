using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using e621_ReBot_v3.CustomControls;
using HtmlAgilityPack;

namespace e621_ReBot_v3.Modules.Grabber
{
    internal static partial class Module_FurAffinity
    {
        internal static void Queue_Prepare(string WebAddress)
        {
            Module_CookieJar.GetCookies(WebAddress, ref Module_CookieJar.Cookies_FurAffinity);
            if (WebAddress.StartsWith("https://www.furaffinity.net/view/") || WebAddress.Contains("https://www.furaffinity.net/full/"))
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

            HtmlNodeCollection HtmlNodeCollectionTemp = HtmlDocumentTemp.DocumentNode.SelectNodes(".//section[contains(@id, 'gallery')]/figure");
            if (HtmlNodeCollectionTemp.Count == 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            ushort SkipCounter = 0;
            Dictionary<string, string> Posts2Grab = new Dictionary<string, string>();
            foreach (HtmlNode HtmlNodeTemp in HtmlNodeCollectionTemp)
            {
                HtmlNode figcaptionNode = HtmlNodeTemp.SelectSingleNode("./figcaption");
                string URL2Post = $"https://www.furaffinity.net{figcaptionNode.SelectSingleNode(".//a").Attributes["href"].Value}";

                string SubmissionType = HtmlNodeTemp.Attributes["class"].Value;
                if (!SubmissionType.Contains("t-image"))
                {
                    Module_Grabber.Report_Info($"Skipped grabbing - Unsupported submission type: {SubmissionType} [@{URL2Post}]");
                    continue;
                }

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
                    string WorkTitle = WebUtility.HtmlDecode(figcaptionNode.SelectSingleNode(".//a").Attributes["title"].Value);
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

        [GeneratedRegex(@"(?<=/)\d+(?=/)")]
        private static partial Regex FA_Regex1();
        [GeneratedRegex(@"(?<=/)\d+(?=/\d+.)")]
        private static partial Regex FA_Regex2();
        internal static void Grab(string WebAddress, string? HTMLSource)
        {
            HTMLSource = string.IsNullOrEmpty(HTMLSource) ? Module_Grabber.GetPageSource(WebAddress, ref Module_CookieJar.Cookies_FurAffinity) : HTMLSource;
            if (string.IsNullOrEmpty(HTMLSource))
            {
                Module_Grabber.Report_Info($"Error encountered in Module_FurAffinitty.Grab [@{WebAddress}]");
                return;
            }

            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(HTMLSource);
            HtmlNode PostNode = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//body");

            HtmlNode LoginTest = PostNode.SelectSingleNode(".//div[@id='standardpage']/section[@class='aligncenter notice-message']");
            if (LoginTest != null)
            {
                Module_Grabber.Report_Info($"Grabbing skipped - Media is behind login [@{WebAddress}]");
                return;
            }

            // - - - - - - - - - - - - - - - -

            string Post_URL = WebAddress;

            string Post_DateTimeTemp = PostNode.SelectSingleNode(".//span[@class='popup_date']").Attributes["title"].Value;
            if (!Post_DateTimeTemp.Contains("AM") && !Post_DateTimeTemp.Contains("PM"))
            {
                Post_DateTimeTemp = PostNode.SelectSingleNode(".//span[@class='popup_date']").InnerText.Trim();
            }
            DateTime Post_DateTime = DateTime.Parse(Post_DateTimeTemp);

            string Post_Title = PostNode.SelectSingleNode(".//div[@class='submission-title' or @class='classic-submission-title information']/h2").InnerText.Trim();
            Post_Title = WebUtility.HtmlDecode(Post_Title.Replace('[', '⟦').Replace(']', '⟧'));

            string ArtistName = WebUtility.HtmlDecode(PostNode.SelectSingleNode(".//div[@class='submission-id-sub-container' or @class='classic-submission-title information']//a").InnerText.Trim());

            HtmlNode Post_TextNode = PostNode.SelectSingleNode(".//div[@class='submission-description' or @class='submission-description user-submitted-links']");
            if (Post_TextNode == null) //classic theme selected
            {
                ///html/body/div[4]/div/table/tbody/tr[1]/td/table/tbody/tr[2]/td/table/tbody/tr[2]
                ///html/body/div[4]/div/table/tbody/tr[1]/td/table/tbody/tr[2]/td/table/tr[2]
                Post_TextNode = PostNode.SelectSingleNode(".//div[@id='page-submission']//table[@class='maintable']//table[@class='maintable']//td[@class='alt1' and @style]");
            }
            string? Post_Text = Module_Html2Text.Html2Text_FurAffinity(Post_TextNode);

            HtmlNode DownloadNode = PostNode.SelectSingleNode(".//div[@class='download' or @class='download fullsize']/a");
            if (DownloadNode == null) //classic theme selected
            {
                DownloadNode = PostNode.SelectSingleNode(".//div[@id='page-submission']//div[@class='alt1 actions aligncenter']//a[text()='Download']");
            }
            string Post_MediaURL = $"https:{DownloadNode.Attributes["href"].Value}";

            HtmlNode MediaSizeNode = PostNode.SelectSingleNode(".//section[@class='info text']/div[4]/span | .//td[@class='alt1 stats-container']//b[text()='Resolution:']/following-sibling::text()");
            string[] MediaSizes = MediaSizeNode.InnerText.Trim().Replace(" x ", "x").Split('x', StringSplitOptions.RemoveEmptyEntries);

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

            string ThumbnailURLTemp = string.Format("https://t.furaffinity.net/{0}@200-{1}.jpg", FA_Regex1().Match(Post_URL), FA_Regex2().Match(Post_MediaURL));
            MediaItem MediaItemTemp = new MediaItem
            {
                Grab_PageURL = Post_URL,
                Grab_MediaURL = Post_MediaURL,
                Grab_ThumbnailURL = ThumbnailURLTemp,
                Grab_DateTime = Post_DateTime,
                Grab_Artist = ArtistName,
                Grab_Title = $"⮚ {Post_Title} ⮘ by {ArtistName} on FurAffinity",
                Grab_TextBody = Post_Text,
                Grid_MediaFormat = Post_MediaURL.Substring(Post_MediaURL.LastIndexOf('.') + 1),
                Grid_MediaWidth = uint.Parse(MediaSizes[0]),
                Grid_MediaHeight = uint.Parse(MediaSizes[1]),
                Grid_MediaByteLength = Module_Grabber.GetMediaSize(Post_MediaURL),
                Grid_ThumbnailFullInfo = true,
                UP_Tags = Post_DateTime.Year.ToString(),
                UP_IsWhitelisted = true
            };
            Module_Uploader.Media2BigCheck(MediaItemTemp);

            lock (Module_Grabber._GrabQueue_WorkingOn)
            {
                Module_Grabber._GrabQueue_WorkingOn[Post_URL] = MediaItemTemp;
            }
            lock (Module_Grabber._Grabbed_MediaItems)
            {
                Module_Grabber._Grabbed_MediaURLs.Add(Post_MediaURL);
            }
            Module_Grabber.Report_Info($"Finished grabbing: {Post_URL}");
        }
    }
}