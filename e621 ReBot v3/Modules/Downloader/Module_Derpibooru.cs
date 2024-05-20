using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using e621_ReBot_v3.CustomControls;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace e621_ReBot_v3.Modules.Downloader
{
    internal static class Module_Derpibooru
    {
        private static string? SpecialSaveFolder = null;
        internal static void Grab(string WebAddress)
        {
            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(Module_CefSharp.BrowserHTMLSource);
            string[] URLParts = WebAddress.Split('/', StringSplitOptions.RemoveEmptyEntries);

            string? PicURL;
            string? ThumbURL;
            string? Media_Format;
            switch (URLParts[2])
            {
                case string Images when Images.Equals("images") && URLParts.Length > 3:
                    {
                        PicURL = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//main[@id='content']//div[@id]//a[@title='Download (tags in filename)']").Attributes["href"].Value;
                        if (Module_Downloader._2Download_DownloadItems.ContainsURL(PicURL) || Module_Downloader.Download_AlreadyDownloaded.Contains(PicURL))
                        {
                            return;
                        }

                        JObject PostUris = JObject.Parse(HttpUtility.HtmlDecode(HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//main[@id='content']//div[@class='image-show-container']").Attributes["data-uris"].Value));
                        ThumbURL = PostUris["thumb"].Value<string>();
                        Media_Format = PicURL.Substring(PicURL.LastIndexOf('.') + 1);

                        Module_Downloader.AddDownloadItem2Queue(
                            PageURL: WebAddress,
                            MediaURL: PicURL,
                            ThumbnailURL: ThumbURL,
                            MediaFormat: Media_Format,
                            Artist: string.Empty);
                        break;
                    }

                default: //multi images, galleries
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

                                ThumbURL = PostUris["thumb"].Value<string>();
                                Media_Format = PicURL.Substring(PicURL.LastIndexOf('.') + 1);

                                Module_Downloader.AddDownloadItem2Queue(
                                    PageURL: WebAddress,
                                    MediaURL: PicURL,
                                    ThumbnailURL: ThumbURL,
                                    MediaFormat: Media_Format,
                                    Artist: string.Empty,
                                    e6PoolName: SpecialSaveFolder);                                 
                            }
                        }
                        break;
                    }
            }
        }
    }
}