﻿using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using e621_ReBot_v3.CustomControls;
using e621_ReBot_v3.Modules;
using e621_ReBot_v3.Modules.Uploader;
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
                if (AppSettings.FirstRun && !string.IsNullOrEmpty(AppSettings.APIKey))
                {
                    MessageBox.Show(Window_Main._RefHolder, "You can change or remove your API key at any time in the settings.", "e621 ReBot Tutorial", MessageBoxButton.OK, MessageBoxImage.Information);
                    AppSettings.FirstRun = false;
                    AppSettings.SaveSettings();
                    MessageBox.Show(Window_Main._RefHolder, "You can now go visit your favorite artists and grab your favorite media. List of supported sites will be shown under this message box.\nYou can also download media from e621.net\n\nWhen there, buttons will appear in the upper right corner, you just need to decide if you wish to download media straight away or grab it for later use. Grabbed media will be stored in Grid tab.\n\nRead button tooltips for further explanations on how things work.", "e621 ReBot Tutorial", MessageBoxButton.OK, MessageBoxImage.Information);
                    BrowserControl._RefHolder.BrowserQuickButtons.IsEnabled = true;
                }
                if (!string.IsNullOrEmpty(AppSettings.APIKey))
                {
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        Module_Credit.Credit_CheckAll();
                        Window_Main._RefHolder.Dispatcher.BeginInvoke(() => { Window_Main._RefHolder.Credit_StackPanel.Visibility = Visibility.Visible; });
                    });
                }
            });
            _RefHolder = null;
        }
    }
}