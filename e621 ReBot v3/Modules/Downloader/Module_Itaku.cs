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

                SpecialSaveFolder = ItakuMultiJSONHolder.First["owner_displayname"].Value<string>();
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

            //https://itaku.ee/api/media/gallery_imgs/xyz/sm_REFLb27.png
            //https://itaku.ee/api/media/gallery_imgs/xyz/lg_i96zjR1.png
            //https://itaku.ee/api/media/gallery_imgs/xyz/xl_GoYCeeW.png
            //https://itaku.ee/api/media/gallery_imgs/xyz.PNG
            string ThumbURL = ItakuJSONToken["image_xl"].Value<string>();
            if (ThumbURL.Length - ThumbURL.LastIndexOf('/') == 8)
            {
                ThumbURL = ThumbURL.Replace("/xl", "/sm");
            }

            Module_Downloader.AddDownloadItem2Queue(
                PageURL: WebAddress,
                MediaURL: "https://itaku.ee/api/media/gallery_imgs/Tiru__Splateon_Sketch_May_2020_hyz3T4W/sm_REFLb27.png", //PicURL,
                ThumbnailURL: "https://itaku.ee/api/media/gallery_imgs/Tiru__Splateon_Fur_Coat_August_2020_w9l5WcU.PNG", //ThumbURL,
                MediaFormat: Media_Format,
                Artist: string.Empty,
                e6PoolName: FolderName);
        }
    }
}