using e621_ReBot_v3.CustomControls;
using e621_ReBot_v3.Modules;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;


namespace e621_ReBot_v3
{
    public partial class Window_APIKey : Custom_Window
    {
        internal static Window_APIKey? _RefHolder;
        public Window_APIKey()
        {
            InitializeComponent();
            _RefHolder = this;
            Owner = Window_Main._RefHolder;
            App.SetWindow2Square(this);
        }

        private async void API_PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    {
                        if (API_PasswordBox.Password.Length < 24)
                        {
                            MessageBox.Show(this, "API key must be 24 characters long.", "e621 ReBot");
                            return;
                        }

                        if (await ValidateAPIKey())
                        {
                            AppSettings.APIKey = Module_Cryptor.Encrypt(API_PasswordBox.Password);
                            Module_e621APIController.ToggleStatus();
                            Close();
                        }
                        break;
                    }

                case Key.Escape:
                    {
                        Close();
                        break;
                    }
            }
        }

        private async Task<bool> ValidateAPIKey()
        {
            string base64Auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{AppSettings.UserName}:{API_PasswordBox.Password}"));
            DownloadResult DownloadResultTemp = await ReBot_HttpClient.DownloadStringAsync($"https://e621.net/users/{AppSettings.UserID}.json", AuthHeader: base64Auth);

            if (!DownloadResultTemp.IsSuccessStatusCode)
            {
                MessageBox.Show(this, $"Error!\n\nStatus Code: {DownloadResultTemp.StatusCode}", "e621 ReBot");
                return false;
            }

            JObject JObjectTemp = JObject.Parse(DownloadResultTemp.TextData);
            if (JObjectTemp.Count < 32)
            {
                MessageBox.Show(this, "API key is not valid.", "e621 ReBot");
                return false;
            }

            Module_Credit.UserLevel = (ushort)JObjectTemp["level"];
            AppSettings.AppName = $"e621 ReBot ({AppSettings.UserName})";
            Window_Main._RefHolder.STB_AppName.Text = AppSettings.AppName;
            return true;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            API_PasswordBox.Focus();
            API_PasswordBox.SelectAll();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            API_PasswordBox.Focus();
            API_PasswordBox.SelectAll();
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            Window_Main._RefHolder.Dispatcher.InvokeAsync(() =>
            {
                if (!string.IsNullOrEmpty(AppSettings.APIKey))
                {
                    Task.Run(() =>
                    {
                        Module_Credit.Credit_CheckAll();
                        Window_Main._RefHolder.Dispatcher.InvokeAsync(() => { Window_Main._RefHolder.Credit_StackPanel.Visibility = Visibility.Visible; });
                    });
                }
                if (AppSettings.FirstRun && !string.IsNullOrEmpty(AppSettings.APIKey))
                {
                    Module_Tutorial.Step_Last();
                }
            });
            _RefHolder = null;
        }
    }
}