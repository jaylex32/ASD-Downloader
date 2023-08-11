using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Linq;
using System.Diagnostics;

namespace new_ASD_Downloader
{
    public class MovieResult
    {
        public string Title { get; set; }
        public string Release_Date { get; set; }
    }

    public class MovieApiResponse
    {
        public int Total_Results { get; set; }
        public List<MovieResult> Results { get; set; }
    }

    public class RenamerObject
    {
        public ObservableCollection<string> SourceFiles { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> RenamedFiles { get; set; } = new ObservableCollection<string>();
    }

    public partial class ASD_Renamer : Window
    {
        public RenamerObject RenamerData { get; set; } = new RenamerObject();
        private string _loginToken; // Define the _loginToken field here
        private Dictionary<string, string> FileRenameMap = new Dictionary<string, string>(); // Define the FileRenameMap here
        public ASD_Renamer()
        {
            InitializeComponent();
            this.DataContext = RenamerData;

            OriginalFileListBox.ItemsSource = RenamerData.SourceFiles;
            NewNameListBox.ItemsSource = RenamerData.RenamedFiles;
        }


        private void OriginalFileListBox_DragOver(object sender, DragEventArgs e)
        {
            // If the data object contains file or folder paths, allow copy.
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OriginalFileListBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string fileOrFolder in files)
                {
                    // If it's a directory, add all files inside the directory
                    if (Directory.Exists(fileOrFolder))
                    {
                        string[] folderFiles = Directory.GetFiles(fileOrFolder);
                        foreach (string file in folderFiles)
                        {
                            RenamerData.SourceFiles.Add(file);
                        }
                    }
                    // If it's a file, add it directly
                    else if (File.Exists(fileOrFolder))
                    {
                        RenamerData.SourceFiles.Add(fileOrFolder);
                    }
                }
            }
        }

        public static int GetLevenshteinDistance(string string1, string string2)
        {
            int len1 = string1.Length;
            int len2 = string2.Length;
            int[,] d = new int[len1 + 1, len2 + 1];

            for (int i = 0; i <= len1; i++) { d[i, 0] = i; }
            for (int j = 0; j <= len2; j++) { d[0, j] = j; }

            for (int j = 1; j <= len2; j++)
            {
                for (int i = 1; i <= len1; i++)
                {
                    int cost = (string1[i - 1] != string2[j - 1]) ? 1 : 0;
                    int min1 = d[i - 1, j] + 1;
                    int min2 = d[i, j - 1] + 1;
                    int min3 = d[i - 1, j - 1] + cost;
                    d[i, j] = Math.Min(Math.Min(min1, min2), min3);
                }
            }

            return d[len1, len2];
        }

        public static double ComputeScore(string title, string apiTitle, string year, string apiYear)
        {
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(apiTitle))
            {
                return double.MaxValue;
            }

            int titleDistance = GetLevenshteinDistance(title.ToLower(), apiTitle.ToLower());
            int yearDistance = (!string.IsNullOrEmpty(year) && !string.IsNullOrEmpty(apiYear))
                                ? GetLevenshteinDistance(year, apiYear)
                                : 0;

            return titleDistance + yearDistance;
        }

        private MovieApiResponse GetMovieInformation(string title, string year)
        {
            string apiKey = "baf66526108dea78b59e123801d8acf9";
            string apiUrl = $"https://api.themoviedb.org/3/search/movie?api_key={apiKey}&query={title}";

            using (HttpClient httpClient = new HttpClient())
            {
                HttpResponseMessage response = httpClient.GetAsync(apiUrl).GetAwaiter().GetResult();
                string content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonConvert.DeserializeObject<MovieApiResponse>(content);
            }
        }

        private SeriesApiResponse GetSeriesInformation(string seriesName)
        {
            string apiKey = "E69C7A2CEF2F3152";
            string apiUrl = "https://api.thetvdb.com/login";
            string queryUrl = $"https://api.thetvdb.com/search/series?name={seriesName}";

            var body = new
            {
                apikey = apiKey
            };

            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                HttpResponseMessage response = httpClient.PostAsync(apiUrl, content).GetAwaiter().GetResult();
                var loginResponse = JsonConvert.DeserializeObject<LoginResponse>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResponse.Token);
                HttpResponseMessage queryResponse = httpClient.GetAsync(queryUrl).GetAwaiter().GetResult();
                var seriesResponse = JsonConvert.DeserializeObject<SeriesApiResponse>(queryResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult());

                return seriesResponse;
            }
        }

        public class LoginResponse
        {
            public string Token { get; set; } // Define the Token property here
        }

        public class SeriesData
        {
            public int Id { get; set; }
            public string SeriesName { get; set; }
            // Add other needed properties here
        }

        public class SeriesApiResponse
        {
            public List<SeriesData> Data { get; set; }
        }

        private EpisodeApiResponse GetEpisodeInformation(int seriesId, int seasonNumber, int episodeNumber)
        {
            string apiKey = "E69C7A2CEF2F3152";
            string apiUrl = $"https://api.thetvdb.com/series/{seriesId}/episodes/query?airedSeason={seasonNumber}&airedEpisode={episodeNumber}";

            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _loginToken); // Use the _loginToken value here

                HttpResponseMessage response = httpClient.GetAsync(apiUrl).GetAwaiter().GetResult();
                var episodeResponse = JsonConvert.DeserializeObject<EpisodeApiResponse>(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());

                return episodeResponse;
            }
        }

        public class EpisodeData
        {
            public string EpisodeName { get; set; }
            public int SeasonNumber { get; set; }
            public int EpisodeNumber { get; set; }
            // Other properties as needed
        }

        public class EpisodeApiResponse
        {
            public List<EpisodeData> Data { get; set; }
        }

        private string SanitizeFileName(string fileName)
        {
            // Replace specific characters
            fileName = fileName.Replace(':', ';');

            // Replace other invalid characters with '_'
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var invalidChar in invalidChars)
            {
                if (invalidChar != ':') // Skip ':' since it's already handled
                {
                    fileName = fileName.Replace(invalidChar, '_');
                }
            }
            return fileName;
        }

        private void RenameMovieFiles(string oldPath, string newName)
        {
            string directoryPath = Path.GetDirectoryName(oldPath);
            string newFileName = Path.GetFileNameWithoutExtension(newName);
            string newPath = Path.Combine(directoryPath, SanitizeFileName(newName));

            if (RenameFolderCheckBox.IsChecked == true)
            {
                string newFolderPath = Path.Combine(directoryPath, newFileName);
                if (!Directory.Exists(newFolderPath))
                {
                    Directory.CreateDirectory(newFolderPath);
                }
                newPath = Path.Combine(newFolderPath, Path.GetFileName(newName));
            }

            if (!newPath.EndsWith(Path.GetExtension(oldPath)))
            {
                newPath += Path.GetExtension(oldPath);
            }
            File.Move(oldPath, newPath);
        }

        private void RenameTvSeriesFiles(string oldPath, string newName)
        {
            string directoryPath = Path.GetDirectoryName(oldPath);
            string newFileName = Path.GetFileNameWithoutExtension(newName);
            string newPath = Path.Combine(directoryPath, SanitizeFileName(newName));

            if (RenameFolderCheckBox.IsChecked == true)
            {
                var match = Regex.Match(newFileName, @"(.+?) - S(\d+)E(\d+)");
                if (match.Success)
                {
                    string showName = match.Groups[1].Value;
                    string seasonNumber = match.Groups[2].Value;
                    string newFolderPath = Path.Combine(directoryPath, showName, "Season " + seasonNumber);
                    if (!Directory.Exists(newFolderPath))
                    {
                        Directory.CreateDirectory(newFolderPath);
                    }
                    newPath = Path.Combine(newFolderPath, Path.GetFileName(newName));
                }
            }

            if (!newPath.EndsWith(Path.GetExtension(oldPath)))
            {
                newPath += Path.GetExtension(oldPath);
            }
            File.Move(oldPath, newPath);
        }

        private void MatchButton_Click(object sender, RoutedEventArgs e)
        {
            RenamerData.RenamedFiles.Clear();

            foreach (var filePath in RenamerData.SourceFiles)
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);

                if (MoviesRadioButton.IsChecked == true)
                {
                    string patternWithYear = "(.+?)\\s\\((\\d{4})\\)";
                    string patternWithoutYear = "(.+?)\\s(\\d{4})";
                    string patternOnlyTitle = "(.+)";

                    string title = "";
                    string year = "";

                    Match matches = Regex.Match(fileName, patternWithYear);
                    if (matches.Success)
                    {
                        title = matches.Groups[1].Value.Trim().TrimEnd('-').Trim();
                        year = matches.Groups[2].Value;
                    }
                    else
                    {
                        matches = Regex.Match(fileName, patternWithoutYear);
                        if (matches.Success)
                        {
                            title = matches.Groups[1].Value.Trim().TrimEnd('-').Trim();
                            year = matches.Groups[2].Value;
                        }
                        else
                        {
                            matches = Regex.Match(fileName, patternOnlyTitle);
                            if (matches.Success)
                            {
                                title = matches.Groups[1].Value.Trim().TrimEnd('-').Trim();
                                year = null;
                            }
                        }
                    }

                    MovieApiResponse movieInfo = GetMovieInformation(title, year);
                    if (movieInfo == null)
                    {
                        // Log or notify the user that no information is found
                        continue;
                    }

                    List<string> options = new List<string>();
                    List<string> directMatches = new List<string>();

                    foreach (var movie in movieInfo.Results)
                    {
                        string apiTitle = movie.Title?.ToLower() ?? "";
                        string fileTitle = title.ToLower();
                        string[] fileTitleWords = fileTitle.Split(' ');
                        string[] apiTitleWords = apiTitle.Split(' ');
                        bool allWordsPresent = fileTitleWords.All(word => apiTitle.Contains(word));
                        bool equalWordCount = fileTitleWords.Length == apiTitleWords.Length;
                        string apiYear = "Unknown";
                        if (movie.Release_Date != null && movie.Release_Date.Length >= 4)
                        {
                            apiYear = movie.Release_Date.Substring(0, 4);
                        }
                        string newName = $"{title} ({apiYear}){System.IO.Path.GetExtension(filePath)}";
                        options.Add(newName);

                        if (allWordsPresent && equalWordCount)
                        {
                            directMatches.Add(newName);
                        }
                    }

                    if (directMatches.Count == 1)
                    {
                        RenamerData.RenamedFiles.Add(directMatches[0]);
                        // Add to rename map if needed
                    }
                    else
                    {
                        // If there's no direct match or more than one direct match, show the selection window
                        var selectionWindow = new SelectionWindow(options);
                        var result = selectionWindow.ShowDialog();
                        if (result.HasValue && result.Value)
                        {
                            string selection = selectionWindow.SelectedOption;
                            RenamerData.RenamedFiles.Add(selection); // This will update the NewNameListBox in the UI
                                                                     // Assuming FileRenameMap is a dictionary mapping file paths to new names
                            FileRenameMap[filePath] = selection; // Update the mapping if needed
                                                                 // Handle the new name selection...
                        }
                    }
                }
                else
                {
                    // Handle TV series
                    string[] patterns = new string[]
                    {
                      @"(.+)\s-\sS(\d{1,2})E(\d{1,2})\s-\s.*",   // Matches "30 Coins - S01E01 - Cobwebs"
                      @"(.+)\s-\s(\d)x(\d{1,2})\s-\s.*",         // Matches "American Gods - 1x01 - The Bone Orchard"
                      @"(.+)\s-\s(\d{1,2})x(\d{1,2})",           // Matches "series - 01x01"
                      @"(.+?)\sS(\d{1,2})E(\d{1,2})",            // Matches "Name S01E01"
                      @"(.+)\s-\sS(\d{1,2})E(\d{1,2})",          // Matches "series - S01E01"
                      @"(.+)\s\((\d{4})\)\s-\s(\d)x(\d{1,2})",  // Matches "series (year) - 1x01"
                      @"(.+)\.S(\d{1,2})E(\d{1,2})\..*",         // Matches "Breaking.Bad.S04E13.720p.BRRip.DD5.1.x264-PSYPHER.mp4"
                      @"(.+?)\.S(\d{1,2})\.E(\d{1,2})\..*",      // Matches "The.Enemy.Within.S01E01.720p.HDTV.x264-AVS-postbot.mp4"
                      @"(.+?)\s\((\d{4})\)\s-\s(\d)x(\d{1,2})\s-\s(.*)(\.\w+)",
                      @"(?i)(.*?)[. _-]+s(\d+)[. _-]*e(\d+)[. _-]",
                      @"(?i)(.*?)[. _-]+(\d+)x(\d+)[. _-]"
                    };

                    foreach (string pattern in patterns)
                    {
                        Match matches = Regex.Match(fileName, pattern);
                        if (matches.Success)
                        {
                            string title = matches.Groups[1].Value.Trim().TrimEnd('-').Trim();
                            string yearOrSeason = matches.Groups[2].Value;
                            string episodeNumber = matches.Groups[3].Value;

                            SeriesApiResponse seriesInfo = GetSeriesInformation(title);
                            var series = seriesInfo.Data.First(); // Assuming Data is a List

                            // Get episode information
                            int seasonNumber = int.Parse(yearOrSeason.TrimStart('0')); // Remove leading zeros
                            int episodeNum = int.Parse(episodeNumber.TrimStart('0')); // Remove leading zeros
                            EpisodeApiResponse episodeInfo = GetEpisodeInformation(series.Id, seasonNumber, episodeNum);

                            // Assume the first result is the correct episode
                            var episodeData = episodeInfo.Data.First(); // Assuming Data is a List
                            string newName = $"{title} - S{yearOrSeason.PadLeft(2, '0')}E{episodeNum.ToString().PadLeft(2, '0')} - {episodeData.EpisodeName}{System.IO.Path.GetExtension(filePath)}";
                            RenamerData.RenamedFiles.Add(newName);

                            // Add to rename map if needed
                            break;
                        }
                    }
                }
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            if (openFileDialog.ShowDialog() == true)
            {
                RenamerData.SourceFiles.Clear(); // Clearing the source files
                foreach (string fileName in openFileDialog.FileNames)
                {
                    RenamerData.SourceFiles.Add(fileName); // Adding files to source files
                }
                RenamerData.RenamedFiles.Clear(); // Clearing the renamed files
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            RenamerData.SourceFiles.Clear();
            RenamerData.RenamedFiles.Clear();
        }

        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < RenamerData.SourceFiles.Count; i++)
            {
                string oldPath = RenamerData.SourceFiles[i];
                string newName = SanitizeFileName(RenamerData.RenamedFiles[i]); // Call SanitizeFileName here

                // Determine whether it's a movie or TV series based on selected options
                if (MoviesRadioButton.IsChecked == true)
                {
                    RenameMovieFiles(oldPath, newName);
                }
                else
                {
                    RenameTvSeriesFiles(oldPath, newName);
                }
            }
        }

        private void FileLocation_Click(object sender, RoutedEventArgs e)
        {
            int selectedIndex = NewNameListBox.SelectedIndex;
            if (selectedIndex != -1 && selectedIndex < RenamerData.SourceFiles.Count)
            {
                string originalFilePath = RenamerData.SourceFiles[selectedIndex];
                string folderPath = Path.GetDirectoryName(originalFilePath);
                if (Directory.Exists(folderPath))
                {
                    Process.Start("explorer.exe", folderPath);
                }
                else
                {
                    MessageBox.Show("Folder does not exist.", "Error");
                }
            }
        }

    }
}