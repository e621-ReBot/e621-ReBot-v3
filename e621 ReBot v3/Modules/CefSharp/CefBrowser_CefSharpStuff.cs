using CefSharp.Enums;
using CefSharp.Handler;
using CefSharp.Structs;
using e621_ReBot_v3;
using e621_ReBot_v3.CustomControls;
using e621_ReBot_v3.Modules;
using e621_ReBot_v3.Modules.Downloader;
using e621_ReBot_v3.Modules.Grabber;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;

namespace CefSharp
{
    internal class CefSharp_LifeSpanHandler : ILifeSpanHandler
    {
        public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            return false;
        }

        public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            //nothing
        }

        public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            //nothing
        }

        public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser? newBrowser)
        {
            newBrowser = null;
            MessageBoxResult MessageBoxResultTemp = Window_Main._RefHolder.Dispatcher.Invoke(() => { return MessageBox.Show(Window_Main._RefHolder, "Prevented new window from opening, do you want to navigate to it instead?", "e621 ReBot", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes); });
            if (MessageBoxResultTemp == MessageBoxResult.Yes)
            {
                chromiumWebBrowser.LoadUrl(targetUrl);
            }
            return true;
        }
    }

    internal class CefSharp_RequestHandler : RequestHandler
    {
        private readonly List<string> AdBlock;

        public CefSharp_RequestHandler()
        {
            AdBlock = new List<string>
            {
                // e6
                "ads.dragonfru.it",

                // FA
                "rv.furaffinity.net",
                "amazon-adsystem.com",
                "googlesyndication.com",
                "intergient.com",
                "smartadserver.com",
                "adnxs.com",
                "a-mo.net",
                "bidswitch.net",

                // Pixiv
                "imp.pixiv.net",
                "pixon.ads",
                "doubleclick.net",
                "onesignal.com",

                //HA
                "doubleclick.net",


                //Hentai Foundry
                "subless.com",

                //Analytics
                "google-analytics.com",
                "analytics.x.com"
            };

            // - - - - - - - - - - - - - - - -

            ButtonEnableCheck = new List<Regex>
            {
                new Regex(@"^\w+://e621\.net/(posts|pools|favorites|popular)"),
                new Regex(@"^\w+://www\.furaffinity\.net/(view|full|gallery|scraps|favorites|search)/"),
                new Regex(@"^\w+://inkbunny\.net/(s|gallery|scraps|submissionsviewall)"),
                //new Regex(@"^\w+://www.pixiv.net/(\w+/artworks/\d+|ajax/user/\d+/profile/(illusts|top|all))"),
                //new Regex(@"^\w+://s.pximg.net/www/js/build/spa.\w+.js"),
                new Regex(@"^\w+://www\.recaptcha\.net/recaptcha/enterprise/reload\?k="), //Pixiv
                new Regex(@"^\w+://www\.hiccears\.com/(contents|file|p)"),
                new Regex(@"^\w+://x\.com/i/api/graphql/.+/(UserTweets|UserMedia|TweetDetail)\?variables="),
                new Regex(@"^\w+://api\.x\.com/graphql/.+/TweetResultByRestId\?variables="), //when not logged in
                new Regex(@"^\w+://\w+\.newgrounds\.com/(movies|portal|art)"),
                new Regex(@"^\w+://\w+\.sofurry\.com/(view|artwork|browse)"),
                new Regex(@"^\w+://www\.weasyl\.com/((~.+/)?submissions|search)"),
                new Regex(@"^\w+://\w+\.\w+/api/v1/(accounts/\d+/statuses|statuses/\d+)"), //Mastodons
                new Regex(@"^\w+://pawoo\.net/@.+/(\d+|media)"),
                new Regex(@"^\w+://www\.hentai-foundry\.com/(pictures|user)"),
                new Regex(@"^\w+://www\.plurk\.com/(p/|TimeLine/|(?!portal|login|signup|search)\w+)"),

                //- - - Download only

                new Regex(@"^\w+://derpibooru\.org/(images|search\?|galleries/)"),
                new Regex(@"^\w+://itaku.ee/api/(galleries/images|posts)/(\d+/$)?") //new Regex(@".+itaku.ee/images/\d+")
            };
        }

        protected override bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect)
        {
            if (AdBlock.Any(s => request.Url.Contains(s)))
            {
                return true;
            }

#if DEBUG
            Debug.WriteLine($"OnBeforeBrowse: {request.Url}");
#endif

            return false;
        }

        private readonly List<Regex> ButtonEnableCheck;
        protected override IResourceRequestHandler? GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
        {
            //if (request.Headers["authorization"] != null && request.Method.Equals("GET") && request.Url.StartsWith("https://api.x.com/graphql/", StringComparison.OrdinalIgnoreCase) && Regex.Match(request.Url, @"^\w+://api.twitter.com/graphql/.+/(UserTweets|UserMedia|TweetDetail)\?variables=").Success)
            //{
            //    Module_Twitter.TwitterAuthorizationHolder = request.Headers["authorization"];
            //    return new CefSharp_ResourceRequestHandler();
            //}

            if (ButtonEnableCheck.Any(regex => regex.IsMatch(request.Url)))
            {
                return new CefSharp_ResourceRequestHandler();
            }

            //if (chromiumWebBrowser.Address.Contains("https://www.deviantart.com/") && request.Url.Contains("https://www.deviantart.com/_napi/da-user-profile/api/gallery/contents") && request.Url.Contains("&folderid="))
            //{
            //    Module_DeviantArt.FolderID = request.Url.Substring(request.Url.IndexOf("&folderid=") + 10);
            //    return null;
            //}

            if (AdBlock.Any(s => request.Url.Contains(s)))
            {
                disableDefaultHandling = true;
                return new CefSharp_ResourceRequestHandler_AdBlocker();
            }

#if DEBUG
            Debug.WriteLine($"GetResourceRequestHandler: {request.Url}");
#endif

            return null;
        }

    }

    internal class CefSharp_ResourceRequestHandler : ResourceRequestHandler
    {
        private readonly MemoryStream MemoryStreamHolder = new MemoryStream();
        protected override IResponseFilter GetResourceResponseFilter(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response)
        {
            return new ResponseFilter.StreamResponseFilter(MemoryStreamHolder);
        }

        protected override void OnResourceLoadComplete(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response, UrlRequestStatus status, long receivedContentLength)
        {
            switch (request.Url)
            {
                case string e621 when e621.StartsWith("https://e621.net/"):
                    {
                        if (response.MimeType.Equals("text/html"))
                        {
                            Module_CefSharp.BrowserHTMLSource = Encoding.UTF8.GetString(MemoryStreamHolder.ToArray());
                            Module_Downloader.DownloadEnabler(request.Url);
                            Window_PoolWatcher.PoolWatcherEnabler(request.Url);
                        }
                        break;
                    }

                case string FurAffinity when FurAffinity.StartsWith("https://www.furaffinity.net/"):
                case string Inkbunny when Inkbunny.StartsWith("https://inkbunny.net/"):
                case string HicceArs when HicceArs.StartsWith("https://www.hiccears.com/"):
                case string SoFurry when SoFurry.Contains(".sofurry.com/"):
                case string Weasyl when Weasyl.Contains(".weasyl.com/"):
                case string Pawoo when Pawoo.StartsWith("https://pawoo.net/@"):
                case string HentaiFoundry when HentaiFoundry.StartsWith("https://www.hentai-foundry.com/"):
                    {
                        if (response.MimeType.Equals("text/html"))
                        {
                            Module_CefSharp.BrowserHTMLSource = Encoding.UTF8.GetString(MemoryStreamHolder.ToArray());
                            Module_Grabber.GrabEnabler(request.Url);
                            Module_Downloader.DownloadEnabler(request.Url);
                        }
                        break;
                    }

                case string Pixiv when Pixiv.StartsWith("https://www.recaptcha.net/recaptcha/enterprise/reload?k"):
                    {
                        Module_Grabber.GrabEnabler(Module_CefSharp.BrowserAddress);
                        Module_Downloader.DownloadEnabler(Module_CefSharp.BrowserAddress);
                        break;
                    }

                case string Twitter when Twitter.Contains("x.com"):
                    {
                        if (response.MimeType.Equals("application/json"))
                        {
                            string Data2String = Encoding.UTF8.GetString(MemoryStreamHolder.ToArray());
                            if (Data2String.Length > 64)
                            {
                                JObject JObjectTemp = JObject.Parse(Data2String);

                                IEnumerable<JToken>? TweetsContainer = null;
                                if (Twitter.StartsWith("https://x.com/i/api/graphql/")) //logged in
                                {
                                    //[@something]makes it go two levels deep.
                                    TweetsContainer = JObjectTemp.SelectTokens("$..data..instructions[?(@.type=='TimelineAddToModule')].moduleItems[*]..tweet_results.result..legacy").Where(token => token["extended_entities"] != null); //media page
                                    if (!TweetsContainer.Any()) TweetsContainer = JObjectTemp.SelectTokens("$..data..instructions[?(@.type=='TimelineAddEntries')].entries[*]..tweet_results.result..legacy").Where(token => token["extended_entities"] != null); //status page
                                }
                                if (Twitter.StartsWith("https://api.x.com/graphql/")) //not logged in
                                {
                                    //TweetsContainer = JObjectTemp.SelectTokens("$..data..instructions[?(@.type=='TimelineAddEntries')].entries[*]..tweet_results.result.legacy").Where(token => token["extended_entities"] != null);
                                    TweetsContainer = JObjectTemp.SelectTokens("$..tweetResult.result.legacy").Where(token => token["extended_entities"] != null);
                                }
                                if (TweetsContainer.Any())
                                {
                                    bool SkipUnion = false;
                                    JArray TweetHolder = new JArray(TweetsContainer);
                                    if (Module_Twitter.TwitterJSONHolder == null)
                                    {
                                        Module_Twitter.TwitterJSONHolder = TweetHolder;
                                        SkipUnion = true;
                                    }

                                    lock (Module_Twitter.TwitterJSONHolder)
                                    {
                                        if (!SkipUnion) Module_Twitter.TwitterJSONHolder.Merge(TweetHolder, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union });

                                        //clear duplicates, happens rarely
                                        if (Module_Twitter.TwitterJSONHolder.Count > 1)
                                        {
                                            //Module_Twitter.TwitterJSONHolder.OrderBy(token => token["id_str"].Value<uint>());
                                            List<string> TweetIDList = new List<string>();
                                            for (int i = Module_Twitter.TwitterJSONHolder.Count - 1; i >= 0; i--)
                                            {
                                                string TweetID = Module_Twitter.TwitterJSONHolder[i]["id_str"].Value<string>();
                                                if (TweetIDList.Contains(TweetID))
                                                {
                                                    Module_Twitter.TwitterJSONHolder.RemoveAt(i);
                                                }
                                                else
                                                {
                                                    TweetIDList.Add(TweetID);
                                                }
                                            }
                                        }
                                    }
                                    Module_Grabber.GrabEnabler(Module_CefSharp.BrowserAddress);
                                }
                            }
                        }
                        break;
                    }

                case string Newgrounds when Newgrounds.Contains(".newgrounds.com/"):
                    {
                        if (response.MimeType.Equals("application/json"))
                        {
                            Module_CefSharp.CefSharpBrowser.Dispatcher.BeginInvoke(() =>
                            {
                                Module_CefSharp.CefSharpBrowser.GetSourceAsync().ContinueWith(taskHtml =>
                                {
                                    Module_CefSharp.BrowserHTMLSource = taskHtml.Result;
                                    Module_Grabber.GrabEnabler(Module_CefSharp.BrowserAddress);
                                });
                            });
                        }
                        else //html
                        {
                            Module_CefSharp.BrowserHTMLSource = Encoding.UTF8.GetString(MemoryStreamHolder.ToArray());
                            Module_Grabber.GrabEnabler(request.Url);
                        }
                        break;
                    }

                case string Mastodon when Mastodon.StartsWith("https://mastodon.social/api/"):
                case string Baraag when Baraag.StartsWith("https://baraag.net/api/"):
                    {
                        if (response.MimeType.Equals("application/json"))
                        {
                            string Data2String = Encoding.UTF8.GetString(MemoryStreamHolder.ToArray());
                            if (Data2String.Length > 64)
                            {
                                JToken JTokenTemp = JToken.Parse(Data2String);
                                //[@something]makes it go two levels deep.
                                IEnumerable<JToken> MastodonsContainer = Data2String.StartsWith("{") ? JTokenTemp : MastodonsContainer = JTokenTemp.SelectTokens("$[?(@.media_attachments[0] != null)]");
                                if (MastodonsContainer.Any())
                                {
                                    bool SkipUnion = false;
                                    JArray MastodonHolder = new JArray(MastodonsContainer);
                                    if (Module_Mastodons.MastodonsJSONHolder == null)
                                    {
                                        Module_Mastodons.MastodonsJSONHolder = MastodonHolder;
                                        SkipUnion = true;
                                    }

                                    lock (Module_Mastodons.MastodonsJSONHolder)
                                    {
                                        if (!SkipUnion) Module_Mastodons.MastodonsJSONHolder.Merge(MastodonHolder, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union });
                                    }
                                    Module_Grabber.GrabEnabler(Module_CefSharp.BrowserAddress);
                                }
                            }
                        }
                        break;
                    }

                case string Plurk when Plurk.StartsWith("https://www.plurk.com/"):
                    {
                        if (response.MimeType.Equals("text/html"))
                        {
                            Module_CefSharp.BrowserHTMLSource = Encoding.UTF8.GetString(MemoryStreamHolder.ToArray());
                            Module_Grabber.GrabEnabler(request.Url);
                        }
                        else //json
                        {
                            Module_CefSharp.CefSharpBrowser.Dispatcher.BeginInvoke(() =>
                            {
                                Module_CefSharp.CefSharpBrowser.GetSourceAsync().ContinueWith(taskHtml =>
                                {
                                    Module_CefSharp.BrowserHTMLSource = taskHtml.Result;
                                    Module_Grabber.GrabEnabler(Module_CefSharp.BrowserAddress);
                                });
                            });
                        }
                        break;
                    }

                //- - - Download only

                case string Derpibooru when Derpibooru.StartsWith("https://derpibooru.org/"):
                    {
                        if (response.MimeType.Equals("text/html"))
                        {
                            Module_CefSharp.BrowserHTMLSource = Encoding.UTF8.GetString(MemoryStreamHolder.ToArray());
                            Module_Downloader.DownloadEnabler(request.Url);
                        }
                        break;
                    }

                case string Itaku when Itaku.StartsWith("https://itaku.ee/"):
                    {
                        if (response.MimeType.Equals("application/json"))
                        {
                            string Data2String = Encoding.UTF8.GetString(MemoryStreamHolder.ToArray());

                            if (Itaku.EndsWith('/') && Itaku.Contains("/images/")) //single
                            {
                                Module_Itaku.ItakuSingleJSONHolder = JObject.Parse(Data2String);
                            }
                            else //gallery
                            {
                                JObject JObjectTemp = JObject.Parse(Data2String);
                                IEnumerable<JToken>? TempContainer = Itaku.Contains("/posts/") ? JObjectTemp.SelectTokens("$.gallery_images.[*]") : JObjectTemp.SelectTokens("$.results.[*]");
                                if (TempContainer.Any())
                                {
                                    bool SkipUnion = false;
                                    JArray ItakuHolder = new JArray(TempContainer);
                                    if (Module_Itaku.ItakuMultiJSONHolder == null)
                                    {
                                        Module_Itaku.ItakuMultiJSONHolder = ItakuHolder;
                                        SkipUnion = true;
                                    }

                                    lock (Module_Itaku.ItakuMultiJSONHolder)
                                    {
                                        if (!SkipUnion) Module_Itaku.ItakuMultiJSONHolder.Merge(ItakuHolder, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union });

                                        //clear duplicates if they happen
                                        if (Module_Itaku.ItakuMultiJSONHolder.Count > 1)
                                        {
                                            List<string> ItakuIDList = new List<string>();
                                            for (int i = Module_Itaku.ItakuMultiJSONHolder.Count - 1; i >= 0; i--)
                                            {
                                                string ItakuID = Module_Itaku.ItakuMultiJSONHolder[i]["id"].Value<string>();
                                                if (ItakuIDList.Contains(ItakuID))
                                                {
                                                    Module_Itaku.ItakuMultiJSONHolder.RemoveAt(i);
                                                }
                                                else
                                                {
                                                    ItakuIDList.Add(ItakuID);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            Module_Downloader.DownloadEnabler(Module_CefSharp.BrowserAddress);
                        }
                        break;
                    }
            }
        }
    }

    internal class CefSharp_ResourceRequestHandler_AdBlocker : ResourceRequestHandler
    {
        protected override CefReturnValue OnBeforeResourceLoad(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
        {
            return CefReturnValue.Cancel;
        }
    }

    internal class CefSharp_DisplayHandler : IDisplayHandler
    {
        internal CefSharp_DisplayHandler()
        {
            ProgressTimer.Tick += (sender, args) =>
            {
                ProgressTimer.Stop();
                BrowserControl._RefHolder.PageProgressBar.Value = 0;
            };
        }

        public void OnAddressChanged(IWebBrowser chromiumWebBrowser, AddressChangedEventArgs addressChangedArgs)
        {
            //Nothing.
        }

        public bool OnAutoResize(IWebBrowser chromiumWebBrowser, IBrowser browser, Structs.Size newSize)
        {
            return false;
        }

        public bool OnConsoleMessage(IWebBrowser chromiumWebBrowser, ConsoleMessageEventArgs consoleMessageArgs)
        {
            return false;
        }

        public bool OnCursorChange(IWebBrowser chromiumWebBrowser, IBrowser browser, nint cursor, CursorType type, CursorInfo customCursorInfo)
        {
            return false;
        }

        public void OnFaviconUrlChange(IWebBrowser chromiumWebBrowser, IBrowser browser, IList<string> urls)
        {
            //Nothing.
        }

        public void OnFullscreenModeChange(IWebBrowser chromiumWebBrowser, IBrowser browser, bool fullscreen)
        {
            //Nothing.
        }

        DispatcherTimer ProgressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        public void OnLoadingProgressChange(IWebBrowser chromiumWebBrowser, IBrowser browser, double progress)
        {
            BrowserControl._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                if (BrowserControl._RefHolder.PageProgressBar.Value == 1)
                {
                    ProgressTimer.Start();
                }
                else
                {
                    BrowserControl._RefHolder.PageProgressBar.Value = progress;
                }
            });
        }

        public void OnStatusMessage(IWebBrowser chromiumWebBrowser, StatusMessageEventArgs statusMessageArgs)
        {
            //Nothing.
        }

        public void OnTitleChanged(IWebBrowser chromiumWebBrowser, TitleChangedEventArgs titleChangedArgs)
        {
            //Nothing.
        }

        public bool OnTooltipChanged(IWebBrowser chromiumWebBrowser, ref string text)
        {
            return false;
        }
    }
}