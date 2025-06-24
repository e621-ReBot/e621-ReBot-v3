using e621_ReBot_v3.CustomControls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace e621_ReBot_v3
{
    //https://stackoverflow.com/a/46014022/8810532
    //http://www.dylansweb.com/2014/10/low-level-global-keyboard-hook-sink-in-c-net/
    internal class KeyboardHotkeys
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x101;
        private const int WM_SYSKEYUP = 0x105;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        internal event EventHandler<Key>? OnKeyPressed;
        internal event EventHandler<Key>? OnKeyUnpressed;

        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        internal KeyboardHotkeys()
        {
            _proc = HookCallback;
            OnKeyPressed += Kbh_OnKeyPressed;
            OnKeyUnpressed += Kbh_OnKeyUnpressed;
        }

        internal void HookKeyboard()
        {
            _hookID = SetHook(_proc);
        }

        internal void UnHookKeyboard()
        {
            UnhookWindowsHookEx(_hookID);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            {
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
                }
            }
        }

        List<Key> PressedKeyList = new List<Key>();
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                Key KeyDetected = KeyInterop.KeyFromVirtualKey(Marshal.ReadInt32(lParam));
                if (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN)
                {
                    OnKeyPressed?.Invoke(this, KeyDetected);
                }
                if (wParam == WM_KEYUP || wParam == WM_SYSKEYUP)
                {
                    OnKeyUnpressed?.Invoke(this, KeyDetected);
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        internal static readonly Cursor Cursor_Default = (Cursor)Application.Current.FindResource("Cursor_Default");
        internal static readonly Cursor Cursor_ReBotNav = (Cursor)Application.Current.FindResource("Cursor_ReBotNav");
        internal static readonly Cursor Cursor_BrowserNav = (Cursor)Application.Current.FindResource("Cursor_BrowserNav");
        private void Kbh_OnKeyPressed(object sender, Key e)
        {
            if (!PressedKeyList.Contains(e))
            {
                PressedKeyList.Add(e);
                switch (e)
                {
                    case Key.F1:
                        {
                            if (Window_Main._RefHolder.IsVisible)
                            {
                                for (int i = Application.Current.Windows.Count - 1; i > 0; i--)
                                {
                                        Application.Current.Windows[i].Close();                              
                                }
                                Window_Main._RefHolder.ReBot_Menu_ListBox.SelectedIndex = 0;
                                Window_Main._RefHolder.ShowInTaskbar = false;
                                Window_Main._RefHolder.Hide();
                            }
                            else
                            {
                                Window_Main._RefHolder.ShowInTaskbar = true;
                                Window_Main._RefHolder.Show();
                                if (Window_Main._RefHolder.WindowState == WindowState.Minimized) Window_Main._RefHolder.WindowState = WindowState.Normal;
                            }
                            break;
                        }
                    case Key.LeftShift:
                    case Key.RightShift:
                        {
                            break;
                        }
                    case Key.LeftCtrl:
                    case Key.RightCtrl:
                        {
                            if (Window_MediaSelect._RefHolder != null && Window_MediaSelect._RefHolder.Title.Contains("Similar Search"))
                            {
                                foreach (MediaSelectItem MediaSelectItemTemp in Window_MediaSelect._RefHolder.ItemPanel.Children)
                                {
                                    MediaSelectItemTemp.Cursor = Cursors.Hand;
                                }
                            }
                            break;
                        }
                    case Key.LeftAlt:
                    case Key.RightAlt:
                        {
                            if (Window_Main._RefHolder.IsVisible)
                            {
                                if (Window_Main._RefHolder.ReBot_Menu_ListBox.SelectedIndex == 2)
                                {
                                    foreach (GridVE GridVETemp in Window_Main._RefHolder.Grid_GridVEPanel.Children)
                                    {
                                        GridVETemp.IsUploaded_DockPanel.Cursor = Cursor_BrowserNav;
                                        GridVETemp.cIsSuperior_Polygon.Cursor = Cursor_BrowserNav;
                                    }
                                }
                            }
                            if (Window_Preview._RefHolder != null) Window_Preview._RefHolder.AlreadyUploaded_Label.Cursor = Cursor_BrowserNav;
                            if (Window_MediaSelect._RefHolder != null && Window_MediaSelect._RefHolder.Title.Contains("Similar Search"))
                            {
                                foreach (MediaSelectItem MediaSelectItemTemp in Window_MediaSelect._RefHolder.ItemPanel.Children)
                                {
                                    MediaSelectItemTemp.Cursor = Cursor_BrowserNav;
                                }
                            }
                            break;
                        }
                }
            }
        }

        private void Kbh_OnKeyUnpressed(object sender, Key e)
        {
            PressedKeyList.Remove(e);
            switch (e)
            {
                case Key.LeftShift:
                case Key.RightShift:
                    {
                        break;
                    }
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    {
                        if (Window_MediaSelect._RefHolder != null)
                        {
                            foreach (MediaSelectItem MediaSelectItemTemp in Window_MediaSelect._RefHolder.ItemPanel.Children)
                            {
                                MediaSelectItemTemp.Cursor = Cursors.No;
                            }
                        }
                        break;
                    }
                case Key.LeftAlt:
                case Key.RightAlt:
                    {
                        if (Window_Main._RefHolder.IsVisible)
                        {
                            if (Window_Main._RefHolder.ReBot_Menu_ListBox.SelectedIndex == 2)
                            {
                                foreach (GridVE GridVETemp in Window_Main._RefHolder.Grid_GridVEPanel.Children)
                                {
                                    GridVETemp.IsUploaded_DockPanel.Cursor = Cursor_ReBotNav;
                                    GridVETemp.cIsSuperior_Polygon.Cursor = Cursor_ReBotNav;
                                }
                            }
                        }
                        if (Window_Preview._RefHolder != null) Window_Preview._RefHolder.AlreadyUploaded_Label.Cursor = Cursor_ReBotNav;
                        if (Window_MediaSelect._RefHolder != null && Window_MediaSelect._RefHolder.Title.Contains("Similar Search"))
                        {
                            foreach (MediaSelectItem MediaSelectItemTemp in Window_MediaSelect._RefHolder.ItemPanel.Children)
                            {
                                MediaSelectItemTemp.Cursor = Cursors.No;
                            }
                        }
                        break;
                    }
            }
        }
    }
}