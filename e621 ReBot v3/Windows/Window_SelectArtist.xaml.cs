using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace e621_ReBot_v3.Windows
{
    public partial class Window_SelectArtist : Window
    {
        internal string? SelectedArtist { get; private set; }
        public Window_SelectArtist(List<string> ArtistList)
        {
            InitializeComponent();
            ArtistItems.ItemsSource = ArtistList;
        }

        private void Artist_Checked(object sender, RoutedEventArgs e)
        {
            SelectedArtist = ((RadioButton)sender).DataContext.ToString();
        }
    }
}