using System;
using System.Windows;
using System.Windows.Input;

namespace e621_ReBot_v3
{
    public partial class Window_Notes : Window
    {
        internal static Window_Notes? _RefHolder;
        public Window_Notes()
        {
            InitializeComponent();
            _RefHolder = this;
            Owner = Window_Main._RefHolder;
            App.SetWindow2Square(this);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Notes_TextBox.Focus();
            if (!string.IsNullOrEmpty(AppSettings.Note))
            {
                Notes_TextBox.Text = AppSettings.Note;
                Notes_TextBox.SelectAll();
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _RefHolder = null;
            Owner.Activate();
        }

        private void Notes_TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    {
                        AppSettings.Note = Notes_TextBox.Text.Trim();
                        Close();
                        break;
                    }
                case Key.Escape:
                    {
                        Close();
                        break;
                    }
            }
        }

        private void DeleteNote_Button_Click(object sender, RoutedEventArgs e)
        {
            AppSettings.Note = null;
            Close();
        }
    }
}