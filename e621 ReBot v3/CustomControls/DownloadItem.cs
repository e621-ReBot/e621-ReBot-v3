namespace e621_ReBot_v3.CustomControls
{
    internal class DownloadItem
    {
        // = = = Grab stuff

        internal string? Grab_PageURL;
        internal string? Grab_MediaURL;
        internal string? Grab_ThumbnailURL;
        internal string? Grab_Artist;
        internal string? Grab_Title;
        internal string? Grab_MediaFormat;

        // = = = e6 stuff

        internal string? e6_PostID;
        internal string? e6_PoolName;
        internal string? e6_PoolPostIndex;
        internal string? e6_Tags;
        internal bool Is_e6Download = false;

        // = = = Grid stiff

        internal MediaItem? MediaItemRef;
    }
}