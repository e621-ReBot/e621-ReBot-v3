using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using e621_ReBot_v3.Modules;

namespace e621_ReBot_v3.CustomControls
{
    public partial class BookmarkControl : UserControl
    {
        public BookmarkControl()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            BookmarkTextBox.TextChanged += BookmarkTextbox_TextChanged;
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                BookmarkList.Visibility = AppSettings.Bookmarks.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                LoadBookmarks();
                SetButtonFunction();
            }
        }

        private void LoadBookmarks()
        {
            if (AppSettings.Bookmarks.Count > 0)
            {
                BookmarkList.Items.Clear();
                foreach (DictionaryEntry DictionaryEntryTemp in AppSettings.Bookmarks)
                {
                    ListViewItem ListViewItemTemp = new ListViewItem
                    {
                        Content = DictionaryEntryTemp.Value,
                        Tag = DictionaryEntryTemp.Key,
                        ToolTip = $"{DictionaryEntryTemp.Value}\n{DictionaryEntryTemp.Key}",
                        IsTabStop= false
                    };
                    BookmarkList.Items.Add(ListViewItemTemp);
                }
            }
        }

        private void SetButtonFunction()
        {
            if (Module_CefSharp.BrowserAddress == null)
            {
                BookmarkButton.IsEnabled = false;
                BookmarkTextBox.IsEnabled = false;
                return;
            }

            BookmarkTextBox.Tag = Module_CefSharp.BrowserAddress;
            if (AppSettings.Bookmarks.Contains(Module_CefSharp.BrowserAddress))
            {
                BookmarkButton.Tag = "Remove";
                BookmarkButton.Content = "✗";
                BookmarkButton.Background = new SolidColorBrush(Colors.Red);
                BookmarkButton.ToolTip = "Remove Bookmark";
                BookmarkTextBox.IsReadOnly = true;
                BookmarkTextBox.Text = AppSettings.Bookmarks[Module_CefSharp.BrowserAddress].ToString();
            }
            else
            {
                BookmarkButton.Tag = null;
                BookmarkButton.Content = "✓";
                BookmarkButton.Background = new SolidColorBrush(Colors.DarkGreen);
                BookmarkButton.ToolTip = "Save Bookmark";
                BookmarkTextBox.IsReadOnly = false;
                BookmarkTextBox.Text = Module_CefSharp.CefSharpBrowser.Title;
            }
            BookmarkButton.IsEnabled = true;
            BookmarkTextBox.IsEnabled = true;
        }

        private void BookmarkTextbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            BookmarkButton.IsEnabled = !string.IsNullOrEmpty(((TextBox)sender).Text);
        }

        private void BookmarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (BookmarkButton.Tag == null)
            {
                AppSettings.Bookmarks.Add(BookmarkTextBox.Tag.ToString(), BookmarkTextBox.Text.Trim());
            }
            else
            {
                AppSettings.Bookmarks.Remove(BookmarkTextBox.Tag.ToString());
            }
            BrowserControl._RefHolder.BrowserBorder.Focus(); //"Close"
        }

        private void BookmarkList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BookmarkList.SelectedIndex > -1)
            {
                Module_CefSharp.CefSharpBrowser.LoadUrl(((ListViewItem)BookmarkList.Items[BookmarkList.SelectedIndex]).Tag.ToString());
                BrowserControl._RefHolder.BrowserBorder.Focus(); //"Close"
            }
        }

        private void UserControl_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!IsKeyboardFocusWithin) Visibility = Visibility.Collapsed;
        }
    }
}