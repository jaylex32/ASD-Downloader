using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Windows.Media;
using System.Windows.Forms;
using System.Windows.Documents;
using System.Windows.Input;
using Athena_Stream_Downloader;

namespace new_ASD_Downloader
{
    public partial class SettingsPage : Window
    {
        public event Action ConfigUpdated;
        private Dictionary<string, string> _config;
        private string _configPath = "./bin/config.conf";

        public SettingsPage()
        {
            InitializeComponent();
            LoadConfig();
            InitializeUI();

            // Define the parameters
            string[] parameters = new string[]
            {
        "%(id)s", "%(title)s", "%(url)s", "%(ext)s", "%(alt_title)s", "%(display_id)s", "%(uploader)s",
        "%(license)s", "%(creator)s", "%(release_date)s", "%(timestamp)s", "%(upload_date)s",
        "%(uploader_id)s", "%(channel)s", "%(channel_id)s", "%(location)s", "%(duration)s",
        "%(view_count)s", "%(like_count)s", "%(dislike_count)s", "%(playlist)s",
        "%(playlist_index)s", "%(playlist_id)s", "%(playlist_title)s", "%(playlist_uploader)s",
        "%(chapter)s", "%(chapter_number)s", "%(chapter_id)s", "%(series)s", "%(season)s",
        "%(season_number)s", "%(season_id)s", "%(episode)s", "%(episode_number)s",
        "%(episode_id)s", "%(track)s", "%(track_number)s", "%(track_id)s", "%(artist)s", "%(genre)s", "%(album)s", "%(album_type)s", "%(release_year)s", "%(album_artist)s", "%(disc_number)s"
            };

            int columnCount = 0;
            StackPanel currentStackPanel = null;
            foreach (var param in parameters)
            {
                var button = new System.Windows.Controls.Button
                {
                    Content = new System.Windows.Controls.Border
                    {
                        Child = new System.Windows.Controls.TextBlock { Text = param + " " },
                        CornerRadius = new System.Windows.CornerRadius(10),
                        Padding = new System.Windows.Thickness(5, 2, 5, 2),
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Transparent),
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray),
                        BorderThickness = new System.Windows.Thickness(1)
                    },
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = param,
                    Margin = new System.Windows.Thickness(5)
                };
                button.Click += Parameter_Click;
                button.Style = (Style)FindResource("TagButtonStyle");
                ParametersWrapPanel.Children.Add(button);
            }
        }

        private void Parameter_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button button = sender as System.Windows.Controls.Button;
            if (button != null && button.Tag is string parameter)
            {
                File_Format_Selected.Text += parameter + " "; // Add the parameter to the TextBox with a space
            }
        }


        private void LoadConfig()
        {
            if (File.Exists(_configPath))
            {
                _config = File.ReadLines(_configPath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))  // Ignore empty lines
                    .Select(line => line.Split('='))
                    .Where(parts => parts.Length == 2)  // Only consider lines with one '=' character
                    .ToDictionary(parts => parts[0], parts => parts[1]);
            }
            else
            {
                _config = new Dictionary<string, string>();
            }
        }

        private void InitializeUI()
        {
            if (_config.ContainsKey("Homepath")) Home_Path.Text = _config["Homepath"];
            if (_config.ContainsKey("moviepath")) MoviePath.Text = _config["moviepath"];
            if (_config.ContainsKey("tvshowpath")) TvShow_Path.Text = _config["tvshowpath"];
            if (_config.ContainsKey("Template")) File_Format_Selected.Text = _config["Template"];

            if (_config.ContainsKey("Cookies"))
            {
                switch (_config["Cookies"])
                {
                    case "Chrome":
                        Chrome.IsChecked = true;
                        break;
                    case "Edge":
                        EdgeBrowser.IsChecked = true;
                        break;
                    case "Firefox":
                        Firefox.IsChecked = true;
                        break;
                    default:
                        break;
                }
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;
                    System.Windows.Controls.TextBox textBoxToUpdate = null;

                    if (sender == HomePath_Button) textBoxToUpdate = Home_Path;
                    else if (sender == MoviePath_Button) textBoxToUpdate = MoviePath;
                    else if (sender == TvShowPath_Button) textBoxToUpdate = TvShow_Path;

                    if (textBoxToUpdate != null)
                    {
                        textBoxToUpdate.Text = selectedPath;
                        SaveToConfig(textBoxToUpdate.Name, selectedPath);
                    }
                }
            }
        }

        private void Format_SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var templateMap = new Dictionary<string, string>
    {
        {"TV_Series_Template", "%(series)s\\Season %(season_number)s\\S%(season_number)sE%(episode_number)s - %(title)s.%(ext)s"},
        {"Movies_Template", "%(title)s %(release_date)s\\%(title)s %(release_date)s.%(ext)s"},
        {"Default_Template", "%(title)s\\%(title)s.%(ext)s"},
        {"Music_Template", "%(album)s\\%(album)s - %(title)s.%(ext)s"},
        {"Chapters_Template", "chapter:%(section_title)s.%(ext)s"}
    };

            if (TV_Series_Template.IsChecked == true)
            {
                File_Format_Selected.Text = templateMap["TV_Series_Template"];
            }
            else if (Movies_Template.IsChecked == true)
            {
                File_Format_Selected.Text = templateMap["Movies_Template"];
            }
            else if (Default_Template.IsChecked == true)
            {
                File_Format_Selected.Text = templateMap["Default_Template"];
            }
            else if (Music_Template.IsChecked == true)
            {
                File_Format_Selected.Text = templateMap["Music_Template"];
            }
            else if (Chapters_Template.IsChecked == true)
            {
                File_Format_Selected.Text = templateMap["Chapters_Template"];
            }

            SaveToConfig("File_Format_Selected", File_Format_Selected.Text);

            string selectedCookiesOption = Chrome.IsChecked == true ? "Chrome" :
                                           EdgeBrowser.IsChecked == true ? "Edge" :
                                           Firefox.IsChecked == true ? "Firefox" : "";

            if (!string.IsNullOrEmpty(selectedCookiesOption))
            {
                SaveToConfig("Cookies", selectedCookiesOption);
            }

            // Notify that settings have been saved
            ConfigUpdated?.Invoke();

            // Manually call LoadSettings in MainWindow
            MainWindow.Instance.LoadSettings();
        }


        private void SaveToConfig(string settingName, string newValue)
        {
            var propertyMap = new Dictionary<string, string>
{
    {"Home_Path", "Homepath"},
    {"MoviePath", "moviepath"},
    {"TvShow_Path", "tvshowpath"},
    {"File_Format_Selected", "Template"},
    {"Cookies", "Cookies"}
};


            if (propertyMap.ContainsKey(settingName))
            {
                string propertyName = propertyMap[settingName];
                var lines = File.ReadAllLines("./bin/config.conf").ToList();
                bool found = false;

                for (var i = 0; i < lines.Count; i++)
                {
                    if (lines[i].StartsWith(propertyName + "="))
                    {
                        lines[i] = $"{propertyName}={newValue}";
                        found = true;
                        break;
                    }
                }

                // If the property was not found, add it
                if (!found)
                {
                    lines.Add($"{propertyName}={newValue}");
                }

                File.WriteAllLines("./bin/config.conf", lines);
            }
        }
    }
}