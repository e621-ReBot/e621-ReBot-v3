using HtmlAgilityPack;
using System.Net;

namespace e621_ReBot_v3.Modules.Downloader
{
    internal static class Module_FurAffinity
    {
        private static string? SpecialSaveFolder = null;
        internal static void GrabMediaLinks(string WebAddress)
        {
            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(Module_CefSharp.BrowserHTMLSource);
            HtmlNode PostNode = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//body");

            HtmlNode LoginTest = PostNode.SelectSingleNode(".//div[@id='standardpage']/section[@class='aligncenter notice-message']");
            if (LoginTest != null)
            {
                Module_Downloader.Report_Info($"Grabbing skipped - Media is behind login [@{WebAddress}]");
                return;
            }

            string? MediaURL;
            string? ThumbURL;
            string? Media_Format;
            if (WebAddress.Contains("/view/")) //single
            {
                HtmlNode DownloadNode = PostNode.SelectSingleNode(".//div[@class='download' or @class='download fullsize']/a");
                if (DownloadNode == null) //classic theme selected
                {
                    DownloadNode = PostNode.SelectSingleNode(".//div[@id='page-submission']//div[@class='alt1 actions aligncenter']//a[text()='Download']");
                }
                MediaURL = $"https:{DownloadNode.Attributes["href"].Value}";

                if (Module_Downloader.CheckDownloadQueue4Duplicate(MediaURL)) return;

                Media_Format = MediaURL.Substring(MediaURL.LastIndexOf('.') + 1);
                ThumbURL = $"https:{PostNode.SelectSingleNode(".//img[@id='submissionImg']").Attributes["data-preview-src"].Value.Replace("@600-", "@200-")}";

                string ArtistName = WebUtility.HtmlDecode(PostNode.SelectSingleNode(".//div[@class='submission-id-sub-container' or @class='classic-submission-title information']//a").InnerText.Trim());

                Module_Downloader.AddDownloadItem2Queue(
                    PageURL: WebAddress,
                    MediaURL: MediaURL,
                    ThumbnailURL: ThumbURL,
                    MediaFormat: Media_Format,
                    Artist: ArtistName);
            }
        }
    }
}