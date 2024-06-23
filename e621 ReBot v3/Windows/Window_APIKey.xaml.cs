using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using e621_ReBot_v3.Modules;
using Newtonsoft.Json.Linq;


namespace e621_ReBot_v3
{
    public partial class Window_APIKey : Window
    {
        internal static Window_APIKey? _RefHolder;
        public Window_APIKey()
        {
            InitializeComponent();
            _RefHolder = this;
            Owner = Window_Main._RefHolder;
            App.SetWindow2Square(this);
        }

        private void API_PasswordBox_KeyDown(object sender, KeyEventArgs e)
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
                        if (ValidateAPIKey())
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

        private bool ValidateAPIKey()
        {
            HttpWebRequest e6APICheck = (HttpWebRequest)WebRequest.Create($"https://e621.net/users/{AppSettings.UserID}.json");
            e6APICheck.UserAgent = AppSettings.GlobalUserAgent;
            e6APICheck.Timeout = 5000;
            e6APICheck.Headers.Add(HttpRequestHeader.Authorization, $"Basic {Convert.ToBase64String(Encoding.ASCII.GetBytes($"{AppSettings.UserName}:{API_PasswordBox.Password}"))}");
            try
            {
                HttpWebResponse HttpWebResponseTemp = (HttpWebResponse)e6APICheck.GetResponse();
                if (HttpWebResponseTemp.StatusCode == HttpStatusCode.OK)
                {
                    using (Stream StreamTemp = HttpWebResponseTemp.GetResponseStream())
                    {
                        using (StreamReader StreamReaderTemp = new StreamReader(StreamTemp))
                        {
                            string JSONReponse = StreamReaderTemp.ReadToEnd();
                            JObject JObjectTemp = JObject.Parse(JSONReponse);
                            if (JObjectTemp.Count > 32)
                            {
                                Module_Credit.UserLevel = JObjectTemp["level"].Value<ushort>();
                                AppSettings.AppName = $"e621 ReBot ({AppSettings.UserName})";
                                Window_Main._RefHolder.STB_AppName.Text = AppSettings.AppName;
                                return true;
                            }
                            else
                            {
                                MessageBox.Show(this, "API key is not valid.", "e621 ReBot");
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show(this, $"Error!\n\nStatus Code: {HttpWebResponseTemp.StatusCode}", "e621 ReBot");
                }
            }
            catch (WebException ex)
            {
                using (HttpWebResponse HttpWebResponseTemp = (HttpWebResponse)ex.Response)
                {
                    MessageBox.Show(this, $"{ex.Message}\n\nStatus Code: {HttpWebResponseTemp.StatusCode}", "e621 ReBot");
                }
            }
            return false;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                if (!string.IsNullOrEmpty(AppSettings.APIKey))
                {
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        Module_Credit.Credit_CheckAll();
                        Window_Main._RefHolder.Dispatcher.BeginInvoke(() => { Window_Main._RefHolder.Credit_StackPanel.Visibility = Visibility.Visible; });
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