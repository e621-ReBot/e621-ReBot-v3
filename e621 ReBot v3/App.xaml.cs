using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace e621_ReBot_v3
{
    //https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory?view=vs-2022
    //https://www.codementor.io/@rowlandbanks/cleaner-stacktraces-13q3hbv2s7
    //https://stackoverflow.com/a/66221592/8810532
    public partial class App : Application
    {
        private Mutex? AppMutex;
        private KeyboardHotkeys? GlobalHotkeys;

        private bool onlyInstance;
        protected override void OnStartup(StartupEventArgs e)
        {
            AppMutex = new Mutex(true, $"Local\\e621 ReBot v3 - e621126e", out onlyInstance);
            if (!onlyInstance)
            {
                ShowExistingWindow();
                Current.Shutdown();
                return;
            }

            //Load form Embedded Resources - This Function is not called if the Assembly is in the Application Folder
            AppDomain.CurrentDomain.AssemblyResolve += LoadMergedDLLs;
            AppDomain.CurrentDomain.UnhandledException += Write2Log;

            GlobalHotkeys = new KeyboardHotkeys();
            GlobalHotkeys.HookKeyboard();
            base.OnStartup(e);
        }

        private static Assembly? LoadMergedDLLs(object sender, ResolveEventArgs e)
        {
            List<string> LoadList = new List<string> { "HtmlAgilityPack", "Newtonsoft.Json", "CefSharp.Wpf", "System.Net.Http.Formatting" };
            if (LoadList.Any(stringName => e.Name.Contains(stringName)))
            {
                Assembly thisAssembly = Assembly.GetExecutingAssembly();
                string DLLName = $"{e.Name.Substring(0, e.Name.IndexOf(','))}.dll";
                //DLLName = DLLName.Replace(".resources.dll", ".dll"); not sure why but not removing resource from name makes it work even though it will load null during debug
                IEnumerable<string> Resource2Load = thisAssembly.GetManifestResourceNames().Where(s => s.EndsWith(DLLName));
                if (Resource2Load.Any())
                {
                    using (Stream StreamTemp = thisAssembly.GetManifestResourceStream(Resource2Load.First()))
                    {
                        if (StreamTemp == null) return null;
                        byte[] ResourceBytes = new byte[StreamTemp.Length];
                        StreamTemp.Read(ResourceBytes, 0, ResourceBytes.Length);
                        return Assembly.Load(ResourceBytes);
                    }
                }
            }
            return null;
        }

        private void Write2Log(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ExceptionHolder = (Exception)e.ExceptionObject;
            string Header = $"{DateTime.UtcNow}, v{Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version}";
            string Message = $"Message: {ExceptionHolder.Message}";
            string InnerException = $"Inner Exception: {ExceptionHolder.InnerException}";
            string Source = $"Source: {ExceptionHolder.Source}";
            string Target = $"Target: {ExceptionHolder.TargetSite}";
            string StackTrace = $"Stack Trace: {ExceptionHolder.StackTrace}";
            File.WriteAllText("ReBotErrorLog.txt", $"{Header}\n{Message}\n{InnerException}\n{Source}\n{Target}\n\n{StackTrace}");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (AppMutex != null && onlyInstance) AppMutex.ReleaseMutex();
            GlobalHotkeys?.UnHookKeyboard();
            base.OnExit(e);
        }

        // - - - - - - - - - - - - - - - -

        [DllImport("User32.dll", EntryPoint = "SetForegroundWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("User32.dll", EntryPoint = "ShowWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("User32.dll", EntryPoint = "IsIconic")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("User32.dll", EntryPoint = "IsWindowVisible")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        // shows the window of the single-instance that is already open
        private static void ShowExistingWindow()
        {
            DateTime CutoffTime = DateTime.Now.AddSeconds(-5);
            Process[] processList = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName);
            foreach (Process process in processList)
            {
                if (process.StartTime < CutoffTime)
                {
                    ShowWindow(process.MainWindowHandle, 1);
                    SetForegroundWindow(process.MainWindowHandle);
                    if (!IsIconic(process.MainWindowHandle) && !IsWindowVisible(process.MainWindowHandle))
                    {
                        MessageBox.Show("I'm still here, just hidden. Did you forget about me?\nPress F1 if it's safe to unhide.", "e621 ReBot", MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.No);
                        //https://michlg.wordpress.com/2013/02/05/wpf-send-keys/ - meh, doesn't work
                    }
                    return;
                }
            }
        }


        // - - - - - - - - - - - - - - - -

        internal enum DWMWINDOWATTRIBUTE
        {
            DWMWA_WINDOW_CORNER_PREFERENCE = 33
        }

        internal enum DWM_WINDOW_CORNER_PREFERENCE
        {
            DWMWCP_DEFAULT = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND = 2,
            DWMWCP_ROUNDSMALL = 3
        }

        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        internal static extern void DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref DWM_WINDOW_CORNER_PREFERENCE pvAttribute, uint cbAttribute);

        internal static void SetWindow2Square(Window WindowRef)
        {
            string OSVersion = Environment.OSVersion.Version.ToString();
            if (OSVersion.StartsWith("10"))
            {
                if (WindowRef.WindowStyle != WindowStyle.None)
                {
                    //windows ends up smaller size than specified, fix that.
                    WindowRef.Width += 16;
                    WindowRef.Height += 23;
                }
                if (OSVersion.StartsWith("10.0.22"))
                {
                    IntPtr hWnd = new WindowInteropHelper(Window.GetWindow(WindowRef)).EnsureHandle();
                    DWM_WINDOW_CORNER_PREFERENCE CornerPreference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
                    DwmSetWindowAttribute(hWnd, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, ref CornerPreference, sizeof(uint));
                }
            }
        }
    }
}