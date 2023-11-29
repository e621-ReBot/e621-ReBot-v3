using e621_ReBot_v3.CustomControls;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace e621_ReBot_v3.Modules.Grabber
{
    internal static class Module_Inkbunny
    {
        internal static void Queue_Prepare(string WebAddress)
        {
            Module_CookieJar.GetCookies(WebAddress, ref Module_CookieJar.Cookies_Inkbunny);
            if (WebAddress.StartsWith("https://inkbunny.net/s/"))
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
            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(HTMLSource);

            string SubmissionType = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//div[@class='elephant elephant_bottom elephant_white']/div[@class='content']/div[3]//span[text()='Type:']").NextSibling.InnerText.Trim();
            if (SubmissionType.Equals("Writing - Document") || SubmissionType.Equals("Music - Single Track"))
            {
                Module_Grabber.Report_Info($"Skipped grabbing - Unsupported submission type: {SubmissionType} [@{WebAddress}]");
                return;
            }

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

            HtmlNodeCollection ThumbNodeSelector = HtmlDocumentTemp.DocumentNode.SelectNodes(".//div[contains(@class, 'CompleteFromSubmission ')]");
            if (ThumbNodeSelector.Count == 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            ushort SkipCounter = 0;
            Dictionary<string, string> Posts2Grab = new Dictionary<string, string>();
            foreach (HtmlNode HtmlNodeTemp in ThumbNodeSelector)
            {
                string URL2Post = $"https://inkbunny.net{HtmlNodeTemp.SelectSingleNode(".//a").Attributes["href"].Value}";

                string SubmissionType = HtmlNodeTemp.SelectSingleNode(".//img[@class='widget_thumbnailFromSubmission_icon'][2]").Attributes["title"].Value;
                if (SubmissionType.Equals("Type: Writing - Document") || SubmissionType.Equals("Type: Music - Single Track"))
                {
                    Module_Grabber.Report_Info($"Skipped grabbing - Unsupported submission type: {SubmissionType.Replace("Type:", "")} [@{URL2Post}]");
                    continue;
                }

                if (Module_Grabber._GrabQueue_URLs.Contains(URL2Post))
                {
                    SkipCounter++;
                    continue;
                }
                else
                {
                    string WorkTitle = WebUtility.HtmlDecode(HtmlNodeTemp.SelectSingleNode(".//div[@class='widget_thumbnailFromSubmission_title']").Attributes["title"].Value);
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
            HTMLSource = string.IsNullOrEmpty(HTMLSource) ? Module_Grabber.GetPageSource(WebAddress, ref Module_CookieJar.Cookies_Inkbunny) : HTMLSource;
            if (string.IsNullOrEmpty(HTMLSource))
            {
                Module_Grabber.Report_Info($"Error encountered in Module_Inkbunny.Grab [@{WebAddress}]");
                return;
            }

            HtmlDocument HtmldocumentTemp = new HtmlDocument();
            HtmldocumentTemp.LoadHtml(HTMLSource);
            HtmlNode PostNode = HtmldocumentTemp.DocumentNode.SelectSingleNode("html");

            HtmlNode LoginTest = PostNode.SelectSingleNode(".//div[@class='elephant elephant_bottom elephant_white']/div[@class='content']/div[@class='title']");
            if (LoginTest != null)
            {
                Module_Grabber.Report_Info($"Grabbing skipped - Media is behind login [@{WebAddress}]");
                return;
            }

            // - - - - - - - - - - - - - - - -

            string Post_URL = WebAddress;

            string Post_DateTimeTemp = PostNode.SelectSingleNode(".//span[@id='submittime_exact']").InnerText.Trim();
            Post_DateTimeTemp = Post_DateTimeTemp.Substring(0, Post_DateTimeTemp.LastIndexOf(':') + 3);
            DateTime Post_DateTime = DateTime.Parse(Post_DateTimeTemp);

            string Post_Title = PostNode.SelectSingleNode(".//div[@id='pictop']//h1").InnerText.Trim();
            Post_Title = WebUtility.HtmlDecode(Post_Title.Replace('[', '⟦').Replace(']', '⟧'));

            string ArtistName = PostNode.SelectSingleNode(".//div[@class='elephant elephant_555753']/div[@class='content' and not(@id)]//a[text()]").Attributes["href"].Value;

            HtmlNode Post_TextNode = PostNode.SelectSingleNode(".//div[@class='elephant elephant_bottom elephant_white']/div[@class='content']//span");
            string? Post_Text = Module_Html2Text.Html2Text_Inkbunny(Post_TextNode);

            string Post_MediaURL;
            List<MediaItem> MediaItemList = new List<MediaItem>();
            ushort SkipCounter = 0;
            if (PostNode.SelectSingleNode(".//form[@id='changethumboriginal_form']") == null)
            {
                if (PostNode.SelectSingleNode(".//div[@id='size_container']").InnerText.ToLower().Contains("download"))
                {
                    Post_MediaURL = PostNode.SelectSingleNode(".//div[@id='size_container']/a").Attributes["href"].Value;
                }
                else
                {
                    Post_MediaURL = PostNode.SelectSingleNode(".//div[@class='content magicboxParent']//img[@class='shadowedimage']").Attributes["src"].Value.Replace("files/screen", "files/full");
                }

                if (Module_Grabber._Grabbed_MediaURLs.Contains(Post_MediaURL))
                {
                    SkipCounter++;
                }
                else
                {
                    MediaItemList.Add(CreateMediaItem(Post_URL, Post_MediaURL, Post_DateTime, ArtistName, Post_Title, Post_Text));
                }
            }
            else
            {
                List<string> MediaTypes = new List<string>() { ".jpg", ".png", ".gif", ".mp4", ".swf" };
                HtmlNode ParentNode = PostNode.SelectSingleNode(".//div[@id='files_area']").ParentNode;
                ProgressBar ProgressBarTemp = Window_Main._RefHolder.Dispatcher.Invoke(() => Module_Grabber.GetProgressBar(ParentNode.SelectNodes(".//img").Count));
                ushort MediaCounter = 0;
                foreach (HtmlNode ImageNode in ParentNode.SelectNodes(".//img"))
                {
                    MediaCounter++;
                    Window_Main._RefHolder.Dispatcher.BeginInvoke(() => ProgressBarTemp.Value = MediaCounter);
                    Post_MediaURL = ImageNode.Attributes["src"].Value;

                    if (Post_MediaURL.Contains("overlays/video")) //if video
                    {
                        HtmlDocument HtmlDocumentTemp2 = new HtmlDocument();
                        HtmlDocumentTemp2.LoadHtml(Module_Grabber.GetPageSource($"{Post_URL}-p{MediaCounter}", ref Module_CookieJar.Cookies_Inkbunny));
                        if (HtmlDocumentTemp2.DocumentNode.SelectSingleNode(".//div[@id='size_container']").InnerText.ToLower().Contains("download"))
                        {
                            Post_MediaURL = HtmlDocumentTemp2.DocumentNode.SelectSingleNode(".//div[@id='size_container']/a").Attributes["href"].Value;
                        }
                        else
                        {
                            Post_MediaURL = HtmlDocumentTemp2.DocumentNode.SelectSingleNode(".//div[@class='content magicboxParent']//img[@class='shadowedimage']").Attributes["src"].Value;
                        }
                    }
                    Post_MediaURL = Post_MediaURL.Replace("thumbnails/medium", "files/full").Replace("_noncustom", "");
                    Post_MediaURL = Post_MediaURL.Substring(0, Post_MediaURL.Length - 4);

                    string MediaLinkFix;
                    foreach (string MediaType in MediaTypes)
                    {
                        MediaLinkFix = $"{Post_MediaURL}{MediaType}";
                        if (CheckURLExists(MediaLinkFix))
                        {
                            Post_MediaURL = MediaLinkFix;
                            break;
                        }
                    }

                    if (Module_Grabber._Grabbed_MediaURLs.Contains(Post_MediaURL))
                    {
                        SkipCounter++;
                        continue;
                    }
                    MediaItemList.Add(CreateMediaItem(Post_URL, Post_MediaURL, Post_DateTime, ArtistName, Post_Title, Post_Text));
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
            lock (Module_Grabber._Grabbed_MediaURLs)
            {
                foreach (MediaItem MediaItemTemp in MediaItemList)
                {
                    Post_MediaURL = MediaItemTemp.Grab_MediaURL;
                    Module_Grabber._Grabbed_MediaURLs.Add(Post_MediaURL);
                }
            }
            string PrintText = $"Finished grabbing: {Post_URL}";
            if (SkipCounter > 0)
            {
                PrintText += $", {SkipCounter} media container{(SkipCounter > 1 ? "s have" : " has")} been skipped";
            }
            Module_Grabber.Report_Info(PrintText);
        }

        private static MediaItem CreateMediaItem(string Post_URL, string Post_MediaURL, DateTime Post_DateTime, string ArtistName, string Post_Title, string? Post_Text)
        {
            string ThumbnailURLTemp;
            switch (Post_MediaURL.Substring(Post_MediaURL.LastIndexOf('.')))
            {
                case ".mp4":
                case ".swf":
                    {
                        ThumbnailURLTemp = Post_MediaURL.Substring(0, Post_MediaURL.Length - 4) + ".jpg";
                        ThumbnailURLTemp = ThumbnailURLTemp.Replace("files/full", "thumbnails/large");
                        ThumbnailURLTemp = CheckURLExists(ThumbnailURLTemp) ? ThumbnailURLTemp : "https://nl.ib.metapix.net/images80/overlays/video.png";
                        break;
                    }

                case ".gif":
                    {
                        ThumbnailURLTemp = Post_MediaURL.Replace("files/full", "files/screen");
                        break;
                    }

                default:
                    {
                        ThumbnailURLTemp = Post_MediaURL.Replace("files/full", "thumbnails/large");
                        ThumbnailURLTemp = ThumbnailURLTemp.Substring(0, ThumbnailURLTemp.Length - 4) + "_noncustom.jpg";
                        break;
                    }
            }

            MediaItem MediaItemTemp = new MediaItem
            {
                Grab_PageURL = Post_URL,
                Grab_MediaURL = Post_MediaURL,
                Grab_ThumbnailURL = ThumbnailURLTemp,
                Grab_DateTime = Post_DateTime,
                Grab_Artist = ArtistName,
                Grab_Title = $"⮚ {Post_Title} ⮘ by {ArtistName} on Inkbunny",
                Grab_TextBody = Post_Text,
                Grid_MediaFormat = Post_MediaURL.Substring(Post_MediaURL.LastIndexOf('.') + 1),
                Grid_MediaByteLength = Module_Grabber.GetMediaSize(Post_MediaURL),
                UP_Tags = Post_DateTime.Year.ToString(),
                UP_IsWhitelisted = true
            };
            return MediaItemTemp;
        }

        private static bool CheckURLExists(string Post_MediaURL)
        {
            HttpWebRequest CheckExistsRequest = (HttpWebRequest)WebRequest.Create(Post_MediaURL);
            CheckExistsRequest.Method = "HEAD";
            CheckExistsRequest.UserAgent = AppSettings.GlobalUserAgent;
            CheckExistsRequest.Timeout = 3000;
            try
            {
                using (HttpWebResponse GetSizeResponse = (HttpWebResponse)CheckExistsRequest.GetResponse())
                {
                    if (GetSizeResponse.StatusCode == HttpStatusCode.OK)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}