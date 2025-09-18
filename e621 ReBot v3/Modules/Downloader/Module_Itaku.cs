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
            string MediaURL = ItakuJSONToken["image"].Value<string>();

            if (Module_Downloader.CheckDownloadQueue4Duplicate(MediaURL)) return;

            string? Media_Format = MediaURL.Substring(MediaURL.LastIndexOf('.') + 1);

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
                MediaURL: MediaURL,
                ThumbnailURL: ThumbURL,
                MediaFormat: Media_Format,
                Artist: string.Empty,
                e6PoolName: FolderName);
        }
    }
}