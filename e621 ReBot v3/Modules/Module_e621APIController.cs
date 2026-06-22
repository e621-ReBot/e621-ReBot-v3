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

        private static readonly DispatcherTimer? API_Timer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(3) };
        internal static readonly Queue<Func<Task>> UserTasks = new Queue<Func<Task>>();
        internal static readonly Queue<Func<Task>> BackgroundTasks = new Queue<Func<Task>>();
        private static async void API_Timer_Tick(object? sender, EventArgs e)
        {
            //Timer is on main thread, is it an issue?
            Task TaskTemp;
            Func<Task> Task2Run;
            if (UserTasks.Count > 0)
            {
                lock (UserTasks)
                {
                    Task2Run = UserTasks.Dequeue();
                }
                if (Task2Run != null)
                {
                    await Task2Run();
                    return;
                }
            }
            if (BackgroundTasks.Count > 0)
            {
                lock (BackgroundTasks)
                {
                    Task2Run = BackgroundTasks.Dequeue();
                }
                if (Task2Run != null)
                {
                    await Task2Run();
                } 
            }
        }

        internal static Task<T> EnqueuePriorityWork<T>(Func<Task<T>> work)
        {
            TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
            Func<Task> wrapper = async () =>
            {
                try
                {
                    T result = await work();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            };

            lock (UserTasks)
            {
                UserTasks.Enqueue(wrapper);
            }

            return tcs.Task;
        }

        internal static Task<T> EnqueueBackgroundWork<T>(Func<Task<T>> work)
        {
            TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();
            Func<Task> wrapper = async () =>
            {
                try
                {
                    T result = await work();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            };

            lock (BackgroundTasks)
            {
                BackgroundTasks.Enqueue(wrapper);
            }

            return tcs.Task;
        }
    }
}