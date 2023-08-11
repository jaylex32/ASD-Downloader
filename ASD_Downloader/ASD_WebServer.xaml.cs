using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Threading;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Athena_Stream_Downloader;
using static new_ASD_Downloader.ASD_WebServer;
using System.Threading.Tasks;
using System.Xml;
using System.Collections.Specialized;

namespace new_ASD_Downloader
{
    public class WebServerDownloadStatus : INotifyPropertyChanged
    {
        public string UniqueID { get; set; }
        private string filename;
        public string Filename
        {
            get { return filename; }
            set { filename = value; OnPropertyChanged(nameof(Filename)); }
        }
        public string PercentageDownloaded { get; set; }
        public string TotalSize { get; set; }
        public string DownloadSpeed { get; set; }
        public string CurrentFragment { get; set; }
        public string TotalFragments { get; set; }
        public string Status { get; set; }
        public DateTime? StartTime { get; set; } // Make StartTime nullable
        public DateTime? EndTime { get; set; }
        public string MaxETA { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }

    public partial class ASD_WebServer : Window
    {
        // Declare a dictionary to store the entire log for each unique ID
        private Dictionary<string, StringBuilder> entireLogBuilders = new Dictionary<string, StringBuilder>();
        private Dictionary<string, StringBuilder> logBuilders = new Dictionary<string, StringBuilder>();
        private Dictionary<string, List<string>> logLines = new Dictionary<string, List<string>>();
        //private DispatcherTimer logRefreshTimer;
        private readonly object logLock = new object();
        private WebServer webServer;
        private HashSet<string> addedItems = new HashSet<string>();
        private DispatcherTimer timer;
        private Dictionary<string, string> configParams; // Configuration parameters
        private Dictionary<string, Logs> logWindows = new Dictionary<string, Logs>();
        public ObservableCollection<VideoInfo> VideoItems { get; set; }
        public ObservableCollection<WebServerDownloadStatus> DownloadStatusItems { get; set; }
        public ObservableCollection<CombinedVideoInfo> CombinedVideoItems { get; set; }

        public ASD_WebServer()
        {
            InitializeComponent();
            configParams = ReadConfigFile("bin/config.conf"); // Read the config file
            webServer = new WebServer();
            VideoItems = new ObservableCollection<VideoInfo>();
            DownloadStatusItems = new ObservableCollection<WebServerDownloadStatus>(); // Initialize the DownloadStatusItems collection
            CombinedVideoItems = new ObservableCollection<CombinedVideoInfo>();
            CombinedVideoItems.CollectionChanged += CombinedVideoItems_CollectionChanged;
            btnStart.Click += BtnStart_Click;
            btnStop.Click += BtnStop_Click;
            this.DataContext = this;
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1); // Set the interval as needed
            timer.Tick += Timer_Tick;
            timer.Start();
            //logRefreshTimer = new DispatcherTimer();
            //logRefreshTimer.Interval = TimeSpan.FromSeconds(5); // Refresh every 5 seconds
            //logRefreshTimer.Tick += LogRefreshTimer_Tick;
            //logRefreshTimer.Start();
        }

        private void CombinedVideoItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            btnClear.IsEnabled = CombinedVideoItems.Any(); // Enable if there are items, disable if not
        }

        private void dataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.Header.ToString() == "Title")
            {
                TextBox t = e.EditingElement as TextBox;
                var combinedVideoInfo = e.Row.DataContext as CombinedVideoInfo;

                // Update the title
                string oldTitle = combinedVideoInfo.VideoDetails.Title;
                string newTitle = t.Text; // The new title entered by the user
                combinedVideoInfo.VideoDetails.Title = newTitle;

                // Update the corresponding Filename in the download status
                string originalFilename = combinedVideoInfo.VideoDetails.OriginalFilename;
                string newFilename = originalFilename.Replace(oldTitle, newTitle); // Replace oldTitle with newTitle
                var downloadStatusItem = DownloadStatusItems.FirstOrDefault(item => item.UniqueID == GenerateUniqueID(originalFilename, combinedVideoInfo.VideoDetails.DownloadURL));
                if (downloadStatusItem != null)
                {
                    downloadStatusItem.Filename = newFilename;
                    downloadStatusItem.UniqueID = GenerateUniqueID(newTitle, combinedVideoInfo.VideoDetails.DownloadURL); // Update the unique ID
                }

                // Update the unique ID in the VideoDetails
                combinedVideoInfo.VideoDetails.UniqueID = GenerateUniqueID(newTitle, combinedVideoInfo.VideoDetails.DownloadURL);

                // Notify the DataGrid that the item has been updated
                var index = CombinedVideoItems.IndexOf(combinedVideoInfo);
                CombinedVideoItems[index] = combinedVideoInfo; // This line triggers the update in the DataGrid

                // Additional debug statement to verify the change
                Debug.WriteLine($"Title changed to: {newTitle}, Unique ID: {combinedVideoInfo.VideoDetails.UniqueID}");
            }
        }

        private Dictionary<string, string> ReadConfigFile(string path) // Method to read config file
        {
            var configValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = File.ReadAllLines(path);

            foreach (var line in lines)
            {
                var parts = line.Split('=');
                if (parts.Length != 2) continue;

                string key = parts[0].Trim();
                string value = parts[1].Trim();
                if (!configValues.ContainsKey(key))
                {
                    configValues.Add(key, value);
                }
            }

            return configValues;
        }

        private string GenerateUniqueID(string title, string url)
        {
            return title + "|" + url; // Example of a simple unique identifier
        }


        private string SanitizeFileName(string fileName)
        {
            // Replace specific characters with the desired replacements
            fileName = fileName.Replace(':', ';');

            // Replace other invalid characters with '_'
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var invalidChar in invalidChars)
            {
                if (invalidChar != ':' && invalidChar != '\'')
                {
                    fileName = fileName.Replace(invalidChar, '_');
                }
            }

            // Use a regular expression to find a year pattern
            Regex yearRegex = new Regex(@"\b\d{4}\b");
            Match match = yearRegex.Match(fileName);

            // If a year is found
            if (match.Success)
            {
                // Check if the year is already enclosed in parentheses
                bool isEnclosedInParentheses = (match.Index > 0 && fileName[match.Index - 1] == '(')
                                               && (match.Index + 4 < fileName.Length && fileName[match.Index + 4] == ')');

                // If not enclosed in parentheses, enclose it
                if (!isEnclosedInParentheses)
                {
                    // Replace the year with the year enclosed in parentheses
                    fileName = fileName.Substring(0, match.Index) + $"({match.Value})" + fileName.Substring(match.Index + 4);
                }
            }

            return fileName;
        }

        public void StartDownload(string title, string url)
        {
            // Sanitize the title
            string sanitizedTitle = SanitizeFileName(title);

            // Get the configuration parameters
            string homePath = configParams["Homepath"];
            string moviePath = configParams["moviepath"];
            string seriesPath = configParams["tvshowpath"];
            string cookies = configParams["Cookies"];
            string path = homePath; // Default path

            // Check which radio button is selected (replace with actual radio button controls)
            if (StreamsRadioButton.IsChecked == true)
            {
                path = homePath;
            }
            else if (MoviesRadioButton.IsChecked == true)
            {
                path = moviePath;
            }
            else if (TvSeriesRadioButton.IsChecked == true)
            {
                path = seriesPath;
            }

            // Get the absolute paths for temp folder and ffmpeg
            string tempPath = System.IO.Path.GetFullPath("./Downloads");
            string ffmpegPath = System.IO.Path.GetFullPath("./bin/ffmpeg.exe");

            // Build the title argument with the desired format using the sanitized title
            string titleArgument;
            if (createfolder.IsChecked == true) // Replace with actual checkbox control
            {
                titleArgument = $"\"{sanitizedTitle}/{sanitizedTitle}.%(ext)s\"";
            }
            else
            {
                titleArgument = $"\"{sanitizedTitle}.%(ext)s\"";
            }

            // Build the arguments
            string arguments = $"--no-part -N 4 --cookies-from-browser {cookies} --progress -P \"temp: {tempPath}\" -P \"home: {path}\" --output {titleArgument} \"{url}\" --ffmpeg-location \"{ffmpegPath}\"";

            // Generate unique ID
            string uniqueID = GenerateUniqueID(title, url);

            // Run the process and print debug information
            Debug.WriteLine($"Starting download for {title}");
            RunProcessAsync("./bin/yt-dlp.exe", arguments, uniqueID);
        }


        // Click event handler for the "Path" button
        private void btnOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            // Get the selected item
            var selectedItem = dataGrid.SelectedItem as CombinedVideoInfo;

            // Check if an item is selected
            if (selectedItem == null)
            {
                MessageBox.Show("Please select a file.", "No file selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Determine the download path of the selected item
            string filename = selectedItem?.DownloadStatus?.Filename;
            if (string.IsNullOrEmpty(filename))
            {
                MessageBox.Show("Filename is not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string downloadPath = GetDownloadPath(filename);

            // Check if the folder or file exists
            if (!string.IsNullOrEmpty(downloadPath))
            {
                // Open the folder in File Explorer
                Process.Start("explorer.exe", "/select," + downloadPath);
            }
            else
            {
                MessageBox.Show("Download path does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Method to determine the download path for a given filename
        private string GetDownloadPath(string filename)
        {
            string tempPath = System.IO.Path.GetFullPath("./Downloads");
            string homePath = configParams["Homepath"];
            string moviePath = configParams["moviepath"];
            string seriesPath = configParams["tvshowpath"];

            // List of paths to check
            string[] pathsToCheck = { tempPath, homePath, moviePath, seriesPath };

            foreach (string path in pathsToCheck)
            {
                string filePath = Path.Combine(path, filename);
                if (File.Exists(filePath))
                {
                    return filePath;
                }
            }

            return string.Empty; // Return an empty string if the file is not found in any location
        }


        //private void LogRefreshTimer_Tick(object sender, EventArgs e)
        //{
        //    foreach (var uniqueID in logWindows.Keys)
        //    {
        //        if (logWindows.TryGetValue(uniqueID, out var logWindow) && logBuilders.TryGetValue(uniqueID, out var logBuilder))
        //        {
        //            // Get the new lines from the logBuilder
        //            string newText = logBuilder.ToString();

        //            // Retrieve or create the entire log StringBuilder for this unique ID
        //            if (!entireLogBuilders.TryGetValue(uniqueID, out var entireLogBuilder))
        //            {
        //                entireLogBuilder = new StringBuilder();
        //                entireLogBuilders[uniqueID] = entireLogBuilder;
        //            }

        //            // Append the new lines to the entire log
        //            entireLogBuilder.Append(newText);

        //            // Update the log window with the entire log
        //            logWindow.AppendLog(entireLogBuilder.ToString(), newText);

        //            // Clear the new lines StringBuilder after updating the window
        //            logBuilder.Clear();
        //        }
        //    }
        //}

        public void ShowLogWindow(string uniqueID)
        {
            // Check if the uniqueID exists in the logBuilders dictionary
            if (!logBuilders.ContainsKey(uniqueID))
            {
                // If it doesn't exist, we can't show a log window for it. We might want to show an error message or take some other action here.
                MessageBox.Show($"No logs available for ID: {uniqueID}");  // For example, show a message box
                return;
            }

            // Check if the window is already open
            if (logWindows.ContainsKey(uniqueID))
            {
                // If it is open, bring it to front
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    if (logWindows[uniqueID].WindowState == WindowState.Minimized)
                    {
                        logWindows[uniqueID].WindowState = WindowState.Normal;
                    }
                    logWindows[uniqueID].Activate();
                }));
            }
            else
            {
                // If it is not open, prepare the log text in a separate task
                Task.Run(() =>
                {
                    // Prepare the log text (this might take some time and we're doing it off the UI thread)
                    List<string> logLines;
                    lock (logLock)
                    {
                        logLines = logBuilders[uniqueID].ToString().Split(new[] { Environment.NewLine }, StringSplitOptions.None).ToList();
                    }
                    string logText = string.Join(Environment.NewLine, logLines);

                    // Now that the log text is ready, switch back to the UI thread to create and show the window
                    Application.Current.Dispatcher.Invoke(new Action(() =>
                    {
                        Logs logWindow = new Logs();
                        logWindow.Closed += (s, e) => logWindows.Remove(uniqueID);  // Remove the window from our dictionary when it is closed
                        logWindows[uniqueID] = logWindow;
                        logWindow.Show();
                        logWindow.AppendLog(logText, string.Empty); // No new text, as we're showing the entire log
                    }));
                });
            }
        }

        private void ViewLogButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the current row
            Button button = sender as Button;
            CombinedVideoInfo combinedVideoInfo = button.DataContext as CombinedVideoInfo;
            string uniqueID = combinedVideoInfo.DownloadStatus.UniqueID;

            // Show the log window
            ShowLogWindow(uniqueID);
        }


        public class CombinedVideoInfo : INotifyPropertyChanged
        {
            public string UniqueID { get; set; }
            private VideoDetails videoDetails;
            private WebServerDownloadStatus downloadStatus;

            public VideoDetails VideoDetails
            {
                get { return videoDetails; }
                set { videoDetails = value; OnPropertyChanged(nameof(VideoDetails)); }
            }
            public WebServerDownloadStatus DownloadStatus
            {
                get { return downloadStatus; }
                set { downloadStatus = value; OnPropertyChanged(nameof(DownloadStatus)); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class VideoDetails : INotifyPropertyChanged
        {
            public string OriginalFilename { get; set; }
            public string UniqueID { get; set; }
            private string title;
            private string displayURL;
            private string downloadURL;

            public string Title
            {
                get { return title; }
                set { title = value; OnPropertyChanged(nameof(Title)); }
            }
            public string DisplayURL
            {
                get { return displayURL; }
                set { displayURL = value; OnPropertyChanged(nameof(DisplayURL)); }
            }
            public string DownloadURL
            {
                get { return downloadURL; }
                set { downloadURL = value; OnPropertyChanged(nameof(DownloadURL)); }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private List<WebServerDownloadStatus> downloadStatusList = new List<WebServerDownloadStatus>();
        private async void RunProcessAsync(string filename, string arguments, string uniqueID)
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

                WebServerDownloadStatus currentDownload = null;
                decimal lastPercentageUpdate = 0;

                object lockObject = new object();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        if (!logBuilders.ContainsKey(uniqueID))
                        {
                            logBuilders[uniqueID] = new StringBuilder();
                            logLines[uniqueID] = new List<string>();
                        }
                        logBuilders[uniqueID].AppendLine(e.Data);
                        logLines[uniqueID].Add(e.Data);

                        var filenameMatch = Regex.Match(e.Data, @"\[download\] Destination: (?<filename>.*)$");
                        if (filenameMatch.Success)
                        {
                            lock (lockObject)
                            {
                                this.Dispatcher.Invoke(() =>
                                {
                                    currentDownload = new WebServerDownloadStatus
                                    {
                                        UniqueID = uniqueID, // Set the UniqueID
                                        Filename = Path.GetFileName(filenameMatch.Groups["filename"].Value),
                                        Status = "Starting",
                                        StartTime = DateTime.Now
                                    };
                                    Debug.WriteLine($"New Filename: {currentDownload.Filename}, Unique ID: {uniqueID}");
                                    DownloadStatusItems.Add(currentDownload); // Add to the DownloadStatusItems collection


                                });
                            }
                        }

                        var downloadMatch = Regex.Match(e.Data, @"\[download\]\s*(?<percentage>.*?)% of ~(?<totalSize>.*?) at (?<downloadSpeed>.*?) ETA (?<eta>.*?) \(frag (?<currentFragment>\d+)/(?<totalFragments>\d+)\)");
                        if (downloadMatch.Success && currentDownload != null)
                        {
                            lock (lockObject)
                            {
                                string newETA = downloadMatch.Groups["eta"].Value;
                                if (currentDownload.MaxETA == null || string.CompareOrdinal(newETA, currentDownload.MaxETA) > 0)
                                {
                                    currentDownload.MaxETA = newETA;
                                }

                                if (decimal.TryParse(downloadMatch.Groups["percentage"].Value, out decimal percentage))
                                {
                                    if (Math.Abs(percentage - lastPercentageUpdate) > 1)
                                    {
                                        this.Dispatcher.Invoke(() =>
                                        {
                                            currentDownload.PercentageDownloaded = $"{Math.Floor(percentage)}%";
                                            currentDownload.TotalSize = downloadMatch.Groups["totalSize"].Value;
                                            currentDownload.DownloadSpeed = downloadMatch.Groups["downloadSpeed"].Value;
                                            currentDownload.CurrentFragment = downloadMatch.Groups["currentFragment"].Value;
                                            currentDownload.TotalFragments = downloadMatch.Groups["totalFragments"].Value;
                                            currentDownload.Status = "Downloading";
                                            // Notify the property change for the specific item
                                            var combinedVideoInfo = CombinedVideoItems.FirstOrDefault(item => item.DownloadStatus.UniqueID == currentDownload.UniqueID);
                                            if (combinedVideoInfo != null)
                                            {
                                                combinedVideoInfo.DownloadStatus = currentDownload; // This will trigger the PropertyChanged event
                                            }
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
                    if (!logBuilders.ContainsKey(uniqueID))
                    {
                        logBuilders[uniqueID] = new StringBuilder();
                        logLines[uniqueID] = new List<string>();
                    }
                    logBuilders[uniqueID].AppendLine(e.Data);
                    logLines[uniqueID].Add(e.Data);
                    lock (lockObject)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            if (currentDownload != null)
                            {
                                currentDownload.Status = "Error";
                                currentDownload.DownloadSpeed = e.Data;

                            }
                        });
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

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
                            // Stop the timer for this unique ID
                            //StopTimer(uniqueID);
                            // Cleanup temp folder
                            CleanupTempFolder(Path.GetFileNameWithoutExtension(currentDownload.Filename)); // Use filename without extension
                        });
                    }
                }
            });

        }

        //public void StopTimer(string uniqueID)
        //{
        //    if (logWindows.TryGetValue(uniqueID, out var logWindow))
        //    {
        //        logRefreshTimer.Stop();
        //    }
        //}


        private void CleanupTempFolder(string selectedTitle)
        {
            string tempPath = System.IO.Path.GetFullPath("./Downloads");
            string folderToDelete = Path.Combine(tempPath, selectedTitle);

            if (Directory.Exists(folderToDelete))
            {
                try
                {
                    Directory.Delete(folderToDelete, true); // Deletes the folder and its contents
                }
                catch (Exception ex)
                {
                    // Handle any exceptions that may occur during deletion
                    Debug.WriteLine($"Failed to delete folder {folderToDelete}: {ex.Message}");
                }
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            // Find all completed downloads
            var completedDownloads = CombinedVideoItems.Where(item => item.DownloadStatus.Status == "Completed").ToList();

            // Remove completed downloads
            foreach (var completedDownload in completedDownloads)
            {
                CombinedVideoItems.Remove(completedDownload);
            }
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            // Get selected items from the data grid
            var selectedItems = dataGrid.SelectedItems.OfType<CombinedVideoInfo>().ToList();

            // Start downloading each selected item
            foreach (var item in selectedItems)
            {
                // Use DownloadURL from the VideoDetails property
                StartDownload(item.VideoDetails.Title, item.VideoDetails.DownloadURL);
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (webServer == null) return;

            var videoInfoList = webServer.GetVideoInfoList();
            foreach (var json in videoInfoList)
            {
                dynamic videoInfo = JsonConvert.DeserializeObject(json);
                string[] urlsArray = videoInfo.URLs.ToObject<List<string>>().ToArray();
                string[] titlesArray = videoInfo.Title.ToObject<List<string>>().ToArray();

                for (int i = 0; i < urlsArray.Length; i++)
                {
                    int titleIndex = i % titlesArray.Length;
                    string title = titlesArray[titleIndex];
                    string url = urlsArray[i];
                    string uniqueID = GenerateUniqueID(title, url);

                    // Truncate the URL to show only the beginning and the last part up to the file extension
                    string lastPart = url.Split('/').Last();
                    int extensionIndex = lastPart.LastIndexOf('.');
                    if (extensionIndex >= 0 && extensionIndex + 5 <= lastPart.Length)
                    {
                        lastPart = lastPart.Substring(0, extensionIndex + 5); // Assuming file extension is 3 characters
                    }
                    else
                    {
                        // In case the condition is not met, keep the lastPart as it is
                        // No need to do anything here; the program will continue to run
                    }

                    string truncatedUrl = url.Length > 30 ? url.Substring(0, 18) + "..." + lastPart : url;

                    var combinedInfo = new CombinedVideoInfo
                    {
                        VideoDetails = new VideoDetails { UniqueID = uniqueID, Title = title, OriginalFilename = title, DisplayURL = truncatedUrl, DownloadURL = url },
                        DownloadStatus = new WebServerDownloadStatus { UniqueID = uniqueID } // Initialize with default values
                    };

                    if (!addedItems.Contains(uniqueID))
                    {
                        CombinedVideoItems.Add(combinedInfo); // Add to CombinedVideoItems
                        addedItems.Add(uniqueID);
                    }
                }
            }
            
            foreach (var combinedInfo in CombinedVideoItems)
            {
                // Find the corresponding DownloadStatus for this VideoDetails
                var downloadStatus = DownloadStatusItems.FirstOrDefault(ds => ds.UniqueID == combinedInfo.VideoDetails.UniqueID);
                if (downloadStatus != null)
                {
                    combinedInfo.DownloadStatus = downloadStatus; // Update the DownloadStatus in the CombinedVideoItems
                }
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            webServer.Start();
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            webServer.Stop();
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
        }
    }

    public class VideoItem
    {
        public VideoDetails VideoDetails { get; set; }
        public DownloadStatus DownloadStatus { get; set; }
    }

    public class VideoInfo
    {
        public string Title { get; set; }
        public string DisplayURL { get; set; }
        public string DownloadURL { get; set; }
        public string Filename { get; set; }
        public string PercentageDownloaded { get; set; }
        public string TotalSize { get; set; }
        public string DownloadSpeed { get; set; }
        public string CurrentFragment { get; set; }
        public string TotalFragments { get; set; }
        public string Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string MaxETA { get; set; }
    }

    public class WebServer
    {
        private HttpListener listener;
        private Thread serverThread;
        private bool isRunning;
        private List<string> videoInfoList;

        public WebServer()
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://*:8765/");
            isRunning = false;
            videoInfoList = new List<string>();
        }

        public void Start()
        {
            if (isRunning)
            {
                return;
            }

            listener.Start();
            isRunning = true;

            serverThread = new Thread(HandleRequests);
            serverThread.Start();
        }

        public void Stop()
        {
            if (!isRunning)
            {
                return;
            }

            isRunning = false;
            listener.Stop();

            if (serverThread != null && serverThread.IsAlive)
            {
                serverThread.Join();
            }

            listener.Close();
        }

        private void HandleRequests()
        {
            while (isRunning)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    ThreadPool.QueueUserWorkItem((_) => { ProcessRequest(context); });
                }
                catch (HttpListenerException)
                {
                    // Exception thrown when listener is stopped. It's safe to ignore.
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            // Read the request body
            string json;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                json = reader.ReadToEnd();
            }

            // Add the raw JSON to the video info list
            videoInfoList.Add(json);

            // Send a response
            string responseString = "Received JSON: " + json;
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        public List<string> GetVideoInfoList()
        {
            // Return a copy of the video info list and clear the original list
            List<string> copy = new List<string>(videoInfoList);
            videoInfoList.Clear();
            return copy;
        }
    }
}