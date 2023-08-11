using new_ASD_Downloader;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Configuration;

namespace Athena_Stream_Downloader
{
    public class DownloadStatus
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Filename { get; set; }
        public string PercentageDownloaded { get; set; }
        public string TotalSize { get; set; }
        public string DownloadSpeed { get; set; }
        public string ETA { get; set; }
        public string CurrentFragment { get; set; }
        public string TotalFragments { get; set; }
        public string Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string MaxETA { get; set; } // Use a string instead of TimeSpan

    }

    public partial class MainWindow : Window
    {
        public static MainWindow Instance;  // Add this line here
        private BindingList<DownloadStatus> outputLines = new BindingList<DownloadStatus>();
        private Dictionary<string, string> configParams;

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;  // Set the instance to this
            CheckAndDownloadDependencies();

            SettingsPage settingsPage = new SettingsPage(); // Your code might be different
            settingsPage.ConfigUpdated += LoadSettings;
            outputLines.AllowNew = false;  // Add this line

            // Read the config file
            configParams = ReadConfigFile("bin/config.conf");

            // Bind the outputLines list to the DataGrid
            OutputDataGrid.ItemsSource = outputLines;

            // Set up event handlers
            Download_Button.Click += DownloadButton_Click;
        }


        public void LoadSettings()
        {
            // Here, reload the settings from the config file
            // and update any variables in MainWindow that depend on these settings
            configParams = ReadConfigFile("./bin/config.conf");
        }


        private void CheckAndDownloadDependencies()
        {
            string binPath = ".\\bin";
            if (!Directory.Exists(binPath))
            {
                Directory.CreateDirectory(binPath);
            }

            string ytDlpPath = "bin\\yt-dlp.exe";
            string ffmpegZipPath = "bin\\ffmpeg.zip";
            string ffmpegPath = "bin\\ffmpeg.exe";
            string ffplayPath = "bin\\ffplay.exe";
            string ffprobePath = "bin\\ffprobe.exe";
            string configPath = ".\\bin\\config.conf";

            DownloadIfNotExists(ytDlpPath, "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe");

            if (!File.Exists(ffmpegPath) || !File.Exists(ffplayPath) || !File.Exists(ffprobePath))
            {
                DownloadIfNotExists(ffmpegZipPath, "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip");

                if (File.Exists(ffmpegZipPath))
                {
                    // Extract the ZIP file
                    ZipFile.ExtractToDirectory(ffmpegZipPath, binPath);

                    // Move exe files to root of bin and delete other folders
                    foreach (string exeFile in Directory.GetFiles(binPath, "*.exe", SearchOption.AllDirectories))
                    {
                        string destinationPath = Path.Combine(binPath, Path.GetFileName(exeFile));
                        if (!File.Exists(destinationPath))
                        {
                            File.Move(exeFile, destinationPath);
                        }
                    }

                    // Delete folders and ZIP file
                    foreach (string dir in Directory.GetDirectories(binPath))
                    {
                        Directory.Delete(dir, true);
                    }
                    File.Delete(ffmpegZipPath);
                }
            }

            if (!File.Exists(configPath))
            {
                string configContent = @"Homepath=.\Downloads
moviepath=.\Downloads
tvshowpath=.\Downloads
Template=%(series)s\Season %(season_number)s\S%(season_number)sE%(episode_number)s - %(title)s.%(ext)s
Cookies=Edge";

                File.WriteAllText(configPath, configContent);
            }


            string streamsPath = ".\\Downloads";
            if (!Directory.Exists(streamsPath))
            {
                Directory.CreateDirectory(streamsPath);
            }
        }

        private void DownloadIfNotExists(string filePath, string downloadUrl)
        {
            if (!File.Exists(filePath))
            {
                string fileName = Path.GetFileName(filePath);

                string message = $"{fileName} was not found in the path. To have download features, you must download it. Do you want to download now?";
                string title = $"Download {fileName}?";

                MessageBoxResult result = System.Windows.MessageBox.Show(message, title, MessageBoxButton.YesNo);

                if (result == MessageBoxResult.Yes) // Note the change here
                {
                    using (WebClient webClient = new WebClient())
                    {
                        webClient.DownloadFile(downloadUrl, filePath);
                    }
                }
            }
        }
        private string GetCookiesBrowserParam()
        {
            string browserSelection = configParams["Cookies"];
            return $"--cookies-from-browser {browserSelection}";
        }

        private string CookiesText = "--cookies cookies.txt";

        private string GetTimeChaptersArgument()
        {
            return Chapters.IsChecked.GetValueOrDefault() ? "--split-chapters" : "--no-warnings";
        }

        private string[] GetCookiesArgument()
        {
            if (Cookies_Browser.IsChecked.GetValueOrDefault())
            {
                return new string[] { "--cookies-from-browser", configParams["Cookies"] };
            }
            else if (Cookies_Txt.IsChecked.GetValueOrDefault())
            {
                return new string[] { CookiesText };
            }
            else
            {
                return Array.Empty<string>();
            }
        }

        private string GetResolutionArgument()
        {
            if (RES720P.IsChecked == true)
                return "-f bestvideo[height<=?720]+bestaudio/best[height<=?720]";
            else if (RES1080P.IsChecked == true)
                return "-f bestvideo[height<=?1080]+bestaudio/best[height<=?1080]";
            else if (RES2160P.IsChecked == true)
                return "-f best";
            else if (MP4_EXT.IsChecked == true)
                return "-f bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best";
            else if (MP3_EXT.IsChecked == true)
                return "-x --extract-audio --audio-format mp3 --embed-metadata";
            else if (M4A_EXT.IsChecked == true)
                return "-f ba[ext=m4a]";
            else if (WEBM_EXT.IsChecked == true)
                return "-f bv[ext=webm]ba";
            else if (OGG_EXT.IsChecked == true)
                return "-i -f bestaudio -w -c --extract-audio --embed-metadata --audio-format best";
            else if (DEFAULT_EXT.IsChecked == true)
                return "-f bv*+ba";

            return string.Empty; // Default case if no radio button is selected
        }

        private void Download_Textbox_MouseMove(object sender, MouseEventArgs e)
        {
            // Check if the mouse is over the TextBox
            if (e.GetPosition(Download_Textbox).X >= 0 && e.LeftButton == MouseButtonState.Released)
            {
                // Declare a variable to hold the clipboard text
                string clipboardText = null;

                // Attempt to get the clipboard text up to 5 times
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        clipboardText = Clipboard.GetText();
                        break;  // If the clipboard text was successfully retrieved, exit the loop
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        // If an error occurred, wait for a short time and then try again
                        System.Threading.Thread.Sleep(100);
                    }
                }

                // Check if the text is a valid URL
                if (clipboardText != null && Uri.IsWellFormedUriString(clipboardText, UriKind.Absolute))
                {
                    // Paste the URL into the TextBox
                    Download_Textbox.Text = clipboardText;
                }
            }
        }


        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if an item is selected in the DataGrid
            if (OutputDataGrid.SelectedItem is DownloadStatus selectedDownload && selectedDownload.Status == "Ready to Download")
            {
                // Get absolute paths
                string tempPath = System.IO.Path.GetFullPath("./Downloads");
                string ffmpegPath = System.IO.Path.GetFullPath("./bin/ffmpeg.exe");
                string homePath = System.IO.Path.GetFullPath(configParams["Homepath"]);
                string safeFilename = selectedDownload.Filename.Replace(":", ";");

                foreach (char c in Path.GetInvalidFileNameChars().Where(x => x != ':'))
                {
                    safeFilename = safeFilename.Replace(c.ToString(), "");
                }

                foreach (char c in Path.GetInvalidPathChars().Where(x => x != ':'))
                {
                    safeFilename = safeFilename.Replace(c.ToString(), "");
                }

                string template = configParams["Template"].Replace("%(title)s", safeFilename);
                string resolutionArgument = GetResolutionArgument();
                string timeChaptersArgument = GetTimeChaptersArgument();
                string[] cookies = GetCookiesArgument();
                string cookiesArgument = string.Join(" ", cookies);
                string arguments = $"--no-part -N 4 {resolutionArgument} {cookiesArgument} --progress restrict-filenames {timeChaptersArgument} -P \"temp: {tempPath}\" -P \"home: {homePath}\" --output \"{template}\" \"{selectedDownload.Url}\" --ffmpeg-location \"{ffmpegPath}\"";

                // Run the process
                RunProcessAsync("./bin/yt-dlp.exe", arguments, selectedDownload);

                // Set the status to "Downloading"
                selectedDownload.Status = "Downloading";
                OutputDataGrid.Items.Refresh();
            }
            else
            {
                // Get the URL from a TextBox (assuming you have a TextBox named UrlTextBox)
                string url = Download_Textbox.Text;
                if (!string.IsNullOrEmpty(url))
                {
                    // Get absolute paths
                    string tempPath = System.IO.Path.GetFullPath("./Downloads");
                    string ffmpegPath = System.IO.Path.GetFullPath("./bin/ffmpeg.exe");
                    string homePath = System.IO.Path.GetFullPath(configParams["Homepath"]);
                    string template = configParams["Template"];
                    string resolutionArgument = GetResolutionArgument();
                    string timeChaptersArgument = GetTimeChaptersArgument();
                    string[] cookies = GetCookiesArgument();
                    string cookiesArgument = string.Join(" ", cookies);
                    string arguments = $"--no-part -N 4 {resolutionArgument} {cookiesArgument} --progress --windows-filenames {timeChaptersArgument} -P \"temp: {tempPath}\" -P \"home: {homePath}\" --output \"{template}\" \"{url}\" --ffmpeg-location \"{ffmpegPath}\"";

                    // Run the process
                    RunProcessAsync("./bin/yt-dlp.exe", arguments);

                    // Clear the URL text box
                    Download_Textbox.Clear();
                }
            }
        }


        private void Settings_Button_Click(object sender, RoutedEventArgs e)
        {
            SettingsPage settingsPage = new SettingsPage();
            settingsPage.Show();
        }

        private void Rename_Button_Click(object sender, RoutedEventArgs e)
        {
            ASD_Renamer renamerWindow = new ASD_Renamer(); // Create an instance of the ASD_Renamer window
            renamerWindow.Show(); // Show the ASD_Renamer window
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            for (int i = outputLines.Count - 1; i >= 0; i--)
            {
                if (outputLines[i].Status == "Completed")
                {
                    outputLines.RemoveAt(i);
                }
            }

            // Since we directly modified the outputLines list, we need to refresh the DataGrid
            OutputDataGrid.Items.Refresh();
        }


        private void PathButton_Click(object sender, RoutedEventArgs e)
        {
            // Assuming selectedDownload is the selected item from the DataGrid
            var selectedDownload = OutputDataGrid.SelectedItem as DownloadStatus;

            if (selectedDownload != null)
            {
                string tempPath = System.IO.Path.GetFullPath("./Downloads");
                string homePath = System.IO.Path.GetFullPath(configParams["Homepath"]);

                // If download is still in progress, open tempPath, otherwise open homePath
                if (selectedDownload.Status == "Downloading" || selectedDownload.Status == "Paused")
                {
                    Process.Start("explorer.exe", tempPath);
                }
                else if (selectedDownload.Status == "Completed")
                {
                    Process.Start("explorer.exe", homePath);
                }
            }
            else
            {
                MessageBox.Show("Please select a file first.");
            }
        }



        private Dictionary<string, string> ReadConfigFile(string path)
        {
            var configValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var lines = File.ReadAllLines(path);

            foreach (var line in lines)
            {
                var parts = line.Split('=');

                // Continue if the line doesn't contain exactly 2 parts
                if (parts.Length != 2) continue;

                string key = parts[0].Trim();
                string value = parts[1].Trim();

                // If the dictionary does not already contain the key, add it
                if (!configValues.ContainsKey(key))
                {
                    configValues.Add(key, value);
                }
            }

            return configValues;
        }

        private void WebServer_Button_Click(object sender, RoutedEventArgs e)
        {
            ASD_WebServer webServerWindow = new ASD_WebServer();
            webServerWindow.Show();
        }

        private async void RunProcessAsync(string filename, string arguments, DownloadStatus selectedDownload = null)
        {
            await Task.Run(() =>
            {
                Process process = new Process();
                process.StartInfo.FileName = filename;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                DownloadStatus currentDownload = selectedDownload;
                decimal lastPercentageUpdate = 0;

                object lockObject = new object();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        // Parse the console output for the filename
                        var filenameMatch = Regex.Match(e.Data, @"\[download\] Destination: (?<filename>.*)$");
                        if (filenameMatch.Success)
                        {
                            lock (lockObject)
                            {
                                this.Dispatcher.Invoke(() =>
                                {
                                    if (currentDownload == null)
                                    {
                                        currentDownload = new DownloadStatus
                                        {
                                            Filename = Path.GetFileName(filenameMatch.Groups["filename"].Value),
                                            Status = "Starting",
                                            StartTime = DateTime.Now
                                        };
                                        outputLines.Add(currentDownload);
                                    }
                                    else
                                    {
                                        currentDownload.Status = "Starting";
                                    }

                                    OutputDataGrid.Items.Refresh();
                                });
                            }
                        }

                        // Parse the console output for download progress
                        var downloadMatch = Regex.Match(e.Data, @"\[download\]\s*(?<percentage>.*?)% of ~(?<totalSize>.*?) at (?<downloadSpeed>.*?) ETA (?<eta>.*?) \(frag (?<currentFragment>\d+)/(?<totalFragments>\d+)\)");
                        if (downloadMatch.Success && currentDownload != null)
                        {
                            lock (lockObject)
                            {
                                // Store the ETA directly as a string
                                string newETA = downloadMatch.Groups["eta"].Value;

                                // If currentDownload.MaxETA is null or newETA is greater than currentDownload.MaxETA, then update currentDownload.MaxETA
                                if (currentDownload.MaxETA == null || string.CompareOrdinal(newETA, currentDownload.MaxETA) > 0)
                                {
                                    currentDownload.MaxETA = newETA;
                                }

                                // Try to parse the percentage to a decimal
                                if (decimal.TryParse(downloadMatch.Groups["percentage"].Value, out decimal percentage))
                                {
                                    // If the change in percentage is more than 1%, update the UI
                                    if (Math.Abs(percentage - lastPercentageUpdate) > 2)
                                    {
                                        this.Dispatcher.Invoke(() =>
                                        {
                                            // If successful, format it as an integer followed by a percentage sign
                                            currentDownload.PercentageDownloaded = $"{Math.Floor(percentage)}%";

                                            currentDownload.TotalSize = downloadMatch.Groups["totalSize"].Value;
                                            currentDownload.DownloadSpeed = downloadMatch.Groups["downloadSpeed"].Value;

                                            currentDownload.CurrentFragment = downloadMatch.Groups["currentFragment"].Value;
                                            currentDownload.TotalFragments = downloadMatch.Groups["totalFragments"].Value;
                                            currentDownload.Status = "Downloading";

                                            OutputDataGrid.Items.Refresh();

                                            // Update the last percentage update
                                            lastPercentageUpdate = percentage;
                                        });
                                    }
                                }
                            }
                        }
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    lock (lockObject)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            if (currentDownload != null)
                            {
                                currentDownload.Status = "Error";
                                currentDownload.DownloadSpeed = e.Data;
                                OutputDataGrid.Items.Refresh();
                            }
                        });
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for the process to finish
                process.WaitForExit();

                // If a download was in progress, mark it as completed
                if (currentDownload != null)
                {
                    lock (lockObject)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            currentDownload.Status = "Completed";
                            currentDownload.EndTime = DateTime.Now;
                            currentDownload.PercentageDownloaded = "100%";
                            currentDownload.CurrentFragment = currentDownload.TotalFragments;
                            OutputDataGrid.Items.Refresh();
                        });
                    }
                }
            });
        }
        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension
            dlg.DefaultExt = ".txt";
            dlg.Filter = "Text documents (.txt)|*.txt";

            // Display OpenFileDialog by calling ShowDialog method
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox
            if (result == true)
            {
                // Open document
                string filename = dlg.FileName;
                string[] lines = File.ReadAllLines(filename);

                for (int i = 0; i < lines.Length; i += 2)
                {
                    string title = lines[i];
                    string url = (i + 1 < lines.Length) ? lines[i + 1] : null;

                    if (!string.IsNullOrEmpty(url))
                    {
                        // Add the url to the list of items to be downloaded
                        DownloadStatus newDownload = new DownloadStatus
                        {
                            Filename = title,
                            Url = url,
                            Status = "Ready to Download",
                            StartTime = DateTime.Now
                        };
                        outputLines.Add(newDownload);
                        OutputDataGrid.Items.Refresh();
                    }
                }
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Explicitly shut down the application when the window is closing
            Application.Current.Shutdown();
        }
    }
}