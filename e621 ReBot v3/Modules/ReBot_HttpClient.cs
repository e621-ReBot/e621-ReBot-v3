using CefSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace e621_ReBot_v3.Modules
{
    //https://topic.alibabacloud.com/a/c--display-of-httpclient-upload-and-download-progress_1_37_32738018.html
    //https://github.com/dotnet/macios/issues/5636
    //https://centage5.rssing.com/chan-4008961/all_p1.html

    internal static class ReBot_HttpClient
    {
        private static readonly HttpClientHandler _Handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };
        private static readonly HttpClient _Client = new HttpClient(_Handler)
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestVersion = HttpVersion.Version30,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };

        internal static async Task<DownloadResult> DownloadBytesAsync(string URL, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            using MemoryStream DownloadedBytes = new MemoryStream();
            DownloadResult Result = await DownloadStreamAsync(URL, DownloadedBytes, progress, cancellationToken);

            return new DownloadResult
            {
                Data = Result.IsSuccessStatusCode ? DownloadedBytes.ToArray() : null,
                StatusCode = Result.StatusCode,
                Message = Result.Message
            };
        }

        internal static async Task<DownloadResult> DownloadFileAsync(string URL, string filename, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            await using FileStream FileStreamOutput = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);

            return await DownloadStreamAsync(URL, FileStreamOutput, progress, cancellationToken);
        }

        internal static async Task<DownloadResult> DownloadStringAsync(string URL, CancellationToken cancellationToken = default, string? AuthHeader = null)
        {
            using HttpResponseMessage HttpResponseMessageTemp = await SendAsync(URL, cancellationToken, AuthHeader);

            string Content = await HttpResponseMessageTemp.Content.ReadAsStringAsync(cancellationToken);
            return new DownloadResult
            {
                TextData = HttpResponseMessageTemp.IsSuccessStatusCode ? Content : null,
                StatusCode = HttpResponseMessageTemp.StatusCode,
                Message = HttpResponseMessageTemp.ReasonPhrase
            };
        }

        private static async Task<DownloadResult> DownloadStreamAsync(string URL, Stream destination, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            using HttpResponseMessage HttpResponseMessageTemp = await SendAsync(URL, cancellationToken);
            if (HttpResponseMessageTemp.IsSuccessStatusCode)
            {
                long? totalBytes = HttpResponseMessageTemp.Content.Headers.ContentLength;

                await using Stream DownloadStream = await HttpResponseMessageTemp.Content.ReadAsStreamAsync(cancellationToken);

                byte[] DownloadBuffer = new byte[65536]; // 64 kB buffer
                long BytesReadSoFar = 0;

                while (true)
                {
                    using CancellationTokenSource ReadTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    ReadTimeout.CancelAfter(TimeSpan.FromSeconds(30));

                    int BytesRead;
                    try
                    {
                        BytesRead = await DownloadStream.ReadAsync(DownloadBuffer.AsMemory(), ReadTimeout.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        return new DownloadResult
                        {
                            StatusCode = HttpStatusCode.GatewayTimeout,
                            Message = "Download timed out: no data received for 30 seconds."
                        };
                    }
                    catch (IOException)
                    {
                        return new DownloadResult
                        {
                            StatusCode = HttpStatusCode.InternalServerError,
                            Message = "Download error: connection interrupted."
                        };
                    }

                    if (BytesRead == 0)
                        break;

                    await destination.WriteAsync(DownloadBuffer.AsMemory(0, BytesRead), cancellationToken);

                    BytesReadSoFar += BytesRead;

                    progress?.Report(new DownloadProgress
                    {
                        BytesDownloaded = BytesReadSoFar,
                        TotalBytes = totalBytes
                    });
                }
            }

            return new DownloadResult
            {
                StatusCode = HttpResponseMessageTemp.StatusCode,
                Message = HttpResponseMessageTemp.ReasonPhrase
            };
        }

        internal static async Task<DownloadResult> HeadRequestAsync(string URL, CancellationToken cancellationToken = default)
        {
            using HttpResponseMessage HttpResponseMessageTemp = await SendAsync(URL, cancellationToken, HeadRequest: true);

            return new DownloadResult
            {
                DataLength = HttpResponseMessageTemp.IsSuccessStatusCode ? HttpResponseMessageTemp.Content.Headers.ContentLength : null,
                StatusCode = HttpResponseMessageTemp.StatusCode,
                Message = HttpResponseMessageTemp.ReasonPhrase
            };
        }


        private static async Task<HttpResponseMessage> SendAsync(string URL, CancellationToken cancellationToken = default, string? AuthHeader = null, bool HeadRequest = false)
        {
            HttpResponseMessage ReturnResponse = new HttpResponseMessage();
            for (int Attempt = 0; Attempt < 4; Attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using HttpRequestMessage HttpRequestMessageTemp = CreateRequest(URL, HeadRequest ? HttpMethod.Head : HttpMethod.Get);
                    HttpRequestMessageTemp.Version = HttpVersion.Version30;
                    HttpRequestMessageTemp.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
                    if (!string.IsNullOrEmpty(AuthHeader))
                        HttpRequestMessageTemp.Headers.Authorization = new AuthenticationHeaderValue("Basic", AuthHeader);

                    HttpResponseMessage HttpResponseMessageTemp = await _Client.SendAsync(HttpRequestMessageTemp, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                    int StatusCode = (int)HttpResponseMessageTemp.StatusCode;
                    bool ShouldRetry = StatusCode is
                        408 or  // Request Timeout
                        425 or  // Too Early
                        429 or  // Too Many Requests
                        500 or  // Internal Server Error
                        502 or  // Bad Gateway
                        503 or  // Service Unavailable
                        504;    // Gateway Timeout

                    if (!ShouldRetry)
                    {
                        ReturnResponse.Dispose();
                        return HttpResponseMessageTemp;
                    }

                    //Got to read header first
                    TimeSpan Delay = HttpResponseMessageTemp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, Attempt + 1));

                    HttpResponseMessageTemp.Dispose();

                    if (Delay < TimeSpan.Zero) Delay = TimeSpan.Zero;
                    if (Delay > TimeSpan.FromSeconds(30)) Delay = TimeSpan.FromSeconds(30);

                    await Task.Delay(Delay, cancellationToken);
                }
                catch (Exception ex) when ((ex is HttpRequestException or IOException or SocketException or OperationCanceledException) && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, Attempt + 1)), cancellationToken);

                    ReturnResponse.StatusCode = HttpStatusCode.InternalServerError;
                    ReturnResponse.ReasonPhrase = ex.Message;
                }
            }

            return ReturnResponse;
        }


        //Handle headers
        private static HttpRequestMessage CreateRequest(string URL, HttpMethod HttpMethodRef)
        {
            HttpRequestMessage HttpRequestMessageTemp = new HttpRequestMessage(HttpMethodRef, URL);
            HttpRequestMessageTemp.Headers.UserAgent.ParseAdd(AppSettings.GlobalUserAgent);

            Uri uri = new Uri(URL);
            switch (uri.Host)
            {
                case "e621.net":
                    {
                        HttpRequestMessageTemp.Headers.UserAgent.Clear();
                        HttpRequestMessageTemp.Headers.UserAgent.ParseAdd(AppSettings.AppName);
                        break;
                    }

                case "d.furaffinity.net":
                    {
                        HttpRequestMessageTemp.Headers.Add("Referer", "https://www.furaffinity.net/");
                        break;
                    }

                case "i.pximg.net":
                    {
                        HttpRequestMessageTemp.Headers.Add("Referer", "https://www.pixiv.net");
                        break;
                    }

                //case "inkbunny.net":
                case string Inkbunny when Inkbunny.Contains("ib.metapix.net"):
                    {
                        HttpRequestMessageTemp.Headers.Add("Referer", "https://inkbunny.net/");
                        break;
                    }

                //case "https://www.hiccears.com": //verify, after fixing hiccears first
                //    {
                //        List<CefSharp.Cookie> CookieList = Cef.GetGlobalCookieManager().VisitUrlCookiesAsync("https://www.hiccears.com", true).Result;
                //        foreach (CefSharp.Cookie CookieHolder in CookieList)
                //        {
                //            if (CookieHolder.Name.Equals("hiccears"))
                //            {
                //                HttpRequestMessageTemp.Headers.Add(CookieHolder.Name, CookieHolder.Value);
                //                break;
                //            }
                //        }
                //        break;
                //    }

                //case "x.com":
                case "pbs.twimg.com":
                case "video.twimg.com":
                    {
                        HttpRequestMessageTemp.Headers.Add("Referer", "https://x.com/");
                        break;
                    }

                //case string Newgrounds when Newgrounds.Contains(".newgrounds.com"):
                case "art.ngfiles.com": //images
                case "uploads.ungrounded.net": //videos
                    {
                        HttpRequestMessageTemp.Headers.Add("Referer", "https://www.newgrounds.com/");
                        break;
                    }


                //case string SoFurry when SoFurry.Contains(".sofurry.com"):


                //case "www.weasyl.com": //used from cache


                //case "mastodon.social":
                //case "baraag.net":
                //case "pawoo.net":


                case "pictures.hentai-foundry.com": //they only have pictures
                    {
                        HttpRequestMessageTemp.Headers.Add("Referer", "https://www.hentai-foundry.com/");
                        break;
                    }

                case "images.plurk.com":
                    {
                        HttpRequestMessageTemp.Headers.Add("Referer", "https://www.plurk.com/");
                        break;
                    }
            }

            return HttpRequestMessageTemp;
        }

        internal static async Task GetCookies(string WebAddress)
        {
            string BaseURL = $"{new Uri(WebAddress).Scheme}://{new Uri(WebAddress).Host}";
            Uri uri = new Uri(BaseURL);

            //Is null if grid session is loaded but browser isn't used
            if (Cef.GetGlobalCookieManager() == null)
                return;

            //Must not be called on main thread (or maybe same thread Cef lives on or it will hang the browser)
            List<CefSharp.Cookie> CefCookies = await Cef.GetGlobalCookieManager().VisitUrlCookiesAsync(BaseURL, true);

            if (CefCookies == null)
                return;

            foreach (CefSharp.Cookie CookieHolder in CefCookies)
            {
                System.Net.Cookie TempCookie = new System.Net.Cookie()
                {
                    Name = CookieHolder.Name,
                    Value = EscapeCookieValue(CookieHolder.Value),
                    Domain = CookieHolder.Domain,
                    //Expires = CookieHolder.Expires == null ? DateTime.Now.AddMonths(1) : (DateTime)CookieHolder.Expires,
                    Secure = CookieHolder.Secure,
                    HttpOnly = CookieHolder.HttpOnly,
                    Path = CookieHolder.Path ?? "/"
                };

                if (CookieHolder.Expires.HasValue)
                    TempCookie.Expires = CookieHolder.Expires.Value;

                // Remove the existing cookie with the same name/domain/path
                foreach (System.Net.Cookie ExistingCookies in _Handler.CookieContainer.GetCookies(uri))
                {
                    if (ExistingCookies.Name == TempCookie.Name && ExistingCookies.Domain == TempCookie.Domain && ExistingCookies.Path == TempCookie.Path)
                    {
                        ExistingCookies.Expired = true;
                    }
                }

                _Handler.CookieContainer.Add(TempCookie);
            }
        }

        public static string EscapeCookieValue(string CookieValue)
        {
            if (string.IsNullOrEmpty(CookieValue)) return string.Empty;

            foreach (char c in CookieValue)
            {
                if (c < 0x21 || c > 0x7E || c == ';' || c == ',' || c == '=' || c == '"' || c == '\\' || c == ' ' || c == '\t')
                {
                    return Uri.EscapeDataString(CookieValue);
                }
            }

            return CookieValue;
        }

        internal static async Task<bool> CheckCookies(string WebAddress)
        {
            string BaseURL = $"{new Uri(WebAddress).Scheme}://{new Uri(WebAddress).Host}";
            Uri uri = new Uri(BaseURL);

            //Is null if grid session is loaded but browser isn't used
            if (Cef.GetGlobalCookieManager() == null)
                return false;

            //Must not be called on main thread (or maybe same thread Cef lives on or it will hang the browser)
            List<CefSharp.Cookie> CefCookies = await Cef.GetGlobalCookieManager().VisitUrlCookiesAsync(BaseURL, true);

            if (CefCookies == null)
                return false;

            return true;
        }
    }

    internal sealed class DownloadProgress
    {
        internal long BytesDownloaded { get; init; }
        internal long? TotalBytes { get; init; }

        internal float? Percentage
        {
            get
            {
                if (TotalBytes.HasValue && TotalBytes.Value > 0)
                    return BytesDownloaded * 100f / TotalBytes.Value;

                return null;
            }
        }
    }

    internal sealed class DownloadResult
    {
        internal byte[]? Data { get; init; }
        internal long? DataLength { get; init; }
        internal string? TextData { get; init; }
        internal HttpStatusCode StatusCode { get; init; }
        internal string? Message { get; init; }

        internal bool IsSuccessStatusCode
        {
            get { return (int)StatusCode >= 200 && (int)StatusCode <= 299; }
        }
    }
}