using CefSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

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
            string BaseURL = $"{new Uri(WebAdress).Scheme}://{new Uri(WebAdress).Host}";
            WhichCookie = FindCookie(BaseURL).Result;
        }

        private static async Task<CookieContainer> FindCookie(string BaseURL)
        {
            CookieContainer ReturnCookieContainer = new CookieContainer();
            if (Cef.GetGlobalCookieManager() != null) //Is null if grid session is loaded but browser isn't used
            {
                List<CefSharp.Cookie> CefCookies = await Cef.GetGlobalCookieManager().VisitUrlCookiesAsync(BaseURL, true);

                if (CefCookies != null)
                {
                    foreach (CefSharp.Cookie CookieHolder in CefCookies)
                    {
                        System.Net.Cookie TempCookie = new System.Net.Cookie()
                        {
                            Domain = CookieHolder.Domain,
                            Expires = CookieHolder.Expires == null ? DateTime.Now.AddMonths(1) : (DateTime)CookieHolder.Expires,
                            Name = CookieHolder.Name,
                            Value = CookieHolder.Value
                        };
                        ReturnCookieContainer.Add(TempCookie);
                    }
                }
            }

            return ReturnCookieContainer;
        }

        internal static bool PixivCookieCheck()
        {
            if (Cookies_Pixiv == null || Cookies_Pixiv.Count == 0) //Also try and get them
            {
                GetCookies("https://www.pixiv.net", ref Cookies_Pixiv);
            }
            if (Cookies_Pixiv.Count == 0) //There are still no cookies
            {
                return false;
            }

            return true;
        }
    }
}