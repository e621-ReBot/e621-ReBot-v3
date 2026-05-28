using e621_ReBot_v3.Modules;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace e621_ReBot_v3
{
    internal static partial class Module_Updater
    {
        internal static void CreateUpdateZip()
        {
            string FileVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
            using (FileStream FileStreamTemp = new FileStream($"e621.ReBot-v{FileVersion}-partial.zip", FileMode.Create))
            {
                using (ZipArchive UpdateZip = new ZipArchive(FileStreamTemp, ZipArchiveMode.Create))
                {
                    DirectoryInfo DirectoryInfoTemp = new DirectoryInfo("./");
                    foreach (FileInfo FileInfoTemp in DirectoryInfoTemp.GetFiles())
                    {
                        if (FileInfoTemp.Name.StartsWith(AppDomain.CurrentDomain.FriendlyName))
                        {
                            UpdateZip.CreateEntryFromFile(FileInfoTemp.FullName, FileInfoTemp.Name);
                        }
                    }
                    ZipArchiveEntry ZipArchiveEntryTemp = UpdateZip.CreateEntry("version.txt");
                    using (Stream StreamTemp = ZipArchiveEntryTemp.Open())
                    {
                        using (StreamWriter StreamWriterTemp = new StreamWriter(StreamTemp))
                        {
                            StreamWriterTemp.Write(FileVersion);
                        }
                    }
                }
            }
            Window_Main._RefHolder.MakeUpdate_WButton.Foreground = new SolidColorBrush(Colors.LimeGreen);
            MessageBox.Show(Window_Main._RefHolder, "Update zip has been made.", "e621 ReBot v3", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        internal static void PreUpdateCheck()
        {
            if (DateTime.UtcNow > AppSettings.Update_LastCheck.AddDays(AppSettings.Update_Interval))
            {
                ThreadPool.QueueUserWorkItem(state => Check4Update());
            }
            else
            {
                Window_Main._RefHolder.ReBot_Menu_ListBox.Visibility = Visibility.Visible;
            }
        }

        [GeneratedRegex(@"\d+\.(\d+)\.(\d+)\.(\d+)")]
        private static partial Regex VersionRegex();
        internal static async void Check4Update()
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.Update_TextBlock.Visibility = Visibility.Visible;
            });
            Thread.Sleep(1000); //To show update text

            JObject? GithubJSON;

            using HttpClient GithubClient = new HttpClient();
            {
                GithubClient.DefaultRequestHeaders.UserAgent.ParseAdd(AppSettings.GlobalUserAgent);
                string JSONResponse = await GithubClient.GetStringAsync("https://api.github.com/repos/e621-ReBot/e621-ReBot-v3/releases/latest");

                if (string.IsNullOrEmpty(JSONResponse))
                {
                    GitHubError();
                    return;
                }

                GithubJSON = JObject.Parse(JSONResponse);
            }

            Match MatchResult = VersionRegex().Match((string)GithubJSON["name"]);
            if (MatchResult.Success)
            {
                Version AppCurrentVersion = new Version(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
                Version AppUpdateVersion = new Version(MatchResult.Captures[0].Value);
                if (AppUpdateVersion > AppCurrentVersion)
                {
                    string? FileURL = GithubJSON.SelectToken("assets[0].browser_download_url")?.Value<string>();
                    DownloadUpdate(FileURL);
                }
                else
                {
                    UpdateNotNeeded();
                }
                AppSettings.Update_LastCheck = DateTime.UtcNow;
                return;
            }
            UpdateError();
        }

        private static async void DownloadUpdate(string ZipURL)
        {
            if (string.IsNullOrEmpty(ZipURL))
            {
                UpdateError();
                return;
            }

            byte[] UpdateBytes = await Module_Downloader.DownloadFileBytes(ZipURL, ActionType.Update);
            Directory.CreateDirectory("ReBotUpdate").Attributes = FileAttributes.Hidden;
            using (MemoryStream bytes2Stream = new MemoryStream(UpdateBytes))
            {
                using (FileStream FileStreamTemp = new FileStream($"ReBotUpdate\\ReBotUpdate.zip", FileMode.Create))
                {
                    bytes2Stream.WriteTo(FileStreamTemp);
                }
            }

            DeleteErrorLog();

            //AppSettings.Update_LastCheck = DateTime.UtcNow;
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                MessageBoxResult MessageBoxResultTemp = MessageBox.Show(Window_Main._RefHolder, "Update is downloaded and ready, do you want to update now?", "e621 ReBot v3", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                if (MessageBoxResultTemp == MessageBoxResult.Yes)
                {
                    if (File.Exists("e621 ReBot Updater.exe"))
                    {
                        Window_Main._RefHolder.Close();
                        Process.Start("e621 ReBot Updater.exe");
                    }
                    else
                    {
                        MessageBox.Show(Window_Main._RefHolder, "Updater.exe not found.", "e621 ReBot v3", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                    }
                }
                Window_Main._RefHolder.Update_TextBlock.Visibility = Visibility.Hidden;
                Window_Main._RefHolder.ReBot_Menu_ListBox.Visibility = Visibility.Visible;
            });
        }

        private static void GitHubError()
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.Update_TextBlock.Text = "Github not working as expected.";
                Window_Main._RefHolder.ReBot_Menu_ListBox.Visibility = Visibility.Visible;
            });
            Thread.Sleep(3000);
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.Update_TextBlock.Visibility = Visibility.Hidden;
            });
        }

        private static void UpdateError()
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.Update_TextBlock.Text = "No updates found.";
                Window_Main._RefHolder.ReBot_Menu_ListBox.Visibility = Visibility.Visible;
            });
            Thread.Sleep(3000);
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.Update_TextBlock.Visibility = Visibility.Hidden;
            });
        }

        private static void UpdateNotNeeded()
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.Update_TextBlock.Text = "Application is up to date.";
                Window_Main._RefHolder.ReBot_Menu_ListBox.Visibility = Visibility.Visible;
            });
            Thread.Sleep(3000);
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
            {
                Window_Main._RefHolder.Update_TextBlock.Visibility = Visibility.Hidden;
            });
        }

        private static void DeleteErrorLog()
        {
            if (File.Exists("ReBotErrorLog.txt")) File.Delete("ReBotErrorLog.txt");
        }
    }
}