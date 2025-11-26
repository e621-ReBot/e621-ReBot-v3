using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace e621_ReBot_v3.Modules
{
    internal class Module_Credit
    {

        internal static ushort UserLevel = 20; //Member = 20; Privileged = 30; Contributor = 33; Janitor = 35; Moderator = 40; Admin = 50.
        internal static bool CanReplace = false;

        internal static ushort Credit_UploadHourly = 30;
        internal static ushort Credit_UploadTotal = 10;
        internal static ushort Credit_Flags = 10;
        internal static ushort Credit_Notes = 50;

        internal static List<DateTime> Timestamps_Upload = new List<DateTime>();
        internal static List<DateTime> Timestamps_Flags = new List<DateTime>();
        internal static List<DateTime> Timestamps_Notes = new List<DateTime>();

        private static void Credit_Reset()
        {
            Credit_UploadHourly = 30;
            Credit_Flags = 10;
            Credit_Notes = 50;
        }

        internal static void Credit_UpdateDisplay()
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                if (Credit_UploadHourly == 0)
                {
                    Window_Main._RefHolder.Upload_CheckBox.IsChecked = false;
                    Module_Uploader._UploadDisableTimer.Stop();
                    Module_Uploader._UploadDisableTimer.Interval = TimeSpan.FromSeconds(1);
                    Module_Uploader._UploadDisableTimer.Tag = null; //"Hourly";
                    Module_Uploader._UploadDisableTimer.Start();
                }
                if (UserLevel < 30)
                {
                    if (Credit_UploadTotal == 0)
                    {
                        Window_Main._RefHolder.Upload_CheckBox.IsChecked = false;
                        Timestamps_Upload.Clear();
                        Timestamps_Upload.Add(DateTime.UtcNow.AddHours(1));
                        Module_Uploader._UploadDisableTimer.Stop();
                        Module_Uploader._UploadDisableTimer.Interval = TimeSpan.FromMinutes(1);
                        Module_Uploader._UploadDisableTimer.Tag = "Total";
                        Module_Uploader._UploadDisableTimer.Start();
                    }
                    Window_Main._RefHolder.CreditFlags_TextBlock.Text = $"{Credit_Flags}";
                    Window_Main._RefHolder.CreditNotes_TextBlock.Text = $"{Credit_Notes}";
                }
                else
                {
                    Window_Main._RefHolder.CreditFlags_TextBlock.Text = "∞";
                    Window_Main._RefHolder.CreditNotes_TextBlock.Text = "∞";
                }
                Window_Main._RefHolder.CreditUpload_TextBlock.Text = $"{Credit_UploadHourly}/{Credit_UploadTotal}";
                Window_Main._RefHolder.Credit_StackPanel.Visibility = Visibility.Visible;
            });
        }

        internal static void Credit_CheckAll()
        {
            Credit_Reset();
            //Member = 20; Privileged = 30; Contributor = 33; Janitor = 35; Moderator = 40; Admin = 50.
            //https://e621.net/wiki_pages/2423
            if (UserLevel < 30)
            {
                Credit_CheckUpload().Wait();
                Credit_CheckFlags().Wait();
                Credit_CheckNotes().Wait();
            }
            Credit_UpdateDisplay();
        }

        internal static async Task Credit_CheckUpload()
        {
            Timestamps_Upload.Clear();
            if (UserLevel < 30)
            {
                Task<string?> RunTaskFirst = new Task<string?>(() => Module_e621Data.DataDownload($"https://e621.net/users/{AppSettings.UserID}.json", true));
                lock (Module_e621APIController.BackgroundTasks)
                {
                    Module_e621APIController.BackgroundTasks.Add(RunTaskFirst);
                }

                string? JSON_UserInfo = await RunTaskFirst;
                if (string.IsNullOrEmpty(JSON_UserInfo) || JSON_UserInfo.StartsWith('ⓔ') || JSON_UserInfo.Length < 32) return;

                JObject UserJObject = JObject.Parse(JSON_UserInfo);
                UserLevel = (ushort)UserJObject["level"];

                CanReplace = (bool)UserJObject["replacements_beta"];

                Credit_UploadTotal = (ushort)UserJObject["upload_limit"];

                AppSettings.UserName = (string)UserJObject["name"]; //In case user changes name

                RunTaskFirst = new Task<string?>(() => Module_e621Data.DataDownload($"https://e621.net/posts.json?limit=30&tags=user:!{AppSettings.UserID}"));
                lock (Module_e621APIController.BackgroundTasks)
                {
                    Module_e621APIController.BackgroundTasks.Add(RunTaskFirst);
                }

                JSON_UserInfo = await RunTaskFirst;
                if (string.IsNullOrEmpty(JSON_UserInfo) || JSON_UserInfo.StartsWith('ⓔ') || JSON_UserInfo.Length < 32) return;

                JObject PostHistory = JObject.Parse(JSON_UserInfo);
                foreach (JObject UploadedPost in PostHistory["posts"])
                {
                    DateTime TempTime = ((DateTime)UploadedPost["created_at"]).ToUniversalTime().AddHours(1);
                    if (DateTime.UtcNow > TempTime)
                    {
                        break;
                    }
                    else
                    {
                        Timestamps_Upload.Add(TempTime);
                        Credit_UploadHourly--;
                    }
                }

                RunTaskFirst = new Task<string?>(() => Module_e621Data.DataDownload($"https://e621.net/post_replacements.json?search[creator_id]={AppSettings.UserID}"));
                lock (Module_e621APIController.BackgroundTasks)
                {
                    Module_e621APIController.BackgroundTasks.Add(RunTaskFirst);
                }

                JSON_UserInfo = await RunTaskFirst;
                if (string.IsNullOrEmpty(JSON_UserInfo) || JSON_UserInfo.StartsWith('ⓔ') || JSON_UserInfo.Length < 32) return;

                JArray PostHistoryArray = JArray.Parse(JSON_UserInfo);
                foreach (JObject UploadedPost in PostHistoryArray)
                {
                    DateTime TempTime = ((DateTime)UploadedPost["created_at"]).ToUniversalTime().AddHours(1);
                    if (DateTime.UtcNow > TempTime)
                    {
                        break;
                    }
                    else
                    {
                        Timestamps_Upload.Add(TempTime);
                        Credit_UploadHourly--;
                    }
                }

                Timestamps_Upload.Sort();
            }
        }

        internal static async Task Credit_CheckFlags()
        {
            Timestamps_Flags.Clear();
            if (UserLevel < 30)
            {
                Task<string?> RunTaskFirst = new Task<string?>(() => Module_e621Data.DataDownload($"https://e621.net/post_flags.json?search[creator_id]={AppSettings.UserID}", true));
                lock (Module_e621APIController.BackgroundTasks)
                {
                    Module_e621APIController.BackgroundTasks.Add(RunTaskFirst);
                }

                string? JSON_UserInfo = await RunTaskFirst;
                if (string.IsNullOrEmpty(JSON_UserInfo) || JSON_UserInfo.StartsWith('ⓔ') || JSON_UserInfo.Length < 32) return;

                JArray FlagHistory = JArray.Parse(JSON_UserInfo);
                for (int x = 0; x <= 10; x++)
                {
                    DateTime TempTime = ((DateTime)FlagHistory[x]["created_at"]).ToUniversalTime().AddHours(1);
                    if (DateTime.UtcNow > TempTime)
                    {
                        break;
                    }
                    else
                    {
                        Timestamps_Flags.Add(TempTime);
                        Credit_Flags--;
                    }
                }
                Timestamps_Flags.Sort();
            }
        }

        internal static async Task Credit_CheckNotes()
        {
            Timestamps_Notes.Clear();
            if (UserLevel < 30)
            {
                Task<string?> RunTaskFirst = new Task<string?>(() => Module_e621Data.DataDownload($"https://e621.net/note_versions.json?search[updater_id]={AppSettings.UserID}", true));
                lock (Module_e621APIController.BackgroundTasks)
                {
                    Module_e621APIController.BackgroundTasks.Add(RunTaskFirst);
                }

                string? JSON_UserInfo = await RunTaskFirst;
                if (string.IsNullOrEmpty(JSON_UserInfo) || JSON_UserInfo.StartsWith('ⓔ') || JSON_UserInfo.Length < 32) return;

                JArray NoteHistory = JArray.Parse(JSON_UserInfo);
                for (int x = 0; x <= 50; x++)
                {
                    DateTime TempTime = ((DateTime)NoteHistory[x]["created_at"]).ToUniversalTime().AddHours(1);
                    if (DateTime.UtcNow > TempTime)
                        break;
                    else
                    {
                        Timestamps_Notes.Add(TempTime);
                        Credit_Notes--;
                    }
                }
                Timestamps_Notes.Sort();
            }
        }
    }
}