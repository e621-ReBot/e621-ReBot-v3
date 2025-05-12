using System;
using System.Collections.Generic;
using System.Windows.Media;
using Newtonsoft.Json;

namespace e621_ReBot_v3.CustomControls
{
    public class MediaItem
    {
        // = = = Grab stuff

        public string? Grab_PageURL;
        public string? Grab_MediaURL;
        public string? Grab_ThumbnailURL;
        public DateTime? Grab_DateTime;
        public string? Grab_Artist;
        public string? Grab_Title;
        public string? Grab_TextBody;

        // = = = Grid stuff

        [JsonIgnore] public ImageSource? Grid_Thumbnail;
        [JsonIgnore] public bool Grid_ThumbnailDLStart = false;
        public bool? Grid_ThumbnailFullInfo = false;
        public string? Grid_MediaFormat;
        public uint Grid_MediaWidth;
        public uint Grid_MediaHeight;
        public uint? Grid_MediaByteLength;
        public string? Grid_MediaMD5;
        public bool? Grid_MediaTooBig;

        // = = = Preview stuff

        public bool Preview_DontDelay = false;

        // = = = Upload stuff

        [JsonIgnore] public bool UP_Queued = false;
        public string? UP_Rating = "E";
        public string? UP_Tags;
        [JsonIgnore] public MediaItem? UP_ParentMediaItem;
        public string? UP_UploadedID;
        public string? UP_Inferior_ID;
        public string? UP_Inferior_Description;
        public List<string>? UP_Inferior_Sources;
        public string? UP_Inferior_ParentID;
        public List<string>? UP_Inferior_Children;
        public bool? UP_Inferior_HasNotes;
        public float UP_Inferior_NoteSizeRatio;
        public bool UP_IsWhitelisted = true;

        // = = = Download stuff

        [JsonIgnore] public bool DL_Queued = false;
        public string? DL_FilePath;
        public string? DL_ImageID;
    }
}