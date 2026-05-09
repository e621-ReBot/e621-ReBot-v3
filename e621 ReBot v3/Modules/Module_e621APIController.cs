using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace e621_ReBot_v3.Modules
{
    internal static class Module_e621APIController
    {
        internal static bool APIEnabled = false;
        internal static void StartTheController()
        {
            API_Timer.Tick += API_Timer_Tick;
            API_Timer.Start();
        }

        internal static void ToggleStatus()
        {
            APIEnabled = !APIEnabled;
            Window_Main._RefHolder.Upload_CheckBox.IsChecked = APIEnabled;
            Window_Main._RefHolder.Download_PoolWatcher.IsEnabled = APIEnabled;
            Window_Main._RefHolder.DownloadQueue_JobImport.IsEnabled = APIEnabled;
            if (Window_Preview._RefHolder != null) Window_Preview._RefHolder.SimilarSearchEnableCheck();

            if (APIEnabled)
            {
                Module_Uploader._UploadTimer.Start();
                Window_Main._RefHolder.SB_APIKey.Content = "Remove API key";
                Window_Main._RefHolder.SB_APIKey.IsEnabled = true;
                Window_Main._RefHolder.Upload_CheckBox.IsEnabled = true;
            }
            else
            {
                API_Timer.Stop();
                Module_Uploader._UploadTimer.Stop();
                Module_Uploader._UploadDisableTimer.Stop();
                Window_Main._RefHolder.SB_APIKey.Content = "Add API key";
            }
            Window_Main._RefHolder.UploadCounterChange(0); //Refresh button
        }

        // - - - - - - - - - - - - - - - -

        private static readonly DispatcherTimer? API_Timer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1) };
        internal static List<Task> UserTasks = new List<Task>();
        internal static List<Task> BackgroundTasks = new List<Task>();
        private static void API_Timer_Tick(object? sender, EventArgs e)
        {
            Task TaskTemp;
            if (UserTasks.Count > 0)
            {
                lock (UserTasks)
                {
                    TaskTemp = UserTasks[0];
                    UserTasks.RemoveAt(0);
                }
                if (TaskTemp != null) TaskTemp.Start();
                return;
            }
            if (BackgroundTasks.Count > 0)
            {
                lock (BackgroundTasks)
                {
                    TaskTemp = BackgroundTasks[0];
                    BackgroundTasks.RemoveAt(0);
                }
                if (TaskTemp != null) TaskTemp.Start();
            }
        }
    }
}