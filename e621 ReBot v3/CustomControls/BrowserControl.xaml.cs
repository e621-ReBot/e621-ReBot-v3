using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CefSharp;
using e621_ReBot_v3.Modules;

namespace e621_ReBot_v3.CustomControls
{
    public partial class BrowserControl : UserControl
    {
        internal static BrowserControl? _RefHolder;

        public BrowserControl()
        {
            InitializeComponent();
            _RefHolder = this;
            Url_TextBox.TextChanged += Url_TextBox_TextChanged;
            BrowserControls_Panel.Visibility = Visibility.Hidden;
            BrowserQuickButtons.IsEnabled = false;

            BB_Grab.Visibility = Visibility.Collapsed;
            BB_GrabAll.Visibility = Visibility.Collapsed;
            BB_Download.Visibility = Visibility.Collapsed;
            BB_PoolWatcher.Visibility = Visibility.Collapsed;
            BB_DevTools.Visibility = Visibility.Hidden;
        }

        internal void InitilizeBrowser()
        {
            if (Module_CefSharp.CefSharpBrowser == null)
            {
                Window_Main._RefHolder.ReBot_Menu_ListBox.IsEnabled = false;
                Window_Main._RefHolder.ReBot_Menu_ListBox.SelectedIndex = 1;
                DispatcherTimer DispatcherTimerTemp = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                DispatcherTimerTemp.Tick += (sender, args) =>
                {
                    DispatcherTimerTemp.Stop();

                    Module_CefSharp.InitializeBrowser();
                    BrowserGrid.Children.Add(Module_CefSharp.CefSharpBrowser);
                    Grid.SetRow(Module_CefSharp.CefSharpBrowser, 1);

                    Visibility = Visibility.Visible;
                    Window_Main._RefHolder.ReBot_e6Button.Cursor = KeyboardHotkeys.Cursor_ReBotNav;
                    Window_Main._RefHolder.ReBot_Menu_ListBox.IsEnabled = true;
                    if (AppSettings.DevMode) BB_DevTools.Visibility = Visibility.Visible;
                };
                DispatcherTimerTemp.Start();
            }
        }

        private void BB_Bookmarks_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //Fix the focus thing so that click works as toggle and hides it when shown.
            ShowBookmarkMenu = !BookmarksMenu.IsVisible;
        }

        bool ShowBookmarkMenu = false;
        private void BB_Bookmarks_Click(object sender, RoutedEventArgs e)
        {
            if (ShowBookmarkMenu)
            {
                BookmarksMenu.Visibility = Visibility.Visible;
                BookmarksMenu.Focus();
            }
        }

        private void BB_Back_Click(object sender, RoutedEventArgs e)
        {
            Module_CefSharp.CefSharpBrowser.Back();
        }

        private void BB_Reload_Click(object sender, RoutedEventArgs e)
        {
            Module_CefSharp.CefSharpBrowser.Reload();
        }

        private void BB_Forward_Click(object sender, RoutedEventArgs e)
        {
            Module_CefSharp.CefSharpBrowser.Forward();
        }

        private void BB_Home_Click(object sender, RoutedEventArgs e)
        {
            //BrowserQuickButtons.Visibility = BrowserQuickButtons.IsVisible ? Visibility.Hidden : Visibility.Visible;
            BrowserQuickButtons.IsEnabled = !BrowserQuickButtons.IsEnabled;
        }

        private void Url_TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Url_TextBox.Dispatcher.BeginInvoke(() => { Url_TextBox.SelectAll(); });
        }

        private void Url_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            BB_Navigate.IsEnabled = !string.IsNullOrEmpty(Url_TextBox.Text);
        }

        private void Url_TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    {
                        e.Handled = true;
                        BB_Navigate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        break;
                    }

                case Key.V:
                    {
                        // Detect Paste
                        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Clipboard.GetDataObject().GetDataPresent(DataFormats.StringFormat))
                        {
                            e.Handled = true;
                            string ClipboardText = (string)Clipboard.GetDataObject().GetData(DataFormats.StringFormat);
                            ClipboardText = WebUtility.UrlDecode(ClipboardText);

                            if (Url_TextBox.SelectedText.Length > 0)
                            {
                                Url_TextBox.Text = Url_TextBox.Text.Replace(Url_TextBox.SelectedText, ClipboardText);
                            }
                            else
                            {
                                Url_TextBox.Text = ClipboardText;
                            }
                            Url_TextBox.SelectionStart = Url_TextBox.Text.Length;
                        }
                        break;
                    }

                default:
                    {
                        break;
                    }
            }
        }

        private void BB_Navigate_Click(object sender, RoutedEventArgs e)
        {
            Module_CefSharp.LoadURL(Url_TextBox.Text);
        }

        private void BQB_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Module_CefSharp.CefSharpBrowser != null) Module_CefSharp.LoadURL(((Button)sender).Tag.ToString());
            //BrowserQuickButtons.Visibility = Visibility.Hidden;
            BrowserQuickButtons.IsEnabled = false;
        }

        private void BB_Grab_Click(object sender, RoutedEventArgs e)
        {
            BB_Grab.Visibility = Visibility.Collapsed;
            //Worker_Sound();
            string TagPass = BB_Grab.Tag.ToString();
            ThreadPool.QueueUserWorkItem(state => Module_Grabber.Grab_1Link(TagPass));
        }

        private void BB_Download_Click(object sender, RoutedEventArgs e)
        {
            BB_Download.Visibility = Visibility.Collapsed;
            //Worker_Sound();
            string TagPass = BB_Download.Tag.ToString();
            ThreadPool.QueueUserWorkItem(state => Module_Downloader.Grab_DownloadMedia(TagPass));       
            //Module_Downloader.UpdateTreeViewNodes();
            //Module_Downloader.timer_Download.Start();
        }

        private void BB_PoolWatcher_Click(object sender, RoutedEventArgs e)
        {
            BB_PoolWatcher.Visibility = Visibility.Collapsed;
            if (BB_PoolWatcher.Tag == null)
            {
                ThreadPool.QueueUserWorkItem(state => Window_PoolWatcher.PoolWatcher_AddPool2Watch());
                Window_Main._RefHolder.SettingsButton_PoolWatcher.IsEnabled = true;
            }
            else
            {
                string PoolID = BB_PoolWatcher.Tag.ToString();
                PoolItem? PoolItemTemp = AppSettings.PoolWatcher.Where(PoolItem => PoolItem.ID == int.Parse(PoolID)).SingleOrDefault();
                lock (AppSettings.PoolWatcher)
                {
                    AppSettings.PoolWatcher.Remove(PoolItemTemp);
                }
                Window_Main._RefHolder.SettingsButton_PoolWatcher.IsEnabled = AppSettings.PoolWatcher.Any();
            }
        }

        private void BB_DevTools_Click(object sender, RoutedEventArgs e)
        {
            Module_CefSharp.CefSharpBrowser.ShowDevTools();
        }
    }
}