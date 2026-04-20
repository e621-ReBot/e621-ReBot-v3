
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace e621_ReBot_v3.Modules.Downloader
{
    internal class Module_DownloadImport
    {
        internal static readonly List<string> _2Do_SubTasks = new List<string>();

        internal static void Import_SubJobs(string[] SubJobs)
        {
            //add jobs IN BACKGROUND!
            //_ = Task.Run(async () =>
            //{
            //    foreach (string line in SubJobs)
            //    {
            //        int lastQuoteEnd = line.LastIndexOf('"');
            //        int lastQuoteStart = line.LastIndexOf('"', lastQuoteEnd - 1);
            //        string folderName = line.Substring(lastQuoteStart + 1, lastQuoteEnd - lastQuoteStart - 1);
            //        string[] tagHolder = line.Substring(0, lastQuoteStart).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            //        string combinedTags = string.Join(' ', tagHolder);
            //        await Module_DLe621.Grab_MediaWithTags(combinedTags, folderName);
            //    }
            //});

            lock (_2Do_SubTasks)
            {
                foreach (string line in SubJobs)
                {
                    if (!_2Do_SubTasks.Contains(line)) _2Do_SubTasks.Add(line);
                }
            }

            Run_SubJob();
        }

        internal static bool SubJobRunning = false;
        internal static async Task Run_SubJob()
        {
            SubJobRunning = true;
            string FirstTask, folderName, combinedTags;
            lock (_2Do_SubTasks)
            {
                FirstTask = _2Do_SubTasks[0];
                _2Do_SubTasks.RemoveAt(0);
                Window_Main._RefHolder.Dispatcher.BeginInvoke(() => { Window_Main._RefHolder.JobImport_TextBlock.Text = $"{_2Do_SubTasks.Count} >"; });

                int lastQuoteEnd = FirstTask.LastIndexOf('"');
                int lastQuoteStart = FirstTask.LastIndexOf('"', lastQuoteEnd - 1);
                folderName = FirstTask.Substring(lastQuoteStart + 1, lastQuoteEnd - lastQuoteStart - 1);
                string[] tagHolder = FirstTask.Substring(0, lastQuoteStart).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                combinedTags = string.Join(' ', tagHolder);
            }

            await Module_DLe621.Grab_MediaWithTags(combinedTags, folderName);
            SubJobRunning = false;
        }
    }
}