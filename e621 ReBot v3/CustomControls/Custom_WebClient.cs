using System;
using System.ComponentModel;
using System.Net;
using System.Windows.Threading;

namespace e621_ReBot_v3.CustomControls
{
    internal class Custom_WebClient : WebClient
    {
        internal Custom_WebClient()
        {
            DownloadTimeoutTimer.Tick += TimeoutTick;
        }

        private readonly DispatcherTimer DownloadTimeoutTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(30) };
        private void TimeoutTick(object? sender, EventArgs e)
        {
            DownloadTimeoutTimer.Stop();
            CancelAsync();
        }

        protected override void OnDownloadProgressChanged(DownloadProgressChangedEventArgs e)
        {
            DownloadTimeoutTimer.Stop();
            base.OnDownloadProgressChanged(e);
            DownloadTimeoutTimer.Start();
        }

        protected override void OnDownloadFileCompleted(AsyncCompletedEventArgs e)
        {
            DownloadTimeoutTimer.Stop();
            base.OnDownloadFileCompleted(e);
        }
    }
}
