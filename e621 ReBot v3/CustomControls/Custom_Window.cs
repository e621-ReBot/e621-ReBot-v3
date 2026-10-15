using System;
using System.Windows;
using System.Windows.Interop;

namespace e621_ReBot_v3.CustomControls
{
    public partial class Custom_Window : Window
    {
        private const int WM_SYSCOMMAND = 0x0112;
        private const long SC_KEYMENU = 0xF100;

        private HwndSource? _hwndSource;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);

            _hwndSource?.AddHook(WindowProc);
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SYSCOMMAND && (wParam.ToInt64() & 0xFFF0) == SC_KEYMENU)
            {
                //ignore window menu command, there is no menu and causes those "lost focus" issues with cursor
                handled = true;
                return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            _hwndSource?.RemoveHook(WindowProc);
            _hwndSource = null;

            base.OnClosed(e);
        }
    }
}