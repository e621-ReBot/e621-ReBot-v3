using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace e621_ReBot_v3.Modules
{
    internal static class Module_e621Data
    {
        private static readonly HttpClientHandler e621_HttpClientHandler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
        private static readonly HttpClient e621_HttpClient = new HttpClient(e621_HttpClientHandler) { Timeout = TimeSpan.FromSeconds(15) };
        internal static string? DataDownload(string Address, bool Authentication = false)
        {
            using (HttpRequestMessage HttpRequestMessageTemp = new HttpRequestMessage(HttpMethod.Get, Address))
            {
                HttpRequestMessageTemp.Headers.UserAgent.ParseAdd(AppSettings.GlobalUserAgent);
                if (Authentication) HttpRequestMessageTemp.Headers.Authorization = new AuthenticationHeaderValue("Basic", $"{Convert.ToBase64String(Encoding.ASCII.GetBytes($"{AppSettings.UserName}:{Module_Cryptor.Decrypt(AppSettings.APIKey)}"))}");

                try
                {
                    using (HttpResponseMessage HttpResponseMessageTemp = e621_HttpClient.Send(HttpRequestMessageTemp))
                    {
                        if (HttpResponseMessageTemp.IsSuccessStatusCode)
                        {
                            return HttpResponseMessageTemp.Content.ReadAsStringAsync().Result;
                        }
                    }
                }
                catch (Exception e) //Timeout most often
                {
                    return $"ⓔ{e.Message}";
                }

            }
            return null;
        }

        internal static MemoryStream? DownloadDBExport(string Address, Button? ButtonRef = null)
        {
            MemoryStream DownloadedBytes = new MemoryStream();
            DateTime DateTimeTempUTC = DateTime.UtcNow;

            ushort RetryCount = 0;
            while (RetryCount < 3)
            {
                try
                {
                    HttpWebRequest DBExportDownloader = (HttpWebRequest)WebRequest.Create(string.Format(Address, DateTimeTempUTC.Year, DateTimeTempUTC.ToString("MM"), DateTimeTempUTC.ToString("dd")));
                    DBExportDownloader.Timeout = 5000;
                    using (HttpWebResponse DownloaderReponse = (HttpWebResponse)DBExportDownloader.GetResponse())
                    {
                        using (Stream DownloadStream = DownloaderReponse.GetResponseStream())
                        {
                            byte[] DownloadBuffer = new byte[65536]; // 64 kB buffer
                            while (DownloadedBytes.Length < DownloaderReponse.ContentLength)
                            {
                                int DownloadStreamPartLength = DownloadStream.Read(DownloadBuffer, 0, DownloadBuffer.Length);
                                if (DownloadStreamPartLength > 0)
                                {
                                    DownloadedBytes.Write(DownloadBuffer, 0, DownloadStreamPartLength);
                                    double ReportPercentage = DownloadedBytes.Length / (double)DownloaderReponse.ContentLength;
                                    ButtonRef.Dispatcher.BeginInvoke(() => { ButtonRef.Content = $"Downloading: {ReportPercentage:P2}"; });
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }
                    return DownloadedBytes;
                }
                catch
                {
                    RetryCount++;
                    DateTimeTempUTC = DateTimeTempUTC.AddDays(-1);
                }
            }
            return null;
        }

        internal static void DLSuggestions()
        {
            ushort SuccessCount = 0;
            MemoryStream? DownloadedStream = DownloadDBExport("https://e621.net/db_export/tags-{0}-{1}-{2}.csv.gz", Window_Main._RefHolder.SettingsButton_DLSuggestions);
            if (DownloadedStream != null)
            {
                Window_Main._RefHolder.SettingsButton_DLSuggestions.Dispatcher.BeginInvoke(() => { Window_Main._RefHolder.SettingsButton_DLSuggestions.Content = "Processing Tags..."; });

                List<string> TagList = new List<string>();
                List<string> ArtistList = new List<string>();
                DownloadedStream.Position = 0;
                using (GZipStream TagsZip = new GZipStream(DownloadedStream, CompressionMode.Decompress))
                {
                    using (StreamReader StreamReaderTemp = new StreamReader(TagsZip))
                    {
                        StreamReaderTemp.ReadLine();
                        string ReadCSV = StreamReaderTemp.ReadToEnd();
                        using (TextFieldParser CSVParser = new TextFieldParser(new StringReader(ReadCSV)))
                        {
                            CSVParser.HasFieldsEnclosedInQuotes = true;
                            CSVParser.SetDelimiters(",");

                            List<Tuple<int, string>> TagListTemp = new List<Tuple<int, string>>();
                            while (!CSVParser.EndOfData)
                            {
                                string[] CSVFields = CSVParser.ReadFields();
                                if (!CSVFields[3].Equals("0")) //post_count
                                {
                                    TagListTemp.Add(new Tuple<int, string>(int.Parse(CSVFields[3]), CSVFields[1]));
                                }
                                if (CSVFields[2].Equals("1")) //category
                                {
                                    ArtistList.Add(CSVFields[1]);
                                }
                            }
                            TagListTemp.Sort((x, y) => y.Item1.CompareTo(x.Item1));
                            //x and y are tuples from list, not items inside a single tuple
                            //Item1 and Item2 are items of single tuple
                            TagList = TagListTemp.Select(x => x.Item2).ToList(); //select Item2 - that is string
                            TagListTemp.Clear();
                        }
                    }
                    DownloadedStream.Dispose();
                }
                TagList = TagList.Distinct().ToList();

                DownloadedStream = DownloadDBExport("https://e621.net/db_export/tag_aliases-{0}-{1}-{2}.csv.gz", Window_Main._RefHolder.SettingsButton_DLSuggestions);
                if (DownloadedStream != null)
                {
                    Window_Main._RefHolder.SettingsButton_DLSuggestions.Dispatcher.BeginInvoke(() => { Window_Main._RefHolder.SettingsButton_DLSuggestions.Content = "Processing Aliases..."; });

                    Dictionary<string, List<string>> TagAliases = new Dictionary<string, List<string>>();
                    DownloadedStream.Position = 0;
                    using (GZipStream TagsZip = new GZipStream(DownloadedStream, CompressionMode.Decompress))
                    {
                        using (StreamReader StreamReaderTemp = new StreamReader(TagsZip))
                        {
                            StreamReaderTemp.ReadLine();
                            string ReadCSV = StreamReaderTemp.ReadToEnd();
                            using (TextFieldParser CSVParser = new TextFieldParser(new StringReader(ReadCSV)))
                            {
                                CSVParser.HasFieldsEnclosedInQuotes = true;
                                CSVParser.SetDelimiters(",");

                                while (!CSVParser.EndOfData)
                                {
                                    string[] CSVFields = CSVParser.ReadFields();
                                    if (CSVFields[4].Equals("active"))
                                    {
                                        if (TagAliases.ContainsKey(CSVFields[2]))
                                        {
                                            TagAliases[CSVFields[2]].Add(CSVFields[1]);
                                        }
                                        else
                                        {
                                            TagAliases.Add(CSVFields[2], new List<string> { CSVFields[1] });
                                        }

                                        if (ArtistList.Contains(CSVFields[2]))
                                        {
                                            ArtistList.Add(CSVFields[1]); //Also add aliases to Artist list
                                        }
                                    }
                                }
                            }
                        }
                    }
                    DownloadedStream.Dispose();

                    if (TagAliases != null)
                    {
                        StringBuilder StringBuilderTemp = new StringBuilder();
                        foreach (string StringTemp in TagList)
                        {
                            StringBuilderTemp.Append(StringTemp);
                            if (TagAliases.ContainsKey(StringTemp))
                            {
                                StringBuilderTemp.Append("," + string.Join(",", TagAliases[StringTemp]));
                            }
                            StringBuilderTemp.Append("✄");
                        }
                        File.WriteAllText("tags.txt", StringBuilderTemp.ToString());
                    }
                    else
                    {
                        File.WriteAllText("tags.txt", string.Join("✄", TagList));
                    }

                    if (ArtistList.Count > 0)
                    {
                        ArtistList.Sort();
                        File.WriteAllText("artists.txt", string.Join("✄", ArtistList));
                    }
                }
                SuccessCount++;
            }

            DownloadedStream = DownloadDBExport("https://e621.net/db_export/pools-{0}-{1}-{2}.csv.gz", Window_Main._RefHolder.SettingsButton_DLSuggestions);
            if (DownloadedStream != null)
            {
                Window_Main._RefHolder.SettingsButton_DLSuggestions.Dispatcher.BeginInvoke(() => { Window_Main._RefHolder.SettingsButton_DLSuggestions.Content = "Processing Pools..."; });

                List<string> PoolList = new List<string>();
                DownloadedStream.Position = 0;
                using (GZipStream TagsZip = new GZipStream(DownloadedStream, CompressionMode.Decompress))
                {
                    using (StreamReader StreamReaderTemp = new StreamReader(TagsZip))
                    {
                        StreamReaderTemp.ReadLine();
                        string ReadCSV = StreamReaderTemp.ReadToEnd();
                        using (TextFieldParser CSVParser = new TextFieldParser(new StringReader(ReadCSV)))
                        {
                            CSVParser.HasFieldsEnclosedInQuotes = true;
                            CSVParser.SetDelimiters(",");

                            string[] CSVFields;
                            while (!CSVParser.EndOfData)
                            {
                                CSVFields = CSVParser.ReadFields();
                                PoolList.Add($"{CSVFields[0]},{CSVFields[1]}");
                            }
                        }
                    }
                }
                DownloadedStream.Dispose();

                PoolList = PoolList.Distinct().ToList();
                PoolList.Reverse();
                File.WriteAllText("pools.txt", string.Join("✄", PoolList));
                SuccessCount++;
            }

            if (SuccessCount == 2)
            {
                Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
                {
                    if (Window_Tagger.SuggestionPopup != null) Window_Tagger.SuggestionPopup.LoadSuggestionBox();
                    Window_Main._RefHolder.SettingsButton_DLSuggestions.Content = "DL Suggestions";
                    Window_Main._RefHolder.SettingsButton_DLSuggestions.IsEnabled = true;
                    MessageBox.Show(Window_Main._RefHolder, "Downloaded all Tags and Pools for tag suggestions.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information);
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                });
            }
        }

        internal static void DLGenders()
        {
            MemoryStream? DownloadedStream = DownloadDBExport("https://e621.net/db_export/tag_aliases-{0}-{1}-{2}.csv.gz", Window_Main._RefHolder.SettingsButton_DLGenders);
            if (DownloadedStream != null)
            {
                Window_Main._RefHolder.SettingsButton_DLGenders.Dispatcher.BeginInvoke(() => { Window_Main._RefHolder.SettingsButton_DLGenders.Content = "Processing Genders..."; });

                string? ReadCSVAliases = null;
                DownloadedStream.Position = 0;
                using (GZipStream TagsZip = new GZipStream(DownloadedStream, CompressionMode.Decompress))
                {
                    using (StreamReader StreamReaderTemp = new StreamReader(TagsZip))
                    {
                        StreamReaderTemp.ReadLine();
                        ReadCSVAliases = StreamReaderTemp.ReadToEnd();
                    }
                }
                DownloadedStream.Dispose();

                string? ReadCSVImplications = null;
                DownloadedStream = DownloadDBExport("https://e621.net/db_export/tag_implications-{0}-{1}-{2}.csv.gz", Window_Main._RefHolder.SettingsButton_DLGenders);
                if (DownloadedStream != null)
                {
                    Window_Main._RefHolder.SettingsButton_DLGenders.Dispatcher.BeginInvoke(() => { Window_Main._RefHolder.SettingsButton_DLGenders.Content = "Processing Implications..."; });

                    DownloadedStream.Position = 0;
                    using (GZipStream TagsZip = new GZipStream(DownloadedStream, CompressionMode.Decompress))
                    {
                        using (StreamReader StreamReaderTemp = new StreamReader(TagsZip))
                        {
                            StreamReaderTemp.ReadLine();
                            ReadCSVImplications = StreamReaderTemp.ReadToEnd();
                        }
                    }
                    DownloadedStream.Dispose();
                }

                List<string> GendersList = new List<string>() { "ambiguous_gender", "male", "female", "intersex" };
                for (int repeat = 0; repeat < 4; repeat++)
                {
                    foreach (string StringTemp in new string[] { ReadCSVAliases, ReadCSVImplications })
                    {
                        using (TextFieldParser CSVParser = new TextFieldParser(new StringReader(StringTemp)))
                        {
                            CSVParser.HasFieldsEnclosedInQuotes = true;
                            CSVParser.SetDelimiters(",");
                            while (!CSVParser.EndOfData)
                            {
                                string[] CSVFields = CSVParser.ReadFields();
                                if (GendersList.Contains(CSVFields[2]) && CSVFields[4].Equals("active"))
                                {
                                    GendersList.Add(CSVFields[1]);
                                }
                            }
                        }
                    }
                    GendersList = GendersList.Distinct().ToList();
                }
                GendersList.Sort();
                File.WriteAllText("genders.txt", string.Join("✄", GendersList));

                GC.WaitForPendingFinalizers();
                GC.Collect();

                Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
                {
                    Window_Main._RefHolder.SettingsButton_DLGenders.Content = "DL Genders";
                    Window_Main._RefHolder.SettingsButton_DLGenders.IsEnabled = true;
                    MessageBox.Show(Window_Main._RefHolder, "Downloaded all Genders.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
        }

        internal static void DLDNPs()
        {
            MemoryStream? DownloadedStream = DownloadDBExport("https://e621.net/db_export/tag_implications-{0}-{1}-{2}.csv.gz", Window_Main._RefHolder.SettingsButton_DLDNPs);
            if (DownloadedStream != null)
            {
                Window_Main._RefHolder.SettingsButton_DLDNPs.Dispatcher.BeginInvoke(() => { Window_Main._RefHolder.SettingsButton_DLDNPs.Content = "Processing DNPs..."; });

                List<string> DNPList = new List<string>();
                string[] DNPStrings = new string[] { "avoid_posting", "conditional_dnp" };

                DownloadedStream.Position = 0;
                using (GZipStream TagsZip = new GZipStream(DownloadedStream, CompressionMode.Decompress))
                {
                    using (StreamReader StreamReaderTemp = new StreamReader(TagsZip))
                    {
                        StreamReaderTemp.ReadLine();
                        string ReadCSVImplications = StreamReaderTemp.ReadToEnd();
                        using (TextFieldParser CSVParser = new TextFieldParser(new StringReader(ReadCSVImplications)))
                        {
                            CSVParser.HasFieldsEnclosedInQuotes = true;
                            CSVParser.SetDelimiters(",");
                            while (!CSVParser.EndOfData)
                            {
                                string[] CSVFields = CSVParser.ReadFields();
                                if (DNPStrings.Any(str => CSVFields[2].Equals(str)) && CSVFields[4].Equals("active"))
                                {
                                    DNPList.Add(CSVFields[1]);
                                }
                            }
                        }
                    }
                }
                DownloadedStream.Dispose();

                DownloadedStream = DownloadDBExport("https://e621.net/db_export/tag_aliases-{0}-{1}-{2}.csv.gz", Window_Main._RefHolder.SettingsButton_DLDNPs);
                if (DownloadedStream != null)
                {
                    Window_Main._RefHolder.SettingsButton_DLDNPs.Dispatcher.BeginInvoke(() => { Window_Main._RefHolder.SettingsButton_DLDNPs.Content = "Processing Aliases..."; });

                    DownloadedStream.Position = 0;
                    using (GZipStream TagsZip = new GZipStream(DownloadedStream, CompressionMode.Decompress))
                    {
                        using (StreamReader StreamReaderTemp = new StreamReader(TagsZip))
                        {
                            StreamReaderTemp.ReadLine();
                            string ReadCSVAliases = StreamReaderTemp.ReadToEnd();
                            using (TextFieldParser CSVParser = new TextFieldParser(new StringReader(ReadCSVAliases)))
                            {
                                CSVParser.HasFieldsEnclosedInQuotes = true;
                                CSVParser.SetDelimiters(",");
                                while (!CSVParser.EndOfData)
                                {
                                    string[] CSVFields = CSVParser.ReadFields();
                                    if (DNPList.Contains(CSVFields[2]) && CSVFields[4].Equals("active"))
                                    {
                                        DNPList.Add(CSVFields[1]);
                                    }
                                }
                            }
                        }
                    }
                    DownloadedStream.Dispose();
                }
                DNPList = DNPList.Distinct().ToList();
                DNPList.Sort();
                File.WriteAllText("DNPs.txt", string.Join("✄", DNPList));

                GC.WaitForPendingFinalizers();
                GC.Collect();

                Window_Main._RefHolder.Dispatcher.BeginInvoke(() =>
                {
                    Window_Main._RefHolder.SettingsButton_DLDNPs.Content = "DL DNPs";
                    Window_Main._RefHolder.SettingsButton_DLDNPs.IsEnabled = true;
                    MessageBox.Show(Window_Main._RefHolder, "Downloaded all DNPs.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
        }
    }
}