using System.Web;
using System.Xml.Serialization;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace e621_ReBot_v3.Modules.Downloader
{
    internal static class Module_Itaku
    {
        private static string? SpecialSaveFolder = null;

        internal static JObject? ItakuSingleJSONHolder;
        internal static JArray? ItakuMultiJSONHolder;
        internal static void GrabMediaLinks(string WebAddress)
        {
            if (WebAddress.Contains("/images/")) //single
            {
                if (ItakuSingleJSONHolder == null) return;

                GetMedia(WebAddress, ItakuSingleJSONHolder);
            }
            else //multi
            {
                if (ItakuMultiJSONHolder == null) return;

                SpecialSaveFolder = WebAddress.Split('/',System.StringSplitOptions.RemoveEmptyEntries)[3];
                SpecialSaveFolder = Module_Downloader.SelectFolderPopup(SpecialSaveFolder);
                foreach (JToken JTokenTemp in ItakuMultiJSONHolder.Children())
                {
                    GetMedia(WebAddress, JTokenTemp, SpecialSaveFolder);
                }
            }
        }

        private static void GetMedia(string WebAddress, JToken ItakuJSONToken, string? FolderName = null)
        {
            string PicURL = ItakuJSONToken["image"].Value<string>();

            if (Module_Downloader._2Download_DownloadItems.ContainsURL(PicURL) || Module_Downloader.Download_AlreadyDownloaded.Contains(PicURL))
            {
                return;
            }

            string? Media_Format = PicURL.Substring(PicURL.LastIndexOf('.') + 1);

            string ThumbURL = ItakuJSONToken["image_xl"].Value<string>().Replace("/xl","/sm");

            Module_Downloader.AddDownloadItem2Queue(
                PageURL: WebAddress,
                MediaURL: PicURL,
                ThumbnailURL: ThumbURL,
                MediaFormat: Media_Format,
                Artist: string.Empty,
                e6PoolName: FolderName);
        }
    }
}