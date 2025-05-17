﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Windows;
using CefSharp.Handler;
using e621_ReBot_v3;
using e621_ReBot_v3.Modules;

namespace CefSharp
{
    internal class MediaBrowser_SchemeHandlerFactory : ISchemeHandlerFactory
    {
        internal const string SchemeName = "https";

        IResourceHandler? ISchemeHandlerFactory.Create(IBrowser browser, IFrame frame, string schemeName, IRequest request)
        {
            ////pixiv js sometimes throws nullexception for browser stuff
            //Uri TestHost = new Uri(request.Url);
            //if (Form_Preview._FormReference != null && TestHost.Host.Equals("www.hiccears.com") && browser.Identifier > 1 && request.Url.EndsWith("/download", StringComparison.OrdinalIgnoreCase))
            //{
            //    return new MediaBrowser_ResourceHandler();
            //}
            return null;
        }
    }

    //https://github.com/cefsharp/CefSharp/blob/master/CefSharp.Example/CefSharpSchemeHandler.cs
    class MediaBrowser_ResourceHandler : ResourceHandler, IResourceHandler
    {
        public override CefReturnValue ProcessRequestAsync(IRequest request, ICallback callback)
        {
            using (callback)
            {
                HttpWebRequest HicceArsMediaRequest = (HttpWebRequest)WebRequest.Create(request.Url);
                HicceArsMediaRequest.CookieContainer = Module_CookieJar.Cookies_HicceArs;
                MemoryStream MemoryStreamTemp = new MemoryStream();
                using (HttpWebResponse HicceArsMediaRequestData = (HttpWebResponse)HicceArsMediaRequest.GetResponse())
                {
                    StatusCode = (int)HicceArsMediaRequestData.StatusCode;
                    if (HicceArsMediaRequestData.StatusCode == HttpStatusCode.OK)
                    {
                        HicceArsMediaRequestData.GetResponseStream().CopyTo(MemoryStreamTemp);
                        MemoryStreamTemp.Position = 0;
                        ResponseLength = MemoryStreamTemp.Length;
                        Stream = MemoryStreamTemp;
                        MimeType = HicceArsMediaRequestData.ContentType;
                        Headers.Add("content-disposition", HicceArsMediaRequestData.Headers["content-disposition"].Replace("attachment;", "inline;"));
                    }
                }
                //MemoryStreamTemp.Dispose();
                if (Stream == null)
                {
                    callback.Cancel();
                }
                else
                {
                    callback.Continue();
                }
            }
            return CefReturnValue.Continue;
        }

        void IResourceHandler.GetResponseHeaders(IResponse response, out long responseLength, out string? redirectUrl)
        {
            redirectUrl = null;
            responseLength = -1L;
            response.MimeType = MimeType;
            response.StatusCode = StatusCode;
            response.StatusText = StatusText;
            response.Headers = Headers;
            if (!string.IsNullOrEmpty(Charset))
            {
                response.Charset = Charset;
            }

            if (ResponseLength.HasValue)
            {
                responseLength = ResponseLength.Value;
            }

            if (Stream != null && Stream.CanSeek)
            {
                if (!ResponseLength.HasValue || responseLength == 0L)
                {
                    responseLength = Stream.Length;
                }

                Stream.Position = 0L;
            }
        }
    }

    class MediaBrowser_RequestHandler : RequestHandler
    {
        protected override IResourceRequestHandler? GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
        {
            string RequestUrlCut = request.Url;
            if (RequestUrlCut.Contains('?')) RequestUrlCut = RequestUrlCut.Substring(0, request.Url.IndexOf('?'));
            switch (RequestUrlCut)
            {
                case string VideoFormat1 when VideoFormat1.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase):
                case string VideoFormat2 when VideoFormat2.EndsWith(".swf", StringComparison.OrdinalIgnoreCase):
                    {
                        return null;
                    }
                default:
                    {
                        if (Module_Downloader.MediaBrowser_MediaCache.ContainsKey(Module_Downloader.MediaFile_GetFileNameOnly(request.Url)))
                        {
                            return null;
                        }
                        else
                        {
                            return new MediaBrowser_ResourceRequestHandler();
                        }
                    }
            }
        }
    }

    class MediaBrowser_ResourceRequestHandler : ResourceRequestHandler
    {
        protected override IResourceHandler? GetResourceHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request)
        {
            return null;
        }

        protected override CefReturnValue OnBeforeResourceLoad(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
        {
            if (request.Url.StartsWith("https://i.pximg.net/", StringComparison.OrdinalIgnoreCase))
            {
                request.SetReferrer("http://www.pixiv.net", ReferrerPolicy.Default);
            }
            return CefReturnValue.Continue;
        }

        protected override bool OnResourceResponse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response)
        {
            return false;
        }

        private Dictionary<ulong, MediaBrowser_ResponseFilter> responseDictionary = new Dictionary<ulong, MediaBrowser_ResponseFilter>();
        protected override void OnResourceLoadComplete(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, IResponse response, UrlRequestStatus status, long receivedContentLength)
        {
            if (status == UrlRequestStatus.Success && responseDictionary.TryGetValue(request.Identifier, out MediaBrowser_ResponseFilter ResponseFilterHolder))
            {
                byte[] ByteData = ResponseFilterHolder.ByteData;

                string FileExt = response.Headers["content-type"].Replace("image/", "").Replace("jpeg", "jpg");
                switch (FileExt)
                {
                    case "jpg":
                    case "png":
                    case "gif":
                    case "webp":
                        {
                            Window_Preview._RefHolder.MediaItemHolder.Grid_MediaByteLength = (uint?)ByteData.Length;
                            string FileName = Module_Downloader.MediaFile_GetFileNameOnly(request.Url);
                            if (FileName.EndsWith(".", StringComparison.Ordinal)) FileName += FileExt;

                            if (!Module_Downloader.MediaBrowser_MediaCache.ContainsKey(FileName))
                            {
                                string SaveLocation = $"CefSharp Cache\\Media Cache\\{FileName}";
                                File.WriteAllBytes(SaveLocation, ByteData);
                                lock (Module_Downloader.MediaBrowser_MediaCache)
                                {
                                    Module_Downloader.MediaBrowser_MediaCache.Add(FileName, SaveLocation);
                                }
                            }
                            break;
                        }
                    case "x-icon": //Ignore
                        {
                            //Why are they sending icons now?
                            break;
                        }

                    default:
                        {
                            Window_Preview._RefHolder.Dispatcher.BeginInvoke(() => { MessageBox.Show(Window_Preview._RefHolder, $"{FileExt} file extension is not supported.", "e621 Rebot Preview Caching Error", MessageBoxButton.OK, MessageBoxImage.Error); });
                            break;
                        }
                }
                ResponseFilterHolder.DisposeIt();
                responseDictionary.Remove(request.Identifier);
            }
        }

        protected override IResponseFilter GetResourceResponseFilter(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IResponse response)
        {
            MediaBrowser_ResponseFilter dataFilter = new MediaBrowser_ResponseFilter();
            responseDictionary.Add(request.Identifier, dataFilter);
            return dataFilter;
        }
    }

    //https://github.com/cefsharp/CefSharp/wiki/General-Usage#resource-handling
    //https://stackoverflow.com/a/45818741/8810532
    public class MediaBrowser_ResponseFilter : IResponseFilter
    {
        private MemoryStream? MemoryStreamHolder;

        bool IResponseFilter.InitFilter()
        {
            MemoryStreamHolder = new MemoryStream();
            return true;
        }

        FilterStatus IResponseFilter.Filter(Stream dataIn, out long dataInRead, Stream dataOut, out long dataOutWritten)
        {
            if (dataIn == null)
            {
                dataInRead = 0;
                dataOutWritten = 0;
                return FilterStatus.Done;
            }

            //Calculate how much data we can read, in some instances dataIn.Length is greater than dataOut.Length
            dataInRead = Math.Min(dataIn.Length, dataOut.Length);
            dataOutWritten = dataInRead;

            byte[] readBytes = new byte[dataInRead];
            dataIn.Read(readBytes, 0, readBytes.Length);
            dataOut.Write(readBytes, 0, readBytes.Length);

            MemoryStreamHolder.Write(readBytes, 0, readBytes.Length);

            //If we read less than the total amount avaliable then we need return FilterStatus.NeedMoreData so we can then write the rest
            if (dataInRead < dataIn.Length)
            {
                return FilterStatus.NeedMoreData;
            }

            return FilterStatus.Done;
        }

        public void Dispose()
        {
            //    MemoryStreamHolder.Dispose();
            //    MemoryStreamHolder = null;
        }

        public void DisposeIt()
        {
            MemoryStreamHolder.Dispose();
            MemoryStreamHolder = null;
        }

        public byte[] ByteData
        {
            get { return MemoryStreamHolder.ToArray(); }
        }
    }

    //https://stackoverflow.com/a/40470855/8810532
    public class MediaBrowser_MenuHandler : IContextMenuHandler
    {
        public void OnBeforeContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
        {
            model.Clear();
        }

        public bool OnContextMenuCommand(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
        {
            return false;
        }

        public void OnContextMenuDismissed(IWebBrowser browserControl, IBrowser browser, IFrame frame)
        {

        }

        public bool RunContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
        {
            return false;
        }
    }

    public class MediaBrowser_FocusHandler : FocusHandler
    {
        protected override bool OnSetFocus(IWebBrowser chromiumWebBrowser, IBrowser browser, CefFocusSource source)
        {
            return true;
        }
        protected override void OnGotFocus(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            //Form_Preview._FormReference.BeginInvoke(new Action(() =>
            //{
            //    Form_Preview._FormReference.panel_Rating.Focus(); //sometimes keypress' don't work even though focus it there
            //    Form_Preview._FormReference.timer_StealFocusBack.Start(); //so add a timer hack
            //}));
        }
    }

}