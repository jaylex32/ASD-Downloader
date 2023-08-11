using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace new_ASD_Downloader
{
    /// <summary>
    /// Interaction logic for Logs.xaml
    /// </summary>
    public partial class Logs : Window
    {
        public Logs()
        {
            InitializeComponent();
        }

        public void AppendLog(string entireLog, string newText)
        {
            LogTextBox.Text = entireLog;
            LogTextBox.AppendText(newText);
            LogTextBox.ScrollToEnd();
        }
    
    }
}
