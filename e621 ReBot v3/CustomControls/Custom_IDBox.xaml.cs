using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace e621_ReBot_v3.CustomControls
{
    public partial class Custom_IDBox : Window
    {
        public Custom_IDBox()
        {
            InitializeComponent();
            App.SetWindow2Square(this);

            ID_TextBox.Focus();
        }

        private readonly List<string> WhitelistedURLs = new List<string>() { "https://e621.net/posts/", "https://e621.net/pools/" };
        private void ID_TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9
            || e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                // Don't allow 0 as first value
                if (ID_TextBox.Text.Length == 0 && (e.Key == Key.NumPad0 || e.Key == Key.D0)) e.Handled = true;
            }
            else
            {
                switch (e.Key)
                {
                    case Key.V:
                        {
                            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Clipboard.GetDataObject().GetDataPresent(DataFormats.StringFormat))
                            {
                                string ClipboardText = (string)Clipboard.GetDataObject().GetData(DataFormats.StringFormat);
                                if (WhitelistedURLs.Any(s => ClipboardText.Contains(s)))
                                {
                                    ClipboardText = ClipboardText.Replace("https://e621.net/", "");
                                    if (ClipboardText.Contains('?')) ClipboardText = ClipboardText.Substring(0, ClipboardText.IndexOf('?'));

                                    string ClipboardID = ClipboardText.Split('/', StringSplitOptions.RemoveEmptyEntries)[1];
                                    if (int.TryParse(ClipboardID, out _))
                                    {
                                        ID_TextBox.Text = ClipboardID;
                                        ID_TextBox.SelectionStart = ID_TextBox.Text.Length;
                                    }
                                    return;
                                }

                                if (ClipboardText.Length < 8 && int.TryParse(ClipboardText, out _))
                                {
                                    ID_TextBox.Text = ClipboardText;
                                    ID_TextBox.SelectionStart = ID_TextBox.Text.Length;
                                    return;
                                }
                            }
                            e.Handled = true;
                            return;
                        }

                    case Key.A:
                        {
                            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
                            break;
                        }

                    case Key.Back:
                        {
                            return;
                        }

                    case Key.Escape:
                        {
                            NotCancel = false;
                            e.Handled = true;
                            Close();
                            return;
                        }

                    case Key.Enter:
                        {
                            NotCancel = true;
                            e.Handled = true;
                            Close();
                            return;
                        }
                }
                e.Handled = true;
            }
        }

        private void ID_TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ID_TextBox.Dispatcher.BeginInvoke(() => { ID_TextBox.SelectAll(); });
        }

        // - - - - - - - - - - - - - - - -

        private bool NotCancel = false;
        internal static string? ShowIDBox(Window OwnerWindow, Point StartingPoint, string Title, SolidColorBrush? BGColor = null)
        {
            Custom_IDBox Custom_IDBoxTemp = new Custom_IDBox()
            {
                Owner = OwnerWindow,
                Left = StartingPoint.X,
                Top = StartingPoint.Y,
                Title = Title
            };
            if (BGColor != null) Custom_IDBoxTemp.Background = BGColor;
            Custom_IDBoxTemp.ShowDialog();

            string? IDEntered = null;
            if (Custom_IDBoxTemp.NotCancel) IDEntered = string.IsNullOrEmpty(Custom_IDBoxTemp.ID_TextBox.Text) ? null : Custom_IDBoxTemp.ID_TextBox.Text;
            return IDEntered;
        }
    }
}