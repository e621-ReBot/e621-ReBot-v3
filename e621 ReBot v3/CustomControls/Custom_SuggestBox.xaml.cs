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

        private void TextBoxRef_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            SuggestionTimer.Stop();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                switch (e.Key)
                {
                    case Key.A: //Don't pop up on select all
                    case Key.C: //Don't pop up on copy
                    case Key.V: //Don't pop up on paste
                    case Key.X: //Don't pop up on cut
                        {
                            IsOpen = false;
                            return;
                        }
                }
            }

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
                        //e.Handled = true;
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

        private readonly ushort MaxItemCount = 14;
        internal void LoadSuggestionBox()
        {
            TagCollection.Clear();
            PoolCollection.Clear();

            for (int i = 0; i < MaxItemCount; i++)
            {
                SuggestBox.Items.Add(new ListBoxItem() { Cursor = Cursors.Hand });
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

        internal void Check4Suggestions()
        {
            string TextBoxText = _TextBoxRef.Text;
            int CursorLocation = _TextBoxRef.SelectionStart;

            if (_TextBoxRef.Text.Length < 2)
            {
                IsOpen = false;
                return;
            }

            WordStartIndex = CursorLocation;
            while (WordStartIndex > 0 && !char.IsWhiteSpace(TextBoxText[WordStartIndex - 1]))
            {
                WordStartIndex--;
            }

            WordEndIndex = CursorLocation;
            while (WordEndIndex < TextBoxText.Length && !char.IsWhiteSpace(TextBoxText[WordEndIndex]))
            {
                WordEndIndex++;
            }

            string Word4Suggest = TextBoxText[WordStartIndex..WordEndIndex];
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
        [GeneratedRegex(@"pool:(\d+)")]
        private static partial Regex PoolRegex();
        internal void BuildSuggestionList(string Word4Suggest)
        {
            SuggestionResultList.Clear();

            int ResultSelectIndex = -1;
            if (PoolMode)
            {
                Regex RegexSearcher = PoolRegex();
                Match RegexMatcher = RegexSearcher.Match(Word4Suggest);
                foreach (KeyValuePair<string, string> KeyValuePairTemp in PoolCollection)
                {
                    if (RegexMatcher.Success)
                    {
                        if (KeyValuePairTemp.Key.Contains(RegexMatcher.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                        {
                            SuggestionResultList.Add(new string[] { $"#{KeyValuePairTemp.Key}", KeyValuePairTemp.Value });
                        }
                    }
                    else
                    {
                        if (KeyValuePairTemp.Value.Contains(Word4Suggest, StringComparison.OrdinalIgnoreCase))
                        {
                            SuggestionResultList.Add(new string[] { $"#{KeyValuePairTemp.Key}", KeyValuePairTemp.Value });
                        }
                    }
                }
            }
            else
            {
                foreach (KeyValuePair<string, string[]?> KeyValuePairTemp in TagCollection)
                {
                    string KeyString = KeyValuePairTemp.Key;
                    string[]? ValueArray = KeyValuePairTemp.Value;

                    if (KeyString.StartsWith(Word4Suggest, StringComparison.OrdinalIgnoreCase))
                    {
                        SuggestionResultList.Add(new string[] { KeyString, string.Empty });
                        continue;
                    }

                    if (ValueArray != null)
                    {
                        foreach (string AlternateValue in ValueArray)
                        {
                            if (AlternateValue.StartsWith(Word4Suggest, StringComparison.OrdinalIgnoreCase))
                            {
                                SuggestionResultList.Add(new string[] { KeyString, AlternateValue });
                            }
                        }
                    }
                }
            }

            if (SuggestionResultList.Any())
            {
                if (DuplicatesDisabled)
                {
                    List<string> TextBoxRefList = new List<string>(_TextBoxRef.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)); //Use list, need the duplicates
                    TextBoxRefList.Remove(Word4Suggest); //remove current word
                    SuggestionResultList.RemoveAll(e => TextBoxRefList.Contains(e[0]));
                    if (SuggestionResultList.Count == 0)
                    {
                        IsOpen = false;
                        return;
                    }
                }

                for (int i = 0; i < SuggestionResultList.Count; i++)
                {
                    string KeyMatch = PoolMode ? $"pool:{SuggestionResultList[i][0].Substring(1)}" : SuggestionResultList[i][0];
                    string ValueMatch = SuggestionResultList[i][1] ?? string.Empty;

                    if (KeyMatch.Equals(Word4Suggest) || ValueMatch.Equals(Word4Suggest))
                    {
                        ResultSelectIndex = i;
                        break;
                    }
                }
                ScrollSelectIndexTracker = Math.Max(0, ResultSelectIndex);

                SuggestScrollBar.Visibility = SuggestionResultList.Count > MaxItemCount ? Visibility.Visible : Visibility.Collapsed;
                SuggestScrollBar.Maximum = SuggestionResultList.Count - 1;
                SuggestScrollBar.Value = ScrollSelectIndexTracker;

                int MaxVisibleControls = Math.Min(MaxItemCount, SuggestionResultList.Count) - 1;
                for (int i = 0; i < SuggestBox.Items.Count; i++)
                {
                    if (i <= MaxVisibleControls)
                    {
                        ((ListBoxItem)SuggestBox.Items[i]).Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ((ListBoxItem)SuggestBox.Items[i]).Visibility = Visibility.Collapsed;
                        ((ListBoxItem)SuggestBox.Items[i]).Content = null;
                    }
                }
                VisibleItemCount = MaxVisibleControls + 1;
                ScrollIndexEnd = Math.Max(MaxVisibleControls, Math.Min(ScrollSelectIndexTracker + MaxVisibleControls, SuggestionResultList.Count - 1));
                ScrollIndexStart = Math.Max(0, Math.Min(ScrollSelectIndexTracker, ScrollIndexEnd - MaxVisibleControls));
                UpdateSuggestionDisplay();
                IsOpen = true;
            }
            else
            {
                IsOpen = false;
            }
        }

        private int ScrollIndexStart = 0;
        private int ScrollIndexEnd = 0;
        private int ScrollSelectIndexTracker = 0;
        private int VisibleItemCount = 0;
        private void UpdateSuggestionDisplay()
        {
            ListBoxItem ListBoxItemTemp;
            for (int i = ScrollIndexStart; i < ScrollIndexStart + VisibleItemCount; i++)
            {
                ListBoxItemTemp = (ListBoxItem)SuggestBox.Items[i - ScrollIndexStart];
                ListBoxItemTemp.Content = SuggestionResultList[i][0];
                ListBoxItemTemp.Tag = SuggestionResultList[i][1];
            }
            if (VisibleItemCount == 1)
            {
                ListBoxItemTemp = (ListBoxItem)SuggestBox.Items[0];
            }
            else
            {
                ListBoxItemTemp = (ListBoxItem)SuggestBox.Items[ScrollSelectIndexTracker - ScrollIndexStart];
            }
            ListBoxItemTemp.IsSelected = true;
            //Debug.WriteLine($"V: {ScrollSelectIndexTracker}, S:{ScrollIndexStart}, E:{ScrollIndexEnd}");
        }

        internal void SelectListItem(int IndexChange)
        {
            ScrollSelectIndexTracker = SetValue(ScrollSelectIndexTracker + IndexChange, 0, SuggestionResultList.Count - 1);
            if (ScrollSelectIndexTracker > ScrollIndexEnd || ScrollSelectIndexTracker < ScrollIndexStart)
            {
                ScrollIndexStart = SetValue(ScrollIndexStart + IndexChange, 0, SuggestionResultList.Count - VisibleItemCount);
                ScrollIndexEnd = SetValue(ScrollIndexEnd + IndexChange, VisibleItemCount - 1, SuggestionResultList.Count - 1);
            }
            SuggestScrollBar.Value = ScrollSelectIndexTracker;
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