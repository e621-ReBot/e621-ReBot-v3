using e621_ReBot_v3.CustomControls;
using e621_ReBot_v3.Modules;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace e621_ReBot_v3
{

    public partial class Window_Tagger : Window
    {
        internal static Window_Tagger? _RefHolder;
        internal MediaItem? MediaItemHolder;
        internal static Custom_SuggestBox? SuggestionPopup;
        static List<string>? Artist_List;
        static List<string> DNP_List = Properties.Resources.DNPs.Split('✄').ToList();
        static List<string> Gender_List = Properties.Resources.genders.Split('✄').ToList();
        public Window_Tagger()
        {
            InitializeComponent();
            _RefHolder = this;
            App.SetWindow2Square(this);

            if (SuggestionPopup == null)
            {
                if (File.Exists("tags.txt") && File.Exists("pools.txt"))
                {
                    SuggestionPopup = new Custom_SuggestBox();
                }
                if (File.Exists("artists.txt"))
                {
                    Artist_List = File.ReadAllText("artists.txt").Split('✄', StringSplitOptions.RemoveEmptyEntries).ToList();
                }
                if (File.Exists("DNPs.txt"))
                {
                    DNP_List = File.ReadAllText("DNPs.txt").Split('✄', StringSplitOptions.RemoveEmptyEntries).ToList();
                }
                if (File.Exists("genders.txt"))
                {
                    Gender_List = File.ReadAllText("genders.txt").Split('✄', StringSplitOptions.RemoveEmptyEntries).ToList();
                }
            }
            if (SuggestionPopup != null)
            {
                SuggestionPopup.SetTextBoxTarget(Tags_TextBox, true);
                SuggestionSwitch.IsEnabled = true;
            }
        }

        internal static void OpenTagger(Window OwnerWindow, MediaItem MediaItemRef, Point TaggerLocation, bool SetPoint = false, bool ForceOnTop = false)
        {
            if (_RefHolder == null) new Window_Tagger();
            _RefHolder.Owner = OwnerWindow;
            _RefHolder.MediaItemHolder = MediaItemRef;
            if (SetPoint)
            {
                //_RefHolder.PointToScreen(TaggerLocation);
                _RefHolder.Left = TaggerLocation.X - 4;
                _RefHolder.Top = TaggerLocation.Y - 2;
            }
            else
            {
                _RefHolder.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            if (ForceOnTop)
            {
                _RefHolder.ShowDialog();
            }
            else
            {
                _RefHolder.Show();
                _RefHolder.Activate();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Title_TextBox.Text = MediaItemHolder.Grab_Title;
            Description_TextBox.Text = MediaItemHolder.Grab_TextBody;
            string ArtistAlias = AppSettings.ArtistAlias_Check(MediaItemHolder);
            if (ArtistAlias != null)
            {
                TB_ArtistAlias.Tag = ArtistAlias;
                TB_ArtistAlias.Foreground = new SolidColorBrush(Colors.RoyalBlue);
            }
            //TB_CommandLine.ForeColor = Properties.Settings.Default.CommandLineCommands == null ? SystemColors.ControlText : Color.RoyalBlue;
            TB_ParentOffset.IsEnabled = false;
            if (Module_Grabber._Grabbed_MediaItems.Count > 1)
            {
                int MediaItemIndex = Module_Grabber._Grabbed_MediaItems.FindIndex(MediaItemHolder);
                if (MediaItemIndex == 0)
                {
                    for (int i = MediaItemIndex = 1; i <= Module_Grabber._Grabbed_MediaItems.Count - 1; i++)
                    {
                        if (Module_Grabber._Grabbed_MediaItems[i].UP_UploadedID != null)
                        {
                            TB_ParentOffset.IsEnabled = true;
                            break;
                        }
                    }
                }
                else
                {
                    TB_ParentOffset.IsEnabled = true;
                }
            }
            TB_ParentOffset.Foreground = new SolidColorBrush(MediaItemHolder.UP_ParentMediaItem == null ? Colors.Black : Colors.RoyalBlue);

            if (MediaItemHolder.UP_Tags != null)
            {
                List<string> SortTags = MediaItemHolder.UP_Tags.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (ArtistAlias != null) SortTags.Add(ArtistAlias);
                SortTags = SortTags.Distinct().ToList();
                Tags_TextBox.Text = $"{string.Join(' ', SortTags)} ";
                Tags_TextBox.SelectionStart = Tags_TextBox.Text.Length;

                CountTags();
            }

            //if (Properties.Settings.Default.AutocompleteTags)
            //{
            //    Form_Loader._FormReference.AutoTags.SetAutocompleteMenu(textBox_Tags, Form_Loader._FormReference.AutoTags);
            //    cGroupBoxColored_AutocompleteSelector.Enabled = true;
            //}
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            Tags_TextBox.Focus();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SuggestionPopup?.SuggestionTimer.Stop();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            SuggestionPopup.PoolMode = false;
            SuggestionPopup.RemoveTextBoxTarget(Tags_TextBox);
            _RefHolder = null;
            Owner.Activate();
        }

        // - - - - - - - - - - - - - - - -

        [GeneratedRegex(@"[ ]{2,}", RegexOptions.None)]
        private static partial Regex Tagger_Regex1();
        internal void CountTags()
        {
            List<string> SortTags = MediaItemHolder.UP_Tags.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
            Title = $"Tagger - Tags: {SortTags.Count}";

            if (Tags_TextBox.SelectionStart > 0)
            {
                int TextBoxCursorIndex = Tags_TextBox.SelectionStart - 1;
                if (Tags_TextBox.Text.Substring(TextBoxCursorIndex, 1).Equals(" "))
                {
                    int WordStartIndex = Tags_TextBox.Text.Substring(0, TextBoxCursorIndex).LastIndexOf(' ') + 1;
                    int WordEndIndex = Tags_TextBox.Text.IndexOf(' ', WordStartIndex);
                    if (WordEndIndex == -1) WordEndIndex = Tags_TextBox.Text.Length;
                    string SelectedWord = Tags_TextBox.Text.Substring(WordStartIndex, WordEndIndex - WordStartIndex);
                    SortTags = Tags_TextBox.Text.Remove(WordStartIndex, WordEndIndex - WordStartIndex).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (SortTags.Contains(SelectedWord))
                    {
                        Tags_TextBox.Text = Tags_TextBox.Text.Remove(WordStartIndex, WordEndIndex - WordStartIndex);
                        Tags_TextBox.SelectionStart = Tags_TextBox.Text.Length;
                    }
                }
            }

            int CursorHolder = Tags_TextBox.SelectionStart;
            Tags_TextBox.Text = Tagger_Regex1().Replace(Tags_TextBox.Text, " "); // replace multiple spaces with one https://codesnippets.fesslersoft.de/how-to-replace-multiple-spaces-with-a-single-space-in-c-or-vb-net/
            Tags_TextBox.Text = Tags_TextBox.Text.TrimStart().ToLower();
            Tags_TextBox.SelectionStart = CursorHolder;
        }

        private void Title_TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    {
                        e.Handled = true;
                        Tags_TextBox.Focus();
                        break;
                    }
                case Key.Escape:
                    {
                        Tags_TextBox.Focus();
                        break;
                    }
            }
        }

        private void Tags_TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    {
                        AddTags();
                        PreCloseChecks();
                        e.Handled = true;
                        return;
                    }
                case Key.Escape:
                    {
                        Close();
                        e.Handled = true;
                        return;
                    }
                case Key.Tab:
                    {
                        e.Handled = true;
                        return;
                    }
            }
        }

        private void PreCloseChecks()
        {
            if (TagsAdded)
            {
                List<string> TagListOnClose = Tags_TextBox.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (Artist_List != null)
                {
                    string? AnyArtist = TagListOnClose.Intersect(Artist_List).FirstOrDefault();
                    if (string.IsNullOrEmpty(AnyArtist) && MessageBox.Show(this, $"There is no artist tagged for this media, DNP list can not be checked, are you sure you want to proceed?", "e621 ReBot", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No)
                    {
                        Tags_TextBox.AppendText(" ");
                        Tags_TextBox.SelectionStart = Tags_TextBox.Text.Length;
                        TagsAdded = false;
                        return;
                    }
                }
                string? DNPArtist = TagListOnClose.Intersect(DNP_List).FirstOrDefault();
                if (!string.IsNullOrEmpty(DNPArtist) && MessageBox.Show(this, $"Artist: {DNPArtist} is on DNP list, are you sure you want to proceed?", "e621 ReBot", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No)
                {
                    Tags_TextBox.AppendText(" ");
                    Tags_TextBox.SelectionStart = Tags_TextBox.Text.Length;
                    TagsAdded = false;
                    return;
                }
                if (!TagListOnClose.Intersect(Gender_List).Any() && MessageBox.Show(this, "You have not added any gender tags, are you sure you want to proceed?", "e621 ReBot", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.No)
                {
                    Tags_TextBox.AppendText(" ");
                    Tags_TextBox.SelectionStart = Tags_TextBox.Text.Length;
                    TagsAdded = false;
                    return;
                }
                MediaItemHolder.UP_Tags = Tags_TextBox.Text;
            }
        }

        private void Tags_TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space:
                    {
                        if (Tags_TextBox.SelectionStart > 0 && Tags_TextBox.Text.Substring(Tags_TextBox.SelectionStart - 1, 1).Equals(" "))
                        {
                            e.Handled = true;
                            break;
                        }
                        if (Tags_TextBox.SelectionStart + 1 <= Tags_TextBox.Text.Length && Tags_TextBox.Text.Substring(Tags_TextBox.SelectionStart, 1).Equals(" "))
                        {
                            e.Handled = true;
                            break;
                        }
                        Dispatcher.BeginInvoke(CountTags);
                        break;
                    }
                case Key.Back:
                case Key.Delete:
                    {
                        Dispatcher.BeginInvoke(CountTags);
                        break;
                    }

                case Key.V: //Doesn't work in KeyDown
                    {
                        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                        {
                            e.Handled = true;
                            if (Clipboard.GetDataObject().GetDataPresent(DataFormats.StringFormat))
                            {
                                List<string> PasteTags = ((string)Clipboard.GetDataObject().GetData(DataFormats.StringFormat)).ToLower().Replace(Environment.NewLine, null).Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();

                                if (PasteTags.Count == 1 && Tags_TextBox.SelectionStart > 0 && Tags_TextBox.Text.Substring(Tags_TextBox.SelectionStart - 1).Equals(":"))
                                {
                                    Tags_TextBox.Text += $"{string.Join(' ', PasteTags)} ";
                                }
                                else
                                {
                                    List<string> TextBoxTags = Tags_TextBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
                                    for (int i = PasteTags.Count - 1; i >= 0; i--)
                                    {
                                        if (TextBoxTags.Contains(PasteTags[i]))
                                        {
                                            PasteTags.RemoveAt(i);
                                        }
                                    }
                                    TextBoxTags.AddRange(PasteTags);
                                    Tags_TextBox.Text = $"{string.Join(' ', TextBoxTags)} ";
                                }
                                Tags_TextBox.SelectionStart = Tags_TextBox.Text.Length;
                                CountTags();
                            }
                            return;
                        }
                        break;
                    }
            }
        }

        private void Tags_TextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CursorClickChange = true;
        }

        private bool CursorClickChange = false;
        private void Tags_TextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (SuggestionPopup != null && CursorClickChange)
            {
                SuggestionPopup.IsOpen = false;
                CursorClickChange = false;
            }
        }

        private void TB_Done_Click(object sender, RoutedEventArgs e)
        {
            AddTags();
            if (ReferenceEquals(Owner, Window_Preview._RefHolder)) Window_Preview._RefHolder.TaggerLocation = new Point(Left, Top);
            PreCloseChecks();
        }

        private bool TagsAdded = false;
        private void AddTags()
        {
            Tags_TextBox.Text = Tags_TextBox.Text.Trim();
            List<string> SortTags = Tags_TextBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
            SortTags.Sort();
            Tags_TextBox.Text = string.Join(' ', SortTags).ToLower();

            if (ReferenceEquals(Owner, Window_Preview._RefHolder)) Window_Preview._RefHolder.Tags_TextBlock.Text = Tags_TextBox.Text;
            if (MediaItemHolder.UP_UploadedID == null)
            {
                GridVE GridVETemp = Module_Grabber.IsVisibleInGrid(MediaItemHolder);
                if (GridVETemp != null) GridVETemp.cTagWarning_TextBlock.Visibility = SortTags.Count < 16 ? Visibility.Visible : Visibility.Hidden;
                TagsAdded = true;
            }
        }

        private void TB_Description_Click(object sender, RoutedEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                string Description = MediaItemHolder.Grab_Title;
                if (!string.IsNullOrEmpty(MediaItemHolder.Grab_TextBody))
                {
                    Description = $"[section={Description}]\n{MediaItemHolder.Grab_TextBody}\n[/section]";
                }
                Clipboard.SetText(Description);
                MessageBox.Show(this, "Descripton has been copied to clipboard.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (Description_TextBox.IsVisible)
            {
                Description_TextBox.Visibility = Visibility.Hidden;
                TB_Description.Content = "↥ Description";
                Tags_TextBox.Visibility = Visibility.Visible;
                Tags_TextBox.Focus();
            }
            else
            {
                Tags_TextBox.Visibility = Visibility.Hidden;
                TB_Description.Content = "↧ Description";
                Description_TextBox.Visibility = Visibility.Visible;
            }
        }

        [GeneratedRegex(@"[ ]{2,}", RegexOptions.None)]
        private static partial Regex Tagger_Regex2();
        private void TB_ArtistAlias_Click(object sender, RoutedEventArgs e)
        {
            Tags_TextBox.Focus();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && TB_ArtistAlias.Tag != null)
            {
                AppSettings.ArtistAlias_Remove(MediaItemHolder);
                Tags_TextBox.Text = Tags_TextBox.Text.Replace(TB_ArtistAlias.Tag.ToString(), null);
                TB_ArtistAlias.Foreground = new SolidColorBrush(Colors.Black);
                TB_ArtistAlias.Tag = null;
                CountTags();
                MessageBox.Show(this, $"Alias removed from artist {MediaItemHolder.Grab_Artist}", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string InputBoxDescription = $"Chose a new alias for artist: {MediaItemHolder.Grab_Artist}{(TB_ArtistAlias.Tag == null ? null : $"\nCurrent alias is: {TB_ArtistAlias.Tag}")}";
            string InputBoxAliastString = TB_ArtistAlias.Tag == null ? MediaItemHolder.Grab_Artist.ToLower() : TB_ArtistAlias.Tag.ToString();
            string InputedText = Custom_InputBox.ShowInputBox(this, "Create Artist Alias", InputBoxDescription, TB_ArtistAlias.PointToScreen(new Point(0, 0)), InputBoxAliastString).ToLower();
            if (InputedText.Equals("☠")) return;

            if (string.IsNullOrEmpty(InputedText))
            {
                MessageBox.Show(this, "Artist Alias can not be blank.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                AppSettings.ArtistAlias_Add(MediaItemHolder, InputedText); //add or update

                MessageBox.Show(this, $"{MediaItemHolder.Grab_Artist} is now aliased to {InputedText}", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information);
                if (TB_ArtistAlias.Tag != null) Tags_TextBox.Text = Tags_TextBox.Text.Replace(TB_ArtistAlias.Tag.ToString(), null);
                TB_ArtistAlias.Foreground = new SolidColorBrush(Colors.RoyalBlue);
                TB_ArtistAlias.Tag = InputedText;
                Tags_TextBox.Text = Tagger_Regex2().Replace(Tags_TextBox.Text, " ").Trim(); // replace multiple spaces with one https://codesnippets.fesslersoft.de/how-to-replace-multiple-spaces-with-a-single-space-in-c-or-vb-net/
                Tags_TextBox.AppendText($" {InputedText} ");
                Tags_TextBox.SelectionStart = Tags_TextBox.Text.Length;
                CountTags();

            }
        }

        private void TB_QuickTags_Click(object sender, RoutedEventArgs e)
        {
            new Window_QuickTags().ShowDialog();
        }

        private void TB_ParentOffset_Click(object sender, RoutedEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                MediaItemHolder.UP_ParentMediaItem = null;
                return;
            }
            Window_MediaSelect.Show_ParentOffset(TB_ParentOffset.PointToScreen(new Point(0, 0)));
            TB_ParentOffset.Foreground = new SolidColorBrush(MediaItemHolder.UP_ParentMediaItem == null ? Colors.Black : Colors.RoyalBlue);
        }

        private void Custom_ToggleSwitch_Click(object sender, RoutedEventArgs e)
        {
            if (SuggestionPopup != null)
            {
                SuggestionPopup.PoolMode = SuggestionSwitch.IsEnabled && SuggestionSwitch.IsChecked == true;
                if (SuggestionPopup.IsOpen) SuggestionPopup.SuggestionTimer.Start();
            }
        }
    }
}