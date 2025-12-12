using e621_ReBot_v3.CustomControls;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace e621_ReBot_v3.Modules.Converter
{
    internal class Module_FFMpeg
    {
        //https://wiki.webmproject.org/ffmpeg/vp9-encoding-guide
        //https://trac.ffmpeg.org/wiki/Encode/VP9

        private static void FFMpeg4Ugoira2APNG(string TempFolderName, string FullFolderPath, string UgoiraFileName, int UgoiraDuration)
        {
            string inputTXTFile = Path.Combine(TempFolderName, "input.txt");
            if (!File.Exists(inputTXTFile))
            {
                throw new Exception("No input file for FFMpeg found");
            }

            using (Process FFMpeg = new Process())
            {
                Window_Main._RefHolder.UploadQueueProcess = FFMpeg;

                FFMpeg.StartInfo.FileName = "ffmpeg.exe";
                FFMpeg.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                FFMpeg.StartInfo.CreateNoWindow = true;
                FFMpeg.StartInfo.UseShellExecute = false;
                FFMpeg.StartInfo.RedirectStandardOutput = true;
                //FFMpeg.StartInfo.RedirectStandardError = true;

                // APNGs have bigger file size but are the only ones that are fully compatible with iOS.
                FFMpeg.StartInfo.Arguments = $"-hide_banner -loglevel error -progress pipe:1 -nostats -y -f concat -i \"{inputTXTFile}\" -vsync vfr -c:v apng -pred mixed -plays 0 \"{FullFolderPath}\\{UgoiraFileName}.apng\"";

                FFMpeg.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        string ReadLine = e.Data;
                        if (ReadLine.StartsWith("out_time=0", StringComparison.OrdinalIgnoreCase)) //time=N/A why does it sometimes happen?
                        {
                            //frame= 1881 fps=139 q=24.0 size=   13568KiB time=00:01:15.16 bitrate=1478.8kbits/s speed=5.56x
                            //or
                            //frame=1234
                            TimeSpan CurrentTime = TimeSpan.Parse(ReadLine.Substring("out_time=".Length));

                            Module_Uploader.Report_Status($"Converting Ugoira to APNG...{(CurrentTime.TotalMilliseconds / UgoiraDuration):P0}");
                        }
                    }
                });
                FFMpeg.Start();
                FFMpeg.BeginOutputReadLine();
                FFMpeg.WaitForExit();
            }
        }

        private static void FFMpeg4Ugoira2WebM(ActionType ActionTypeEnum, string TempFolderName, string FullFolderPath, string UgoiraFileName, ProgressBar? ProgressBarRef = null)
        {
            string ImageExtension = UgoiraFileName.Substring(UgoiraFileName.LastIndexOf('.') + 1);
            UgoiraFileName = Path.GetFileNameWithoutExtension(UgoiraFileName);
            int UgoiraDuration = 0;
            int avgFPS = 15;
            string inputTXTFile = Path.Combine(TempFolderName, "input.txt");
            if (File.Exists(inputTXTFile))
            {
                List<string> lines = File.ReadAllLines(inputTXTFile).ToList();

                List<int> Durations = new List<int>();
                for (int i = lines.Count - 1; i >= 0; i -= 2)
                {
                    //if (lines[i].StartsWith("duration "))
                    int singleFrame = (int)(float.Parse(lines[i].Substring(9)) * 1000);
                    Durations.Add(singleFrame);
                    UgoiraDuration += singleFrame;
                }

                int avgFrameDuration = UgoiraDuration / Durations.Count; //in ms
                //avgFPS = MathF.Truncate((1000f / avgFrameDuration) * 100) / 100;
                avgFPS = (int)MathF.Round(1000 / avgFrameDuration);
            }
            else
            {
                throw new Exception("No input file for FFMpeg found");
            }

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
                FFMpeg.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                FFMpeg.StartInfo.CreateNoWindow = true;
                FFMpeg.StartInfo.UseShellExecute = false;
                FFMpeg.StartInfo.RedirectStandardOutput = true;
                //FFMpeg.StartInfo.RedirectStandardError = true;

                //FFMpeg.StartInfo.Arguments = $"-hide_banner -loglevel error -progress pipe:1 -nostats -y -f concat -i \"{TempFolderName}\\input.txt\" -vsync vfr -c:v libvpx-vp9 -pix_fmt yuv420p -lossless 1 -row-mt 1 -an \"{FullFolderPath}\\{UgoiraFileName}.webm\"";
                FFMpeg.StartInfo.Arguments = $"-hide_banner -loglevel error -progress pipe:1 -nostats -y -framerate {avgFPS} -i \"{FullFolderPath}\\{UgoiraFileName}%d.{ImageExtension}\" -r {avgFPS} -c:v libvpx-vp9 -g 1 -pix_fmt yuv420p -crf 8 -cpu-used 2 -an \"{FullFolderPath}\\{UgoiraFileName}.webm\"";

                FFMpeg.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        string ReadLine = e.Data;
                        if (ReadLine.StartsWith("out_time=0", StringComparison.OrdinalIgnoreCase)) //time=N/A why does it sometimes happen?
                        {
                            //frame= 1881 fps=139 q=24.0 size=   13568KiB time=00:01:15.16 bitrate=1478.8kbits/s speed=5.56x
                            //or
                            //frame=1234
                            TimeSpan CurrentTime = TimeSpan.Parse(ReadLine.Substring("out_time=".Length));
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
                });
                FFMpeg.Start();
                FFMpeg.BeginOutputReadLine();
                FFMpeg.WaitForExit();
            }
        }

        private static TimeSpan VideoDuration;

        private static void FFMpeg4Video(ActionType ActionTypeEnum, string TempFolderName, string TempVideoFileName, string TempVideoFormat, string? FullFolderPath = null, ProgressBar? ProgressBarRef = null)
        {
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
                FFMpeg.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                FFMpeg.StartInfo.CreateNoWindow = true;
                FFMpeg.StartInfo.UseShellExecute = false;
                //FFMpeg.StartInfo.RedirectStandardOutput = true;
                FFMpeg.StartInfo.RedirectStandardError = true;
                FFMpeg.StartInfo.Arguments = $"-hide_banner -loglevel info -y -i \"{TempFolderName}\\{TempVideoFileName}.{TempVideoFormat}\" -c:v libvpx-vp9 -pix_fmt yuv420p -crf 24 -b:v 0 -c:a libopus -b:a 192k -cpu-used 2 -row-mt 1 \"{FullFolderPath}\\{TempVideoFileName}.webm\"";

                VideoDuration = TimeSpan.Zero;
                FFMpeg.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        string ReadLine = e.Data;
                        if (VideoDuration == TimeSpan.Zero && ReadLine.StartsWith("  Duration: "))
                        {
                            //__Duration: 00:06:08.40, start: 0.000000, bitrate: 1000 kb/s
                            //or
                            //total_size=1234567
                            //out_time_us = 123456789
                            //out_time_ms = 123456
                            //out_time = 00:02:03.456000
                            VideoDuration = TimeSpan.Parse(ReadLine.Substring("  Duration: ".Length, 11));
                            return;
                        }
                        if (ReadLine.StartsWith("frame= ") && !ReadLine.Contains("time=N/A")) //time=N/A why does it sometimes happen?
                        {
                            //frame= 1881 fps=139 q=24.0 size=   13568KiB time=00:01:15.16 bitrate=1478.8kbits/s speed=5.56x
                            //or
                            //frame=1234
                            TimeSpan CurrentTime = TimeSpan.Parse(ReadLine.Substring(ReadLine.IndexOf("time=") + 5, 11));

                            double ProgressPercentage = CurrentTime.TotalMilliseconds / VideoDuration.TotalMilliseconds;
                            switch (ActionTypeEnum)
                            {
                                case ActionType.Upload:
                                    {
                                        Module_Uploader.Report_Status($"Converting Video to WebM...{ProgressPercentage:P0}");
                                        break;
                                    }

                                case ActionType.Download:
                                    {
                                        ProgressBarRef.Dispatcher.BeginInvoke(() => { ProgressBarRef.Value = (int)(ProgressPercentage * 100); });
                                        break;
                                    }

                                case ActionType.Conversion:
                                    {
                                        //ReportConversionProgress("CV", ProgressPercentage, in DataRowRef);
                                        break;
                                    }
                            }
                        }
                    }
                });
                FFMpeg.Start();
                FFMpeg.BeginErrorReadLine();
                FFMpeg.WaitForExit();
            }
        }


        // - - - - - - - - - - - - - - - - 

        internal static async Task<string> UgoiraJSONResponse(string Grab_URL)
        {
            if (Module_Downloader._PixivClient == null) Module_Downloader.MakePixivClient();
            string WorkID = Grab_URL.Substring(Grab_URL.LastIndexOf('/') + 1);
            Module_CookieJar.GetCookies(Grab_URL, ref Module_CookieJar.Cookies_Pixiv);
            using HttpRequestMessage HttpRequestMessageTemp = new HttpRequestMessage(HttpMethod.Get, $"https://www.pixiv.net/ajax/illust/{WorkID}/ugoira_meta");
            using HttpResponseMessage HttpResponseMessageTemp = await Module_Downloader._PixivClient.SendAsync(HttpRequestMessageTemp, HttpCompletionOption.ResponseContentRead);
            HttpResponseMessageTemp.EnsureSuccessStatusCode();
            return await HttpResponseMessageTemp.Content.ReadAsStringAsync();
        }

        private static async Task<int> DownloadUgoira(string PageURL, string MediaURL, string TempFolderPath, ActionType ActionTypEnum, ProgressBar ProgressBarRef = null)
        {
            string JSONResponse = await UgoiraJSONResponse(PageURL);
            JToken UgoiraJObject = JObject.Parse(JSONResponse)["body"];

            //Do a head check to see if last image exists as original
            string URLBase = MediaURL.Remove(MediaURL.LastIndexOf('.') - 1); //want to turn "ugoira0." into "ugoira" only
            string MediaExtension = Path.GetExtension(MediaURL); //includes the dot

            int TotalUgoiraLength = 0;
            int FrameCount = UgoiraJObject["frames"].Count();
            StringBuilder UgoiraConcat = new StringBuilder();

            using HttpRequestMessage HttpRequestMessageTemp = new HttpRequestMessage(HttpMethod.Head, $"{URLBase}{FrameCount - 1}{MediaExtension}");
            using HttpResponseMessage HttpResponseMessageTemp = await Module_Downloader._PixivClient.SendAsync(HttpRequestMessageTemp, HttpCompletionOption.ResponseContentRead);
            if (HttpResponseMessageTemp.IsSuccessStatusCode)
            {
                float MultiDLFactor = 1f / FrameCount;
                float MultiProgressBase = 0;
                for (int x = 0; x < FrameCount; x++)
                {
                    string NewURL = $"{URLBase}{x}{MediaExtension}";
                    string FileName = Path.GetFileName(NewURL);

                    //download each file
                    byte[] TempBytes = await Module_Downloader.DownloadFileBytes(NewURL, ActionTypEnum, ProgressBarRef, MultiDLFactor, MultiProgressBase);
                    Module_Downloader.SaveFileBytes(TempBytes, FileName, TempFolderPath);
                    MultiProgressBase += MultiDLFactor;

                    UgoiraConcat.AppendLine($"file '{FileName}'"); // FFmpeg wants / instead of \
                    int FrameDelay = (int)UgoiraJObject["frames"][x]["delay"];
                    UgoiraConcat.AppendLine($"duration {FrameDelay / 1000d}");
                    TotalUgoiraLength += FrameDelay;

                    await Task.Delay(500);
                }
            }
            else
            {
                if (HttpResponseMessageTemp.StatusCode == HttpStatusCode.NotFound)
                {
                    string NewURL = (string)UgoiraJObject["originalSrc"];
                    string FileName = Path.GetFileName(NewURL);

                    //download zip
                    byte[] TempBytes = await Module_Downloader.DownloadFileBytes(NewURL, ActionTypEnum, ProgressBarRef);
                    //extract zip
                    Module_Downloader.SaveFileBytes(TempBytes, FileName, TempFolderPath);

                    foreach (JToken UgoiraFrame in UgoiraJObject["frames"])
                    {
                        UgoiraConcat.AppendLine($"file {(string)UgoiraFrame["file"]}"); // FFmpeg wants / instead of \
                        UgoiraConcat.AppendLine($"duration {(int)UgoiraFrame["delay"] / 1000d}");
                        TotalUgoiraLength += (int)UgoiraFrame["delay"];
                    }
                }
                else
                {
                    throw new Exception($"HTTP error {HttpResponseMessageTemp.StatusCode}");
                }
            }

            File.WriteAllText(Path.Combine(TempFolderPath, "input.txt"), UgoiraConcat.ToString());

            return TotalUgoiraLength;
        }

        // - - - - - - - - - - - - - - - -

        internal static void UploadQueue_Ugoira2APNG(string Grab_URL, out byte[] bytes2Send, out string FileName, in string ExtraSourceURL)
        {
            //Get FileName
            string UgoiraFileName = Path.GetFileNameWithoutExtension(ExtraSourceURL);
            UgoiraFileName = UgoiraFileName.TrimEnd('0'); ; //turn "ugoira0" into "ugoira"

            //check if file exists as download before doing separate dl and conversion?

            //Make temp work folder for Ugoira job
            string TempFolderName = Path.Combine("FFMpegTemp", "Upload");
            if (Directory.Exists(TempFolderName)) Directory.Delete(TempFolderName, true);
            Directory.CreateDirectory(TempFolderName).Attributes = FileAttributes.Hidden;

            //Download the images, either originals or samples from zip
            int UgoiraDuration = DownloadUgoira(Grab_URL, ExtraSourceURL, TempFolderName, ActionType.Upload).GetAwaiter().GetResult();

            //Convert to APNG
            Module_Uploader.Report_Status("Converting Ugoira to APNG...");
            FFMpeg4Ugoira2APNG(TempFolderName, TempFolderName, UgoiraFileName, UgoiraDuration);
            Module_Uploader.Report_Status("Converting Ugoira to APNG...100%");
            Window_Main._RefHolder.UploadQueueProcess = null;

            //Read bytes for upload
            FileName = $"{UgoiraFileName}.apng";
            bytes2Send = File.ReadAllBytes(Path.Combine(TempFolderName, FileName));

            //Since this is used only for upload don't delete the folder if it exceedes the file size but fallback to WebM conversion instead.
            int TwentyMiB = 20 * 1024 * 1024;
            if (bytes2Send.Length > TwentyMiB)
            {
                return;
            }

            //Delete temp work folder
            Directory.Delete(TempFolderName, true);
        }

        internal static void UploadQueue_Ugoira2WebM(out byte[] bytes2Send, out string FileName, string UgoiraFileName)
        {
            string TempFolderName = Path.Combine("FFMpegTemp", "Upload");

            //Convert to Webm
            Module_Uploader.Report_Status("Converting Ugoira to WebM...");
            FFMpeg4Ugoira2WebM(ActionType.Upload, TempFolderName, TempFolderName, UgoiraFileName);
            Module_Uploader.Report_Status("Converting Ugoira to WebM...100%");
            Window_Main._RefHolder.UploadQueueProcess = null;

            UgoiraFileName = Path.GetFileNameWithoutExtension(UgoiraFileName);
            //Read bytes for upload
            FileName = $"{UgoiraFileName}.webm";
            bytes2Send = File.ReadAllBytes(Path.Combine(TempFolderName, FileName));

            //Delete temp work folder
            Directory.Delete(TempFolderName, true);
        }

        internal static void UploadQueue_Videos2WebM(out byte[] bytes2Send, out string FileName, in string ExtraSourceURL)
        {
            //Get FileName
            string VideoFileName = Path.GetFileName(ExtraSourceURL);
            string VideoFormat = VideoFileName.Substring(VideoFileName.LastIndexOf('.') + 1);
            VideoFileName = VideoFileName.Substring(0, VideoFileName.LastIndexOf('.'));

            //Make temp work folder for job
            string TempFolderName = Path.Combine("FFMpegTemp", "Upload");
            if (Directory.Exists(TempFolderName)) Directory.Delete(TempFolderName, true);
            Directory.CreateDirectory(TempFolderName).Attributes = FileAttributes.Hidden;

            //Download the video
            ushort RetryCount = 0;
            byte[] TempBytes = Array.Empty<byte>();
            while (RetryCount < 3)
            {
                RetryCount++;
                TempBytes = Module_Downloader.DownloadFileBytes(ExtraSourceURL, ActionType.Upload).GetAwaiter().GetResult();
                if (TempBytes.Length > 0) break;
                Thread.Sleep(500);
            }
            if (TempBytes.Length == 0)
            {
                throw new Exception("0 bytes error @Upload Video");
            }
            Module_Downloader.SaveFileBytes(TempBytes, $"{VideoFileName}.{VideoFormat}", TempFolderName);

            //Convert to Webm
            Module_Uploader.Report_Status($"Converting Video to WebM...");
            FFMpeg4Video(ActionType.Upload, TempFolderName, VideoFileName, VideoFormat, TempFolderName);
            Module_Uploader.Report_Status($"Converting Video to WebM...100%");
            Window_Main._RefHolder.UploadQueueProcess = null;

            //Read bytes for upload
            FileName = $"{VideoFileName}.webm";
            bytes2Send = File.ReadAllBytes(Path.Combine(TempFolderName, FileName));

            //Delete temp work folder
            Directory.Delete(TempFolderName, true);
        }

        // - - - - - - - - - - - - - - - -

        internal static async Task DownloadQueue_Ugoira2WebM(DownloadVE DownloadVERef)
        {
            //Get FileName
            string UgoiraFileName = Path.GetFileNameWithoutExtension(DownloadVERef._DownloadItemRef.Grab_MediaURL);
            UgoiraFileName = UgoiraFileName.TrimEnd('0'); ; //turn "ugoira0" into "ugoira"

            //Get Artist for folder name
            string PurgeArtistName = DownloadVERef._DownloadItemRef.Grab_Artist.Replace('/', '-');
            PurgeArtistName = string.Concat(PurgeArtistName.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));

            //Get Host for folder name
            string HostString = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(new Uri(DownloadVERef._DownloadItemRef.Grab_PageURL).Host.Split('.')[1]);

            //Make Download path and folder
            string FolderPath = Path.Combine(AppSettings.Download_FolderLocation, HostString, PurgeArtistName);
            Directory.CreateDirectory(FolderPath);

            //Check if file already exist, if it does skip the task
            string FullFilePath = Path.Combine(FolderPath, $"{UgoiraFileName}.webm");
            if (File.Exists(FullFilePath))
            {
                //Report task finish
                Module_Converter.Report_Info($"Ugoira WebM already exists, skipped coverting {DownloadVERef._DownloadItemRef.Grab_MediaURL}");
                if (DownloadVERef._DownloadItemRef.MediaItemRef != null)
                {
                    DownloadVERef._DownloadItemRef.MediaItemRef.DL_FilePath = FullFilePath;
                }
                DownloadQueue_ConvertFinished(DownloadVERef, FullFilePath);
                return;
            }

            //Make temp work folder for Ugoira job
            string TempFolderName = Path.Combine("FFMpegTemp", UgoiraFileName);
            if (Directory.Exists(TempFolderName)) Directory.Delete(TempFolderName, true);
            Directory.CreateDirectory(TempFolderName).Attributes = FileAttributes.Hidden;

            //Download the images, either originals or samples from zip
            int UgoiraDuration = await DownloadUgoira(DownloadVERef._DownloadItemRef.Grab_PageURL, DownloadVERef._DownloadItemRef.Grab_MediaURL, TempFolderName, ActionType.Download, DownloadVERef.DownloadProgress);

            //check for 0 error here?

            //Convert to Webm
            FFMpeg4Ugoira2WebM(ActionType.Download, TempFolderName, FolderPath, UgoiraFileName, DownloadVERef.ConversionProgress);

            //Delete temp work folder
            Directory.Delete(TempFolderName, true);

            //Report task finish
            Module_Converter.Report_Info($"Converted Ugoira from: {DownloadVERef._DownloadItemRef.Grab_MediaURL} to WebM");
            //Form_Loader._FormReference.BeginInvoke(new Action(() =>
            //{
            //    if (Form_Preview._FormReference != null && Form_Preview._FormReference.IsHandleCreated && ReferenceEquals(Form_Preview._FormReference.Preview_RowHolder, GridDataRow))
            //    {
            //        Form_Preview._FormReference.PB_ViewFile.Visible = true;
            //    }
            //}));
            if (DownloadVERef._DownloadItemRef.MediaItemRef != null)
            {
                DownloadVERef._DownloadItemRef.MediaItemRef.UP_Tags += $"{(UgoiraDuration < 30000 ? " short_playtime" : " long_playtime")} animated";
                DownloadVERef._DownloadItemRef.MediaItemRef.DL_FilePath = FullFilePath;
            }
            DownloadQueue_ConvertFinished(DownloadVERef, FullFilePath);
        }

        internal static async Task DownloadQueue_Video2WebM(DownloadVE DownloadVERef)
        {
            //Get FileName
            string VideoFileName = Module_Downloader.MediaFile_GetFileNameOnly(DownloadVERef._DownloadItemRef.Grab_MediaURL, DownloadVERef._DownloadItemRef.Grab_MediaFormat);
            string VideoFormat = DownloadVERef._DownloadItemRef.Grab_MediaFormat;

            //Get Artist for folder name
            string PurgeArtistName = DownloadVERef._DownloadItemRef.Grab_Artist.Replace('/', '-');
            PurgeArtistName = string.Concat(PurgeArtistName.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));

            //Get Host for folder name
            string HostString = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(new Uri(DownloadVERef._DownloadItemRef.Grab_PageURL).Host.Split('.')[1]);

            //Make Download path and folder
            string FolderPath = Path.Combine(AppSettings.Download_FolderLocation, HostString, PurgeArtistName);
            Directory.CreateDirectory(FolderPath);

            //Check if file already exist, if it does skip the task
            string FullFilePath = Path.Combine(FolderPath, $"{VideoFileName}.webm");
            if (File.Exists(FullFilePath))
            {
                //Report task finish
                Module_Converter.Report_Info($"Video WebM already exists, skipped coverting {DownloadVERef._DownloadItemRef.Grab_MediaURL}");
                if (DownloadVERef._DownloadItemRef.MediaItemRef != null)
                {
                    DownloadVERef._DownloadItemRef.MediaItemRef.DL_FilePath = FullFilePath;
                }
                DownloadQueue_ConvertFinished(DownloadVERef, FullFilePath);
            }

            //Make temp work folder for job
            string TempFolderName = Path.Combine("FFMpegTemp", VideoFileName);
            if (Directory.Exists(TempFolderName)) Directory.Delete(TempFolderName, true);
            Directory.CreateDirectory(TempFolderName).Attributes = FileAttributes.Hidden;

            //Download the video
            byte[] TempBytes = await Module_Downloader.DownloadFileBytes(DownloadVERef._DownloadItemRef.Grab_MediaURL, ActionType.Download, DownloadVERef.DownloadProgress);
            Module_Downloader.SaveFileBytes(TempBytes, VideoFileName, FolderPath);

            //Convert to Webm
            string OriginalVideoFormat = DownloadVERef._DownloadItemRef.Grab_MediaFormat;
            FFMpeg4Video(ActionType.Download, TempFolderName, VideoFileName, OriginalVideoFormat, FolderPath, DownloadVERef.ConversionProgress);


            //Delete temp work folder
            Directory.Delete(TempFolderName, true);

            //Report task finish
            Module_Converter.Report_Info($"Converted Video from: {DownloadVERef._DownloadItemRef.Grab_MediaURL} to WebM");
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
            string VideoFileName = Path.GetFileNameWithoutExtension(VideoPath);

            string FullFolderPath = Path.GetDirectoryName(VideoPath);
            string FullFilePath = Path.Combine(FullFolderPath, $"{VideoFileName}.webm");

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
                FFmpeg.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                FFmpeg.StartInfo.CreateNoWindow = false;
                FFmpeg.StartInfo.UseShellExecute = false;
                FFmpeg.StartInfo.RedirectStandardError = false;
                FFmpeg.StartInfo.RedirectStandardOutput = false;
                FFmpeg.StartInfo.Arguments = $"-hide_banner -loglevel info -y -i \"{VideoPath}\" -c:v libvpx-vp9 -pix_fmt yuv420p -crf 24 -b:v 0 -c:a libopus -b:a 192k -cpu-used 2 -row-mt 1 \"{FullFilePath}\"";
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