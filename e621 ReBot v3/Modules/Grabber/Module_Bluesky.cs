using e621_ReBot_v3.CustomControls;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace e621_ReBot_v3.Modules.Grabber
{
    internal static partial class Module_Bluesky
    {
        internal static JArray? BlueskyJSONHolder;

        internal static void Queue_Prepare(string WebAddress)
        {
            if (WebAddress.Contains("/post/"))
            {
                Queue_Single(WebAddress);
            }
            else
            {
                Queue_Multi(WebAddress);
            }
        }

        [GeneratedRegex(@"(?<=post/)\w+(?=/)?")]
        private static partial Regex Bluesky_Regex();
        private static void Queue_Single(string WebAddress)
        {
            if (Module_Grabber._GrabQueue_URLs.Contains(WebAddress))
            {
                Module_Grabber.Report_Info($"Skipped grabbing - Already in queue [@{WebAddress}]");
                return;
            }
            else
            {
                lock (Module_Grabber._GrabQueue_URLs)
                {
                    Module_Grabber._GrabQueue_URLs.Add(WebAddress);
                }
                //Isn't guaranteed to be first token, have to search for it.
                string PostID = Bluesky_Regex().Match(WebAddress).Value;
                //needed token might not be first
                JToken PostJToken = BlueskyJSONHolder.FirstOrDefault(token => ((string)token["uri"]).Contains($".post/{PostID}"));

                if (PostJToken == null)
                {
                    Module_Grabber.Report_Info($"Skipped grabbing - Post was not found inside the Bluesky JSON [@{WebAddress}]");
                    return;
                }

                Window_Main._RefHolder.Dispatcher.Invoke(() => { Module_Grabber.TreeView_GetParentItem(WebAddress, WebAddress, PostJToken.ToString(), true); });
            }
        }

        private static void Queue_Multi(string WebAddress)
        {
            if (BlueskyJSONHolder == null || !BlueskyJSONHolder.Any())
            {
                Module_Grabber.Report_Info($"Skipped grabbing - No Media found [@{WebAddress}]");
                return;
            }

            ushort SkipCounter = 0;
            Dictionary<string, string> Posts2Grab = new Dictionary<string, string>();
            foreach (JToken JTokenTemp in BlueskyJSONHolder)
            {
                string ArtistHandle = (string)JTokenTemp["author"]["handle"];
                string PostID = (string)JTokenTemp["uri"];
                PostID = PostID.Substring(PostID.LastIndexOf('/') + 1);
                string URL2Post = $"https://bsky.app/profile/{ArtistHandle}/post/{PostID}";

                if (Module_Grabber._GrabQueue_URLs.Contains(URL2Post))
                {
                    SkipCounter++;
                    continue;
                }
                else
                {
                    lock (Module_Grabber._GrabQueue_URLs)
                    {
                        Module_Grabber._GrabQueue_URLs.Add(URL2Post);
                    }
                    Posts2Grab.Add(URL2Post, JTokenTemp.ToString());
                }
            }
            if (SkipCounter > 0)
            {
                Module_Grabber.Report_Info($"Skipped grabbing {SkipCounter} media containers that were already in queue [@{WebAddress}]");
            }
            if (Posts2Grab.Any())
            {
                TreeViewItem? TreeViewItemParent = Window_Main._RefHolder.Dispatcher.Invoke(() => { return Module_Grabber.TreeView_GetParentItem(WebAddress, WebAddress); });
                foreach (string URL2Post in Posts2Grab.Keys)
                {
                    Window_Main._RefHolder.Dispatcher.BeginInvoke(() => { Module_Grabber.TreeView_MakeChildItem(TreeViewItemParent, URL2Post, URL2Post, Posts2Grab[URL2Post]); });
                }
            }
        }

        internal static async Task Grab(string WebAddress, string JSONSource)
        {
            if (string.IsNullOrEmpty(JSONSource))
            {
                Module_Grabber.Report_Info($"Error encountered in Module_Bluesky.Grab [@{WebAddress}]");
                return;
            }

            JObject BlueskyJSON = JObject.Parse(JSONSource);

            string Post_URL = WebAddress;

            DateTime Post_DateTime = DateTime.ParseExact((string)BlueskyJSON["record"]["createdAt"], "MM/dd/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

            string ArtistName = (string)BlueskyJSON["author"]["handle"];

            string Post_Text = (string)BlueskyJSON["record"]["text"];

            string? Post_MediaURL;
            string? Post_ThumbnailURL;
            List<MediaItem> MediaItemList = new List<MediaItem>();
            ushort SkipCounter = 0;

            //blob https://bsky.social/xrpc/com.atproto.sync.getBlob?did={did}&cid={cid}
            //Select media.type first, then embed.type if media.type doesn't exist
            string embedType = BlueskyJSON.SelectToken("record.embed.media.$type")?.ToString() ?? BlueskyJSON.SelectToken("record.embed.$type")?.ToString();
            if (embedType.Contains("video"))
            {
                //https://video.bsky.app/watch/{did}/{cid}/playlist.m3u8
                //https://video.bsky.app/watch/{did}/{cid}/thumbnail.jpg
                string did = (string)BlueskyJSON["author"]["did"];
                string cid = BlueskyJSON.SelectToken("record..video.ref.$link").ToString();
                Post_MediaURL = $"https://bsky.social/xrpc/com.atproto.sync.getBlob?did={did}&cid={cid}";

                if (Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
                {
                    Post_ThumbnailURL = BlueskyJSON.SelectToken("embed..thumbnail").ToString();
                    string VideoFormat = BlueskyJSON.SelectToken("record..video.mimeType").ToString();
                    VideoFormat = VideoFormat.Substring(VideoFormat.IndexOf('/') + 1);

                    MediaItem MediaItemTemp = new MediaItem
                    {
                        Grab_PageURL = Post_URL,
                        Grab_MediaURL = Post_MediaURL,
                        Grab_ThumbnailURL = Post_ThumbnailURL,
                        Grab_DateTime = Post_DateTime,
                        Grab_Artist = ArtistName,
                        Grab_Title = $"Post by @{ArtistName}",
                        Grab_TextBody = Post_Text,
                        Grid_MediaFormat = VideoFormat,
                        Grid_MediaWidth = (uint)BlueskyJSON.SelectToken("record.embed..aspectRatio.width"),
                        Grid_MediaHeight = (uint)BlueskyJSON.SelectToken("record.embed..aspectRatio.height"),
                        Grid_MediaByteLength = (uint)BlueskyJSON.SelectToken("record..video.size"),
                        Grid_ThumbnailFullInfo = true,
                        UP_Tags = Post_DateTime.Year.ToString(),
                        UP_IsWhitelisted = true
                    };
                    Module_Uploader.Media2BigCheck(MediaItemTemp);
                    MediaItemList.Add(MediaItemTemp);
                }
            }
            else //image(s)
            {
                //sometimes it's record.embed.images, other times it's record.embed.media.images
                foreach (JToken MediaNode in BlueskyJSON.SelectToken("record.embed..images"))
                {
                    //https://cdn.bsky.app/img/feed_fullsize/plain/{did}/{cid}
                    //https://cdn.bsky.app/img/feed_thumbnail/plain/{did}/{cid}
                    string did = (string)BlueskyJSON["author"]["did"];
                    string cid = (string)MediaNode["image"]["ref"]["$link"];
                    Post_MediaURL = $"https://bsky.social/xrpc/com.atproto.sync.getBlob?did={did}&cid={cid}";

                    if (!Module_Grabber.CheckShouldGrabConditions(Post_MediaURL))
                    {
                        SkipCounter += 1;
                        continue;
                    }

                    Post_ThumbnailURL = $"https://cdn.bsky.app/img/feed_thumbnail/plain/{did}/{cid}";
                    string ImageFormat = (string)MediaNode["image"]["mimeType"];
                    ImageFormat = ImageFormat.Substring(ImageFormat.IndexOf('/') + 1);

                    MediaItem MediaItemTemp = new MediaItem
                    {
                        Grab_PageURL = Post_URL,
                        Grab_MediaURL = Post_MediaURL,
                        Grab_ThumbnailURL = Post_ThumbnailURL,
                        Grab_DateTime = Post_DateTime,
                        Grab_Artist = ArtistName,
                        Grab_Title = $"Post by @{ArtistName}",
                        Grab_TextBody = Post_Text,
                        Grid_MediaFormat = ImageFormat,
                        Grid_MediaWidth = (uint)MediaNode["aspectRatio"]["width"],
                        Grid_MediaHeight = (uint)MediaNode["aspectRatio"]["height"],
                        Grid_MediaByteLength = (uint)MediaNode["image"]["size"],
                        Grid_ThumbnailFullInfo = true,
                        UP_Tags = Post_DateTime.Year.ToString(),
                        UP_IsWhitelisted = true //is giving errors so turn it into byte upload
                    };
                    Module_Uploader.Media2BigCheck(MediaItemTemp);
                    MediaItemList.Add(MediaItemTemp);
                    //Thread.Sleep(Module_Grabber.PauseBetweenImages); //Not doing any requests
                }
            }

            // - - - - - - - - - - - - - - - -

            if (MediaItemList.Count == 0)
            {
                lock (Module_Grabber._GrabQueue_WorkingOn)
                {
                    Module_Grabber._GrabQueue_WorkingOn.Remove(Post_URL);
                }
                Module_Grabber.Report_Info($"Grabbing skipped - {(SkipCounter > 1 ? "All m" : "M")}edia already grabbed or ignored [@{Post_URL}]");
                return;
            }

            lock (Module_Grabber._GrabQueue_WorkingOn)
            {
                Module_Grabber._GrabQueue_WorkingOn[Post_URL] = MediaItemList.Count == 1 ? MediaItemList.First() : MediaItemList;
            }
            string PrintText = $"Finished grabbing: {Post_URL}";
            if (SkipCounter > 0)
            {
                PrintText += $", {SkipCounter} media container{(SkipCounter > 1 ? "s have" : " has")} been skipped";
            }
            Module_Grabber.Report_Info(PrintText);
        }

        private static readonly HashSet<string> AllowedTypes = new HashSet<string>() { "app.bsky.embed.images", "app.bsky.embed.video" };
        internal static bool VerifyJSONValid(string WebAddress, string JSONPassed)
        {
            JObject JObjectTemp = JObject.Parse(JSONPassed);

            //Only check for entries that contain media
            IEnumerable<JToken>? PostsContainer = null;
            if (WebAddress.Contains("/xrpc/app.bsky."))
            {
                PostsContainer = JObjectTemp.SelectTokens("$.feed[*].post").Where(token => AllowedTypes.Contains(token["record"]?["embed"]?["$type"]?.ToString() ?? string.Empty)); //multi posts page
                if (PostsContainer == null || !PostsContainer.Any())
                {
                    //single post page
                    JToken? FirstPost = JObjectTemp["thread"][0]["value"]["post"];
                    string? EmbedType = FirstPost.SelectToken("record.embed.media.$type")?.ToString() ?? FirstPost.SelectToken("record.embed.$type")?.ToString();
                    PostsContainer = AllowedTypes.Contains(EmbedType ?? string.Empty) ? new JToken[] { FirstPost } : null;
                }
            }

            // Exit if no valid posts
            if (PostsContainer == null || !PostsContainer.Any()) return false;

            if (BlueskyJSONHolder == null)
            {
                BlueskyJSONHolder = new JArray(PostsContainer);
                return true;
            }

            lock (BlueskyJSONHolder)
            {
                HashSet<string?> ExistingIDs = BlueskyJSONHolder.Select(post => (string)post["uri"]).ToHashSet();

                foreach (JToken post in PostsContainer)
                {
                    string PostID = (string)post["uri"];
                    //Try to add to existing ids, if it fails it means it's duplicate
                    if (ExistingIDs.Add(PostID)) BlueskyJSONHolder.Add(post);
                }
            }
            return true;
        }
    }
}