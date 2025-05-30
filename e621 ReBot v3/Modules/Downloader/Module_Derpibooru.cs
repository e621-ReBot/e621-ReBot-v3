﻿using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Web;

namespace e621_ReBot_v3.Modules.Downloader
{
    internal static class Module_Derpibooru
    {
        private static string? SpecialSaveFolder = null;
        internal static void GrabMediaLinks(string WebAddress)
        {
            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(Module_CefSharp.BrowserHTMLSource);

            string? PicURL;
            string? ThumbURL;
            string? Media_Format;

            string[] URLParts = WebAddress.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (URLParts[2].Equals("images") && URLParts.Length > 3) //single
            {
                PicURL = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//main[@id='content']//div[@id]//a[@title='Download (tags in filename)']").Attributes["href"].Value;
                if (Module_Downloader._2Download_DownloadItems.ContainsURL(PicURL) || Module_Downloader.Download_AlreadyDownloaded.Contains(PicURL))
                {
                    return;
                }

                Media_Format = PicURL.Substring(PicURL.LastIndexOf('.') + 1);

                JObject PostUris = JObject.Parse(HttpUtility.HtmlDecode(HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//main[@id='content']//div[@class='image-show-container']").Attributes["data-uris"].Value));
                ThumbURL = PostUris["thumb"].Value<string>();

                Module_Downloader.AddDownloadItem2Queue(
                    PageURL: WebAddress,
                    MediaURL: PicURL,
                    ThumbnailURL: ThumbURL,
                    MediaFormat: Media_Format,
                    Artist: string.Empty);
            }
            else //multi images, galleries
            {
                if (URLParts[2].Equals("galleries"))
                {
                    SpecialSaveFolder = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//main[@id]//span[@class='block__header__title page__title']//strong").InnerText.Trim();
                }

                SpecialSaveFolder = Module_Downloader.SelectFolderPopup(SpecialSaveFolder);
                HtmlNodeCollection NodeSelector = HtmlDocumentTemp.DocumentNode.SelectNodes(".//main[@id]//div[@class='media-box']");
                if (NodeSelector != null)
                {
                    foreach (HtmlNode Post in NodeSelector)
                    {
                        JObject PostUris = JObject.Parse(HttpUtility.HtmlDecode(Post.SelectSingleNode(".//div[@data-uris]").Attributes["data-uris"].Value));
                        PicURL = PostUris["full"].Value<string>();
                        if (Module_Downloader._2Download_DownloadItems.ContainsURL(PicURL) || Module_Downloader.Download_AlreadyDownloaded.Contains(PicURL))
                        {
                            continue;
                        }

                        Media_Format = PicURL.Substring(PicURL.LastIndexOf('.') + 1);
                        ThumbURL = PostUris["thumb"].Value<string>();

                        Module_Downloader.AddDownloadItem2Queue(
                            PageURL: WebAddress,
                            MediaURL: PicURL,
                            ThumbnailURL: ThumbURL,
                            MediaFormat: Media_Format,
                            Artist: string.Empty,
                            e6PoolName: SpecialSaveFolder);
                    }
                }
            }
        }
    }
}