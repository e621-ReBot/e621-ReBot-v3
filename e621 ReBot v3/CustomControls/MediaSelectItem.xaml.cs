using System.Windows.Controls;
using System.Windows.Media;

namespace e621_ReBot_v3.CustomControls
{
    public partial class MediaSelectItem : UserControl
    {
        public string? PostID { get; set; }
        public string? Rating { get; set; }

        public MediaSelectItem()
        {
            InitializeComponent();
        }

        internal void ChangeRating(string NewRating)
        {
            Rating = NewRating;
            switch (NewRating)
            {
                case "S":
                    {
                        cRating_Polygon.Fill = new SolidColorBrush(Colors.LimeGreen);
                        break;
                    }
                case "Q":
                    {
                        cRating_Polygon.Fill = new SolidColorBrush(Colors.Yellow);
                        break;
                    }
                default:
                    {
                        cRating_Polygon.Fill = new SolidColorBrush(Colors.Red);
                        break;
                    }
            }
        }
    }
}
