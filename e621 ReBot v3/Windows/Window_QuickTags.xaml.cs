using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using e621_ReBot_v3.CustomControls;

namespace e621_ReBot_v3
{
    public partial class Window_QuickTags : Window
    {
        private readonly bool RestorePoolMode = false;
        public Window_QuickTags()
        {
            InitializeComponent();
            App.SetWindow2Square(this);
            Owner = Window_Tagger._RefHolder;
            Window_Tagger.SuggestionPopup.SetTextBoxTarget(QuickTags_TextBox, true);
            RestorePoolMode = Window_Tagger.SuggestionPopup.PoolMode;
            Window_Tagger.SuggestionPopup.PoolMode = false;
            LoadQuickTags();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Window_Tagger.SuggestionPopup.PoolMode = RestorePoolMode;
            Window_Tagger._RefHolder.Tags_TextBox.Focus();
            Window_Tagger._RefHolder.Tags_TextBox.SelectionStart = Window_Tagger._RefHolder.Tags_TextBox.Text.Length;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            QuickTags_TextBox.Focus();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    {
                        Close();
                        break;
                    }
            }
        }

        private void QuickTags_TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            QuickTags_TextBox.SelectAll();
        }

        private void QuickTags_TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    {
                        CreateQuickTag_Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        break;
                    }
                case Key.Escape:
                    {
                        Close();
                        break;
                    }
            }
        }

        private void CreateQuickTag_Button_Click(object sender, RoutedEventArgs e)
        {
            if (QuickTags_TextBox.Text.Length == 0)
            {
                MessageBox.Show(this, "You must add some tags first.", "Quick Tags", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                string InputedText = Custom_InputBox.ShowInputBox(this, "Quick Tags Title", "Add Title to Quick Tags button.", CreateQuickTag_Button.PointToScreen(new Point(0, 0))).ToLower();
                if (InputedText.Equals("☠")) return;

                if (string.IsNullOrEmpty(InputedText))
                {
                    MessageBox.Show(this, "Quick Tags Title can not be blank.", "Quick Tags Title", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (AppSettings.QuickTags.ContainsKey(InputedText))
                {
                    MessageBox.Show(this, "Quick Tags Title must be unique.", "Quick Tags Title", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                QuickTags_TextBox.Focus();

                lock (AppSettings.QuickTags)
                {
                    AppSettings.QuickTags.Add(InputedText, string.Join(' ', QuickTags_TextBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
                }
                LoadQuickTags();
            }
        }

        private void LoadQuickTags()
        {
            QuickTags_WrapPanel.Children.Clear();
            foreach (KeyValuePair<string, string> QuickTagPair in AppSettings.QuickTags)
            {
                Button ButtonTemp = new Button
                {
                    Foreground = new SolidColorBrush(Colors.Black),
                    Margin = new Thickness(2, 0, 0, 2),
                    Height = 24,
                    MinWidth = 32,
                    MaxWidth = 64,
                    Content = QuickTagPair.Key,
                    Tag = QuickTagPair.Value,
                    ToolTip = QuickTagPair.Value,
                    Cursor = Cursors.Hand,
                    ContextMenu = (ContextMenu)FindResource("QuickTagRemove_ContextMenu")
                };
                ButtonTemp.Click += QuickTagButton_Click;
                QuickTags_WrapPanel.Children.Add(ButtonTemp);
            }
        }

        private void QuickTagButton_Click(object sender, RoutedEventArgs e)
        {
            Window_Tagger._RefHolder.Tags_TextBox.Text = Window_Tagger._RefHolder.Tags_TextBox.Text.Trim();
            Window_Tagger._RefHolder.Tags_TextBox.AppendText($" {((Button)sender).Tag} ");
        }

        private void QuickTagRemove_ContextMenu_Remove(object sender, RoutedEventArgs e)
        {
            MenuItem MenuItemClicked = (MenuItem)sender;
            ContextMenu ContextMenuParent = (ContextMenu)MenuItemClicked.Parent;
            Button ButtonTarget = (Button)ContextMenuParent.PlacementTarget;

            lock (AppSettings.QuickTags)
            {
                AppSettings.QuickTags.Remove((string)ButtonTarget.Content);
            }
            LoadQuickTags();
        }
    }
}
