using HtmlAgilityPack;
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

            string? MediaURL;
            string? ThumbURL;
            string? Media_Format;

            string[] URLParts = WebAddress.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (URLParts[2].Equals("images") && URLParts.Length > 3) //single
            {
                MediaURL = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//main[@id='content']//div[@id]//a[@title='Download (tags in filename)']").Attributes["href"].Value;

                if (Module_Downloader.CheckDownloadQueue4Duplicate(MediaURL)) return;

                Media_Format = MediaURL.Substring(MediaURL.LastIndexOf('.') + 1);

                JObject PostUris = JObject.Parse(HttpUtility.HtmlDecode(HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//main[@id='content']//div[@class='image-show-container']").Attributes["data-uris"].Value));
                ThumbURL = (string?)PostUris["thumb"];

                Module_Downloader.AddDownloadItem2Queue(
                    PageURL: WebAddress,
                    MediaURL: MediaURL,
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
                        MediaURL = (string)PostUris["full"];

                        if (Module_Downloader.CheckDownloadQueue4Duplicate(MediaURL)) continue;

                        Media_Format = MediaURL.Substring(MediaURL.LastIndexOf('.') + 1);
                        ThumbURL = (string)PostUris["thumb"];

                        Module_Downloader.AddDownloadItem2Queue(
                            PageURL: WebAddress,
                            MediaURL: MediaURL,
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