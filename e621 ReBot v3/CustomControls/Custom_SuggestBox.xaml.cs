using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using CefSharp;

namespace e621_ReBot_v3.CustomControls
{
    public partial class Custom_SuggestBox : Popup
    {
        private readonly List<KeyValuePair<string, string[]?>> TagCollection;
        private readonly List<KeyValuePair<string, string>> PoolCollection;

        public Custom_SuggestBox()
        {
            InitializeComponent();
            TagCollection = new List<KeyValuePair<string, string[]?>>();
            PoolCollection = new List<KeyValuePair<string, string>>();
            LoadSuggestionBox();
            SuggestionTimer.Tick += SuggestionTimer_Tick;
            ClickInputDelay.Tick += ClickInputDelay_Tick;
        }

        private bool DuplicatesDisabled = false;
        internal void SetTextBoxTarget(TextBox TextBoxRef, bool DisableDuplicates = false)
        {
            _TextBoxRef = TextBoxRef;
            DuplicatesDisabled = DisableDuplicates;
            TextBoxRef.GotFocus += TextBoxRef_GotFocus;
            TextBoxRef.PreviewKeyDown += TextBoxRef_PreviewKeyDown;
            TextBoxRef.PreviewMouseWheel += TextBoxRef_PreviewMouseWheel;
            Window.GetWindow(TextBoxRef).LostFocus += TextBoxRefWindow_LostFocus;
            Window.GetWindow(TextBoxRef).Closing += TextBoxRefWindow_Closing;
        }

        internal void RemoveTextBoxTarget(TextBox TextBoxRef)
        {
            TextBoxRef.GotFocus -= TextBoxRef_GotFocus;
            TextBoxRef.PreviewKeyDown -= TextBoxRef_PreviewKeyDown;
            TextBoxRef.PreviewMouseWheel -= TextBoxRef_PreviewMouseWheel;
            Window.GetWindow(TextBoxRef).LostFocus -= TextBoxRefWindow_LostFocus;
            Window.GetWindow(TextBoxRef).Closing -= TextBoxRefWindow_Closing;
            _TextBoxRef = null;
        }

        private void TextBoxRef_GotFocus(object sender, RoutedEventArgs e)
        {
            _TextBoxRef = (TextBox?)sender;
        }

        private readonly KeyConverter _KeyConverter = new KeyConverter();
        private void TextBoxRef_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            SuggestionTimer.Stop();
            switch (e.Key)
            {
                case Key.Down:
                    {
                        if (IsOpen)
                        {
                            SelectListItem(1);
                            e.Handled = true;
                        }
                        break;
                    }
                case Key.Up:
                    {
                        if (IsOpen)
                        {
                            SelectListItem(-1);
                            e.Handled = true;
                        }
                        break;
                    }
                case Key.PageDown:
                    {
                        if (IsOpen)
                        {
                            SelectListItem(MaxItemCount);
                            e.Handled = true;
                        }
                        break;
                    }
                case Key.PageUp:
                    {
                        if (IsOpen)
                        {
                            SelectListItem(MaxItemCount * (-1));
                            e.Handled = true;
                        }
                        break;
                    }
                case Key.Space:
                    {
                        if (IsOpen)
                        {
                            IsOpen = false;
                        }
                        break;
                    }
                case Key.Enter:
                case Key.Tab:
                    {
                        if (IsOpen)
                        {
                            InputSuggestion();
                            e.Handled = true;
                        }
                        break;
                    }
                case Key.Escape:
                    {
                        if (IsOpen)
                        {
                            IsOpen = false;
                            e.Handled = true;
                        }
                        break;
                    }
                case Key.CapsLock:
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LeftCtrl:
                case Key.RightCtrl:
                case Key.LeftAlt:
                case Key.RightAlt:
                case Key.LWin:
                case Key.RWin:
                case Key.F1:
                case Key.F2:
                case Key.F3:
                case Key.F4:
                case Key.F5:
                case Key.F6:
                case Key.F7:
                case Key.F8:
                case Key.F9:
                case Key.F10:
                case Key.F11:
                case Key.F12:
                case Key.Print:
                case Key.PrintScreen:
                case Key.Scroll:
                case Key.Pause:
                case Key.Insert:
                case Key.Delete:
                case Key.Home:
                case Key.End:
                    {
                        //e.Handled = false;
                        break;
                    }
                default:
                    {
                        SuggestionTimer.Start();
                        break;
                    }
            }
        }

        private void TextBoxRef_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (IsOpen)
            {
                _TextBoxRef = (TextBox)sender;
                SelectListItem((short)(e.Delta < 0 ? 1 : -1));
            }
        }

        private void TextBoxRefWindow_LostFocus(object sender, RoutedEventArgs e)
        {
            IsOpen = false;
        }

        private void TextBoxRefWindow_Closing(object? sender, CancelEventArgs e)
        {
            RemoveTextBoxTarget(_TextBoxRef);
        }

        // - - - - - - - - - - - - - - - -

        private readonly short MaxItemCount = 14;
        internal void LoadSuggestionBox()
        {
            TagCollection.Clear();
            PoolCollection.Clear();

            for (int i = 0; i < MaxItemCount; i++)
            {
                SuggestBox.Items.Add(new ListBoxItem());
            }

            if (File.Exists("tags.txt"))
            {
                foreach (string stringTemp in File.ReadAllText("tags.txt").Split('✄', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (stringTemp.Contains(','))
                    {
                        List<string> ListOfTags = stringTemp.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                        string ActualTag = ListOfTags[0];
                        ListOfTags.RemoveAt(0);
                        TagCollection.Add(new KeyValuePair<string, string[]?>(ActualTag, ListOfTags.ToArray()));
                    }
                    else
                    {
                        TagCollection.Add(new KeyValuePair<string, string[]?>(stringTemp, null));
                    }
                }
            }
            if (File.Exists("pools.txt"))
            {
                List<string> ListOfPools = new List<string>(File.ReadAllText("pools.txt").Split('✄', StringSplitOptions.RemoveEmptyEntries));
                foreach (string PoolData in ListOfPools)
                {
                    string[] DataSplitter = PoolData.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    PoolCollection.Add(new KeyValuePair<string, string>(DataSplitter[0], DataSplitter[1]));
                }
            }
        }

        internal readonly DispatcherTimer? SuggestionTimer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(100) };
        private void SuggestionTimer_Tick(object? sender, EventArgs e)
        {
            SuggestionTimer.Stop();
            Check4Suggestions();
        }

        [GeneratedRegex(@"\S")]
        private static partial Regex SuggestionRegex();
        internal void Check4Suggestions()
        {
            if (_TextBoxRef.Text.Length < 2)
            {
                IsOpen = false;
                return;
            }

            Regex RegexSearch = SuggestionRegex();
            int iLoop = _TextBoxRef.SelectionStart;
            while (iLoop >= 0 && iLoop < _TextBoxRef.Text.Length)
            {
                if (!RegexSearch.IsMatch(_TextBoxRef.Text[iLoop].ToString())) break;
                iLoop++;
            }
            WordEndIndex = iLoop;

            iLoop = _TextBoxRef.SelectionStart;
            while (iLoop > 0 && (iLoop - 1) < _TextBoxRef.Text.Length)
            {
                if (!RegexSearch.IsMatch(_TextBoxRef.Text[iLoop - 1].ToString())) break;
                iLoop--;
            }
            WordStartIndex = iLoop;
            string Word4Suggest = _TextBoxRef.Text[WordStartIndex..WordEndIndex];
            if (Word4Suggest.Length < 2)
            {
                IsOpen = false;
                return;
            }

            //if (TagCollection == null || TagCollection.Count== 0) return;
            //if (PoolCollection == null || PoolCollection.Count == 0) return;
            PlacementTarget = _TextBoxRef;
            PlacementRectangle = _TextBoxRef.GetRectFromCharacterIndex(WordStartIndex, true);

            BuildSuggestionList(Word4Suggest);
        }

        internal bool PoolMode = false;
        private readonly List<string[]> SuggestionResultList = new List<string[]>();
        [GeneratedRegex(@"pool:\d+")]
        private static partial Regex PoolRegex();
        internal void BuildSuggestionList(string Word4Suggest)
        {
            SuggestionResultList.Clear();

            int ResultSelectIndex = -1;
            int MatchLoop = 0;
            if (PoolMode)
            {
                Regex RegexSearcher = PoolRegex();
                List<KeyValuePair<string, string>> SelectedSource = PoolCollection;
                foreach (KeyValuePair<string, string> KeyValuePairTemp in SelectedSource)
                {
                    if (RegexSearcher.IsMatch(Word4Suggest))
                    {
                        string MakePoolString = $"pool:{KeyValuePairTemp.Key}";
                        if (MakePoolString.Contains(Word4Suggest, StringComparison.OrdinalIgnoreCase))
                        {
                            SuggestionResultList.Add(new string[] { $"#{KeyValuePairTemp.Key}", KeyValuePairTemp.Value });
                            if (MakePoolString.Equals(Word4Suggest, StringComparison.OrdinalIgnoreCase)) ResultSelectIndex = MatchLoop;
                            MatchLoop++;
                        }
                        continue;
                    }
                    if (KeyValuePairTemp.Value.Contains(Word4Suggest, StringComparison.OrdinalIgnoreCase))
                    {
                        SuggestionResultList.Add(new string[] { $"#{KeyValuePairTemp.Key}", KeyValuePairTemp.Value });
                        if (KeyValuePairTemp.Key.Equals(Word4Suggest, StringComparison.OrdinalIgnoreCase)) ResultSelectIndex = MatchLoop;
                        MatchLoop++;
                        continue;
                    }
                }
            }
            else
            {
                List<KeyValuePair<string, string[]?>> SelectedSource = TagCollection;
                foreach (KeyValuePair<string, string[]?> KeyValuePairTemp in SelectedSource)
                {
                    string KeyString = KeyValuePairTemp.Key;
                    string[]? ValueArray = KeyValuePairTemp.Value;
                    if (KeyString.StartsWith(Word4Suggest, StringComparison.OrdinalIgnoreCase))
                    {
                        SuggestionResultList.Add(new string[] { KeyString, string.Empty });
                        if (KeyString.Equals(Word4Suggest, StringComparison.OrdinalIgnoreCase)) ResultSelectIndex = MatchLoop;
                        MatchLoop++;
                        continue;
                    }
                    if (ValueArray != null)
                    {
                        foreach (string AlternateValue in ValueArray)
                        {
                            if (AlternateValue.StartsWith(Word4Suggest, StringComparison.OrdinalIgnoreCase))
                            {
                                SuggestionResultList.Add(new string[] { KeyString, AlternateValue });
                                if (AlternateValue.Equals(Word4Suggest, StringComparison.OrdinalIgnoreCase)) ResultSelectIndex = MatchLoop;
                                MatchLoop++;
                            }
                        }
                    }
                }
            }

            SIndexTracker = Math.Max(0, ResultSelectIndex);
            if (SuggestionResultList.Any())
            {
                List<string> TextBoxRefList = _TextBoxRef.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                SuggestionResultList.RemoveAll(l => TextBoxRefList.Contains(l.First()));
                if (SuggestionResultList.Count == 0)
                {
                    IsOpen = false;
                    return;
                }

                SuggestScrollBar.Visibility = SuggestionResultList.Count > MaxItemCount ? Visibility.Visible : Visibility.Collapsed;
                SuggestScrollBar.Maximum = SuggestionResultList.Count - 1;
                SuggestScrollBar.Value = SIndexTracker;

                int MaxVisibleControls = Math.Min(MaxItemCount - 1, SuggestionResultList.Count - 1);
                ushort iLoop = 0;
                foreach (ListBoxItem ListBoxItemTemp in SuggestBox.Items)
                {
                    ListBoxItemTemp.Visibility = iLoop <= MaxVisibleControls ? Visibility.Visible : Visibility.Collapsed;
                    iLoop++;
                }
                VisibleItemCount = MaxVisibleControls + 1;
                SIndexEnd = Math.Max(MaxVisibleControls, Math.Min(SIndexTracker + MaxVisibleControls, SuggestionResultList.Count - 1));
                SIndexStart = Math.Max(0, Math.Min(SIndexTracker, SIndexEnd - MaxVisibleControls));
                UpdateSuggestionDisplay();
                IsOpen = true;
            }
            else
            {
                IsOpen = false;
            }
        }

        private int SIndexStart = 0;
        private int SIndexEnd = 0;
        private int SIndexTracker = 0;
        private int VisibleItemCount = 0;
        private void UpdateSuggestionDisplay()
        {
            ListBoxItem ListBoxItemTemp;
            for (int i = SIndexStart; i < SIndexStart + VisibleItemCount; i++)
            {
                ListBoxItemTemp = (ListBoxItem)SuggestBox.Items[i - SIndexStart];
                ListBoxItemTemp.Content = SuggestionResultList[i][0];
                ListBoxItemTemp.Tag = SuggestionResultList[i][1];
                ListBoxItemTemp.Cursor = Cursors.Hand;
            }
            ListBoxItemTemp = (ListBoxItem)SuggestBox.Items[SIndexTracker - SIndexStart];
            ListBoxItemTemp.IsSelected = true;
        }

        internal void SelectListItem(int IndexChange)
        {
            SIndexTracker = SetValue(SIndexTracker + IndexChange, 0, SuggestionResultList.Count - 1);
            if (SIndexTracker > SIndexEnd || SIndexTracker < SIndexStart)
            {
                SIndexStart = SetValue(SIndexStart + IndexChange, 0, SuggestionResultList.Count - VisibleItemCount);
                SIndexEnd = SetValue(SIndexEnd + IndexChange, VisibleItemCount - 1, SuggestionResultList.Count - 1);
            }
            SuggestScrollBar.Value = SIndexTracker;
            //Debug.WriteLine($"V: {SIndexTracker}, S:{SIndexStart}, E:{SIndexEnd}");
            UpdateSuggestionDisplay();
        }

        private static int SetValue(int value, int minValue, int maxValue)
        {
            return Math.Max(minValue, Math.Min(value, maxValue));
        }

        private int WordStartIndex = 0;
        private int WordEndIndex = 0;
        private TextBox? _TextBoxRef;
        internal void InputSuggestion()
        {
            IsOpen = false;
            _TextBoxRef.Text = _TextBoxRef.Text.Remove(WordStartIndex, WordEndIndex - WordStartIndex);
            string InsertString = $"{((ListBoxItem)SuggestBox.SelectedItem).Content} ";
            if (PoolMode && InsertString.StartsWith("#")) InsertString = $"pool:{InsertString.Substring(1)}";
            _TextBoxRef.Text = _TextBoxRef.Text.Insert(WordStartIndex, InsertString);
            _TextBoxRef.Focus();
            _TextBoxRef.SelectionStart = _TextBoxRef.Text.Length;
        }

        private bool ItemClicked = false;
        private void ListBoxItemPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ItemClicked = true;
            ClickInputDelay.Start();
        }

        internal readonly DispatcherTimer? ClickInputDelay = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(50) };
        private void ClickInputDelay_Tick(object? sender, EventArgs e)
        {
            ClickInputDelay.Stop();
            InputSuggestion();
        }

        private void SuggestBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ItemClicked)
            {
                ClickInputDelay.Stop();
                ItemClicked = false;
                InputSuggestion();
            }
        }

        private void SuggestBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            SelectListItem((short)(e.Delta < 0 ? 1 : -1));
        }

        private void SuggestBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!SuggestBox.IsKeyboardFocusWithin) IsOpen = false;
        }
    }
}