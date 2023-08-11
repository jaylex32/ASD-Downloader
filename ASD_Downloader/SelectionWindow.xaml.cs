using System;
using System.Collections.Generic;
using System.Windows;

namespace new_ASD_Downloader
{
    /// <summary>
    /// Interaction logic for SelectionWindow.xaml
    /// </summary>
    public partial class SelectionWindow : Window
    {
        public string SelectedOption { get; private set; }

        public SelectionWindow(List<string> options)
        {
            InitializeComponent();
            OptionsListBox.ItemsSource = options;
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedOption = OptionsListBox.SelectedItem?.ToString();
            DialogResult = true;
            Close();
        }
    }
}