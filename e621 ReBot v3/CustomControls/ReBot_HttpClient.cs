using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Handlers;

namespace e621_ReBot_v3.CustomControls
{
    //https://topic.alibabacloud.com/a/c--display-of-httpclient-upload-and-download-progress_1_37_32738018.html
    //https://github.com/dotnet/macios/issues/5636
    //https://centage5.rssing.com/chan-4008961/all_p1.html

    internal static class ReBot_HttpClient
    {
        static readonly HttpClient _Client;
        static readonly ProgressMessageHandler _ClientProgressMessageHandler;

        static ReBot_HttpClient()
        {
            _ClientProgressMessageHandler = new ProgressMessageHandler();
            _ClientProgressMessageHandler.HttpReceiveProgress += _ClientProgressMessageHandler_HttpReceiveProgress;
            _ClientProgressMessageHandler.HttpSendProgress += _ClientProgressMessageHandler_HttpSendProgress;
            _Client = HttpClientFactory.Create(_ClientProgressMessageHandler);
        }

        internal static void CreateClient()
        {
            using (HttpRequestMessage HttpRequestMessageTemp = new HttpRequestMessage(new HttpMethod("GET"), "https://static1.e621.net/data/6f/99/6f996ec2f9f0cda06e86dc9ac18799f6.webm"))
            {
                //HttpResponseMessage HttpResponseMessageTemp = _Client.Send(HttpRequestMessageTemp);
                _Client.SendAsync(HttpRequestMessageTemp);
                //_Client.SendAsync(HttpRequestMessageTemp).ContinueWith(task =>
                //{
                //    if (task.Result.IsSuccessStatusCode)
                //    {
                //        var breaker = 0;
                //    }
                //});
            }
        }
        internal static void CreateClient1()
        {
            using (HttpRequestMessage HttpRequestMessageTemp = new HttpRequestMessage(new HttpMethod("GET"), "https://static1.e621.net/data/fa/36/fa3636d78f9418ec0fa1486b42ddc32f.gif"))
            {
                //HttpResponseMessage HttpResponseMessageTemp = _Client.Send(HttpRequestMessageTemp);
                _Client.SendAsync(HttpRequestMessageTemp);
                //_Client.SendAsync(HttpRequestMessageTemp).ContinueWith(task =>
                //{
                //    if (task.Result.IsSuccessStatusCode)
                //    {
                //        var breaker = 0;
                //    }
                //});
            }
        }

        private static void _ClientProgressMessageHandler_HttpReceiveProgress(object? sender, HttpProgressEventArgs e)
        {
            Debug.WriteLine(e.ProgressPercentage);
        }

        private static void _ClientProgressMessageHandler_HttpSendProgress(object? sender, HttpProgressEventArgs e)
        {
            Debug.WriteLine(e.ProgressPercentage);
        }
    }
}
