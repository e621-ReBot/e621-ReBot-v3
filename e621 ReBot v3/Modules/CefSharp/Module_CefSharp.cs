using System;
using System.Drawing;
using System.Net;
using System.Web;
using System.Windows;
using CefSharp;
using CefSharp.Wpf;
using e621_ReBot_v3.CustomControls;
using e621_ReBot_v3.Modules.Grabber;
using HtmlAgilityPack;

namespace e621_ReBot_v3.Modules
{
    static internal class Module_CefSharp
    {
        internal static ChromiumWebBrowser? CefSharpBrowser;

        private static readonly BrowserControl _browserControl = BrowserControl._RefHolder;

        internal static void InitializeBrowser()
        {
            CefSettings CefSharp_Settings = new CefSettings
            {
                CachePath = $"{AppDomain.CurrentDomain.BaseDirectory}\\CefSharp Cache",
                PersistSessionCookies = true,
                BackgroundColor = (uint)ColorTranslator.ToWin32(Color.DimGray),
                LogSeverity = LogSeverity.Disable,
            };
            CefSharpSettings.ShutdownOnExit = true;

            //if (Properties.Settings.Default.DisableGPU) CefSharp_Settings.CefCommandLineArgs.Add("disable-gpu", "1");

            CefSharp_Settings.RegisterScheme(new CefCustomScheme
            {
                SchemeName = MediaBrowser_SchemeHandlerFactory.SchemeName,
                SchemeHandlerFactory = new MediaBrowser_SchemeHandlerFactory()
            });

            Cef.Initialize(CefSharp_Settings);
            //Cef.EnableHighDPISupport();
            CefSharpBrowser = new ChromiumWebBrowser()
            {
                DisplayHandler = new CefSharp_DisplayHandler(),
                RequestHandler = new CefSharp_RequestHandler(),
                LifeSpanHandler = new CefSharp_LifeSpanHandler(),
            };
            CefSharpBrowser.IsBrowserInitializedChanged += CefSharpBrowser_IsBrowserInitializedChanged;
        }

        private static void CefSharpBrowser_IsBrowserInitializedChanged(object? sender, DependencyPropertyChangedEventArgs e)
        {
            if (AppSettings.FirstRun)
            {
                LoadURL("https://e621.net/session/new");
            }
            else
            {
                if ((bool)e.NewValue) CefSharpBrowser.LoadHtml($"<html><body style=\"background-color:DimGray;\"><h1>\n\nCefSharpVersion {Cef.ChromiumVersion}<br>\n\nCefVersion {Cef.CefVersion}<br>\n\nCefSharpVersion {Cef.CefSharpVersion}</h1></body></html>");
            }
            CefSharpBrowser.LoadingStateChanged += CefSharpBrowser_FirstLoad;
        }

        private static void CefSharpBrowser_FirstLoad(object? sender, LoadingStateChangedEventArgs e)
        {
            if (!e.IsLoading)
            {
                CefSharpBrowser.LoadingStateChanged -= CefSharpBrowser_FirstLoad;
                CefSharpBrowser.AddressChanged += CefSharpBrowser_AddressChanged;
                CefSharpBrowser.TitleChanged += CefSharpBrowser_TitleChanged;
                CefSharpBrowser.LoadingStateChanged += CefSharpBrowser_LoadingStateChanged;
                //CefSharpBrowser.FrameLoadEnd += CefSharpBrowser_FrameLoadEnd;
                InvokeOnUIThread(() =>
                {
                    if (Window_Main._RefHolder != null) Window_Main._RefHolder.BrowserTabGrid.Children.Remove(Window_Main._RefHolder.BrowserLoadingIcon);
                    _browserControl.BrowserControls_Panel.Visibility = Visibility.Visible;
                    if (AppSettings.FirstRun)
                    {
                        _browserControl.BB_Reload.IsEnabled = true;
                        _browserControl.Url_TextBox.Text = CefSharpBrowser.Address;
                        Window_Main._RefHolder.Dispatcher.BeginInvoke(() => { MessageBox.Show(Window_Main._RefHolder, "Thanks for trying me out.\n\nFor a start, you should log in into e621 and provide me with API key so I could do the tasks you will require.\n\nI opened the login page for you.", "e621 ReBot Tutorial", MessageBoxButton.OK, MessageBoxImage.Information); });
                    }
                    else
                    {
                        _browserControl.BrowserQuickButtons.IsEnabled = true;
                        _browserControl.Url_TextBox.Text = "about:blank";
                    }
                });
            }
        }

        private static void InvokeOnUIThread(Action action)
        {
            if (Window_Main._RefHolder.CheckAccess())
            {
                Window_Main._RefHolder.Dispatcher.BeginInvoke(action);
            }
            else
            {
                Window_Main._RefHolder.Dispatcher.Invoke(action);
            }
        }

        internal static void LoadURL(string urlString)
        {
            if (string.IsNullOrEmpty(urlString) || CefSharpBrowser == null) return;
            if (urlString.Equals("about:blank"))
            {
                CefSharpBrowser.LoadUrl(urlString);
                return;
            }

            if (urlString.Contains('.'))
            {
                Uri url;
                bool success = Uri.TryCreate(urlString, UriKind.RelativeOrAbsolute, out url);
                if (success)
                {
                    if (url.IsAbsoluteUri)
                    {
                        CefSharpBrowser.LoadUrl(urlString);
                        return;
                    }

                    UriHostNameType hostNameType = Uri.CheckHostName(urlString);
                    if (hostNameType == UriHostNameType.IPv4 || hostNameType == UriHostNameType.IPv6)
                    {
                        CefSharpBrowser.LoadUrl(urlString);
                        return;
                    }

                    if (hostNameType == UriHostNameType.Dns)
                    {
                        try
                        {
                            IPHostEntry hostEntry = Dns.GetHostEntry(urlString);
                            if (hostEntry.AddressList.Length > 0)
                            {
                                CefSharpBrowser.LoadUrl(urlString);
                                return;
                            }
                        }
                        catch (Exception)
                        {
                            //Invoke(new Action(() => { label_NavError.Text = ex1.Message; }));
                        }
                    }
                }
            }

            string searchUrl = $"https://www.google.com/search?q={Uri.EscapeDataString(urlString)}";
            CefSharpBrowser.LoadUrl(searchUrl);
        }

        private static void CefSharpBrowser_AddressChanged(object? sender, DependencyPropertyChangedEventArgs e)
        {
            BrowserAddress = e.NewValue.ToString();
            Module_Twitter.TwitterJSONHolder = null;
            Module_Mastodons.MastodonsJSONHolder = null;
            InvokeOnUIThread(() =>
            {
                _browserControl.Url_TextBox.Text = HttpUtility.UrlDecode(BrowserAddress);
                _browserControl.BB_Grab.Visibility = Visibility.Collapsed;
                _browserControl.BB_GrabAll.Visibility = Visibility.Collapsed;
                _browserControl.BB_Download.Visibility = Visibility.Collapsed;
                _browserControl.BB_PoolWatcher.Visibility = Visibility.Collapsed;
                _browserControl.BrowserQuickButtons.IsEnabled = false;
            });
        }

        private static void CefSharpBrowser_TitleChanged(object? sender, DependencyPropertyChangedEventArgs e)
        {
            CefSharpBrowser.Title = e.NewValue.ToString();
        }

        internal static string? BrowserAddress;
        internal static string? BrowserHTMLSource;
        private static void CefSharpBrowser_LoadingStateChanged(object? sender, LoadingStateChangedEventArgs e)
        {
            InvokeOnUIThread(() =>
            {
                _browserControl.BB_Back.IsEnabled = e.CanGoBack;
                _browserControl.BB_Forward.IsEnabled = e.CanGoForward;
                if (BrowserAddress != null)
                {
                    _browserControl.BB_Reload.IsEnabled = !BrowserAddress.Equals("about:blank");
                }

                //if (e.IsLoading)
                //{
                //_browserControl.BB_Grab.Visibility= Visibility.Collapsed;
                //_browserControl.BB_GrabAll.Visibility = Visibility.Collapsed;
                //_browserControl.BB_Download.Visibility = Visibility.Collapsed;
                //_browserControl.BrowserQuickButtons.IsEnabled = false;
                //}
            });

            if (e.IsLoading)
            {
                BrowserHTMLSource = null;
            }
            else
            {
                e.Browser.GetSourceAsync().ContinueWith(taskHtml =>
                {
                    BrowserHTMLSource = taskHtml.Result;
                    InvokeOnUIThread(() => { BrowserPageLoadedActions(); });
                });

            }
        }

        internal static bool FrameLoadEndEnabler = false;
        private static void CefSharpBrowser_FrameLoadEnd(object? sender, FrameLoadEndEventArgs e)
        {
            if (FrameLoadEndEnabler)
            {
                if (BrowserAddress.StartsWith("https://www.pixiv.net/"))//|| e.Url.StartsWith("https://www.pixiv.net/") && e.Frame.Name.Equals("footer"))
                {
                    FrameLoadEndEnabler = false;
                    Module_Grabber.GrabEnabler(BrowserAddress);
                }
            }
        }

        private static void BrowserPageLoadedActions()
        {
            string CefAdress = HttpUtility.UrlDecode(BrowserAddress);

            if (AppSettings.FirstRun)
            {
                switch (CefAdress)
                {
                    case "https://e621.net/posts":
                        {
                            LoadURL("https://e621.net/users/home");
                            break;
                        }

                    case "https://e621.net/users/home":
                        {
                            MessageBox.Show(Window_Main._RefHolder, "I will grab some needed data from this page, then I'm going to point you to the next step.", "e621 ReBot Tutorial", MessageBoxButton.OK, MessageBoxImage.Information);

                            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
                            HtmlDocumentTemp.LoadHtml(BrowserHTMLSource);

                            string TextNode = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//head/meta[@name='current-user-name']").Attributes["content"].Value;
                            AppSettings.UserName = TextNode;
                            TextNode = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//head/meta[@name='current-user-id']").Attributes["content"].Value;
                            AppSettings.UserID = TextNode;

                            LoadURL($"https://e621.net/users/{TextNode}/api_key");
                            break;
                        }

                    case string _3 when _3.StartsWith("https://e621.net/users/", StringComparison.OrdinalIgnoreCase) && _3.EndsWith("/api_key/view", StringComparison.OrdinalIgnoreCase):
                        {
                            MessageBox.Show(Window_Main._RefHolder, "Generate you API key and copy it into the floating box.", "e621 ReBot Tutorial", MessageBoxButton.OK, MessageBoxImage.Information);
                            if (Window_APIKey._RefHolder == null) new Window_APIKey().Show();
                            break;
                        }
                }
                return;
            }

            //if (CefAdress.Contains("mastodon.social/@"))
            //{
            //    CefSharpBrowser.ExecuteScriptAsync("document.querySelectorAll(\"button[class='status__content__spoiler-link']\").forEach(button=>button.click())");
            //    CefSharpBrowser.ExecuteScriptAsync("document.querySelectorAll(\"button[class='spoiler-button__overlay']\").forEach(button=>button.click())");
            //}

            //Module_Grabber.GrabEnabler(CefAdress);
            //Module_Downloader.DownloadEnabler(CefAdress);
        }
    }
}