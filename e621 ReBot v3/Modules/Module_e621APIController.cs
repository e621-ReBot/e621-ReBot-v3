using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace e621_ReBot_v3.Modules
{
    internal static class Module_e621APIController
    {
        internal static bool APIEnabled = false;
        internal static void ToggleStatus()
        {
            APIEnabled = !APIEnabled;
            Window_Main._RefHolder.Upload_CheckBox.IsChecked = APIEnabled;
            //Form_Loader._FormReference.cCheckGroupBox_Retry.Checked = APIEnabled;
            Window_Main._RefHolder.Download_PoolWatcher.IsEnabled = APIEnabled;
            //Form_Loader._FormReference.bU_RefreshCredit.Enabled = APIEnabled;
            if (Window_Preview._RefHolder != null) Window_Preview._RefHolder.panel_Search.IsEnabled = APIEnabled;

            if (APIEnabled)
            {
                if (API_Timer == null)
                {
                    API_Timer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1) };
                    API_Timer.Tick += API_Timer_Tick;
                    API_Timer.Start();
                }
                Module_Uploader._UploadTimer.Start();
                //Module_Retry.timer_Retry.Start();
                Window_Main._RefHolder.SB_APIKey.Content = "Remove API key";
            }
            else
            {
                if (API_Timer != null) API_Timer.Stop();
                Module_Uploader._UploadTimer.Stop();
                Module_Uploader._UploadDisableTimer.Stop();
                //Module_Retry.timer_Retry.Stop();
                //Module_Retry.timer_RetryDisable.Stop();
                Window_Main._RefHolder.SB_APIKey.Content = "Add API key";
            }
            Window_Main._RefHolder.UploadCounterChange(0); //Refresh button
        }

        // - - - - - - - - - - - - - - - -

        private static DispatcherTimer? API_Timer;
        internal static List<Task> UserTasks = new List<Task>();
        internal static List<Task> BackgroundTasks = new List<Task>();
        private static void API_Timer_Tick(object? sender, EventArgs e)
        {
            if (UserTasks.Any())
            {
                Task TaskTemp = UserTasks[0];
                TaskTemp.Start();
                lock (UserTasks)
                {
                    UserTasks.RemoveAt(0);
                }
                return;
            }
            if (BackgroundTasks.Any())
            {
                Task TaskTemp = BackgroundTasks[0];
                TaskTemp.Start();
                lock (BackgroundTasks)
                {
                    BackgroundTasks.RemoveAt(0);
                }
            }
        }
    }
}