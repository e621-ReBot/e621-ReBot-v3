using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace e621_ReBot_v3.Modules.Downloader
{
    internal static class Module_Inkbunny
    {
        private static string? SpecialSaveFolder = null;
        internal static async Task GrabMediaLinks(string WebAddress)
        {
            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(Module_CefSharp.BrowserHTMLSource);
            HtmlNode PostNode = HtmlDocumentTemp.DocumentNode.SelectSingleNode("html");

            HtmlNode LoginTest = PostNode.SelectSingleNode(".//div[@class='elephant elephant_bottom elephant_white']/div[@class='content']/div[@class='title']");
            if (LoginTest != null)
            {
                Module_Downloader.Report_Info($"Grabbing skipped - Media is behind login [@{WebAddress}]");
                return;
            }

            HashSet<string> MediaList = new HashSet<string>();
            if (WebAddress.Contains("/s/"))
            {
                if (PostNode.SelectSingleNode(".//form[@id='changethumboriginal_form']") == null) //single
                {
                    if (PostNode.SelectSingleNode(".//div[@id='size_container']").InnerText.Contains("download", StringComparison.OrdinalIgnoreCase))
                    {
                        MediaList.Add(PostNode.SelectSingleNode(".//div[@id='size_container']/a").Attributes["href"].Value);
                    }
                    else
                    {
                        MediaList.Add(PostNode.SelectSingleNode(".//div[@class='content magicboxParent']//img[@class='shadowedimage']").Attributes["src"].Value.Replace("files/screen", "files/full"));
                    }
                }
                else
                {
                    HashSet<string> MediaTypes = new HashSet<string>() { ".jpg", ".png", ".gif", ".mp4", ".swf" };
                    HtmlNode ParentNode = PostNode.SelectSingleNode(".//div[@id='files_area']").ParentNode;
                    ushort MediaCounter = 0;
                    foreach (HtmlNode ImageNode in ParentNode.SelectNodes(".//img"))
                    {
                        MediaCounter++;
                        string PicURL = ImageNode.Attributes["src"].Value;

                        if (PicURL.Contains("overlays/writing")) //don't need writing thumb
                            continue;

                        if (PicURL.Contains("overlays/video")) //if video
                        {
                            DownloadResult DownloadResultTemp = await ReBot_HttpClient.DownloadStringAsync($"{PicURL}-p{MediaCounter}");
                            if (!DownloadResultTemp.IsSuccessStatusCode)
                            {
                                Module_Downloader.Report_Info($"Error encountered in Module_Inkbunny.Download [@{WebAddress}]");
                                return;
                            }

                            HtmlDocument HtmlDocumentTemp2 = new HtmlDocument();
                            HtmlDocumentTemp2.LoadHtml(DownloadResultTemp.TextData);
                            if (HtmlDocumentTemp2.DocumentNode.SelectSingleNode(".//div[@id='size_container']").InnerText.Contains("download", StringComparison.OrdinalIgnoreCase))
                            {
                                PicURL = HtmlDocumentTemp2.DocumentNode.SelectSingleNode(".//div[@id='size_container']/a").Attributes["href"].Value;
                            }
                            else
                            {
                                PicURL = HtmlDocumentTemp2.DocumentNode.SelectSingleNode(".//div[@class='content magicboxParent']//img[@class='shadowedimage']").Attributes["src"].Value;
                            }
                        }

                        PicURL = PicURL.Replace("thumbnails/medium", "files/full").Replace("_noncustom", null);
                        PicURL = PicURL.Substring(0, PicURL.Length - 4);

                        string MediaLinkFix;
                        foreach (string MediaType in MediaTypes)
                        {
                            MediaLinkFix = $"{PicURL}{MediaType}";
                            bool TypeFound = await CheckURLExists(MediaLinkFix);
                            if (TypeFound)
                            {
                                PicURL = MediaLinkFix;
                                break;
                            }
                        }

                        MediaList.Add(PicURL);

                        await Task.Delay(Module_Grabber.PauseBetweenImages);
                    }
                }

                string ArtistName = PostNode.SelectSingleNode(".//div[@class='elephant elephant_555753']/div[@class='content' and not(@id)]//a[text()]").Attributes["href"].Value;


                //lock should already be in background thread as the task is done in background
                lock (Module_Downloader._2Download_DownloadItems)
                {
                    foreach (string MediaURL in MediaList)
                    {
                        if (Module_Downloader.CheckDownloadQueue4Duplicate(MediaURL)) continue;

                        string ThumbURL = MediaURL;
                        string Media_Format = MediaURL.Substring(MediaURL.LastIndexOf('.') + 1);
                        switch (Media_Format)
                        {
                            case "mp4":
                            case "swf":
                                {
                                    ThumbURL = ThumbURL.Substring(0, ThumbURL.Length - 4) + ".jpg";
                                    ThumbURL = ThumbURL.Replace("files/full", "thumbnails/large");

                                    if (CheckURLExists(ThumbURL).Result == false)
                                        ThumbURL = "https://nl.ib.metapix.net/images80/overlays/video.png";

                                    break;
                                }

                            case "gif":
                                {
                                    ThumbURL = ThumbURL.Replace("files/full", "files/screen");
                                    break;
                                }

                            default:
                                {
                                    ThumbURL = ThumbURL.Replace("files/full", "thumbnails/large");
                                    ThumbURL = ThumbURL.Substring(0, ThumbURL.Length - 4) + "_noncustom.jpg";
                                    break;
                                }
                        }

                        Module_Downloader.AddDownloadItem2Queue(
                            PageURL: WebAddress,
                            MediaURL: MediaURL,
                            ThumbnailURL: ThumbURL,
                            MediaFormat: Media_Format,
                            Artist: ArtistName,
                            LockDLList: false);
                    }
                }
            }
        }

        private static async Task<bool> CheckURLExists(string Post_MediaURL)
        {
            DownloadResult DownloadResultTemp = await ReBot_HttpClient.HeadRequestAsync(Post_MediaURL);

            if (DownloadResultTemp.IsSuccessStatusCode)
                return true;

            return false;
        }
    }
}