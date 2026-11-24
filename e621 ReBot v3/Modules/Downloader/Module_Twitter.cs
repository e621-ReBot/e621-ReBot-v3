using e621_ReBot_v3.Modules.Grabber;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace e621_ReBot_v3.Modules.Downloader
{
    internal static partial class Module_Twitter_D
    {
        private static string? SpecialSaveFolder = null;

        internal static void GrabMediaLinks(string WebAddress)
        {
            if (WebAddress.Contains("/status/")) //single
            {
                if (Module_Twitter_G.TwitterJSONHolder == null) return;

                //Isn't guaranteed to be first token, have to search for it.
                string TweetID = Twitter_Regex().Match(WebAddress).Value;
                JToken TweetJSON = Module_Twitter_G.TwitterJSONHolder.SelectToken($"$.[?(@.id_str == '{TweetID}')]");
                if (TweetJSON == null) return;

                GetMedia(WebAddress, TweetJSON);
            }
            else //multi
            {
                //TODO
                //TODO
                //TODO

                //if (TwitterMultiJSONHolder == null) return;

                //SpecialSaveFolder = (string)TwitterMultiJSONHolder.First["owner_displayname"];
                //SpecialSaveFolder = Module_Downloader.SelectFolderPopup(SpecialSaveFolder);
                //foreach (JToken JTokenTemp in TwitterMultiJSONHolder.Children())
                //{
                //    GetMedia(WebAddress, JTokenTemp, SpecialSaveFolder);
                //}
            }
        }

        [GeneratedRegex(@"(?<=status/)\d+(?=/)?")]
        private static partial Regex Twitter_Regex();
        private static void GetMedia(string WebAddress, JToken TwitterJSONToken, string? FolderName = null)
        {
            JArray MediaHolder = (JArray)TwitterJSONToken?["extended_entities"]?["media"];
            if (MediaHolder == null || MediaHolder.Count == 0)
            {
                return;
            }

            string? MediaURL;
            string? ThumbURL;
            foreach (JToken MediaNode in MediaHolder)
            {
                if (MediaNode["video_info"] != null)
                {
                    JToken? BestVideo = null;
                    foreach (JToken VideoCheck in MediaNode["video_info"]["variants"])
                    {
                        if (VideoCheck["bitrate"] != null)
                        {
                            if (BestVideo == null)
                            {
                                BestVideo = VideoCheck;
                                continue;
                            }
                            if ((int)VideoCheck["bitrate"] > (int)BestVideo["bitrate"])
                            {
                                BestVideo = VideoCheck;
                            }
                        }
                    }
                    MediaURL = (string?)BestVideo["url"];
                    if (MediaURL.Contains('?')) MediaURL = MediaURL.Substring(0, MediaURL.IndexOf('?'));
                }
                else
                {
                    MediaURL = (string)MediaNode["media_url_https"];
                }
                string Media_Format = MediaURL.Substring(MediaURL.LastIndexOf('.') + 1); //Take it before :orig
                MediaURL = $"{MediaURL}{(MediaURL.EndsWith(".mp4") ? null : ":orig")}";

                if (Module_Downloader.CheckDownloadQueue4Duplicate(MediaURL)) continue;

                ThumbURL = (string)MediaNode["media_url_https"];

                Module_Downloader.AddDownloadItem2Queue(
                    PageURL: WebAddress,
                    MediaURL: MediaURL,
                    ThumbnailURL: ThumbURL,
                    MediaFormat: Media_Format,
                    Artist: string.Empty,
                    DL_Folder: FolderName);
            }
        }


    }
}