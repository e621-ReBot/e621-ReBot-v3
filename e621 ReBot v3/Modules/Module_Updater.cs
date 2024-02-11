using System.IO.Compression;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using e621_ReBot_v3.Modules;
using System.Net;
using HtmlAgilityPack;
using System;
using System.Threading;
using System.Text.RegularExpressions;

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
            if (!AppSettings.FirstRun)
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
        }


        [GeneratedRegex(@"(?:.+/v)(\d\.\d+\.\d+\.\d+)")]
        private static partial Regex VersionRegex();
        private static CookieContainer CookieContainerGitHub = new CookieContainer();
        internal static void Check4Update()
        {
            Window_Main._RefHolder.Dispatcher.BeginInvoke(() => 
            {
                Window_Main._RefHolder.Update_TextBlock.Visibility = Visibility.Visible;
            });
            Thread.Sleep(1000);

            string UpdateSource = "https://github.com/e621-ReBot/e621-ReBot-v3/releases";
            string HTMLSource = Module_Grabber.GetPageSource(UpdateSource, ref CookieContainerGitHub);
            if (string.IsNullOrEmpty(HTMLSource))
            {
                GitHubError();
                return;
            }
            Thread.Sleep(1000);

            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(HTMLSource);
            HtmlNode ReleaseNode = HtmlDocumentTemp.DocumentNode.SelectSingleNode("//div[@class='Box-footer']//include-fragment"); //SelectSingleNode(".//turbo-frame[@id='repo-content-turbo-frame']//div[@data-pjax]/section");
            if (ReleaseNode != null)
            {
                string ReleaseTag = ReleaseNode.Attributes["src"].Value;//.Replace("https://github.com/e621-ReBot/e621-ReBot-v3/releases/expanded_assets/v", null);
                Match MatchResult = VersionRegex().Match(ReleaseTag);
                if (MatchResult.Success)
                {
                    ReleaseTag = MatchResult.Groups[1].Value;
                    string[] CVSHolder = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
                    string[] LVSHolder = ReleaseTag.Split('.', StringSplitOptions.RemoveEmptyEntries);
                    int CurrVerNum = (int)(int.Parse(CVSHolder[1]) * Math.Pow(10, 6) + int.Parse(CVSHolder[2]) * Math.Pow(10, 3) + int.Parse(CVSHolder[3]));
                    int UpdateVerNum = (int)(int.Parse(LVSHolder[1]) * Math.Pow(10, 6) + int.Parse(LVSHolder[2]) * Math.Pow(10, 3) + int.Parse(LVSHolder[3]));
                    if (UpdateVerNum > CurrVerNum)
                    {
                        DownloadUpdate(ReleaseNode.Attributes["src"].Value);
                    }
                    else
                    {
                        UpdateNotNeeded();
                    }
                    AppSettings.Update_LastCheck = DateTime.UtcNow;
                    return;
                }
            }
            UpdateError();
        }

        private static void DownloadUpdate(string AssetsURL)
        {
            string HTMLSource = Module_Grabber.GetPageSource(AssetsURL, ref CookieContainerGitHub);
            if (string.IsNullOrEmpty(HTMLSource))
            {
                GitHubError();
                return;
            }

            HtmlDocument HtmlDocumentTemp = new HtmlDocument();
            HtmlDocumentTemp.LoadHtml(HTMLSource);

            HtmlNode UpdateZipFile = HtmlDocumentTemp.DocumentNode.SelectSingleNode(".//ul[@data-view-component]//li//a");
            if (UpdateZipFile != null)
            {
                if (UpdateZipFile.SelectSingleNode("./span").InnerText.Trim().StartsWith("e621.ReBot-v3"))
                {
                    byte[] UpdateBytes = Module_Downloader.DownloadFileBytes($"https://github.com/{UpdateZipFile.Attributes["href"].Value}", ActionType.Update);
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
                            Window_Main._RefHolder.Close();
                            Process.Start("e621 ReBot Updater.exe");
                        }
                        Window_Main._RefHolder.Update_TextBlock.Visibility = Visibility.Hidden;
                        Window_Main._RefHolder.ReBot_Menu_ListBox.Visibility = Visibility.Visible;
                    });
                }
                return;
            }
            UpdateError();
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