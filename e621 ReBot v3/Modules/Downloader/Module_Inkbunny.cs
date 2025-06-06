using HtmlAgilityPack;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace e621_ReBot_v3.Modules.Downloader
{
    internal static class Module_Inkbunny
    {
        private static string? SpecialSaveFolder = null;
        internal static void GrabMediaLinks(string WebAddress)
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

            string? PicURL;
            string? ThumbURL;
            string? Media_Format;
            if (WebAddress.Contains("/s/")) //single
            {
                List<string> MediaList = new List<string>();

                if (PostNode.SelectSingleNode(".//form[@id='changethumboriginal_form']") == null)
                {
                    if (PostNode.SelectSingleNode(".//div[@id='size_container']").InnerText.ToLower().Contains("download"))
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
                    List<string> MediaTypes = new List<string>() { ".jpg", ".png", ".gif", ".mp4", ".swf" };
                    HtmlNode ParentNode = PostNode.SelectSingleNode(".//div[@id='files_area']").ParentNode;
                    ushort MediaCounter = 0;
                    foreach (HtmlNode ImageNode in ParentNode.SelectNodes(".//img"))
                    {
                        MediaCounter++;
                        PicURL = ImageNode.Attributes["src"].Value;

                        if (PicURL.Contains("overlays/writing")) //don't need writing thumb
                        {
                            continue;
                        }

                        if (PicURL.Contains("overlays/video")) //if video
                        {
                            HtmlDocument HtmlDocumentTemp2 = new HtmlDocument();
                            HtmlDocumentTemp2.LoadHtml(Module_Grabber.GetPageSource($"{PicURL}-p{MediaCounter}", ref Module_CookieJar.Cookies_Inkbunny));
                            if (HtmlDocumentTemp2.DocumentNode.SelectSingleNode(".//div[@id='size_container']").InnerText.ToLower().Contains("download"))
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
                            if (CheckURLExists(MediaLinkFix))
                            {
                                PicURL = MediaLinkFix;
                                break;
                            }
                        }

                        MediaList.Add(PicURL);

                        Thread.Sleep(Module_Grabber.PauseBetweenImages);
                    }
                }

                string ArtistName = PostNode.SelectSingleNode(".//div[@class='elephant elephant_555753']/div[@class='content' and not(@id)]//a[text()]").Attributes["href"].Value;

                foreach (string MediaURL in MediaList)
                {
                    if (Module_Downloader._2Download_DownloadItems.ContainsURL(MediaURL) || Module_Downloader.Download_AlreadyDownloaded.Contains(MediaURL))
                    {
                        continue;
                    }

                    Media_Format = MediaURL.Substring(MediaURL.LastIndexOf('.') + 1);

                    switch (MediaURL.Substring(MediaURL.LastIndexOf('.')))
                    {
                        case ".mp4":
                        case ".swf":
                            {
                                ThumbURL = MediaURL.Substring(0, MediaURL.Length - 4) + ".jpg";
                                ThumbURL = ThumbURL.Replace("files/full", "thumbnails/large");
                                ThumbURL = CheckURLExists(ThumbURL) ? ThumbURL : "https://nl.ib.metapix.net/images80/overlays/video.png";
                                break;
                            }

                        case ".gif":
                            {
                                ThumbURL = MediaURL.Replace("files/full", "files/screen");
                                break;
                            }

                        default:
                            {
                                ThumbURL = MediaURL.Replace("files/full", "thumbnails/large");
                                ThumbURL = ThumbURL.Substring(0, ThumbURL.Length - 4) + "_noncustom.jpg";
                                break;
                            }
                    }

                    Module_Downloader.AddDownloadItem2Queue(
                        PageURL: WebAddress,
                        MediaURL: MediaURL,
                        ThumbnailURL: ThumbURL,
                        MediaFormat: Media_Format,
                        Artist: ArtistName);
                }
            }
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