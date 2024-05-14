using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using e621_ReBot_v3.CustomControls;
using e621_ReBot_v3.Modules;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;

namespace e621_ReBot_v3
{
    public partial class Window_MediaSelect : Window
    {
        internal static Window_MediaSelect? _RefHolder;
        public Window_MediaSelect()
        {
            InitializeComponent();
            _RefHolder = this;
            App.SetWindow2Square(this);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _RefHolder = null;
            Owner.Activate();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        }

        // - - - - - - - - - - - - - - - -

        internal static void Show_ParentOffset(Point StartingPoint)
        {
            Window_MediaSelect Window_MediaSelectTemp = new Window_MediaSelect
            {
                Owner = Window_Tagger._RefHolder,
                Left = StartingPoint.X - 8,
                Top = StartingPoint.Y,
                Title = "Parent Offset"
            };
            Window_MediaSelectTemp.SearchStatus.Visibility = Visibility.Collapsed;

            int RowIndex = Module_Grabber._Grabbed_MediaItems.FindIndex(Window_Tagger._RefHolder.MediaItemHolder);
            bool LaunchTimer = false;
            for (int i = 0; i <= RowIndex - 1; i++)
            {
                MediaSelectItem MediaSelectItemTemp = new MediaSelectItem
                {
                    Tag = Module_Grabber._Grabbed_MediaItems[i],
                    Cursor = Cursors.Hand
                };
                if (Module_Grabber._Grabbed_MediaItems[i].Grid_Thumbnail == null && Module_Grabber._Grabbed_MediaItems[i].Grid_ThumbnailDLStart != true)
                {
                    Module_Grabber.Grab_Thumbnail(Module_Grabber._Grabbed_MediaItems[i]);
                    LaunchTimer = true;
                }
                else
                {
                    MediaSelectItemTemp.cThumbnail_Image.Source = Module_Grabber._Grabbed_MediaItems[i].Grid_Thumbnail;
                }
                MediaSelectItemTemp.cIsUploaded_TextBlock.Visibility = Visibility.Hidden;
                MediaSelectItemTemp.MouseLeftButtonDown += MediaSelectItemTemp_MouseLeftButtonDownOffset;
                Window_MediaSelectTemp.ItemPanel.Children.Add(MediaSelectItemTemp);
            }

            for (int i = RowIndex + 1; i <= Module_Grabber._Grabbed_MediaItems.Count - 1; i++)
            {
                if (Module_Grabber._Grabbed_MediaItems[i].UP_UploadedID != null)
                {
                    MediaSelectItem MediaSelectItemTemp = new MediaSelectItem
                    {
                        Tag = Module_Grabber._Grabbed_MediaItems[i],
                        Cursor = Cursors.Hand
                    };
                    if (Module_Grabber._Grabbed_MediaItems[i].Grid_Thumbnail == null && Module_Grabber._Grabbed_MediaItems[i].Grid_ThumbnailDLStart != true)
                    {
                        Module_Grabber.Grab_Thumbnail(Module_Grabber._Grabbed_MediaItems[i]);
                        LaunchTimer = true;
                    }
                    else
                    {
                        MediaSelectItemTemp.cThumbnail_Image.Source = Module_Grabber._Grabbed_MediaItems[i].Grid_Thumbnail;
                    }
                    MediaSelectItemTemp.cIsUploaded_TextBlock.Visibility = Visibility.Hidden;
                    MediaSelectItemTemp.MouseLeftButtonDown += MediaSelectItemTemp_MouseLeftButtonDownOffset;
                    Window_MediaSelectTemp.ItemPanel.Children.Add(MediaSelectItemTemp);
                }
            }

            _RefHolder.MediaSelect_ScrollViewer.ScrollToVerticalOffset(Math.Floor(Math.Max(0, RowIndex - 1) / 3d) * 204); //202 + 1 + 1

            if (LaunchTimer)
            {
                _RefHolder.ThumbLoadTimer.Tick += ThumbLoadTimer_Tick;
                _RefHolder.ThumbLoadTimer.Start();
            }
            Window_MediaSelectTemp.ShowDialog();
        }

        private static void MediaSelectItemTemp_MouseLeftButtonDownOffset(object sender, MouseButtonEventArgs e)
        {
            Window_Tagger._RefHolder.MediaItemHolder.UP_ParentMediaItem = (MediaItem?)((MediaSelectItem)sender).Tag;
            _RefHolder.Close();
        }

        // - - - - - - - - - - - - - - - -

        internal static void Show_SimilarSearch(Point StartingPoint, string WhichDB)
        {
            Window_MediaSelect Window_MediaSelectTemp = new Window_MediaSelect
            {
                Owner = Window_Preview._RefHolder,
                Left = StartingPoint.X,
                Top = StartingPoint.Y,
                Title = $"{WhichDB} Similar Search",
                Tag = WhichDB
            };
            Window_MediaSelectTemp.ContentRendered += Window_MediaSelect_ContentRendered;
            Window_MediaSelectTemp.ShowDialog();
        }

        private static void Window_MediaSelect_ContentRendered(object? sender, EventArgs e)
        {
            RunTasksAsync();
        }

        private static async Task RunTasksAsync()
        {
            string ModeString = _RefHolder.Tag.ToString();
            await Task.Run(() => CheckSiteStatus(ModeString));
            FindSimilarMedia();
        }

        private static readonly HttpClientHandler SS_HttpClientHandler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, UseCookies = false };
        private static readonly HttpClient SS_HttpClient = new HttpClient(SS_HttpClientHandler) { Timeout = TimeSpan.FromSeconds(5) };
        private static async Task CheckSiteStatus(string ModeString)
        {
            using (HttpRequestMessage HttpRequestMessageTemp = new HttpRequestMessage(HttpMethod.Head, ModeString.Equals("IQDBQ") ? "https://e621.net/iqdb_queries" : "https://saucenao.com/"))
            {
                HttpRequestMessageTemp.Headers.UserAgent.ParseAdd(AppSettings.GlobalUserAgent);

                using (HttpResponseMessage HttpResponseMessageTemp = await SS_HttpClient.SendAsync(HttpRequestMessageTemp))
                {
                    if (HttpResponseMessageTemp.IsSuccessStatusCode)
                    {
                        if (_RefHolder == null) return;
                        _RefHolder.Dispatcher.Invoke(() =>
                        {
                            _RefHolder.SearchStatus.Foreground = new SolidColorBrush(Colors.LimeGreen);
                            _RefHolder.SearchStatus.Text = $"{ModeString} is online.";
                        });
                        await Task.Delay(500);

                        if (_RefHolder == null) return;
                        _RefHolder.Dispatcher.Invoke(() =>
                        {
                            _RefHolder.SearchStatus.Foreground = new SolidColorBrush(Colors.Black);
                            _RefHolder.SearchStatus.Text = $"Checking for similar images...";
                        });
                        await Task.Delay(500);
                    }
                    else
                    {
                        if (_RefHolder == null) return;
                        _RefHolder.Dispatcher.Invoke(() =>
                        {
                            _RefHolder.SearchStatus.Foreground = new SolidColorBrush(Colors.Red);
                            _RefHolder.SearchStatus.Text = $"{ModeString} is offline.";
                        });
                        await Task.Delay(1000);

                        if (_RefHolder == null) return;
                        _RefHolder.Dispatcher.Invoke(() =>
                        {
                            _RefHolder.Close();
                        });
                    }
                }
            }
        }

        private static void FindSimilarMedia()
        {
            if (_RefHolder == null) return;
            if (_RefHolder.Tag.ToString().Equals("IQDBQ"))
            {
                IQDBQSearch();
            }
            else
            {
                SaunceNaoSearch();
            }
        }

        //Has 100 daily search and 5 per 30s search limit.
        private static async void SaunceNaoSearch()
        {
            string? ResponseString = null;
            using (HttpRequestMessage HttpRequestMessageTemp = new HttpRequestMessage(HttpMethod.Get, $"https://saucenao.com/search.php?db=29&url={Window_Preview._RefHolder.MediaItemHolder.Grab_MediaURL}"))
            {
                HttpRequestMessageTemp.Headers.UserAgent.ParseAdd(AppSettings.GlobalUserAgent);
                HttpRequestMessageTemp.Headers.Add("Cookie", "hide=0");
                using (CancellationTokenSource cts = new CancellationTokenSource())
                {
                    try
                    {
                        using (HttpResponseMessage HttpResponseMessageTemp = await SS_HttpClient.SendAsync(HttpRequestMessageTemp, cts.Token))
                        {
                            if (HttpResponseMessageTemp.IsSuccessStatusCode)
                            {
                                ResponseString = await HttpResponseMessageTemp.Content.ReadAsStringAsync();
                            }
                            else
                            {
                                MessageBox.Show(Window_Preview._RefHolder, $"Error at SauceNao Search response!\n\nStatus code: {HttpResponseMessageTemp.StatusCode}\n{ResponseString}", "e621 ReBot Similar Search", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }
                    }
                    catch (WebException webException)
                    {
                        MessageBox.Show(Window_Preview._RefHolder, $"Error at SauceNao Search response!\n{webException.Message}", "e621 ReBot Similar Search", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    catch
                    {
                        MessageBox.Show(Window_Preview._RefHolder, "Error at SauceNao Search response!", "e621 ReBot Similar Search", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
            }

            if (_RefHolder == null) return;

            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(ResponseString);

            HtmlNodeCollection ResultCollection = HtmlDocumentTemp.DocumentNode.SelectNodes(".//div[@id='middle']/div[@class='result']");
            if (ResultCollection.Last().InnerText.Contains("Low similarity results have been hidden."))
            {
                ResultCollection.RemoveAt(ResultCollection.Count - 1);
            }
            if (ResultCollection.Any() && ResultCollection.Last().InnerText.Contains("No results found..."))
            {
                ResultCollection.RemoveAt(ResultCollection.Count - 1);
            }

            Dictionary<string, string> ResultList = new Dictionary<string, string>();
            if (ResultCollection.Any())
            {
                foreach (HtmlNode Post in ResultCollection)
                {
                    string PostID = Post.SelectSingleNode(".//div[@class='resultmatchinfo']//a").Attributes["href"].Value;
                    PostID = PostID.Replace("https://e621.net/post/show/", "");
                    string ThumbLink = Post.SelectSingleNode(".//td[@class='resulttableimage']//img").Attributes["src"].Value;
                    ResultList.Add(PostID, ThumbLink);
                }
            }
            else
            {
                MessageBox.Show(Window_Preview._RefHolder, "No probable matches found.", "e621 ReBot Similar Search", MessageBoxButton.OK, MessageBoxImage.Information);
                _RefHolder.Close();
                return;
            }

            _RefHolder.SearchStatus.Text = "Getting image data...";

            string? JSON_SimilarData = Module_e621Data.DataDownload($"https://e621.net/posts.json?tags=id:{string.Join(',', ResultList)}");
            if (!string.IsNullOrEmpty(JSON_SimilarData) && JSON_SimilarData.Length > 24)
            {
                if (JSON_SimilarData.StartsWith('ⓔ'))
                {
                    MessageBox.Show(Window_Preview._RefHolder, $"{JSON_SimilarData}", "e621 ReBot Similar Search", MessageBoxButton.OK, MessageBoxImage.Error);
                    _RefHolder.Close();
                    return;
                };

                JToken e6SimilarData = JObject.Parse(JSON_SimilarData)["posts"];
                foreach (JObject e6Post in e6SimilarData.Children())
                {
                    MediaSelectItem MediaSelectItemTemp = new MediaSelectItem
                    {
                        PostID = e6Post["id"].Value<string>(),
                        Tag = string.Join(' ', e6Post.SelectTokens("$.tags.*[*]")),
                        Cursor = Cursors.No
                    };
                    MediaSelectItemTemp.cIsUploaded_TextBlock.FontSize = 14;
                    MediaSelectItemTemp.cIsUploaded_TextBlock.HorizontalAlignment = HorizontalAlignment.Left;
                    MediaSelectItemTemp.cIsUploaded_TextBlock.VerticalAlignment = VerticalAlignment.Top;
                    MediaSelectItemTemp.cIsUploaded_TextBlock.TextAlignment = TextAlignment.Left;
                    MediaSelectItemTemp.cIsUploaded_TextBlock.Margin = new Thickness(4, 4, 0, 0);
                    MediaSelectItemTemp.ChangeRating(e6Post["rating"].Value<string>().ToUpper());
                    string PostID = $"#{e6Post["id"].Value<string>()}";
                    string MediaSizeFormat = $"{e6Post["file"]["width"].Value<ushort>()} x {e6Post["file"]["height"].Value<ushort>()} .{e6Post["file"]["ext"].Value<string>()}";
                    string ByteSize = $"{(uint)(e6Post["file"]["size"].Value<uint>() / 1024f)} kB";
                    MediaSelectItemTemp.cIsUploaded_TextBlock.Text = $"{PostID} - {MediaSizeFormat}\n{ByteSize}";

                    string ThumbLink = ResultList[MediaSelectItemTemp.PostID];
                    MediaSelectItemTemp.cThumbnail_Image.Source = new BitmapImage(new Uri(ThumbLink, UriKind.Absolute));
                    MediaSelectItemTemp.MouseLeftButtonDown += MediaSelectItemTemp_MouseLeftButtonDownSimilar;
                    _RefHolder.ItemPanel.Children.Add(MediaSelectItemTemp);
                }
                _RefHolder.SearchStatus.Visibility = Visibility.Collapsed;
            }
            else
            {
                MessageBox.Show(Window_Preview._RefHolder, "No probable matches found.", "e621 ReBot Similar Search", MessageBoxButton.OK, MessageBoxImage.Information);
                _RefHolder.Close();
            }
        }

        private static readonly ImageSource MediaDeletedThumb = new ImageSourceConverter().ConvertFrom(Properties.Resources.E6Image_Deleted) as ImageSource;
        private static void IQDBQSearch()
        {
            _RefHolder.SearchStatus.Text = "Getting image data...";
            //no md5 or extension without auth
            string? JSON_SimilarData = Module_e621Data.DataDownload($"https://e621.net/iqdb_queries.json?url={Window_Preview._RefHolder.MediaItemHolder.Grab_MediaURL}", true);
            if (!string.IsNullOrEmpty(JSON_SimilarData) && JSON_SimilarData.Length > 24)
            {
                if (JSON_SimilarData.StartsWith('ⓔ'))
                {
                    MessageBox.Show(Window_Preview._RefHolder, $"{JSON_SimilarData}", "e621 ReBot Similar Search", MessageBoxButton.OK, MessageBoxImage.Error);
                    _RefHolder.Close();
                    return;
                };

                JArray e6SimilarData = JArray.Parse(JSON_SimilarData);
                foreach (JObject e6Post in e6SimilarData.Children())
                {
                    JToken PostHolder = e6Post["post"]["posts"];
                    MediaSelectItem MediaSelectItemTemp = new MediaSelectItem
                    {
                        PostID = PostHolder["id"].Value<string>(),
                        Tag = PostHolder["tag_string"].Value<string>(),
                        Cursor = Cursors.No,
                    };
                    MediaSelectItemTemp.cIsUploaded_TextBlock.FontSize = 14;
                    MediaSelectItemTemp.cIsUploaded_TextBlock.HorizontalAlignment = HorizontalAlignment.Left;
                    MediaSelectItemTemp.cIsUploaded_TextBlock.VerticalAlignment = VerticalAlignment.Top;
                    MediaSelectItemTemp.cIsUploaded_TextBlock.TextAlignment = TextAlignment.Left;
                    MediaSelectItemTemp.cIsUploaded_TextBlock.Margin = new Thickness(4, 4, 0, 0);
                    MediaSelectItemTemp.ChangeRating(PostHolder["rating"].Value<string>().ToUpper());
                    string PostID = $"#{PostHolder["id"].Value<string>()}";
                    string MediaSize = $"{PostHolder["image_width"].Value<ushort>()} x {PostHolder["image_height"].Value<ushort>()}";
                    string? MediaFormat = null;
                    string ByteSize = $"{(uint)(PostHolder["file_size"].Value<uint>() / 1024f)} kB";
                    if (PostHolder["is_deleted"].Value<bool>())
                    {
                        MediaSelectItemTemp.cThumbnail_Image.Source = MediaDeletedThumb;
                    }
                    else
                    {
                        MediaFormat = $".{PostHolder["file_ext"].Value<string>()}";
                        MediaSelectItemTemp.cThumbnail_Image.Source = new BitmapImage(new Uri(PostHolder["preview_file_url"].Value<string>(), UriKind.Absolute));
                    }
                    MediaSelectItemTemp.cIsUploaded_TextBlock.Text = $"{PostID} - {MediaSize}{(MediaFormat == null ? null : $" {MediaFormat}")}\n{ByteSize}";

                    MediaSelectItemTemp.MouseLeftButtonDown += MediaSelectItemTemp_MouseLeftButtonDownSimilar;
                    _RefHolder.ItemPanel.Children.Add(MediaSelectItemTemp);
                }
                _RefHolder.SearchStatus.Visibility = Visibility.Collapsed;
            }
            else
            {
                MessageBox.Show(Window_Preview._RefHolder, "No probable matches found.", "e621 ReBot Similar Search", MessageBoxButton.OK, MessageBoxImage.Information);
                _RefHolder.Close();
            }
        }

        private static void MediaSelectItemTemp_MouseLeftButtonDownSimilar(object sender, MouseButtonEventArgs e)
        {
            MediaSelectItem MediaSelectItemTemp = (MediaSelectItem)sender;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                Window_Preview.SuperiorSub(MediaSelectItemTemp.PostID, Window_Preview._RefHolder.MediaItemHolder);
                _RefHolder.Close();
                return;
            }

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                string e6Post = $"https://e621.net/post/show/{MediaSelectItemTemp.PostID}";
                Process.Start(new ProcessStartInfo(e6Post) { UseShellExecute = true });
                return;
            }

            Window_Preview._RefHolder.MediaItemHolder.UP_Rating = MediaSelectItemTemp.Rating;
            Window_Preview._RefHolder.MediaItemHolder.UP_UploadedID = MediaSelectItemTemp.PostID;
            Window_Preview._RefHolder.MediaItemHolder.UP_Tags = MediaSelectItemTemp.Tag.ToString();
            GridVE GridVETemp = Module_Grabber.IsVisibleInGrid(Window_Preview._RefHolder.MediaItemHolder);
            if (GridVETemp != null)
            {
                GridVETemp.ChangeRating(MediaSelectItemTemp.Rating);
                GridVETemp.IsUploaded_SetText(MediaSelectItemTemp.PostID);
            }
            Window_Preview._RefHolder.Tags_TextBlock.Text = MediaSelectItemTemp.Tag.ToString();
            Window_Preview._RefHolder.AlreadyUploaded_Label.Text = $"#{MediaSelectItemTemp.PostID}";
            Window_Preview._RefHolder.SetRatingColour();
            //if (Properties.Settings.Default.ManualInferiorSave)
            //{
            //    Module_DB.DB_Media_CreateRecord(DataRowTemp);
            //}
            _RefHolder.Close();
        }

        // - - - - - - - - - - - - - - - -

        private DispatcherTimer ThumbLoadTimer = new DispatcherTimer() { Interval = TimeSpan.FromMilliseconds(500) };
        private static void ThumbLoadTimer_Tick(object? sender, EventArgs e)
        {
            _RefHolder.ThumbLoadTimer.Stop();
            bool LaunchTimer = false;
            ImageSource ThumbImageSource;
            foreach (MediaSelectItem MediaSelectItemTemp in _RefHolder.ItemPanel.Children)
            {
                ThumbImageSource = ((MediaItem)MediaSelectItemTemp.Tag).Grid_Thumbnail;
                if (ThumbImageSource == null)
                {
                    LaunchTimer = true;
                }
                else
                {
                    MediaSelectItemTemp.cThumbnail_Image.Source = ThumbImageSource;
                }
            }
            if (LaunchTimer) _RefHolder.ThumbLoadTimer.Start();
        }
    }
}
