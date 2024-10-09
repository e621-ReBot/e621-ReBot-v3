using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace e621_ReBot_v3
{
    public partial class Window_Blacklist : Window
    {
        internal static Window_Blacklist? _RefHolder;
        public Window_Blacklist()
        {
            InitializeComponent();
            _RefHolder = this;
            Owner = Window_Main._RefHolder;
            App.SetWindow2Square(this);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppSettings.Blacklist.Any())
            {
                Blacklist_TextBox.Text = string.Join(Environment.NewLine, AppSettings.Blacklist) + Environment.NewLine;
                Blacklist_TextBox.SelectionStart = Blacklist_TextBox.Text.Length;
            }
            Blacklist_TextBox.Focus();
        }

        private void Blacklist_TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    {
                        if (Blacklist_TextBox.SelectionStart > 2)
                        {
                            string NewLineChecker = Blacklist_TextBox.Text.Substring(Blacklist_TextBox.SelectionStart - 2, 2);
                            if (NewLineChecker.Equals("\r\n"))
                            {
                                e.Handled = true;
                                return;
                            }
                        }
                        break;
                    }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            List<string> NewBlacklist = new List<string>();
            Blacklist_TextBox.Text = Blacklist_TextBox.Text.Trim().ToLower();
            for (int i = 0; i < Blacklist_TextBox.LineCount; i++)
            {
                string TBLine = Blacklist_TextBox.GetLineText(i).Trim();
                if (TBLine.Length > 0) NewBlacklist.Add(TBLine);
            }
            AppSettings.Blacklist = NewBlacklist;
            _RefHolder = null;
            Owner.Activate();
        }
    }
}