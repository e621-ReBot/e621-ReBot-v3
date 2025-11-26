using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace e621_ReBot_v3.Modules.Downloader
{
    internal static partial class Module_Pixiv
    {
        private static string? SpecialSaveFolder = null;

        [GeneratedRegex(@"(?<=/)\d+(?=/)?")]
        private static partial Regex Pixiv_Regex();
        internal static async void GrabMediaLinks(string WebAddress)
        {
            Module_CookieJar.GetCookies(WebAddress, ref Module_CookieJar.Cookies_Pixiv);

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
            if (WebAddress.Contains("/artworks/")) //single
            {
                string ArtistName = PostNode.SelectSingleNode(".//aside/section/h2//a[@class]").InnerText; ;

                string Post_ID = Pixiv_Regex().Match(WebAddress).Value;

                string JSONSourceTest = await Module_Grabber.GetPageSource($"https://www.pixiv.net/ajax/illust/{Post_ID}/pages", Module_CookieJar.Cookies_Pixiv);
                JObject PixivJSON = JObject.Parse(JSONSourceTest);

                foreach (JToken JTokenTemp in PixivJSON["body"].Children())
                {
                    MediaURL = (string)JTokenTemp["urls"]["original"];

                    if (Module_Downloader.CheckDownloadQueue4Duplicate(MediaURL)) return;

                    ThumbURL = ((string)JTokenTemp["urls"]["thumb_mini"]).Replace("/128x128/", "/360x360_70/"); // /250x250_80_a2/ is cut off, that's not good.;
                    Media_Format = MediaURL.Substring(MediaURL.LastIndexOf('.') + 1);

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
}