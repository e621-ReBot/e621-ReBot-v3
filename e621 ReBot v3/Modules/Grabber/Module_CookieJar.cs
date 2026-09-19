using System.Net;

namespace e621_ReBot_v3.Modules
{
    internal static class Module_CookieJar
    {
        internal static CookieContainer? Cookies_FurAffinity;
        internal static CookieContainer? Cookies_Inkbunny;
        internal static CookieContainer? Cookies_Pixiv;
        internal static CookieContainer? Cookies_HicceArs;
        //internal static CookieContainer? Cookies_Twitter;
        internal static CookieContainer? Cookies_Newgrounds;
        internal static CookieContainer? Cookies_SoFurry;
        internal static CookieContainer? Cookies_Weasyl;
        internal static CookieContainer? Cookies_Mastodon;
        internal static CookieContainer? Cookies_Baraag;
        internal static CookieContainer? Cookies_Pawoo;
        internal static CookieContainer? Cookies_HentaiFoundry;
        internal static CookieContainer? Cookies_Plurk;


        internal static bool PixivCookieCheck()
        {
            if (Module_CefSharp.CefSharpBrowser != null && Module_CefSharp.CefSharpBrowser.IsInitialized)
            {
                if (Cookies_Pixiv == null || Cookies_Pixiv.Count == 0) //Also try and get them
                {
                    //GetCookies("https://www.pixiv.net", ref Cookies_Pixiv);
                }
                if (Cookies_Pixiv.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}