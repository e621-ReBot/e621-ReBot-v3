using e621_ReBot_v3.CustomControls;
using e621_ReBot_v3.Modules.Converter;
using e621_ReBot_v3.Modules.Uploader;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Handlers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace e621_ReBot_v3.Modules
{
    internal static class Module_Uploader
    {
        static Module_Uploader()
        {
            Upload_BGW.DoWork += UploadBGW_StartWork;
            Upload_BGW.RunWorkerCompleted += UploadBGW_WorkDone;
            _UploadTimer.Tick += UploadTimer_Tick;
            _UploadDisableTimer.Tick += UploadDisableTimer_Tick;
            e621_HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AppSettings.AppName);
        }

        internal static void Media2BigCheck(MediaItem MediaItemRef)
        {
            //https://e621.net/wiki_pages/howto:sites_and_sources
            if (MediaItemRef.Grid_MediaByteLength == null)
            {
                string FileType = MediaItemRef.Grid_MediaFormat;
                uint? byteLength = MediaItemRef.Grid_MediaByteLength;
                switch (FileType)
                {
                    case "gif":
                        {
                            if (byteLength > 20971520) // 20 * 1024 * 1024 = 20MB Limit
                            {
                                MediaItemRef.Grid_MediaTooBig = true;
                                return;
                            }
                            break;
                        }

                    case "jpg":
                    case "jpeg":
                    case "png":
                    case "webp":
                        {
                            if (byteLength > 104857600) // 100 * 1024 * 1024 = 100MB Limit
                            {
                                MediaItemRef.Grid_MediaTooBig = true;
                                return;
                            }
                            break;
                        }
                }
            }
            if (MediaItemRef.Grid_MediaWidth != null)
            {
                int BiggerR = Math.Max((int)MediaItemRef.Grid_MediaWidth, (int)MediaItemRef.Grid_MediaHeight);
                if (BiggerR > 15000)
                {
                    MediaItemRef.Grid_MediaTooBig = true;
                }
            }
            MediaItemRef.Grid_MediaTooBig = false;
        }

        internal static bool Media2Big4User(MediaItem MediaItemRef, bool ShowMsgBox = true)
        {
            if (MediaItemRef.Grid_MediaTooBig == true)
            {
                Window ParentWindow = Window_Preview._RefHolder != null ? Window_Preview._RefHolder : Window_Main._RefHolder;
                if (ShowMsgBox) MessageBox.Show(ParentWindow, "File is too big too be uploaded.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            else
            {
                return false;
            }
        }

        internal static MediaItemList _2Upload_MediaItems = new MediaItemList();
        internal static void UploadButtonClick(bool CurrentPageOnly = false)
        {
            int StartIndex = CurrentPageOnly ? Window_Main._RefHolder.Grid_ItemStartIndex : 0;
            int EndIndex = (CurrentPageOnly ? StartIndex + Window_Main._RefHolder.Grid_GridVEPanel.Children.Count : Module_Grabber._Grabbed_MediaItems.Count) - 1;

            MediaItem? MediaItemTemp;
            for (int i = StartIndex; i <= EndIndex; i++)
            {
                MediaItemTemp = Module_Grabber._Grabbed_MediaItems[i];
                if (MediaItemTemp.UP_Queued && MediaItemTemp.UP_Tags.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 16)
                {
                    MessageBoxResult MessageBoxResultTemp = Window_Main._RefHolder.Dispatcher.Invoke(() => { return MessageBox.Show(Window_Main._RefHolder, "There is media with insufficent number of tags selected for upload. Are you sure you want to proceed?", "e621 ReBot - Uploader", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No); });
                    if (MessageBoxResultTemp == MessageBoxResult.No) return;
                    break;
                }
            }

            int UploadAdditionCounter = 0;
            lock (_2Upload_MediaItems)
            {
                for (int i = StartIndex; i <= EndIndex; i++)
                {
                    MediaItemTemp = Module_Grabber._Grabbed_MediaItems[i];
                    if (MediaItemTemp.Grid_MediaTooBig == null) Media2BigCheck(MediaItemTemp);
                    if (MediaItemTemp.UP_UploadedID == null && MediaItemTemp.UP_Queued && MediaItemTemp.Grid_MediaTooBig == false && !_2Upload_MediaItems.ContainsURL(MediaItemTemp.Grab_MediaURL))
                    {
                        _2Upload_MediaItems.Add(MediaItemTemp);
                        UploadTreeView_CreateJob(MediaItemTemp);
                        UploadAdditionCounter++;
                    }
                }
            }
            Window_Main._RefHolder.GBU_Change.Text = $"+{UploadAdditionCounter}";
            Window_Main._RefHolder.GBU_Change.IsEnabled = true; //Makes it local, so animation no longer work becase it takes priority over style
            Window_Main._RefHolder.GBU_Change.IsEnabled = false;
        }

        internal static void UploadTreeView_CreateJob(MediaItem MediaItemRef)
        {
            TreeViewItem? TreeViewItemParent = new TreeViewItem
            {
                Header = MediaItemRef.Grab_MediaURL,
                Tag = MediaItemRef,
            };

            if (Module_Credit.CanReplace && MediaItemRef.UP_Inferior_ID != null)
            {
                TreeViewItemParent.Items.Add(new TreeViewItem { Header = "Replace Inferior" });
            }
            else
            {
                TreeViewItemParent.Items.Add(new TreeViewItem { Header = "Upload" });
                if (MediaItemRef.UP_Inferior_HasNotes != null) TreeViewItemParent.Items.Add(new TreeViewItem { Header = "Copy Notes" });
                if (MediaItemRef.UP_Inferior_Children != null) TreeViewItemParent.Items.Add(new TreeViewItem { Header = "Move Children" });
                if (MediaItemRef.UP_Inferior_ID != null) TreeViewItemParent.Items.Add(new TreeViewItem { Header = "Flag Inferior" });
            }
            Window_Main._RefHolder.Upload_TreeView.Items.Add(TreeViewItemParent);
            Window_Main._RefHolder.Upload_CheckBox.Content = $"Uploader{(Window_Main._RefHolder.Upload_TreeView.Items.Count > 0 ? $" ({Window_Main._RefHolder.Upload_TreeView.Items.Count})" : null)}";
        }

        internal static void ReverseUploadTreeView()
        {
            lock (_2Upload_MediaItems)
            {
                _2Upload_MediaItems.Reverse();
            }
            List<TreeViewItem> ReverseList = new List<TreeViewItem>();
            foreach (TreeViewItem TreeViewItemTemp in Window_Main._RefHolder.Upload_TreeView.Items)
            {
                ReverseList.Insert(0, TreeViewItemTemp);
            }
            Window_Main._RefHolder.Upload_TreeView.Items.Clear();
            foreach (TreeViewItem TreeViewItemTemp in ReverseList)
            {
                Window_Main._RefHolder.Upload_TreeView.Items.Add(TreeViewItemTemp);
            }
        }

        internal static void Report_Info(string InfoMessage)
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.Upload_InfoTextBox.Text = $"{DateTime.Now.ToLongTimeString()}, {InfoMessage}\n{Window_Main._RefHolder.Upload_InfoTextBox.Text}";
            });
        }

        internal static void Report_Status(string StatusMessage)
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.Upload_StatusTextBlock.Text = $"Status: {StatusMessage}";
            });
        }

        internal static void Report_WorkingOn(string? WorkURL)
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.Upload_WorkingOnTextBlock.Text = $"Working on: {WorkURL ?? "Nothing."}";
                Window_Main._RefHolder.Upload_ProgressCanvas.Visibility = WorkURL == null ? Visibility.Hidden : Visibility.Visible;
            });
        }

        internal static void Report_Error(string StatusMessage, string TitleMessage)
        {
            Window_Main._RefHolder.Dispatcher.Invoke(() =>
            {
                Window_Main._RefHolder.Upload_CheckBox.IsChecked = false;
                MessageBox.Show(Window_Main._RefHolder, StatusMessage, TitleMessage, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        // - - - - - - - - - - - - - - - -

        internal static readonly BackgroundWorker Upload_BGW = new BackgroundWorker();
        internal static DispatcherTimer _UploadTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private static void UploadTimer_Tick(object? sender, EventArgs e)
        {
            if (Window_Main._RefHolder.Upload_CheckBox.IsChecked == true && Window_Main._RefHolder.Upload_TreeView.HasItems && !Upload_BGW.IsBusy)
            {
                TreeViewItem ParentJobNode = (TreeViewItem)Window_Main._RefHolder.Upload_TreeView.Items[0];
                if (ParentJobNode.ContextMenu != null) ParentJobNode.ContextMenu.IsOpen = false;

                TreeViewItem JobTreeViewItem;
                string TaskName = string.Empty;
                bool NoTask = true;
                for (int i = 0; i < ParentJobNode.Items.Count; i++)
                {
                    JobTreeViewItem = (TreeViewItem)ParentJobNode.Items[i];
                    TaskName = JobTreeViewItem.Header.ToString().Replace(" ", null);
                    switch (TaskName)
                    {
                        case "Upload":
                        case "ReplaceInferior":
                            {
                                if (Module_Credit.Credit_UploadHourly == 0 || Module_Credit.Credit_UploadTotal == 0)
                                {
                                    Module_Credit.Credit_UpdateDisplay();
                                    break;
                                }
                                NoTask = false;
                                break;
                            }
                        //see what to do when upload fails
                        case "CopyNotes":
                            {
                                if (Module_Credit.Credit_Notes == 0)
                                {
                                    continue;
                                }
                                NoTask = false;
                                break;
                            }
                        case "MoveChildren":
                            {
                                //could loop here, should probably handle it
                                NoTask = false;
                                break;
                            }
                        case "FlagInferior":
                            {
                                if (Module_Credit.Credit_Flags == 0)
                                {
                                    continue;
                                }
                                NoTask = false;
                                break;
                            }
                    }
                    goto DoTask;
                }

            DoTask:
                if (NoTask)
                {
                    //disable upload or put into retry here
                    //Module_RetryQueue.MoveItem2RetryQueue(_2Upload_MediaItems[0]);
                }
                else
                {
                    if (string.IsNullOrEmpty(TaskName)) return;

                    UploadMediaItemHolder = _2Upload_MediaItems[0];
                    int NodeCount = Window_Main._RefHolder.Upload_TreeView.Items.Count;
                    if (ParentJobNode.Items.Count == 1)
                    {
                        ParentJobNode.Visibility = Visibility.Collapsed;
                        NodeCount--;
                    }
                    else
                    {
                        JobTreeViewItem = (TreeViewItem)ParentJobNode.Items[0];
                        JobTreeViewItem.Visibility = Visibility.Collapsed;
                    }
                    Window_Main._RefHolder.Upload_CheckBox.Content = $"Uploader{(NodeCount > 0 ? $" ({NodeCount})" : null)}";
                    Upload_BGW.RunWorkerAsync(TaskName);
                    Report_WorkingOn(UploadMediaItemHolder.Grab_MediaURL);
                }
            }
        }

        private static MediaItem? UploadMediaItemHolder;
        private static void UploadBGW_StartWork(object? sender, DoWorkEventArgs e)
        {
            //typeof(Module_Uploader).GetMethod($"UploadTask_{e.Argument}", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { _2Upload_MediaItems[0] });
            if (e.Argument != null) Thread.Sleep(500);

            switch (e.Argument.ToString())
            {
                case "Upload":
                    {
                        UploadTask_Upload(UploadMediaItemHolder);
                        break;
                    }
                case "ReplaceInferior":
                    {
                        UploadTask_ReplaceInferior(UploadMediaItemHolder);
                        break;
                    }
                case "CopyNotes":
                    {
                        UploadTask_CopyNotes(UploadMediaItemHolder);
                        break;
                    }
                case "MoveChildren":
                    {
                        UploadTask_MoveChildren(UploadMediaItemHolder);
                        break;
                    }
                case "FlagInferior":
                    {
                        UploadTask_FlagInferior(UploadMediaItemHolder);
                        break;
                    }
            }
        }

        private static bool FailedUploadTask;
        private static void UploadBGW_WorkDone(object? sender, RunWorkerCompletedEventArgs e)
        {
            Report_WorkingOn(null);
            Report_Status("Waiting...");
            Module_Credit.Credit_UpdateDisplay();

            TreeViewItem ParentJobNode = (TreeViewItem)Window_Main._RefHolder.Upload_TreeView.Items[0];
            TreeViewItem JobTreeViewItem = (TreeViewItem)ParentJobNode.Items[0];
            if (FailedUploadTask)
            {
                Window_Main._RefHolder.Upload_CheckBox.IsChecked = false; //already disabled in report error
                Module_RetryQueue.MoveItem2RetryQueue(_2Upload_MediaItems[0]);
                if (ParentJobNode.Items.Count == 1)
                {
                    Window_Main._RefHolder.Upload_TreeView.Items.RemoveAt(0);
                }
                else
                {
                    ParentJobNode.Items.RemoveAt(0);
                }
                //if (ParentJobNode.Items.Count == 1)
                //{
                //    ParentJobNode.Visibility = Visibility.Visible;
                //}
                //else
                //{
                //    JobTreeViewItem.Visibility = Visibility.Visible;
                //}
                FailedUploadTask = false;
            }
            else
            {
                if (ParentJobNode.Items.Count == 1)
                {
                    Window_Main._RefHolder.Upload_TreeView.Items.RemoveAt(0);
                    lock (_2Upload_MediaItems)
                    {
                        _2Upload_MediaItems.RemoveAt(0);
                    }
                }
                else
                {
                    ParentJobNode.Items.RemoveAt(0);
                }
            }
            int NodeCount = Window_Main._RefHolder.Upload_TreeView.Items.Count;
            Window_Main._RefHolder.Upload_CheckBox.Content = $"Uploader{(NodeCount > 0 ? $" ({NodeCount})" : null)}";

            //TreeView TreeViewUploadRef = Window_Main._RefHolder.Upload_TreeView;
            //if (TreeViewUploadRef.Items.Count > 0)
            //{
            //    // To finish notes/flags even when there's no upload credit left, instead of waiting for upload credit
            //    TreeViewItem FirstJobTreeViewItem = (TreeViewItem)((TreeViewItem)TreeViewUploadRef.Items[0]).Items[0];
            //    string TaskName = FirstJobTreeViewItem.Header.ToString().Replace(" ", null);
            //    if (_UploadDisableTimer.IsEnabled && !TaskName.Equals("Upload"))
            //    {                
            //        Upload_BGW.RunWorkerAsync(TaskName);
            //        return;
            //    }
            //}

            UploadMediaItemHolder = null;
        }

        private static readonly HttpClientHandler e621_HttpClientHandler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
        private static readonly ProgressMessageHandler e621_ProgressMessageHandler = new ProgressMessageHandler(e621_HttpClientHandler);
        private static readonly HttpClient e621_HttpClient = new HttpClient(e621_ProgressMessageHandler) { Timeout = TimeSpan.FromSeconds(600) };
        internal static void E621UploadRequest(string SendAddress, string SendMethod, Dictionary<string, string> SendDictionary, out HttpResponseMessage? e621HttpResponseMessage, out string? e621StringResponse, in byte[]? bytes2Send = null)
        {
            string MPF_Boundary = $"----e621 ReBot----";
            MultipartFormDataContent EncodedContent = new MultipartFormDataContent(MPF_Boundary);
            foreach (KeyValuePair<string, string> DataPair in SendDictionary)
            {
                if (DataPair.Key.EndsWith("file]"))
                {
                    if (bytes2Send == null) throw new ApplicationException("bytes2Send is null somehow!");
                    EncodedContent.Add(new ByteArrayContent(bytes2Send), name: DataPair.Key, fileName: DataPair.Value);
                }
                else
                {
                    EncodedContent.Add(new StringContent(DataPair.Value), name: DataPair.Key);
                }
            }
            //string PostText = EncodedContent.ReadAsStringAsync().Result.ToString();

            using (HttpRequestMessage HttpRequestMessageTemp = new HttpRequestMessage(new HttpMethod(SendMethod), SendAddress))
            {
                HttpRequestMessageTemp.Content = EncodedContent;

                if (bytes2Send != null)
                {
                    Report_Status("Uploading Media...0%");
                    e621_ProgressMessageHandler.HttpSendProgress += E621_ProgressMessageHandler_HttpSendProgress;
                }
                HttpResponseMessage HttpResponseMessageTemp = e621_HttpClient.Send(HttpRequestMessageTemp);
                e621StringResponse = HttpResponseMessageTemp.Content.ReadAsStringAsync().Result;
                e621HttpResponseMessage = HttpResponseMessageTemp;
            }
            if (bytes2Send != null) e621_ProgressMessageHandler.HttpSendProgress -= E621_ProgressMessageHandler_HttpSendProgress;
        }

        private static void E621_ProgressMessageHandler_HttpSendProgress(object? sender, HttpProgressEventArgs e)
        {
            Report_Status($"Uploading Media...{e.ProgressPercentage}%");
        }

        private static void SuccessfulUpload_DisplayUpdates(MediaItem MediaItemRef, string PostID, bool Save2DB = true)
        {
            MediaItemRef.UP_UploadedID = PostID;
            if (Save2DB) AppSettings.MediaRecord_Add(MediaItemRef);

            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                GridVE GridVETemp = Module_Grabber.IsVisibleInGrid(MediaItemRef);
                if (GridVETemp == null)
                {
                    MediaItemRef.UP_Queued = false;
                    Window_Main._RefHolder.UploadCounterChange(-1);
                }
                else
                {
                    GridVETemp.cUpload_CheckBox.IsChecked = false;
                    GridVETemp.cUpload_CheckBox.IsEnabled = false;
                    GridVETemp.IsUploaded_SetText(MediaItemRef.UP_UploadedID);
                }

                if (Window_Preview._RefHolder != null && ReferenceEquals(Window_Preview._RefHolder.MediaItemHolder, MediaItemRef))
                {
                    Window_Preview._RefHolder.SetUPColour();
                    Window_Preview._RefHolder.AlreadyUploaded_Label.Text = $"#{PostID}";
                }
            });
        }

        // - - - - - - - - - - - - - - - -

        private static void UploadTask_Upload(MediaItem MediaItemRef)
        {
            if (!Module_e621APIController.APIEnabled) return;

            Report_Status("Uploading...");

            string Upload_Sources = MediaItemRef.Grab_PageURL;
            if (MediaItemRef.UP_Inferior_Sources != null)
            {
                foreach (string InferiorSource in MediaItemRef.UP_Inferior_Sources)
                {
                    if (!Upload_Sources.Contains(InferiorSource)) Upload_Sources += $"%0A{InferiorSource}";
                }
            }

            string? Upload_Description = MediaItemRef.Grab_TextBody == null ? $"[code]{MediaItemRef.Grab_Title}[/code]" : $"[section,expanded={MediaItemRef.Grab_Title}]\n{MediaItemRef.Grab_TextBody}\n[/section]";
            string Upload_DescriptionNoExtras = Upload_Description;

            Dictionary<string, string> POST_Dictionary = new Dictionary<string, string>();

            bool isByteUpload = false;
            string UploadedURL4Report = MediaItemRef.Grab_MediaURL;
            switch (MediaItemRef.Grid_MediaFormat)
            {
                case "ugoira":
                    {
                        POST_Dictionary.Add("upload[file]", MediaItemRef.Grab_MediaURL);
                        string UgoiraFileName = MediaItemRef.Grab_MediaURL.Substring(MediaItemRef.Grab_MediaURL.LastIndexOf('/') + 1);
                        UploadedURL4Report = $"{UgoiraFileName.Substring(0, UgoiraFileName.IndexOf("_ugoira0."))}_ugoira1920x1080.webm, converted from {MediaItemRef.Grab_PageURL}";
                        Upload_Description += "\nConverted from Ugoira using FFmpeg: -c:v libvpx-vp9 -pix_fmt yuv420p -lossless 1 -an";
                        isByteUpload = true;
                        break;
                    }

                case "mp4":
                case "swf":
                    {
                        POST_Dictionary.Add("upload[file]", MediaItemRef.Grab_MediaURL);
                        string VideoFileName = MediaItemRef.Grab_MediaURL.Remove(MediaItemRef.Grab_MediaURL.Length - 4);
                        VideoFileName = $"{VideoFileName.Substring(VideoFileName.LastIndexOf('/') + 1)}.webm";
                        UploadedURL4Report = $"{VideoFileName}, converted from {MediaItemRef.Grab_PageURL}";
                        Upload_Description += "\nConverted using FFmpeg: -c:v libvpx-vp9 -pix_fmt yuv420p -b:a 192k -crf 24 -b:v 4M realtime -cpu-used 2";
                        isByteUpload = true;
                        break;
                    }

                default:
                    {
                        if (MediaItemRef.UP_IsWhitelisted)
                        {
                            POST_Dictionary.Add("upload[direct_url]", MediaItemRef.Grab_MediaURL);
                        }
                        else
                        {
                            POST_Dictionary.Add("upload[file]", MediaItemRef.Grab_MediaURL);
                            isByteUpload = true;
                        }
                        break;
                    }
            }

            byte[]? bytes2Send = null;
            if (isByteUpload)
            {
                if (MediaItemRef.DL_FilePath != null && File.Exists(MediaItemRef.DL_FilePath))
                {
                    string CachedFileName = MediaItemRef.DL_FilePath;
                    CachedFileName = CachedFileName.Substring(CachedFileName.LastIndexOf(@"\") + 1);
                    POST_Dictionary["upload[file]"] = CachedFileName;
                    bytes2Send = File.ReadAllBytes(MediaItemRef.DL_FilePath);
                }
                else
                {
                    string FileName = Module_Downloader.MediaFile_GetFileNameOnly(MediaItemRef.Grab_MediaURL);
                    string ExtraSourceURL = MediaItemRef.Grab_MediaURL;
                    switch (MediaItemRef.Grid_MediaFormat)
                    {
                        case "ugoira":
                            {
                                Module_FFMpeg.UploadQueue_Ugoira2WebM(MediaItemRef.Grab_PageURL, out bytes2Send, out FileName, out ExtraSourceURL);
                                break;
                            }

                        case "mp4":
                        case "swf":
                            {
                                Module_FFMpeg.UploadQueue_Videos2WebM(out bytes2Send, out FileName, in ExtraSourceURL);
                                break;
                            }

                        default:
                            {
                                bytes2Send = Module_Downloader.DownloadFileBytes(MediaItemRef.Grab_MediaURL, ActionType.Upload);
                                break;
                            }
                    }
                    POST_Dictionary["upload[file]"] = FileName;
                    Upload_Sources = $"{ExtraSourceURL}%0A{Upload_Sources}";
                }
            }

            if (MediaItemRef.UP_Inferior_Description != null)
            {
                string Inferior_Description = MediaItemRef.UP_Inferior_Description;
                if (!Inferior_Description.Contains(Upload_DescriptionNoExtras) && !Inferior_Description.Contains(MediaItemRef.Grab_TextBody))
                {
                    Upload_Description += $"\n - - - - - \n{Inferior_Description}";
                }
            }

            string PostTags = MediaItemRef.UP_Tags;
            if (MediaItemRef.UP_ParentMediaItem != null && MediaItemRef.UP_ParentMediaItem.UP_UploadedID != null)
            {
                if (PostTags.Contains("parent:"))
                {
                    string ParentTag2Remove = PostTags.Substring(PostTags.IndexOf("parent:"));
                    ParentTag2Remove = ParentTag2Remove.Substring(0, ParentTag2Remove.IndexOf(' '));
                    PostTags = PostTags.Replace(ParentTag2Remove, $"parent:{MediaItemRef.UP_ParentMediaItem.UP_UploadedID}");
                }
            }

            // - - - - - - - - - - - - - - - -

            POST_Dictionary.Add("upload[source]", Upload_Sources);
            POST_Dictionary.Add("upload[rating]", MediaItemRef.UP_Rating.ToLower());
            POST_Dictionary.Add("upload[tag_string]", PostTags);
            POST_Dictionary.Add("upload[description]", Upload_Description);
            POST_Dictionary.Add("login", AppSettings.UserName);
            POST_Dictionary.Add("api_key", Module_Cryptor.Decrypt(AppSettings.APIKey));

            HttpResponseMessage? e621HttpResponseMessage;
            string? e621StringResponse = string.Empty;
            E621UploadRequest("https://e621.net/uploads.json", "POST", POST_Dictionary, out e621HttpResponseMessage, out e621StringResponse, in bytes2Send);
            switch (e621HttpResponseMessage.StatusCode)
            {
                case HttpStatusCode.OK:
                    {
                        JObject Upload_ReponseData = JObject.Parse(e621StringResponse);
                        Module_Credit.Credit_UploadHourly--;
                        if (Module_Credit.UserLevel < 30)
                        {
                            Module_Credit.Credit_UploadTotal--;
                            Module_Credit.Timestamps_Upload.Add(DateTime.UtcNow.AddHours(1));
                        }
                        SuccessfulUpload_DisplayUpdates(MediaItemRef, Upload_ReponseData["post_id"].Value<string>());
                        Report_Info($"Uploaded: {UploadedURL4Report}");
                        break;
                    }

                case HttpStatusCode.PreconditionFailed:
                    {
                        //{{"success": false,"reason": "invalid","message": "error: ActiveRecord::RecordInvalid - Validation failed: Md5 duplicate of pending replacement on post #0123456"}}

                        JObject Upload_ReponseData = JObject.Parse(e621StringResponse);
                        if (Upload_ReponseData["reason"].Value<string>().Equals("duplicate"))
                        {
                            SuccessfulUpload_DisplayUpdates(MediaItemRef, Upload_ReponseData["post_id"].Value<string>());
                            Report_Info($"Error uploading: {UploadedURL4Report}, duplicate of #{Upload_ReponseData["post_id"].Value<string>()}");
                        }
                        else
                        {
                            FailedUploadTask = true;
                            Report_Error($"Some other Error - {e621HttpResponseMessage.StatusCode}\n{e621StringResponse}", "e621 ReBot - Upload");
                        }
                        break;
                    }

                default:
                    {
                        FailedUploadTask = true;
                        Report_Info($"Error uploading: {UploadedURL4Report}");
                        Report_Error($"Some other Error - {e621HttpResponseMessage.StatusCode}\n{e621StringResponse}", "e621 ReBot - Upload");
                        break;
                    }
            }
            e621HttpResponseMessage.Dispose();
        }

        private static void UploadTask_ReplaceInferior(MediaItem MediaItemRef)
        {
            if (!Module_e621APIController.APIEnabled) return;

            Report_Status("Flagging for replacement...");

            Dictionary<string, string> POST_Dictionary = new Dictionary<string, string>();
            bool isByteUpload = false;

            switch (MediaItemRef.Grid_MediaFormat)
            {
                case "ugoira":
                case "mp4":
                case "swf":
                    {
                        isByteUpload = true;
                        break;
                    }

                default:
                    {
                        if (MediaItemRef.UP_IsWhitelisted)
                        {
                            POST_Dictionary.Add("post_replacement[replacement_url]", MediaItemRef.Grab_MediaURL);
                        }
                        else
                        {
                            isByteUpload = true;
                        }
                        break;
                    }
            }

            byte[]? bytes2Send = null;
            if (isByteUpload)
            {
                if (MediaItemRef.DL_FilePath != null && File.Exists(MediaItemRef.DL_FilePath))
                {
                    string CachedFileName = MediaItemRef.DL_FilePath;
                    CachedFileName = CachedFileName.Substring(CachedFileName.LastIndexOf('\\') + 1);
                    POST_Dictionary.Add("post_replacement[replacement_file]", CachedFileName);
                    bytes2Send = File.ReadAllBytes(MediaItemRef.DL_FilePath);
                }
                else
                {
                    string FileName = Module_Downloader.MediaFile_GetFileNameOnly(MediaItemRef.Grab_MediaURL);
                    string ExtraSourceURL = MediaItemRef.Grab_MediaURL;
                    switch (MediaItemRef.Grid_MediaFormat)
                    {
                        case "ugoira":
                            {
                                Module_FFMpeg.UploadQueue_Ugoira2WebM(MediaItemRef.Grab_PageURL, out bytes2Send, out FileName, out ExtraSourceURL);
                                break;
                            }

                        case "mp4":
                        case "swf":
                            {
                                Module_FFMpeg.UploadQueue_Videos2WebM(out bytes2Send, out FileName, in ExtraSourceURL);
                                break;
                            }

                        default:
                            {
                                bytes2Send = Module_Downloader.DownloadFileBytes(MediaItemRef.Grab_MediaURL, ActionType.Upload);
                                break;
                            }
                    }
                    POST_Dictionary.Add("post_replacement[replacement_file]", FileName);
                }
            }

            // - - - - - - - - - - - - - - - -

            POST_Dictionary.Add("post_replacement[source]", MediaItemRef.Grab_PageURL);
            POST_Dictionary.Add("post_replacement[reason]", "Superior version");
            POST_Dictionary.Add("login", AppSettings.UserName);
            POST_Dictionary.Add("api_key", Module_Cryptor.Decrypt(AppSettings.APIKey));

            HttpResponseMessage? e621HttpResponseMessage;
            string? e621StringResponse;
            E621UploadRequest($"https://e621.net/post_replacements.json?post_id={MediaItemRef.UP_Inferior_ID}", "POST", POST_Dictionary, out e621HttpResponseMessage, out e621StringResponse, in bytes2Send);
            switch (e621HttpResponseMessage.StatusCode)
            {
                case HttpStatusCode.OK:
                    {
                        //JObject Upload_ReponseData = JObject.Parse(e621StringResponse);
                        Module_Credit.Credit_UploadHourly--;
                        if (Module_Credit.UserLevel < 30)
                        {
                            Module_Credit.Credit_UploadTotal--;
                            Module_Credit.Timestamps_Upload.Add(DateTime.UtcNow.AddHours(1));
                        }
                        SuccessfulUpload_DisplayUpdates(MediaItemRef, MediaItemRef.UP_Inferior_ID);
                        Report_Info($"Flagged #{MediaItemRef.UP_Inferior_ID} for replacement as inferior of @{MediaItemRef.Grab_MediaURL}");
                        break;
                    }

                case HttpStatusCode.PreconditionFailed:
                    {
                        JObject Upload_ReponseData = JObject.Parse(e621StringResponse);
                        string ResponseMessage = Upload_ReponseData["message"].Value<string>();
                        if (ResponseMessage.Contains("duplicate"))
                        {
                            string PostID = ResponseMessage.Substring(ResponseMessage.IndexOf('#') + 1);
                            if (PostID.Contains(';')) PostID = PostID.Substring(0, PostID.IndexOf(';'));
                            SuccessfulUpload_DisplayUpdates(MediaItemRef, PostID);
                            Report_Info($"Error uploading: {MediaItemRef.Grab_MediaURL}, duplicate of pending replacement on #{PostID}");
                        }
                        else
                        {
                            FailedUploadTask = true;
                            Report_Error($"Some other Error - {e621HttpResponseMessage.StatusCode}\n{e621StringResponse}", "e621 ReBot - Replace Inferior");
                        }
                        break;
                    }

                default:
                    {
                        FailedUploadTask = true;
                        Report_Info($"Error uploading: {MediaItemRef.Grab_MediaURL}");
                        Report_Error($"Some other Error - {e621HttpResponseMessage.StatusCode}\n{e621StringResponse}", "e621 ReBot - Replace Inferior");
                        break;
                    }
            }
            e621HttpResponseMessage.Dispose();
        }

        private static async void UploadTask_CopyNotes(MediaItem MediaItemRef)
        {
            if (!Module_e621APIController.APIEnabled) return;

            Report_Status("Getting Notes data...");

            Task<string?> RunTaskFirst = new Task<string?>(() => Module_e621Data.DataDownload($"https://e621.net/notes.json?search[post_id]={MediaItemRef.UP_Inferior_ID}"));
            lock (Module_e621APIController.UserTasks)
            {
                Module_e621APIController.UserTasks.Add(RunTaskFirst);
            }

            string? JSON_NoteData = await RunTaskFirst;
            if (string.IsNullOrEmpty(JSON_NoteData) || JSON_NoteData.StartsWith('ⓔ') || JSON_NoteData.Length < 32) return;

            JArray NoteList = JArray.Parse(JSON_NoteData);
            foreach (JObject Note in NoteList.Reverse())
            {
                if (!Note["is_active"].Value<bool>())
                {
                    NoteList.Remove(Note);
                }
            }

            float? RatioData = MediaItemRef.UP_Inferior_NoteSizeRatio;
            for (int i = 0; i < NoteList.Count; i++)
            {
                Report_Status($"Copying Notes: {i + 1}/{NoteList.Count}");

                Dictionary<string, string> POST_Dictionary = new Dictionary<string, string>()
                {
                    { "note[post_id]", MediaItemRef.UP_UploadedID },
                    { "note[x]", (NoteList[i]["x"].Value<int>() * RatioData).ToString() },
                    { "note[y]", (NoteList[i]["y"].Value<int>() * RatioData).ToString() },
                    { "note[width]", (NoteList[i]["width"].Value<int>() * RatioData).ToString() },
                    { "note[height]", (NoteList[i]["height"].Value<int>() * RatioData).ToString() },
                    { "note[body]", NoteList[i]["body"].Value<string>() },
                    { "login",AppSettings.UserName },
                    { "api_key", Module_Cryptor.Decrypt(AppSettings.APIKey) }
                };

                HttpResponseMessage? e621HttpResponseMessage;
                string? e621StringResponse;
                E621UploadRequest("https://e621.net/uploads.json", "POST", POST_Dictionary, out e621HttpResponseMessage, out e621StringResponse);
                switch (e621HttpResponseMessage.StatusCode)
                {
                    case HttpStatusCode.OK:
                        {
                            if (Module_Credit.UserLevel < 30) //is user level needed?
                            {
                                Module_Credit.Credit_Notes -= 1;
                                Module_Credit.Timestamps_Notes.Add(DateTime.UtcNow.AddHours(1));
                            }
                            MediaItemRef.UP_Inferior_HasNotes = null;
                            break;
                        }

                    case (HttpStatusCode)422:
                        {
                            Report_Info($"Note edit limit reached, copied {i + 1} out of {NoteList.Count} notes");
                            Module_Credit.Credit_Notes = 0;

                            //Form_Loader._FormReference.Invoke(new Action(() =>
                            //{
                            //    TreeNode clonedNode = new TreeNode()
                            //    {
                            //        Text = $"Copy Notes from #{(string)DataRowRef["Inferior_ID"]} to #{(string)DataRowRef["Uploaded_As"]}",
                            //        Tag = RatioData
                            //    };
                            //    Form_Loader._FormReference.cTreeView_RetryQueue.Nodes.Add(clonedNode);
                            //}));
                            //Module_Retry.timer_RetryDisable.Start();
                            //Module_Retry.timer_Retry.Start();
                            break;
                        }

                    default:
                        {
                            MessageBox.Show(Window_Main._RefHolder, $" Error code {e621HttpResponseMessage.StatusCode}\n{e621StringResponse}", "e621 ReBot - Copy Notes", MessageBoxButton.OK, MessageBoxImage.Error);
                            break;
                        }
                }
                e621HttpResponseMessage.Dispose();
                Thread.Sleep(500);
            }
        }

        private static void UploadTask_MoveChildren(MediaItem MediaItemRef)
        {
            if (!Module_e621APIController.APIEnabled) return;

            Report_Status("Moving Children...");

            List<string>? ChildrenList = MediaItemRef.UP_Inferior_Children;

            for (int i = 0; i < ChildrenList.Count; i++)
            {
                Report_Status($"Moving Children: {i + 1}/{ChildrenList.Count}");

                Dictionary<string, string> POST_Dictionary = new Dictionary<string, string>()
                {
                    { "post[old_parent_id]", MediaItemRef.UP_Inferior_ID }, // Child's current/old parent
                    { "post[parent_id]", MediaItemRef.UP_UploadedID }, // Child's new parent / id of new image
                    { "login", AppSettings.UserName },
                    { "api_key", Module_Cryptor.Decrypt(AppSettings.APIKey) }
                };

                HttpResponseMessage? e621HttpResponseMessage;
                string? e621StringResponse;
                E621UploadRequest($"https://e621.net/posts/{ChildrenList[i]}.json", "PATCH", POST_Dictionary, out e621HttpResponseMessage, out e621StringResponse);
                switch (e621HttpResponseMessage.StatusCode)
                {
                    case HttpStatusCode.OK:
                        {
                            Report_Info($"Post #{ChildrenList[i]} set as child of #{MediaItemRef.UP_UploadedID}");
                            break;
                        }

                    default:
                        {
                            MessageBox.Show(Window_Main._RefHolder, $" Error code {e621HttpResponseMessage.StatusCode}\n{e621StringResponse}", "e621 ReBot - Move Children", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                }
                e621HttpResponseMessage.Dispose();
                Thread.Sleep(500);
            }
            MediaItemRef.UP_Inferior_Children = null;
        }

        private static void UploadTask_FlagInferior(MediaItem MediaItemRef)
        {
            if (!Module_e621APIController.APIEnabled) return;

            Report_Status("Flagging inferior...");

            Dictionary<string, string> POST_Dictionary = new Dictionary<string, string>()
            {
                { "post_flag[post_id]", MediaItemRef.UP_Inferior_ID }, // Inferior image
                { "post_flag[reason_name]", "inferior" },
                { "post_flag[parent_id]", MediaItemRef.UP_UploadedID }, //Superior image
                { "login", AppSettings.UserName },
                { "api_key", Module_Cryptor.Decrypt(AppSettings.APIKey) }
            };

            HttpResponseMessage? e621HttpResponseMessage;
            string? e621StringResponse;
            E621UploadRequest("https://e621.net/post_flags.json", "POST", POST_Dictionary, out e621HttpResponseMessage, out e621StringResponse);
            switch (e621HttpResponseMessage.StatusCode)
            {
                case HttpStatusCode.Created:
                    {
                        //JObject Upload_ReponseData = JObject.Parse(e621StringResponse);
                        Module_Credit.Credit_UploadHourly -= 1;
                        if (Module_Credit.UserLevel < 30)
                        {
                            Module_Credit.Credit_Flags -= 1;
                            Module_Credit.Timestamps_Flags.Add(DateTime.UtcNow.AddHours(1));
                        }
                        Report_Info($"Flagged #{MediaItemRef.UP_Inferior_ID} as inferior of #{MediaItemRef.UP_UploadedID}");
                        break;
                    }

                case (HttpStatusCode)422:
                    {
                        Report_Info($"Hourly flag limit reached, did not flag #{MediaItemRef.UP_UploadedID}");
                        Module_Credit.Credit_Flags = 0;
                        UploadTask_ChangeParent(MediaItemRef);

                        //Form_Loader._FormReference.Invoke(new Action(() =>
                        //{
                        //    TreeNode clonedNode = new TreeNode()
                        //    {
                        //        Text = $"Flag #{(string)DataRowRef["Inferior_ID"]} as inferior of #{(string)DataRowRef["Uploaded_As"]}"
                        //    };
                        //    Form_Loader._FormReference.cTreeView_RetryQueue.Nodes.Add(clonedNode);
                        //}));
                        //Module_Retry.timer_RetryDisable.Start();
                        //Module_Retry.timer_Retry.Start();
                        break;
                    }

                default:
                    {
                        //FailedUploadTask = true;
                        MessageBox.Show(Window_Main._RefHolder, $"Error code {e621HttpResponseMessage.StatusCode}\n{e621StringResponse}", "e621 ReBot - Flag Inferior", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    }
            }
            e621HttpResponseMessage.Dispose();
        }

        private static void UploadTask_ChangeParent(MediaItem MediaItemRef)
        {
            if (!Module_e621APIController.APIEnabled) return;

            Report_Status("Changing Parent...");

            Dictionary<string, string> POST_Dictionary = new Dictionary<string, string>()
            {
                { "post[parent_id]", MediaItemRef.UP_UploadedID },
                { "post[edit_reason]", "For future flag." },
                { "login", AppSettings.UserName },
                { "api_key", Module_Cryptor.Decrypt(AppSettings.APIKey) }
            };

            HttpResponseMessage? e621HttpResponseMessage;
            string? e621StringResponse;
            E621UploadRequest($"https://e621.net/posts/{MediaItemRef.UP_Inferior_ID}.json", "PATCH", POST_Dictionary, out e621HttpResponseMessage, out e621StringResponse);
            switch (e621HttpResponseMessage.StatusCode)
            {
                case HttpStatusCode.OK:
                    {
                        //JObject Upload_ReponseData = JObject.Parse(e621StringResponse);
                        Report_Info($"Changed parent #{MediaItemRef.UP_Inferior_ID} to #{MediaItemRef.UP_UploadedID}");
                        break;
                    }

                default:
                    {
                        //FailedUploadTask = true;
                        MessageBox.Show(Window_Main._RefHolder, $"Error code {e621HttpResponseMessage.StatusCode}\n{e621StringResponse}", "e621 ReBot - Change Parent", MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                    }
            }
            e621HttpResponseMessage.Dispose();
        }

        // - - - - - - - - - - - - - - - -

        internal static DispatcherTimer _UploadDisableTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private static void UploadDisableTimer_Tick(object? sender, EventArgs e)
        {
            _UploadDisableTimer.Stop();
            if (_UploadDisableTimer.Tag != null) //Total
            {
                ThreadPool.QueueUserWorkItem(state => Module_Credit.Credit_CheckAll());
            }
            else //Hourly
            {
                List<DateTime> TimestampsCopy = Module_Credit.Timestamps_Upload;
                for (int i = 0; i < Module_Credit.Timestamps_Upload.Count; i++)
                {
                    if (Module_Credit.Timestamps_Upload[0] < DateTime.UtcNow)
                    {
                        TimestampsCopy.RemoveAt(0);
                        Module_Credit.Credit_UploadHourly++;
                    }
                    else
                    {
                        break;
                    }
                }

                lock (Module_Credit.Timestamps_Upload)
                {
                    Module_Credit.Timestamps_Upload = TimestampsCopy;
                }
                Module_Credit.Credit_UpdateDisplay();
                if (Module_Credit.Credit_UploadHourly > 0)
                {
                    Window_Main._RefHolder.Upload_CheckBox.IsChecked = true;
                }
                else
                {
                    TimeSpan TempTimeSpan = Module_Credit.Timestamps_Upload[0] - DateTime.UtcNow;
                    if (TempTimeSpan.TotalSeconds < 60d)
                    {
                        _UploadDisableTimer.Interval = TimeSpan.FromSeconds(1);
                        Window_Main._RefHolder.Upload_StatusTextBlock.Text = $"Upload Queue (available again in {TempTimeSpan.Seconds} seconds)";
                    }
                    else
                    {
                        _UploadDisableTimer.Interval = TimeSpan.FromMinutes(1);
                        Window_Main._RefHolder.Upload_StatusTextBlock.Text = $"Upload Queue (available again in {TempTimeSpan.Minutes + 1} minutes)";
                    }
                    _UploadDisableTimer.Start();
                }
            }
        }

    }
}