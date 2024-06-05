using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace e621_ReBot_Updater
{
    public partial class App : Application
    {
        //https://www.codeproject.com/Articles/32908/C-Single-Instance-App-With-the-Ability-To-Restore
        private Mutex? AppMutex;

        private bool onlyInstance;
        protected override void OnStartup(StartupEventArgs e)
        {
            AppMutex = new Mutex(true, $"Local\\e621 ReBot Updater - e621126e", out onlyInstance);
            if (!onlyInstance)
            {
                Shutdown();
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += Write2Log;
            base.OnStartup(e);
        }

        private void Write2Log(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ExceptionHolder = (Exception)e.ExceptionObject;
            File.WriteAllText("UpdaterReBotErrorLog.txt", $"{DateTime.UtcNow}\n{ExceptionHolder.Message}\n\n{ExceptionHolder.StackTrace}");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (AppMutex != null && onlyInstance) AppMutex.ReleaseMutex();
            base.OnExit(e);
        }
    }
}