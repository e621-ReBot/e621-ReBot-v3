using CefSharp;
using e621_ReBot_v3.CustomControls;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace e621_ReBot_v3.Modules.Grabber
{
    internal static class Module_Plurk
    {
        internal static void Queue_Prepare(string WebAddress)
        {
            Module_CookieJar.GetCookies(WebAddress, ref Module_CookieJar.Cookies_Plurk);
            Module_CefSharp.BrowserHTMLSource = Module_CefSharp.CefSharpBrowser.GetSourceAsync().Result;
            if (WebAddress.StartsWith("https://www.plurk.com/p/"))
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

            HtmlNodeCollection PlurkNodeSelector = HtmlDocumentTemp.DocumentNode.SelectNodes(".//div[@id='timeline_cnt']//div[@id and @class='plurk_cnt']//img/ancestor::div[@data-type='plurk']");
            if (PlurkNodeSelector.Count == 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            ushort SkipCounter = 0;
            List<string> Posts2Grab = new List<string>();
            foreach (HtmlNode HtmlNodeTemp in PlurkNodeSelector)
            {
                string URL2Post = $"https://www.plurk.com{HtmlNodeTemp.SelectSingleNode(".//td[@class='td_response_count']/a").Attributes["href"].Value}";
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

        internal static async Task Grab(string WebAddress, string HTMLSource)
        {
            HTMLSource = string.IsNullOrEmpty(HTMLSource) ? await Module_Grabber.GetPageSource(WebAddress, Module_CookieJar.Cookies_Plurk) : HTMLSource;
            if (string.IsNullOrEmpty(HTMLSource))
            {
                Module_Grabber.Report_Info($"Error encountered in Module_Plurk.Grab [@{WebAddress}]");
                return;
            }

            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(HTMLSource);
            HtmlNode PostNode = HtmlDocumentTemp.DocumentNode.SelectSingleNode("html");

            HtmlNode LoginTest = PostNode.SelectSingleNode(".//div[@class='need-login']");
            if (LoginTest != null)
            {
                Module_Grabber.Report_Info($"Grabbing skipped - Media is behind login [@{WebAddress}]");
                return;
            }

            // - - - - - - - - - - - - - - - -

            string Post_URL = WebAddress;

            string Post_DateTimeTemp = PostNode.SelectSingleNode(".//time[@class='timeago']").Attributes["datetime"].Value;
            DateTime Post_DateTime = DateTime.Parse(Post_DateTimeTemp);

            string ArtistName = PostNode.SelectSingleNode(".//article[@id='permanent-plurk']//div[@class='user']//a").InnerText.Trim();
            string ArtistProfileName = PostNode.SelectSingleNode(".//article[@id='permanent-plurk']//div[@class='avatar']//a").Attributes["href"].Value.Substring(1);
            ArtistName = $"{ArtistName} (@{ArtistProfileName})";

            HtmlNode Post_TextNode = PostNode.SelectSingleNode(".//article[@id='permanent-plurk']//div[@class='text_holder']");
            string? Post_Text = null; //Module_Html2Text.Html2Text_Plurk(Post_TextNode);

            List<string> PlurkImages = new List<string>();
            if (Post_TextNode != null)
            {
                foreach (HtmlNode DescriptionLine in Post_TextNode.ChildNodes)
                {
                    switch (DescriptionLine.Name)
                    {
                        case "#text":
                        case "span":
                            {
                                Post_Text += DescriptionLine.InnerText.Trim();
                                break;
                            }

                        case "br":
                            {
                                Post_Text += "\n";
                                break;
                            }

                        case "a":
                            {
                                if (DescriptionLine.Attributes["class"] != null)
                                {
                                    if (DescriptionLine.Attributes["class"].Value.Contains("ex_link pictureservices"))
                                    {
                                        PlurkImages.Add(DescriptionLine.Attributes["href"].Value);
                                    }
                                    else if (DescriptionLine.Attributes["class"].Value.Contains("ex_link"))
                                    {
                                        Post_Text += $"\"{DescriptionLine.InnerText}\":{DescriptionLine.Attributes["href"].Value} ";
                                    }
                                }
                                else
                                {
                                    Post_Text += DescriptionLine.InnerText.Trim();
                                }
                                break;
                            }

                        case "img":
                            {
                                //skip
                                break;
                            }

                        default:
                            {
                                Post_Text += "UNKNOWN ELEMENT\n";
                                break;
                            }
                    }
                }
            }
            if (Post_Text != null) Post_Text = WebUtility.HtmlDecode(Post_Text).Trim() + " ";

            string Post_MediaURL;
            List<MediaItem> MediaItemList = new List<MediaItem>();
            ushort SkipCounter = 0;
            foreach (string PlurkImage in PlurkImages)
            {
                Post_MediaURL = PlurkImage;

                if (!Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
                {
                    SkipCounter++;
                    continue;
                }

                string Post_ThumbnailURL = Post_MediaURL.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
                Post_ThumbnailURL = $"https://images.plurk.com/mx_{Post_ThumbnailURL.Substring(0, Post_ThumbnailURL.IndexOf('.'))}.jpg";
                MediaItem MediaItemTemp = new MediaItem
                {
                    Grab_PageURL = Post_URL,
                    Grab_MediaURL = Post_MediaURL,
                    Grab_ThumbnailURL = Post_ThumbnailURL,
                    Grab_DateTime = Post_DateTime,
                    Grab_Artist = ArtistName,
                    Grab_Title = $"Plurk by {ArtistName}",
                    Grab_TextBody = Post_Text,
                    Grid_MediaFormat = Post_MediaURL.Substring(Post_MediaURL.LastIndexOf('.') + 1),
                    Grid_MediaByteLength = Module_Grabber.GetMediaSize(Post_MediaURL),
                    UP_Tags = Post_DateTime.Year.ToString(),
                    UP_IsWhitelisted = true
                };
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
    }
}