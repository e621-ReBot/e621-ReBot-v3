using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace e621_ReBot_v3.CustomControls
{
    public partial class Custom_ToggleSwitch : ToggleButton
    {
        public static readonly DependencyProperty OffTextProperty = DependencyProperty.Register("OffText", typeof(string), typeof(Custom_ToggleSwitch), new UIPropertyMetadata("OffText"));
        public static readonly DependencyProperty OnTextProperty = DependencyProperty.Register("OnText", typeof(string), typeof(Custom_ToggleSwitch), new UIPropertyMetadata("OnText"));

        public string? OffText 
        {
            get => (string)GetValue(OffTextProperty);
            set => SetValue(OffTextProperty, value);
        }

        public string? OnText
        {
            get => (string)GetValue(OnTextProperty);
            set => SetValue(OnTextProperty, value);
        }

        public Custom_ToggleSwitch()
        {
            InitializeComponent();
        }

        private void ToggleButton_Loaded(object sender, RoutedEventArgs e)
        {
            Toggle_Text.Text = IsChecked == true ? OnText : OffText;
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            Toggle_Button.HorizontalAlignment = HorizontalAlignment.Right;
            Toggle_Text.HorizontalAlignment = HorizontalAlignment.Left;
            Toggle_Text.TextAlignment = TextAlignment.Left;
            Toggle_Text.Text = OnText;
            Background = new SolidColorBrush(Colors.Red);
        }

        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            Toggle_Button.HorizontalAlignment = HorizontalAlignment.Left;
            Toggle_Text.HorizontalAlignment = HorizontalAlignment.Right;
            Toggle_Text.TextAlignment = TextAlignment.Right;
            Toggle_Text.Text = OffText;
        }

        private void ToggleButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Toggle_Button.Fill = new SolidColorBrush(IsEnabled ? Colors.RoyalBlue : Colors.Silver);
        }
    }
}
