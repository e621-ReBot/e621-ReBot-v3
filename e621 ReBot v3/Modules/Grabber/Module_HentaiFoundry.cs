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
    internal static partial class Module_HentaiFoundry
    {
        private static readonly ImageSource HentaiFoundryThumbDefault = new ImageSourceConverter().ConvertFrom(Properties.Resources.BrowserIcon_HentaiFoundry) as ImageSource;

        [GeneratedRegex(@"^\w+://www.hentai-foundry.com/pictures/user/.+/\d+/.+")]
        private static partial Regex HF_Regex();
        internal static void Queue_Prepare(string WebAddress)
        {
            Module_CookieJar.GetCookies(WebAddress, ref Module_CookieJar.Cookies_HentaiFoundry);
            if (HF_Regex().Match(WebAddress).Success)
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

            HtmlNodeCollection HtmlNodeCollectionTemp = HtmlDocumentTemp.DocumentNode.SelectNodes(".//div[@id='yw0']//div[@class='thumb_square']");
            if (HtmlNodeCollectionTemp.Count == 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            ushort SkipCounter = 0;
            Dictionary<string, string> Posts2Grab = new Dictionary<string, string>();
            foreach (HtmlNode HtmlNodeTemp in HtmlNodeCollectionTemp)
            {
                string URL2Post = $"https://www.hentai-foundry.com{HtmlNodeTemp.SelectSingleNode(".//a").Attributes["href"].Value}";

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
                    string WorkTitle = WebUtility.HtmlDecode(HtmlNodeTemp.SelectSingleNode(".//a").InnerText);
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
            HTMLSource = string.IsNullOrEmpty(HTMLSource) ? Module_Grabber.GetPageSource(WebAddress, ref Module_CookieJar.Cookies_HentaiFoundry) : HTMLSource;
            if (string.IsNullOrEmpty(HTMLSource))
            {
                Module_Grabber.Report_Info($"Error encountered in Module_HentaiFoundry.Grab [@{WebAddress}]");
                return;
            }

            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(HTMLSource);
            HtmlNode PostNode = HtmlDocumentTemp.DocumentNode.SelectSingleNode("//body");

            //HtmlNode LoginTest = PostNode.SelectSingleNode(".//div[@id='standardpage']/section[@class='aligncenter notice-message']");
            //if (LoginTest != null)
            //{
            //    Module_Grabber.Report_Info($"Grabbing skipped - Media is behind login [@{WebAddress}]");
            //    return;
            //}

            // - - - - - - - - - - - - - - - -

            string Post_URL = WebAddress;

            string Post_DateTimeTemp = PostNode.SelectSingleNode(".//section[@id='pictureGeneralInfoBox']//time[@datetime]").Attributes["datetime"].Value;
            DateTime Post_DateTime = DateTime.Parse(Post_DateTimeTemp);

            string Post_Title = PostNode.SelectSingleNode(".//section[@id='picBox']//span[@class='imageTitle']").InnerText.Trim();
            Post_Title = WebUtility.HtmlDecode(Post_Title.Replace('[', '⟦').Replace(']', '⟧'));

            string ArtistName = WebUtility.HtmlDecode(PostNode.SelectSingleNode(".//section[@id='picBox']//a").InnerText.Trim());

            HtmlNode Post_TextNode = PostNode.SelectSingleNode(".//section[@id='descriptionBox']//div[@class='picDescript']");
            string? Post_Text = Module_Html2Text.Html2Text_HentaiFoundry(Post_TextNode);

            //HtmlNode ImageNodeTest = PostNode.SelectSingleNode(".//section[@id='picBox']/div[@class='boxbody']").SelectSingleNode(".//img | .//embed");
            string Post_MediaURL = $"https:{PostNode.SelectSingleNode(".//section[@id='picBox']/div[@class='boxbody']//img").Attributes["src"].Value}";
            if (Post_MediaURL.Contains("/thumb.php"))
            {
                Post_MediaURL = $"https:{PostNode.SelectSingleNode(".//section[@id='picBox']//img[@class='center']").Attributes["onClick"].Value.Split("&#039;", StringSplitOptions.RemoveEmptyEntries)[1]}";
            }

            // - - - - - - - - - - - - - - - -

            if (Module_Grabber._Grabbed_MediaURLs.Contains(Post_MediaURL))
            {
                lock (Module_Grabber._GrabQueue_WorkingOn)
                {
                    Module_Grabber._GrabQueue_WorkingOn.Remove(Post_URL);
                }
                Module_Grabber.Report_Info($"Grabbing skipped - Media already grabbed [@{Post_URL}]");
                return;
            }

            string picID = Post_MediaURL.Split('/', StringSplitOptions.RemoveEmptyEntries)[4];
            string Post_ThumbnailURL = $"https://thumbs.hentai-foundry.com/thumb.php?pid={picID}&size=200";
            MediaItem MediaItemTemp = new MediaItem
            {
                Grab_PageURL = Post_URL,
                Grab_MediaURL = Post_MediaURL,
                Grab_ThumbnailURL = Post_ThumbnailURL,
                Grab_DateTime = Post_DateTime,
                Grab_Artist = ArtistName,
                Grab_Title = $"⮚ {Post_Title} ⮘ by {ArtistName} on Hentai-Foundry",
                Grab_TextBody = Post_Text,
                Grid_MediaFormat = Post_MediaURL.Substring(Post_MediaURL.LastIndexOf('.') + 1),
                Grid_MediaByteLength = Module_Grabber.GetMediaSize(Post_MediaURL),
                UP_Tags = Post_DateTime.Year.ToString(),
                UP_IsWhitelisted = true
            };
            if (MediaItemTemp.Grid_MediaFormat.Equals("swf"))
            {
                MediaItemTemp.Grid_Thumbnail = HentaiFoundryThumbDefault;
            }

            lock (Module_Grabber._GrabQueue_WorkingOn)
            {
                Module_Grabber._GrabQueue_WorkingOn[Post_URL] = MediaItemTemp;
            }
            lock (Module_Grabber._Grabbed_MediaURLs)
            {
                Module_Grabber._Grabbed_MediaURLs.Add(Post_MediaURL);
            }
            Module_Grabber.Report_Info($"Finished grabbing: {Post_URL}");
        }
    }
}