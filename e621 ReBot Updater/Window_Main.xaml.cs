using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace e621_ReBot_Updater
{
    public partial class Window_Main : Window
    {
        public Window_Main()
        {
            InitializeComponent();
            Updating_TextBlock.Visibility = Visibility.Hidden;
            DelayTimer.Tick += DelayTimer_Tick;

            UpdateWorker.DoWork += UpdateWorker_DoWork;
            UpdateWorker.RunWorkerCompleted += UpdateWorker_RunWorkerCompleted;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CurrentVersionString = FileVersionInfo.GetVersionInfo("e621 ReBot v3.exe").FileVersion;
            CurrentVersion_TextBlock.Text = $"Current Version: {CurrentVersionString}";
        }

        DispatcherTimer DelayTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1) };
        private void Window_ContentRendered(object sender, EventArgs e)
        {
            DelayTimer.Start();
        }

        private void DelayTimer_Tick(object? sender, EventArgs e)
        {
            DelayTimer.Stop();
            if (!Check4ReBotRunning() && Directory.Exists("ReBotUpdate") && File.Exists("ReBotUpdate\\ReBotUpdate.zip"))
            {
                UpdateWorker.RunWorkerAsync();
            }
            else
            {
                Close();
                Process.Start("e621 ReBot v3.exe");
            }
        }

        private bool Check4ReBotRunning()
        {
            foreach (Process ProcessTemp in Process.GetProcesses())
            {
                if (ProcessTemp.ProcessName.Equals("e621 ReBot v3"))
                {
                    MessageBox.Show(this, "You must close ReBot first.", "e621 ReBot Updater", MessageBoxButton.OK, MessageBoxImage.Error);
                    return true;
                }
            }
            return false;
        }

        private string? CurrentVersionString;
        readonly BackgroundWorker UpdateWorker = new BackgroundWorker();
        private void UpdateWorker_DoWork(object? sender, DoWorkEventArgs e)
        {
            List<ZipArchiveEntry> ZippedFileList = new List<ZipArchiveEntry>();
            using (MemoryStream bytes2Stream = new MemoryStream(File.ReadAllBytes("ReBotUpdate\\ReBotUpdate.zip")))
            {
                using (ZipArchive UpdateZip = new ZipArchive(bytes2Stream, ZipArchiveMode.Read))
                {
                    string LatestVersionString = CurrentVersionString;
                    foreach (ZipArchiveEntry ZipEntry in UpdateZip.Entries)
                    {
                        if (ZipEntry.Name.Equals("version.txt"))
                        {
                            using (Stream StreamTemp = ZipEntry.Open())
                            {
                                using (StreamReader StreamReaderTemp = new StreamReader(StreamTemp))
                                {
                                    LatestVersionString = StreamReaderTemp.ReadToEnd().Trim();
                                    LatestVersion_TextBlock.Dispatcher.BeginInvoke(() => LatestVersion_TextBlock.Text = $"Latest Version: {LatestVersionString}");
                                }
                            }
                        }
                        else
                        {
                            ZippedFileList.Add(ZipEntry);
                        }
                    }

                    string[] CVSHolder = CurrentVersionString.Split('.', StringSplitOptions.RemoveEmptyEntries);
                    string[] LVSHolder = LatestVersionString.Split('.', StringSplitOptions.RemoveEmptyEntries);
                    int CurrVerNum = (int)(int.Parse(CVSHolder[1]) * Math.Pow(10, 6) + int.Parse(CVSHolder[2]) * Math.Pow(10, 3) + int.Parse(CVSHolder[3]));
                    int UpdateVerNum = (int)(int.Parse(LVSHolder[1]) * Math.Pow(10, 6) + int.Parse(LVSHolder[2]) * Math.Pow(10, 3) + int.Parse(LVSHolder[3]));
                    if (UpdateVerNum > CurrVerNum)
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            LatestVersion_TextBlock.Foreground = new SolidColorBrush(Colors.LimeGreen);
                            Updating_TextBlock.Visibility = Visibility.Visible;
                            ((Storyboard)FindResource("Spinner")).Begin(this);
                        });

                        Thread.Sleep(1000);

                        ushort ExtractCounter = 0;
                        foreach (ZipArchiveEntry ZipEntry in ZippedFileList)
                        {
                            ExtractCounter++;
                            Updating_TextBlock.Dispatcher.BeginInvoke(() => Updating_TextBlock.Text = $"Extracting: #{ExtractCounter} of {ZippedFileList.Count}");
                            if (ZipEntry.FullName.EndsWith("/"))
                            {
                                Directory.CreateDirectory(ZipEntry.FullName);
                            }
                            else
                            {
                                ZipEntry.ExtractToFile(Path.Combine("./", ZipEntry.FullName), true);
                            }
                        }

                        Updating_TextBlock.Dispatcher.BeginInvoke(() => Updating_TextBlock.Text = Updating_TextBlock.Text = $"Finished! Starting ReBot...");
                    }
                    else
                    {
                        LatestVersion_TextBlock.Dispatcher.BeginInvoke(() => LatestVersion_TextBlock.Foreground = new SolidColorBrush(Colors.Red));
                    }
                    Thread.Sleep(1000);
                }
            }
        }

        private void UpdateWorker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
        {
            Directory.Delete("ReBotUpdate", true);
            Close();
            Process.Start("e621 ReBot v3.exe");
        }
    }
}