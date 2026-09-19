using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace e621_ReBot_v3.Modules
{
    internal static class Module_e621Data
    {
        internal static async Task<string?> DataDownload(string Address, bool Authentication = false)
        {
            string? base64Auth = Authentication ? Convert.ToBase64String(Encoding.ASCII.GetBytes($"{AppSettings.UserName}:{Module_Cryptor.Decrypt(AppSettings.APIKey)}")) : null;
            DownloadResult DownloadResultTemp = await ReBot_HttpClient.DownloadStringAsync(Address, AuthHeader: base64Auth);

            if (!DownloadResultTemp.IsSuccessStatusCode)
                return $"ⓔ{DownloadResultTemp.Message}";

            return DownloadResultTemp.TextData;
        }

        internal static async Task<byte[]?> DownloadDBExport(string Address)
        {
            IProgress<DownloadProgress> DownloadProgressTracker = new Progress<DownloadProgress>(ProgressReport =>
            {
                Window_Main._RefHolder.SettingsButton_DLSuggestions.Dispatcher.InvokeAsync(() => { Window_Main._RefHolder.SettingsButton_DLSuggestions.Content = $"Downloading: {ProgressReport.Percentage:F2}%"; });
            });

            DownloadResult DownloadResultTemp = await ReBot_HttpClient.DownloadBytesAsync(Address, DownloadProgressTracker);
            if (DownloadResultTemp.IsSuccessStatusCode)
                return DownloadResultTemp.Data;

            return null;
        }

        internal static async void DLSuggestions()
        {
            ushort SuccessCount = 0;

            byte[]? AliasBytes = await DownloadDBExport("https://static1.e621.net/data/db_export/tag_aliases.csv.gz");
            if (AliasBytes == null)
                return;

            //Process Aliases for later use
            Dictionary<string, List<string>> aliases = new Dictionary<string, List<string>>();
            Window_Main._RefHolder.SettingsButton_DLSuggestions.Dispatcher.InvokeAsync(() => { Window_Main._RefHolder.SettingsButton_DLSuggestions.Content = "Processing Aliases..."; });
            using (MemoryStream AliasStream = new MemoryStream(AliasBytes))
            {
                using (GZipStream TagsZip = new GZipStream(AliasStream, CompressionMode.Decompress))
                {
                    using (TextFieldParser CSVParser = new TextFieldParser(TagsZip))
                    {
                        CSVParser.HasFieldsEnclosedInQuotes = true;
                        CSVParser.SetDelimiters(",");
                        CSVParser.ReadLine(); //skip header
                        while (!CSVParser.EndOfData)
                        {
                            //id, antecedent_name, consequent_name, created_at, status
                            string[] CSVFields = CSVParser.ReadFields();
                            string old_name = CSVFields[1];
                            string new_name = CSVFields[2];

                            if (!aliases.TryGetValue(new_name, out List<string>? list))
                            {
                                list = new List<string>();
                                aliases[new_name] = list;
                            }
                            list.Add(old_name);
                        }
                    }
                }
            }
            AliasBytes = null;

            byte[]? ImplicationBytes = await DownloadDBExport("https://static1.e621.net/data/db_export/tag_implications.csv.gz");
            if (ImplicationBytes == null)
                return;

            //Process Implications for later use
            Dictionary<string, List<string>> implications = new Dictionary<string, List<string>>();
            Window_Main._RefHolder.SettingsButton_DLSuggestions.Dispatcher.InvokeAsync(() => { Window_Main._RefHolder.SettingsButton_DLSuggestions.Content = "Processing Implications..."; });
            using (MemoryStream? ImplicationStream = new MemoryStream(ImplicationBytes))
            {
                using (GZipStream TagsZip = new GZipStream(ImplicationStream, CompressionMode.Decompress))
                {
                    using (TextFieldParser CSVParser = new TextFieldParser(TagsZip))
                    {
                        CSVParser.HasFieldsEnclosedInQuotes = true;
                        CSVParser.SetDelimiters(",");
                        CSVParser.ReadLine(); //skip header
                        while (!CSVParser.EndOfData)
                        {
                            //id, antecedent_name, consequent_name, created_at, status
                            string[] CSVFields = CSVParser.ReadFields();
                            string base_Tag = CSVFields[1];
                            string implicated_tag = CSVFields[2];
                            string status = CSVFields[4];

                            if (status.Equals("active"))
                            {
                                if (!implications.TryGetValue(implicated_tag, out List<string>? implicationstemp))
                                {
                                    implicationstemp = new List<string>();
                                    implications[implicated_tag] = implicationstemp;
                                }
                                implicationstemp.Add(base_Tag);
                            }
                        }
                    }
                }
            }
            ImplicationBytes = null;

            byte[]? TagBytes = await DownloadDBExport("https://static1.e621.net/data/db_export/tags.csv.gz");
            if (TagBytes == null)
                return;

            //Do tags and artists and DNPs
            List<string> TagList = new List<string>();
            Dictionary<string, List<string>> TagAliases = new Dictionary<string, List<string>>();
            HashSet<string> ArtistList = new HashSet<string>();
            HashSet<string> DNPList = new HashSet<string>();
            Window_Main._RefHolder.SettingsButton_DLSuggestions.Dispatcher.InvokeAsync(() => { Window_Main._RefHolder.SettingsButton_DLSuggestions.Content = "Processing Tags..."; });
            using (MemoryStream? TagStream = new MemoryStream(TagBytes))
            {
                using (GZipStream TagsZip = new GZipStream(TagStream, CompressionMode.Decompress))
                {
                    using (TextFieldParser CSVParser = new TextFieldParser(TagsZip))
                    {
                        CSVParser.HasFieldsEnclosedInQuotes = true;
                        CSVParser.SetDelimiters(",");
                        CSVParser.ReadLine(); //skip header

                        Dictionary<string, int> TagListTemp = new Dictionary<string, int>();
                        string[] DNPStrings = new string[] { "avoid_posting", "conditional_dnp" };
                        while (!CSVParser.EndOfData)
                        {
                            //id, name, category, post_count
                            string[] CSVFields = CSVParser.ReadFields();
                            string name = CSVFields[1];
                            string category = CSVFields[2];
                            int postCount = int.Parse(CSVFields[3]);

                            if (postCount > 0)
                            {
                                TagListTemp.Add(name, postCount);

                                //Also note down it's alias
                                if (aliases.TryGetValue(name, out List<string>? aliasList))
                                {
                                    TagAliases[name] = aliasList;
                                }
                            }

                            if (category.Equals("1")) //Also add to separate artist list
                            {
                                if (DNPStrings.Any(str => name.Equals(str)))
                                {
                                    DNPList.Add(name);
                                    if (aliases.TryGetValue(name, out List<string>? dnp_aliasList1))
                                    {
                                        DNPList.UnionWith(dnp_aliasList1);
                                    }

                                    if (implications.TryGetValue(name, out List<string>? implicationList))
                                    {
                                        DNPList.UnionWith(implicationList);
                                        foreach (string impliedTag in implicationList)
                                        {
                                            if (aliases.TryGetValue(impliedTag, out List<string>? dnp_aliasList2))
                                            {
                                                DNPList.UnionWith(dnp_aliasList2);
                                            }
                                        }
                                    }
                                }

                                ArtistList.Add(name);
                                //Also note down it's alias
                                if (aliases.TryGetValue(name, out List<string>? aliasList))
                                {
                                    ArtistList.UnionWith(aliasList);
                                }

                                ////There are some implications as well
                                //if (implications.TryGetValue(name, out List<string>? ImplicationList))
                                //{
                                //    ArtistList.UnionWith(ImplicationList);
                                //}
                            }
                        }
                        TagList = TagListTemp.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();
                    }
                }
                SuccessCount++;
            }
            TagBytes = null;

            //Do Genders
            HashSet<string> GendersList = new HashSet<string>() { "ambiguous_gender", "male", "female", "intersex" };
            Queue<string> queue = new Queue<string>(GendersList);
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();

                if (aliases.TryGetValue(current, out var aliasList))
                {
                    foreach (var next in aliasList)
                    {
                        if (GendersList.Add(next)) queue.Enqueue(next);
                    }
                }

                if (implications.TryGetValue(current, out var implList))
                {
                    foreach (var next in implList)
                    {
                        if (GendersList.Add(next)) queue.Enqueue(next);
                    }
                }
            }

            byte[]? PoolBytes = await DownloadDBExport("https://static1.e621.net/data/db_export/pools.csv.gz");
            if (PoolBytes == null)
                return;


            //Do Pools
            List<string> PoolList = new List<string>();
            Window_Main._RefHolder.SettingsButton_DLSuggestions.Dispatcher.InvokeAsync(() => { Window_Main._RefHolder.SettingsButton_DLSuggestions.Content = "Processing Pools..."; });
            using (MemoryStream? PoolStream = new MemoryStream(PoolBytes))
            {
                using (GZipStream TagsZip = new GZipStream(PoolStream, CompressionMode.Decompress))
                {
                    using (TextFieldParser CSVParser = new TextFieldParser(TagsZip))
                    {
                        CSVParser.HasFieldsEnclosedInQuotes = true;
                        CSVParser.SetDelimiters(",");
                        CSVParser.ReadLine(); //skip header
                        while (!CSVParser.EndOfData)
                        {
                            //id, name, created_at, updated_at, creator_id, description, is_active, post_ids
                            string[] CSVFields = CSVParser.ReadFields();
                            string pool_id = CSVFields[0];
                            string pool_name = CSVFields[1];
                            PoolList.Add($"{pool_id},{pool_name}");
                        }
                    }
                }

                PoolList = PoolList.Distinct().ToList();
                PoolList.Reverse();
                SuccessCount++;
            }
            PoolBytes = null;


            Window_Main._RefHolder.SettingsButton_DLSuggestions.Dispatcher.InvokeAsync(() => { Window_Main._RefHolder.SettingsButton_DLSuggestions.Content = "Writting Files..."; });
            if (TagList.Count > 0)
            {
                StringBuilder StringBuilderTemp = new StringBuilder();
                foreach (string tag in TagList)
                {
                    StringBuilderTemp.Append(tag);

                    if (TagAliases.TryGetValue(tag, out List<string>? aliasesList))
                    {
                        StringBuilderTemp.Append(',');
                        StringBuilderTemp.Append(string.Join(',', aliasesList));
                    }
                    StringBuilderTemp.Append('✄');
                }
                File.WriteAllText("tags.txt", StringBuilderTemp.ToString());
            }
            if (ArtistList.Count > 0)
            {
                File.WriteAllText("artists.txt", string.Join('✄', ArtistList.OrderBy(x => x)));
            }
            if (GendersList.Count > 0)
            {
                File.WriteAllText("genders.txt", string.Join("✄", GendersList.OrderBy(x => x)));
            }
            if (DNPList.Count > 0)
            {
                File.WriteAllText("DNPs.txt", string.Join("✄", DNPList.OrderBy(x => x)));
            }
            if (PoolList.Count > 0)
            {
                File.WriteAllText("pools.txt", string.Join('✄', PoolList));
            }

            if (SuccessCount == 2)
            {
                Window_Main._RefHolder.Dispatcher.InvokeAsync(() =>
                {
                    if (Window_Tagger.SuggestionPopup != null)
                        Window_Tagger.SuggestionPopup.LoadSuggestionBox();

                    Window_Main._RefHolder.SettingsButton_DLSuggestions.Content = "DL Suggestions";
                    Window_Main._RefHolder.SettingsButton_DLSuggestions.IsEnabled = true;
                    MessageBox.Show(Window_Main._RefHolder, "Downloaded all Tags and Pools for tag suggestions in addition to Artists for DNPs check.", "e621 ReBot", MessageBoxButton.OK, MessageBoxImage.Information);

                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                });
            }
        }
    }
}