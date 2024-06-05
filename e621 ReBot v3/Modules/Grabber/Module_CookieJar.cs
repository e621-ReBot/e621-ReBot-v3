using System;
using System.Collections.Generic;
using System.Net;
using CefSharp;

namespace e621_ReBot_v3.Modules
{
    internal static class Module_CookieJar
    {
        internal static CookieContainer? Cookies_FurAffinity;
        internal static CookieContainer? Cookies_Inkbunny;
        internal static CookieContainer? Cookies_Pixiv;
        internal static CookieContainer? Cookies_HicceArs;
        internal static CookieContainer? Cookies_Twitter;
        internal static CookieContainer? Cookies_Newgrounds;
        internal static CookieContainer? Cookies_SoFurry;
        internal static CookieContainer? Cookies_Weasyl;
        internal static CookieContainer? Cookies_Mastodon;
        internal static CookieContainer? Cookies_Baraag;
        internal static CookieContainer? Cookies_Pawoo;
        internal static CookieContainer? Cookies_HentaiFoundry;
        internal static CookieContainer? Cookies_Plurk;

        internal static void GetCookies(string WebAdress, ref CookieContainer? WhichCookie)
        {
            if (WhichCookie == null) WhichCookie = new CookieContainer();

            string BaseURL = $"{new Uri(WebAdress).Scheme}://{new Uri(WebAdress).Host}";
            List<CefSharp.Cookie> CookieList = Cef.GetGlobalCookieManager().VisitUrlCookiesAsync(BaseURL, true).Result;
            foreach (CefSharp.Cookie CookieHolder in CookieList)
            {
                System.Net.Cookie TempCookie = new System.Net.Cookie()
                {
                    Domain = CookieHolder.Domain,
                    Expires = CookieHolder.Expires == null ? DateTime.Now.AddMonths(1) : (DateTime)CookieHolder.Expires,
                    Name = CookieHolder.Name,
                    Value = CookieHolder.Value
                };
                WhichCookie.Add(TempCookie);
            }
        }
    }
}