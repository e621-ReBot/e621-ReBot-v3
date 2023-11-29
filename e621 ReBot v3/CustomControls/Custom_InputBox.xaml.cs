using System;
using System.Windows;
using System.Windows.Input;

namespace e621_ReBot_v3.CustomControls
{
    public partial class Custom_InputBox : Window
    {
        public Custom_InputBox(Window OwnerRef)
        {
            InitializeComponent();
            Owner = OwnerRef;
            App.SetWindow2Square(this);
        }

        internal static string ShowInputBox(Window OwnerRef, string Title, string Description, Point StartingLocation, string Input4Start = "")
        {
            Custom_InputBox cInputBox = new Custom_InputBox(OwnerRef)
            {
                Left = StartingLocation.X - 7,
                Top = StartingLocation.Y - 2,
                Title = Title
            };

            cInputBox.Description_Label.Text = Description;
            cInputBox.Input_TextBox.Text = Input4Start;
            cInputBox.ShowDialog();

            return cInputBox.InputedText;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            Input_TextBox.Focus();
        }

        private void Input_TextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Input_TextBox.SelectAll();
        }

        private string? InputedText;
        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space:
                    {
                        e.Handled = Title.Equals("e621 ReBot");
                        break;
                    }

                case Key.Enter:
                    {
                        InputedText = Input_TextBox.Text;
                        NotCanceled = true;
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

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            InputedText = Input_TextBox.Text;
            NotCanceled = true;
            Close();
        }

        private bool NotCanceled = false;
        private void Window_Closed(object sender, EventArgs e)
        {
            //InputedText = NotCanceled ? (string.IsNullOrEmpty(InputedText) ? "☠" : InputedText) : "☠";
            InputedText = NotCanceled ? (InputedText ?? "☠") : "☠";
            Owner.Activate();
        }
    }
}