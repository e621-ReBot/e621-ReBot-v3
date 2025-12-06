using e621_ReBot_v3.CustomControls;
using e621_ReBot_v3.Modules;
using e621_ReBot_v3.Modules.Uploader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace e621_ReBot_v3
{
    internal static class AppSettings
    {
        internal static readonly string GlobalUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36";
        internal static bool DevMode = false;
        internal static bool FirstRun = true;
        internal static bool FirstRunSession = true;
        internal static string AppName = "e621 ReBot";
        internal static string? UserName;
        internal static string? UserID;
        internal static string? APIKey;
        // - - - - - - - - - - - - - - - -
        internal static ushort Volume = 25;
        // - - - - - - - - - - - - - - - -
        internal static ushort Update_Interval = 1;
        internal static DateTime Update_LastCheck = DateTime.UtcNow.AddYears(-1);
        // - - - - - - - - - - - - - - - -
        internal static bool BigMode = false;
        internal static bool Grid_SaveSession = true;
        internal static List<string> MediaIgnoreList = new List<string>();
        // - - - - - - - - - - - - - - - -
        internal static string Download_FolderLocation = $"{AppDomain.CurrentDomain.BaseDirectory}Downloads\\";
        internal static ushort Download_ThreadsCount = 4;
        internal static bool Download_SaveTags = false;
        internal static ushort NamingPattern_e6 = 0;
        internal static ushort NamingPattern_Web = 0;
        internal static bool Download_IgnoreErrors = false;
        // - - - - - - - - - - - - - - - -
        internal static bool Browser_ClearCache = false;
        // - - - - - - - - - - - - - - - -
        internal static bool Upload_DontConvertVideos = true;
        // - - - - - - - - - - - - - - - -
        internal static bool Converter_DontConvertVideos = true;
        // - - - - - - - - - - - - - - - -
        internal static OrderedDictionary Bookmarks = new OrderedDictionary();
        internal static List<string> Blacklist = new List<string>();
        private static Dictionary<string, string> MediaRecords = new Dictionary<string, string>();
        private static Dictionary<string, string> ArtistAliases = new Dictionary<string, string>();
        internal static List<PoolItem> PoolWatcher = new List<PoolItem>();
        internal static Dictionary<string, string> QuickTags = new Dictionary<string, string>();
        // - - - - - - - - - - - - - - - -
        internal static string? Note;

        internal static void SaveSettings()
        {
            JObject JObjectTemp = new JObject
            {
                { "FirstRun",  FirstRun },
                { "UserName",  UserName },
                { "UserID",  UserID },
                { "APIKey",  APIKey },
                { "Volume", Volume },
                { "ThemeBackground", ((SolidColorBrush)Application.Current.Resources["ThemeBackground"]).Color.ToString()},
                { "ThemeForeground", ((SolidColorBrush)Application.Current.Resources["ThemeForeground"]).Color.ToString()},
                { "ThemeFocus", ((SolidColorBrush)Application.Current.Resources["ThemeFocus"]).Color.ToString()},
                { "Update_Interval", Update_Interval },
                { "Update_LastCheck", Update_LastCheck },
                { "BigMode", BigMode },
                { "Grid_SaveSession", Grid_SaveSession },
                { "Download_FolderLocation", Download_FolderLocation },
                { "Download_ThreadsCount", Download_ThreadsCount },
                { "Download_SaveTags", Download_SaveTags },
                { "NamingPattern_e6", NamingPattern_e6 },
                { "NamingPattern_Web", NamingPattern_Web },
                { "Download_IgnoreErrors", Download_IgnoreErrors },
                { "Browser_ClearCache", Browser_ClearCache },
                { "Converter_DontConvertVideos", Converter_DontConvertVideos },
                { "Note", Note }
            };
            if (Bookmarks.Count > 0) JObjectTemp.Add("Bookmarks", JObject.FromObject(Bookmarks));
            if (Blacklist.Any()) JObjectTemp.Add("Blacklist", JArray.FromObject(Blacklist));
            if (MediaRecords.Any()) JObjectTemp.Add("MediaRecords", JObject.FromObject(MediaRecords));
            if (ArtistAliases.Any()) JObjectTemp.Add("ArtistAliases", JObject.FromObject(ArtistAliases));
            if (PoolWatcher.Any()) JObjectTemp.Add("PoolWatcher", JArray.FromObject(PoolWatcher));
            if (QuickTags.Any()) JObjectTemp.Add("QuickTags", JObject.FromObject(QuickTags));
            if (MediaIgnoreList.Any()) JObjectTemp.Add("MediaIgnoreList", JArray.FromObject(MediaIgnoreList));

            JsonSerializer JsonSerializerTemp = new JsonSerializer() { NullValueHandling = NullValueHandling.Ignore };
            JArray? MediaJArray;
            lock (Module_Grabber._Grabbed_MediaItems)
            {
                if (Grid_SaveSession && Module_Grabber._Grabbed_MediaItems.Count > 0)
                {
                    MediaJArray = new JArray();
                    foreach (MediaItem MediaItemTemp in Module_Grabber._Grabbed_MediaItems)
                    {
                        MediaJArray.Add(JObject.FromObject(MediaItemTemp, JsonSerializerTemp));
                    }
                    JObjectTemp.Add("Grid_Session", JArray.FromObject(MediaJArray));
                }
            }
            lock (Module_RetryQueue._2Retry_MediaItems)
            {
                if (Module_RetryQueue._2Retry_MediaItems.Count > 0)
                {
                    MediaJArray = new JArray();
                    foreach (MediaItem MediaItemTemp in Module_RetryQueue._2Retry_MediaItems)
                    {
                        MediaJArray.Add(JObject.FromObject(MediaItemTemp, JsonSerializerTemp));
                    }
                    JObjectTemp.Add("RetryQueue", JArray.FromObject(MediaJArray));
                }
            }
            string SaveSettingsString = JsonConvert.SerializeObject(JObjectTemp, Formatting.Indented);
            File.WriteAllText("settings.json", SaveSettingsString);
        }

        internal static void LoadSettings()
        {
            if (File.Exists("settings.json"))
            {
                string LoadSettingsString = File.ReadAllText("settings.json");
                JObject LoadSettingsJObject = JObject.Parse(LoadSettingsString);
                foreach (JToken JTokenTemp in LoadSettingsJObject.Children())
                {
                    switch (JTokenTemp.Path)
                    {
                        case "DevMode":
                            {
                                DevMode = (bool)LoadSettingsJObject["DevMode"];
                                break;
                            }
                        case "FirstRun":
                            {
                                FirstRun = (bool)LoadSettingsJObject["FirstRun"];
                                break;
                            }
                        case "UserName":
                            {
                                UserName = (string)LoadSettingsJObject["UserName"];
                                AppName = $"e621 ReBot ({UserName ?? "<Name>"})";
                                Window_Main._RefHolder.STB_AppName.Text = AppName;
                                break;
                            }
                        case "UserID":
                            {
                                UserID = (string)LoadSettingsJObject["UserID"];
                                break;
                            }
                        case "APIKey":
                            {
                                APIKey = (string)LoadSettingsJObject["APIKey"];
                                break;
                            }
                        case "Volume":
                            {
                                Volume = (ushort)LoadSettingsJObject["Volume"];
                                Window_Main._RefHolder.Settings_VolumeSlider.SetVolume(Volume);
                                break;
                            }
                        case "ThemeBackground":
                            {
                                string BackgroundColorHex = (string)LoadSettingsJObject["ThemeBackground"];
                                Application.Current.Resources["ThemeBackground"] = (SolidColorBrush)(new BrushConverter().ConvertFrom(BackgroundColorHex));
                                Window_Main._RefHolder.ColorBox_Background.Text = BackgroundColorHex.Substring(3);
                                break;
                            }
                        case "ThemeForeground":
                            {
                                string ForegroundColorHex = (string)LoadSettingsJObject["ThemeForeground"];
                                Application.Current.Resources["ThemeForeground"] = (SolidColorBrush)(new BrushConverter().ConvertFrom(ForegroundColorHex));
                                Window_Main._RefHolder.ColorBox_Foreground.Text = ForegroundColorHex.Substring(3);
                                break;
                            }
                        case "ThemeFocus":
                            {
                                string FocusColorHex = (string)LoadSettingsJObject["ThemeFocus"];
                                Application.Current.Resources["ThemeFocus"] = (SolidColorBrush)(new BrushConverter().ConvertFrom(FocusColorHex));
                                Window_Main._RefHolder.ColorBox_Focus.Text = FocusColorHex.Substring(3);
                                break;
                            }
                        case "Update_Interval":
                            {
                                Update_Interval = (ushort)LoadSettingsJObject["Update_Interval"];
                                //((RadioButton)Window_Main._RefHolder.UpdateInterval_StackPanel.FindName("RadionButton_UI" + Update_Interval)).IsChecked = true;
                                break;
                            }
                        case "Update_LastCheck":
                            {
                                Update_LastCheck = (DateTime)LoadSettingsJObject["Update_LastCheck"];
                                break;
                            }
                        case "BigMode":
                            {
                                BigMode = (bool)LoadSettingsJObject["BigMode"];
                                break;
                            }
                        case "Grid_SaveSession":
                            {
                                Grid_SaveSession = (bool)LoadSettingsJObject["Grid_SaveSession"];
                                break;
                            }
                        case "Grid_Session":
                            {
                                JToken JTokenMedia = LoadSettingsJObject["Grid_Session"];
                                foreach (JToken MediaItemTokenTemp in JTokenMedia.Children())
                                {
                                    Module_Grabber._Grabbed_MediaItems.Add(MediaItemTokenTemp.ToObject<MediaItem>());
                                    if (MediaItemTokenTemp["Grid_ThumbnailFullInfo"] == null) Module_Grabber._Grabbed_MediaItems[Module_Grabber._Grabbed_MediaItems.Count - 1].Grid_ThumbnailFullInfo = true;
                                }
                                //Window_Main._RefHolder.Dispatcher.BeginInvoke(() => Window_Main._RefHolder.Grid_Populate(true));
                                break;
                            }
                        case "MediaIgnoreList":
                            {
                                MediaIgnoreList = LoadSettingsJObject["MediaIgnoreList"].ToObject<List<string>>();
                                break;
                            }
                        case "Download_FolderLocation":
                            {
                                Download_FolderLocation = (string)LoadSettingsJObject["Download_FolderLocation"];
                                Window_Main._RefHolder.Download_DownloadFolderLocation.ToolTip = $"Current path: {Download_FolderLocation}";
                                break;
                            }
                        case "Download_ThreadsCount":
                            {
                                Download_ThreadsCount = (ushort)LoadSettingsJObject["Download_ThreadsCount"];
                                //((RadioButton)Window_Main._RefHolder.DLThreads_StackPanel.FindName($"RadionButton_DLT{Download_ThreadsCount}")).IsChecked = true;
                                Module_Downloader.DLThreadsWaiting = Download_ThreadsCount;
                                //Window_Main._RefHolder.Download_DownloadVEPanel.Children.Clear();
                                //for (int i = 0; i < Download_ThreadsCount; i++)
                                //{
                                //    Window_Main._RefHolder.Download_DownloadVEPanel.Children.Add(new DownloadVE());
                                //}
                                break;
                            }
                        case "Download_SaveTags":
                            {
                                Download_SaveTags = (bool)LoadSettingsJObject["Download_SaveTags"];
                                //(Window_Main._RefHolder.SettingsCheckBox_DownloadSaveTags).IsChecked = Download_SaveTags;
                                break;
                            }
                        case "NamingPattern_e6":
                            {
                                NamingPattern_e6 = (ushort)LoadSettingsJObject["NamingPattern_e6"];
                                //((RadioButton)Window_Main._RefHolder.NamingPattern_e6_StackPanel.FindName($"RadionButton_NPe6_{NamingPattern_e6}")).IsChecked = true;
                                break;
                            }
                        case "NamingPattern_Web":
                            {
                                NamingPattern_Web = (ushort)LoadSettingsJObject["NamingPattern_Web"];
                                //((RadioButton)Window_Main._RefHolder.NamingPattern_Web_StackPanel.FindName($"RadionButton_NPWeb_{NamingPattern_Web}")).IsChecked = true;
                                break;
                            }
                        case "Download_IgnoreErrors":
                            {
                                Download_IgnoreErrors = (bool)LoadSettingsJObject["Download_IgnoreErrors"];
                                break;
                            }
                        case "Browser_ClearCache":
                            {
                                Browser_ClearCache = (bool)LoadSettingsJObject["Browser_ClearCache"];
                                //(Window_Main._RefHolder.SettingsCheckBox_BrowserClearCache).IsChecked = Browser_ClearCache;
                                break;
                            }
                        case "Converter_DontConvertVideos":
                            {
                                Converter_DontConvertVideos = (bool)LoadSettingsJObject["Converter_DontConvertVideos"];
                                break;
                            }
                        case "Blacklist":
                            {
                                Blacklist = LoadSettingsJObject["Blacklist"].ToObject<List<string>>();
                                break;
                            }
                        case "Bookmarks":
                            {
                                Bookmarks = LoadSettingsJObject["Bookmarks"].ToObject<OrderedDictionary>();
                                break;
                            }
                        case "MediaRecords":
                            {
                                MediaRecords = LoadSettingsJObject["MediaRecords"].ToObject<Dictionary<string, string>>();
                                break;
                            }
                        case "ArtistAliases":
                            {
                                ArtistAliases = LoadSettingsJObject["ArtistAliases"].ToObject<Dictionary<string, string>>();
                                break;
                            }
                        case "PoolWatcher":
                            {
                                PoolWatcher = LoadSettingsJObject["PoolWatcher"].ToObject<List<PoolItem>>();
                                Window_Main._RefHolder.Download_PoolWatcher.IsEnabled = PoolWatcher.Any();
                                break;
                            }
                        case "Note":
                            {
                                Note = (string)LoadSettingsJObject["Note"];
                                break;
                            }
                        case "QuickTags":
                            {
                                QuickTags = LoadSettingsJObject["QuickTags"].ToObject<Dictionary<string, string>>();
                                break;
                            }
                        case "RetryQueue":
                            {
                                JToken JTokenMedia = LoadSettingsJObject["RetryQueue"];
                                MediaItem? MediaItemTemp;
                                foreach (JToken MediaItemTokenTemp in JTokenMedia.Children())
                                {
                                    MediaItemTemp = MediaItemTokenTemp.ToObject<MediaItem>();
                                    int IndexCheck = -1;
                                    if (Module_Grabber._Grabbed_MediaItems.Count > 0)
                                    {
                                        IndexCheck = Module_Grabber._Grabbed_MediaItems.FindURLIndex(MediaItemTemp.Grab_MediaURL);
                                        if (IndexCheck >= 0)
                                        {
                                            Module_RetryQueue._2Retry_MediaItems.Add(Module_Grabber._Grabbed_MediaItems[IndexCheck]);
                                            continue;
                                        }
                                    }
                                    Module_RetryQueue._2Retry_MediaItems.Add(MediaItemTemp);
                                }
                                break;
                            }
                    }
                }

                if (DevMode)
                {
                    Window_Main._RefHolder.MakeUpdate_WButton.Visibility = Visibility.Visible;
                    Window_Main._RefHolder.SettingsButton_DLGenders.Visibility = Visibility.Visible;
                    Window_Main._RefHolder.SettingsButton_DLDNPs.Visibility = Visibility.Visible;
                }
            }
        }

        internal static void SetLoadedSettings()
        {
            // - - - Settings

            ((RadioButton)Window_Main._RefHolder.UpdateInterval_StackPanel.FindName("RadionButton_UI" + Update_Interval)).IsChecked = true;
            Window_Main._RefHolder.SettingsCheckBox_BigMode.IsChecked = BigMode;
            Window_Main._RefHolder.SettingsCheckBox_GridSaveSession.IsChecked = Grid_SaveSession;
            Window_Main._RefHolder.SettingsCheckBox_BrowserClearCache.IsChecked = Browser_ClearCache;

            // - - - Jobs

            while (Module_RetryQueue._2Retry_MediaItems.Count > 0)
            {
                Module_RetryQueue.MoveItem2UploadQueue(Module_RetryQueue._2Retry_MediaItems[0]);
            }

            // - - - Download

            Window_Main._RefHolder.Download_DownloadFolderLocation.ToolTip = $"Current path: {Download_FolderLocation}";
            ((RadioButton)Window_Main._RefHolder.DLThreads_StackPanel.FindName($"RadionButton_DLT{Download_ThreadsCount}")).IsChecked = true;
            Window_Main._RefHolder.Download_DownloadVEPanel.Children.Clear();
            for (int i = 0; i < Download_ThreadsCount; i++)
            {
                DownloadVE DownloadVETemp = new DownloadVE();
                Window_Main._RefHolder._DownloadVEList.Add(DownloadVETemp);
                Window_Main._RefHolder.Download_DownloadVEPanel.Children.Add(DownloadVETemp);

            }
            Window_Main._RefHolder.SettingsCheckBox_DownloadSaveTags.IsChecked = Download_SaveTags;
            ((RadioButton)Window_Main._RefHolder.NamingPattern_e6_StackPanel.FindName($"RadionButton_NPe6_{NamingPattern_e6}")).IsChecked = true;
            ((RadioButton)Window_Main._RefHolder.NamingPattern_Web_StackPanel.FindName($"RadionButton_NPWeb_{NamingPattern_Web}")).IsChecked = true;
            Window_Main._RefHolder.SettingsCheckBox_IgnoreErrors.IsChecked = Download_IgnoreErrors;

            // - - - Grid

            Window_Main._RefHolder.Dispatcher.BeginInvoke(() => Window_Main._RefHolder.Grid_Populate(true));
        }

        internal static void MediaRecord_Check(MediaItem MediaItemRef)
        {
            if (MediaRecords.ContainsKey(MediaItemRef.Grab_MediaURL)) MediaItemRef.UP_UploadedID = MediaRecords[MediaItemRef.Grab_MediaURL];
        }

        internal static void MediaRecord_Add(MediaItem MediaItemRef)
        {
            if (!MediaRecords.ContainsKey(MediaItemRef.Grab_MediaURL)) MediaRecords.Add(MediaItemRef.Grab_MediaURL, MediaItemRef.UP_UploadedID);
        }

        internal static void MediaRecord_Remove(MediaItem MediaItemRef)
        {
            if (MediaRecords.ContainsKey(MediaItemRef.Grab_MediaURL)) MediaRecords.Remove(MediaItemRef.Grab_MediaURL);
        }

        internal static string? ArtistAlias_Check(MediaItem MediaItemRef)
        {
            string ArtistNameAtWebsite = $"{MediaItemRef.Grab_Artist}@{new Uri(MediaItemRef.Grab_PageURL).Host}";
            return ArtistAliases.ContainsKey(ArtistNameAtWebsite) ? ArtistAliases[ArtistNameAtWebsite] : null;
        }

        internal static void ArtistAlias_Add(MediaItem MediaItemRef, string NewAlias)
        {
            string ArtistNameAtWebsite = $"{MediaItemRef.Grab_Artist}@{new Uri(MediaItemRef.Grab_PageURL).Host}";
            if (ArtistAliases.ContainsKey(ArtistNameAtWebsite))
            {
                ArtistAliases[ArtistNameAtWebsite] = NewAlias;
            }
            else
            {
                ArtistAliases.Add(ArtistNameAtWebsite, NewAlias);
            }
        }

        internal static void ArtistAlias_Remove(MediaItem MediaItemRef)
        {
            string ArtistNameAtWebsite = $"{MediaItemRef.Grab_Artist}@{new Uri(MediaItemRef.Grab_PageURL).Host}";
            if (ArtistAliases.ContainsKey(ArtistNameAtWebsite)) ArtistAliases.Remove(ArtistNameAtWebsite);
        }
    }
}