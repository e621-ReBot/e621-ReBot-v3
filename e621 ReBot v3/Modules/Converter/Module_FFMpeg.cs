using e621_ReBot_v3.CustomControls;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace e621_ReBot_v3.Modules.Converter
{
    internal class Module_FFMpeg
    {
        //https://wiki.webmproject.org/ffmpeg/vp9-encoding-guide
        //https://trac.ffmpeg.org/wiki/Encode/VP9

        private static void FFMpeg4Ugoira(ActionType ActionTypeEnum, string TempFolderName, string FullFolderPath, string UgoiraFileName, int UgoiraDuration, ProgressBar? ProgressBarRef = null)
        {
            using (Process FFMpeg = new Process())
            {
                // APNGs are too big.
                // If TotalUgoiraLength < 10000 Then '10 seconds
                // Dim Framerate As Double = 1 / (TotalUgoiraLength / 1000 / UgoiraJObject("frames").Count)
                //FFMpeg.StartInfo.Arguments = string.Format("-hide_banner -y  -f concat -i {0}\input.txt apng -plays 0 ""{2}\{3}.png"" -y", Framerate, TempFolderName, FolderPath, UgoiraFileName) '-r is framerate
                //FFMpeg.StartInfo.Arguments = $"-hide_banner -y -f concat -i {TempFolderName}\\input.txt -plays 0 \"{FolderPath}\\{UgoiraFileName}.apng\"";
                // else

                switch (ActionTypeEnum)
                {
                    case ActionType.Upload:
                        {
                            Window_Main._RefHolder.UploadQueueProcess = FFMpeg;
                            break;
                        }

                    case ActionType.Conversion:
                        {
                            Window_Main._RefHolder.ConversionQueueProcess = FFMpeg;
                            break;
                        }
                }

                FFMpeg.StartInfo.FileName = "ffmpeg.exe";
                FFMpeg.StartInfo.Arguments = $"-hide_banner -y -f concat -i {TempFolderName}\\input.txt -c:v libvpx-vp9 -pix_fmt yuv420p -lossless 1 -row-mt 1 -an \"{FullFolderPath}\\{UgoiraFileName}.webm\"";
                FFMpeg.StartInfo.CreateNoWindow = true;
                FFMpeg.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                FFMpeg.StartInfo.UseShellExecute = false;
                FFMpeg.StartInfo.RedirectStandardError = true;
                FFMpeg.Start();
                while (!FFMpeg.HasExited)
                {
                    string ReadLine = FFMpeg.StandardError.ReadLine();
                    while (ReadLine != null)
                    {
                        if (ReadLine.StartsWith("frame= ", StringComparison.OrdinalIgnoreCase))
                        {
                            string ReadTime = ReadLine.Substring(ReadLine.IndexOf("time=") + 5, 11);
                            if (ReadTime.Substring(0, 1).All(char.IsDigit))
                            {
                                TimeSpan CurrentTime = TimeSpan.Parse(ReadTime);
                                switch (ActionTypeEnum)
                                {
                                    case ActionType.Upload:
                                        {
                                            Module_Uploader.Report_Status($"Converting Ugoira to WebM...{(CurrentTime.TotalMilliseconds / UgoiraDuration):P0}");
                                            break;
                                        }

                                    case ActionType.Download:
                                        {
                                            ProgressBarRef.Dispatcher.BeginInvoke(() => { ProgressBarRef.Value = (int)(CurrentTime.TotalMilliseconds / UgoiraDuration * 100d); });
                                            break;
                                        }

                                    case ActionType.Conversion:
                                        {
                                            //ReportConversionProgress("CU", CurrentTime.TotalMilliseconds / UgoiraDuration, in DataRowRef);
                                            break;
                                        }
                                }
                            }
                        }
                        ReadLine = FFMpeg.StandardError.ReadLine(); // doesn't want to exit on first pass otherwise
                    }
                }
                FFMpeg.WaitForExit();
            }
        }

        private static void FFMpeg4Video(ActionType ActionTypeEnum, string TempFolderName, string TempVideoFileName, string TempVideoFormat, string? FullFolderPath = null, ProgressBar? ProgressBarRef = null)
        {
            TimeSpan VideoDuration = TimeSpan.Zero;
            using (Process FFMpeg = new Process())
            {
                switch (ActionTypeEnum)
                {
                    case ActionType.Upload:
                        {
                            Window_Main._RefHolder.UploadQueueProcess = FFMpeg;
                            break;
                        }

                    case ActionType.Conversion:
                        {
                            Window_Main._RefHolder.ConversionQueueProcess = FFMpeg;
                            break;
                        }
                }
                FFMpeg.StartInfo.FileName = "ffmpeg.exe";
                FFMpeg.StartInfo.UseShellExecute = false;
                FFMpeg.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                FFMpeg.StartInfo.CreateNoWindow = true;
                FFMpeg.StartInfo.RedirectStandardError = true;

                FFMpeg.StartInfo.Arguments = $"-hide_banner -y -i {TempFolderName}\\{TempVideoFileName}.{TempVideoFormat} -c:v libvpx-vp9 -pix_fmt yuv420p -b:a 192k -crf 24 -b:v 4M -deadline realtime -cpu-used 2 -row-mt 1 \"{FullFolderPath}\\{TempVideoFileName}.webm\"";
                FFMpeg.Start();

                Thread.Sleep(1000);

                while (!FFMpeg.HasExited)
                {
                    string ReadLine = FFMpeg.StandardError.ReadLine();
                    while (ReadLine != null)
                    {
                        if (VideoDuration == TimeSpan.Zero && ReadLine.Contains("Duration:"))
                        {
                            //__Duration: 00:06:08.40, start: 0.000000, bitrate: 1000 kb/s
                            VideoDuration = TimeSpan.Parse(ReadLine.Substring(12, 11));
                            continue;
                        }
                        //frame= 1881 fps=139 q=24.0 size=   13568KiB time=00:01:15.16 bitrate=1478.8kbits/s speed=5.56x
                        if (ReadLine.StartsWith("frame= ", StringComparison.OrdinalIgnoreCase))
                        {
                            string ReadTime = ReadLine.Substring(ReadLine.IndexOf("time=") + 5, 11);
                            TimeSpan CurrentTime = TimeSpan.Parse(ReadTime);
                            switch (ActionTypeEnum)
                            {
                                case ActionType.Upload:
                                    {
                                        Module_Uploader.Report_Status($"Converting Video to WebM...{(CurrentTime.TotalMilliseconds / VideoDuration.TotalMilliseconds):P0}");
                                        break;
                                    }

                                case ActionType.Download:
                                    {
                                        ProgressBarRef.Dispatcher.BeginInvoke(() => { ProgressBarRef.Value = (int)(CurrentTime.TotalMilliseconds / VideoDuration.TotalMilliseconds * 100d); });
                                        break;
                                    }

                                case ActionType.Conversion:
                                    {
                                        //ReportConversionProgress("CV", CurrentTime.TotalMilliseconds / VideoDuration.TotalMilliseconds, in DataRowRef);
                                        break;
                                    }
                            }
                            Thread.Sleep(1000);
                        }
                        ReadLine = FFMpeg.StandardError.ReadLine();
                    }
                }
                FFMpeg.WaitForExit();
            }
        }

        // - - - - - - - - - - - - - - - - 

        private static string UgoiraJSONResponse(string Grab_URL)
        {
            string WorkID = Grab_URL.Substring(Grab_URL.LastIndexOf('/') + 1);
            Module_CookieJar.GetCookies(Grab_URL, ref Module_CookieJar.Cookies_Pixiv);
            HttpWebRequest PixivDownload = (HttpWebRequest)WebRequest.Create($"https://www.pixiv.net/ajax/illust/{WorkID}/ugoira_meta");
            PixivDownload.CookieContainer = Module_CookieJar.Cookies_Pixiv;
            PixivDownload.Timeout = 5000;
            PixivDownload.UserAgent = AppSettings.GlobalUserAgent;
            using (StreamReader PixivStreamReader = new StreamReader(PixivDownload.GetResponse().GetResponseStream()))
            {
                return PixivStreamReader.ReadToEnd();
            }
        }

        // - - - - - - - - - - - - - - - -

        internal static void UploadQueue_Ugoira2WebM(string Grab_URL, out byte[] bytes2Send, out string FileName, out string ExtraSourceURL)
        {
            JToken UgoiraJObject = JObject.Parse(UgoiraJSONResponse(Grab_URL))["body"];

            string UgoiraFileName = UgoiraJObject["originalSrc"].Value<string>();
            ExtraSourceURL = UgoiraFileName;
            UgoiraFileName = UgoiraFileName.Substring(UgoiraFileName.LastIndexOf('/') + 1);
            UgoiraFileName = UgoiraFileName.Substring(0, UgoiraFileName.LastIndexOf('.'));

            int TotalUgoiraLength = 0;
            StringBuilder UgoiraConcat = new StringBuilder();
            foreach (JToken UgoiraFrame in UgoiraJObject["frames"])
            {
                UgoiraConcat.AppendLine($"file {UgoiraFrame["file"].Value<string>()}"); // FFmpeg wants / instead of \
                UgoiraConcat.AppendLine($"duration {UgoiraFrame["delay"].Value<int>() / 1000d}");
                TotalUgoiraLength += UgoiraFrame["delay"].Value<int>();
            }
            if (Directory.Exists("FFMpegTemp\\Upload"))
            {
                Directory.Delete("FFMpegTemp\\Upload", true);
            }
            Directory.CreateDirectory("FFMpegTemp\\Upload").Attributes = FileAttributes.Hidden;
            File.WriteAllText(@"FFMpegTemp\\Upload\input.txt", UgoiraConcat.ToString());

            byte[] TempBytes = Module_Downloader.DownloadFileBytes(UgoiraJObject["originalSrc"].Value<string>(), ActionType.Upload);
            Module_Downloader.SaveFileBytes(ActionType.Upload, TempBytes, $"{UgoiraFileName}.zip");

            Module_Uploader.Report_Status("Converting Ugoira to WebM...");
            FFMpeg4Ugoira(ActionType.Upload, "FFMpegTemp\\Upload", "FFMpegTemp\\Upload", UgoiraFileName, TotalUgoiraLength);
            Module_Uploader.Report_Status("Converting Ugoira to WebM...100%");
            Window_Main._RefHolder.UploadQueueProcess = null;

            FileName = $"{UgoiraFileName}.webm";
            bytes2Send = File.ReadAllBytes($"FFMpegTemp\\Upload\\{FileName}");
            Directory.Delete("FFMpegTemp\\Upload", true);
        }

        internal static void UploadQueue_Videos2WebM(out byte[] bytes2Send, out string FileName, in string ExtraSourceURL)
        {
            string WorkURL = ExtraSourceURL;

            string VideoFileName = WorkURL.Substring(WorkURL.LastIndexOf('/') + 1);
            string VideoFormat = VideoFileName.Substring(VideoFileName.LastIndexOf('.') + 1);
            VideoFileName = VideoFileName.Substring(0, VideoFileName.LastIndexOf('.'));

            if (Directory.Exists("FFMpegTemp\\Upload")) Directory.Delete("FFMpegTemp\\Upload", true);
            Directory.CreateDirectory("FFMpegTemp\\Upload").Attributes = FileAttributes.Hidden;

            ushort RetryCount = 0;
            byte[] TempBytes;
            do
            {
                RetryCount++;
                TempBytes = Module_Downloader.DownloadFileBytes(WorkURL, ActionType.Upload);
                if (TempBytes.Length == 0)
                {
                    Thread.Sleep(500);
                    continue;
                }
                Module_Downloader.SaveFileBytes(ActionType.Upload, TempBytes, $"{VideoFileName}.{VideoFormat}");
                RetryCount = 69;
            } while (RetryCount < 4);

            if (TempBytes.Length == 0)
            {
                throw new Exception("0 bytes error @Upload Video");
            }

            Module_Uploader.Report_Status($"Converting Video to WebM...");
            FFMpeg4Video(ActionType.Upload, "FFMpegTemp\\Upload", VideoFileName, VideoFormat, "FFMpegTemp\\Upload");
            Module_Uploader.Report_Status($"Converting Video to WebM...100%");
            Window_Main._RefHolder.UploadQueueProcess = null;

            FileName = $"{VideoFileName}.webm";
            bytes2Send = File.ReadAllBytes($"FFMpegTemp\\Upload\\{FileName}");
            Directory.Delete("FFMpegTemp\\Upload", true);
        }

        // - - - - - - - - - - - - - - - -

        internal static void DownloadQueue_Ugoira2WebM(DownloadVE DownloadVERef)
        {
            Uri DomainURL = new Uri(DownloadVERef._DownloadItemRef.Grab_PageURL);
            string HostString = DomainURL.Host.Remove(DomainURL.Host.LastIndexOf('.')).Replace("www.", "");
            HostString = $"{new CultureInfo("en-US", false).TextInfo.ToTitleCase(HostString)}\\";

            JToken UgoiraJObject = JObject.Parse(UgoiraJSONResponse(DownloadVERef._DownloadItemRef.Grab_PageURL))["body"];

            string UgoiraFileName = UgoiraJObject["originalSrc"].Value<string>();
            UgoiraFileName = UgoiraFileName.Substring(UgoiraFileName.LastIndexOf('/') + 1);
            UgoiraFileName = UgoiraFileName.Substring(0, UgoiraFileName.LastIndexOf('.'));

            string PurgeArtistName = DownloadVERef._DownloadItemRef.Grab_Artist.Replace('/', '-');
            PurgeArtistName = Path.GetInvalidFileNameChars().Aggregate(PurgeArtistName, (current, c) => current.Replace(c.ToString(), string.Empty));
            string FolderPath = Path.Combine(AppSettings.Download_FolderLocation, HostString, PurgeArtistName);
            Directory.CreateDirectory(FolderPath);

            string FullFilePath = $"{FolderPath}\\{UgoiraFileName}.webm";
            bool SkippedConvert = false;
            if (File.Exists(FullFilePath))
            {
                SkippedConvert = true;
                goto SkipDLandConvert;
            }

            int TotalUgoiraLength = 0;
            StringBuilder UgoiraConcat = new StringBuilder();
            foreach (JToken UgoiraFrame in UgoiraJObject["frames"])
            {
                UgoiraConcat.AppendLine($"file {UgoiraFrame["file"].Value<string>()}"); // FFmpeg wants / instead of \
                UgoiraConcat.AppendLine($"duration {UgoiraFrame["delay"].Value<int>() / 1000d}");
                TotalUgoiraLength += UgoiraFrame["delay"].Value<int>();
            }
            string TempFolderName = $"FFMpegTemp\\{UgoiraFileName}";
            if (Directory.Exists(TempFolderName)) Directory.Delete(TempFolderName, true);
            Directory.CreateDirectory(TempFolderName).Attributes = FileAttributes.Hidden;
            File.WriteAllText($"{TempFolderName}\\input.txt", UgoiraConcat.ToString());

            byte[] TempBytes = Module_Downloader.DownloadFileBytes(UgoiraJObject["originalSrc"].Value<string>(), ActionType.Download, DownloadVERef.DownloadProgress);
            Module_Downloader.SaveFileBytes(ActionType.Download, TempBytes, UgoiraFileName, FolderPath);

            string OriginalVideoFormat = DownloadVERef._DownloadItemRef.Grab_MediaFormat;
            FFMpeg4Ugoira(ActionType.Download, TempFolderName, FolderPath, UgoiraFileName, TotalUgoiraLength, DownloadVERef.ConversionProgress);
            Directory.Delete(TempFolderName, true);

            if (DownloadVERef._DownloadItemRef.MediaItemRef != null)
            {
                DownloadVERef._DownloadItemRef.MediaItemRef.UP_Tags += $"{(TotalUgoiraLength < 30000 ? " short_playtime" : " long_playtime")} animated no_sound webm";
            }

        SkipDLandConvert:
            if (SkippedConvert)
            {
                Module_Converter.Report_Info($"Ugoira WebM already exists, skipped coverting {DownloadVERef._DownloadItemRef.Grab_MediaURL}");
            }
            else
            {
                Module_Converter.Report_Info($"Converted Ugoira from: {DownloadVERef._DownloadItemRef.Grab_MediaURL} to WebM");
            }
            //Form_Loader._FormReference.BeginInvoke(new Action(() =>
            //{
            //    if (Form_Preview._FormReference != null && Form_Preview._FormReference.IsHandleCreated && ReferenceEquals(Form_Preview._FormReference.Preview_RowHolder, GridDataRow))
            //    {
            //        Form_Preview._FormReference.PB_ViewFile.Visible = true;
            //    }
            //}));
            if (DownloadVERef._DownloadItemRef.MediaItemRef != null)
            {
                DownloadVERef._DownloadItemRef.MediaItemRef.DL_FilePath = FullFilePath;
            }
            DownloadQueue_ConvertFinished(DownloadVERef, FullFilePath);
        }

        internal static void DownloadQueue_Video2WebM(DownloadVE DownloadVERef)
        {
            Uri DomainURL = new Uri(DownloadVERef._DownloadItemRef.Grab_PageURL);
            string HostString = DomainURL.Host.Remove(DomainURL.Host.LastIndexOf('.')).Replace("www.", "");
            HostString = $"{new CultureInfo("en-US", false).TextInfo.ToTitleCase(HostString)}\\";

            string VideoFileName = Module_Downloader.MediaFile_GetFileNameOnly(DownloadVERef._DownloadItemRef.Grab_MediaURL, DownloadVERef._DownloadItemRef.Grab_MediaFormat);
            string VideoFormat = DownloadVERef._DownloadItemRef.Grab_MediaFormat;
            string PurgeArtistName = DownloadVERef._DownloadItemRef.Grab_Artist.Replace('/', '-');
            PurgeArtistName = Path.GetInvalidFileNameChars().Aggregate(PurgeArtistName, (current, c) => current.Replace(c.ToString(), string.Empty));
            string FolderPath = Path.Combine(AppSettings.Download_FolderLocation, HostString, PurgeArtistName);
            Directory.CreateDirectory(FolderPath);

            string FullFilePath = $"{FolderPath}\\{VideoFileName}.webm";
            bool SkippedConvert = false;
            if (File.Exists(FullFilePath))
            {
                SkippedConvert = true;
                goto SkipDLandConvert;
            }

            string TempFolderName = $"FFMpegTemp\\{VideoFileName}";
            if (Directory.Exists(TempFolderName)) Directory.Delete(TempFolderName, true);
            Directory.CreateDirectory(TempFolderName).Attributes = FileAttributes.Hidden;

            byte[] TempBytes = Module_Downloader.DownloadFileBytes(DownloadVERef._DownloadItemRef.Grab_MediaURL, ActionType.Download, DownloadVERef.DownloadProgress);
            Module_Downloader.SaveFileBytes(ActionType.Download, TempBytes, VideoFileName, FolderPath);

            string OriginalVideoFormat = DownloadVERef._DownloadItemRef.Grab_MediaFormat;
            FFMpeg4Video(ActionType.Download, TempFolderName, VideoFileName, OriginalVideoFormat, FolderPath, DownloadVERef.ConversionProgress);
            Directory.Delete(TempFolderName, true);

        SkipDLandConvert:
            if (SkippedConvert)
            {
                Module_Converter.Report_Info($"Video WebM already exists, skipped coverting {DownloadVERef._DownloadItemRef.Grab_MediaURL}");
            }
            else
            {
                Module_Converter.Report_Info($"Converted Video from: {DownloadVERef._DownloadItemRef.Grab_MediaURL} to WebM");
            }
            //Form_Loader._FormReference.BeginInvoke(new Action(() =>
            //{
            //    if (Form_Preview._FormReference != null && Form_Preview._FormReference.IsHandleCreated && ReferenceEquals(Form_Preview._FormReference.Preview_RowHolder, GridDataRow))
            //    {
            //        Form_Preview._FormReference.PB_ViewFile.Visible = true;
            //    }
            //}));
            if (DownloadVERef._DownloadItemRef.MediaItemRef != null)
            {
                DownloadVERef._DownloadItemRef.MediaItemRef.DL_FilePath = FullFilePath;
            }
            DownloadQueue_ConvertFinished(DownloadVERef, FullFilePath);
        }

        private static void DownloadQueue_ConvertFinished(DownloadVE DownloadVERef, string FullFilePath)
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                DownloadVERef.FolderIcon.Tag = FullFilePath;
                DownloadVERef._DownloadFinished = true;
                Module_Downloader._DownloadVEFinisherTimer.Stop();
                Module_Downloader._DownloadVEFinisherTimer.Start();
            });

            //e6_DownloadItem e6_DownloadItemRef = (e6_DownloadItem)e.Result;
            //DataRow DataRowTemp = (DataRow)e6_DownloadItemRef.Tag;

            //lock (Module_Downloader.Download_AlreadyDownloaded)
            //{
            //    Module_Downloader.Download_AlreadyDownloaded.Add((string)DataRowTemp["Grab_MediaURL"]);
            //}

            //Image ImageHolder = e6_DownloadItemRef.picBox_ImageHolder.Image ?? e6_DownloadItemRef.picBox_ImageHolder.BackgroundImage;
            //Form_Loader._FormReference.DownloadFLP_Downloaded.ResumeLayout();
            //UIDrawController.SuspendDrawing(Form_Loader._FormReference.DownloadFLP_Downloaded);
            //Module_Downloader.AddPic2FLP((string)DataRowTemp["Grab_ThumbnailURL"], e6_DownloadItemRef.DL_FolderIcon.Tag.ToString(), ImageHolder);
            //Form_Loader._FormReference.DownloadFLP_Downloaded.ResumeLayout();
            //UIDrawController.ResumeDrawing(Form_Loader._FormReference.DownloadFLP_Downloaded);

            //e6_DownloadItemRef.Dispose();
            //((BackgroundWorker)sender).Dispose();
            //Module_Downloader.timer_Download.Start();
        }

        // - - - - - - - - - - - - - - - -

        internal static void DragDropConvert(string VideoPath)
        {
            string VideoFileName = VideoPath.Substring(VideoPath.LastIndexOf('\\') + 1);
            VideoFileName = VideoFileName.Substring(0, VideoFileName.LastIndexOf('.'));

            string FullFolderPath = Path.GetDirectoryName(VideoPath);
            string FullFilePath = $"{FullFolderPath}\\{VideoFileName}.webm";

            if (File.Exists(FullFilePath))
            {
                MessageBoxResult MessageBoxResultTemp = MessageBox.Show("Converted video file already exists, do you want to continue regardless?", "e621 ReBot", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (MessageBoxResultTemp != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            using (Process FFmpeg = new Process())
            {
                FFmpeg.StartInfo.FileName = "ffmpeg.exe";
                FFmpeg.StartInfo.UseShellExecute = false;
                FFmpeg.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                FFmpeg.StartInfo.CreateNoWindow = false;
                FFmpeg.StartInfo.RedirectStandardError = false;
                FFmpeg.StartInfo.RedirectStandardOutput = false;
                FFmpeg.StartInfo.Arguments = $"-hide_banner -y -i \"{VideoPath}\" -c:v libvpx-vp9 -pix_fmt yuv420p -b:a 192k -crf 24 -b:v 4M -deadline realtime -cpu-used 2 -row-mt 1 \"{FullFilePath}\"";
                FFmpeg.Start();
                FFmpeg.WaitForExit();
                if (FFmpeg.ExitCode < 0)
                {
                    return; //canceled by user
                }
            }
        }
    }
}