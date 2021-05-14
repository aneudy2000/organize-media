using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Windows.Forms;
using System.Threading;

namespace organize_media
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Boolean isCancelled = false;

        private static readonly string[] extensions = new string[] { ".png", ".jpeg", ".gif", ".jpg", ".psd", ".bmp", ".mp4", ".mov", ".avi" };

        /** Indexes for Date Taken, Date Acquired, Date Modified, Date Created, Media Created. */
        private static readonly int[] propIndexes = new int[] { 12, 136, 3, 4, 208 };

        public MainWindow()
        {
            InitializeComponent();
        }

        private void sourcePicker_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            dialog.ShowDialog();
            sourceTextBox.Text = dialog.SelectedPath;
        }

        private void targetPicker_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            dialog.ShowDialog();
            targetTextBox.Text = dialog.SelectedPath;
        }

        private void organizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (formIsValid())
            {
                organizeButton.IsEnabled = false;
                string sourceDirectory = sourceTextBox.Text;
                Thread staThread = new Thread(r =>
                {
                    List<string> paths = getPaths(sourceDirectory);
                    foreach (string path in paths)
                    {
                        if (isCancelled)
                        {
                            Console.WriteLine("Cancelled");
                            break;
                        }
                        List<DateTime> dates = getDates(path, sourceDirectory);
                        DateTime dateTimeTaken = dates[0];
                        Console.WriteLine(">>> " + dateTimeTaken);
                    }
                });
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
            }
            organizeButton.IsEnabled = true;
            isCancelled = false;
        }

        private bool formIsValid()
        {
            return sourceTextBox.Text != "" && targetTextBox.Text == "";
        }

        private List<string> getPaths(string source)
        {

            var paths = Directory.EnumerateFiles(source, "*.*", SearchOption.TopDirectoryOnly)
                .Where(file => extensions.Contains(System.IO.Path.GetExtension(file).ToLower())).ToList();
            return paths;
        }

        private List<DateTime> getDates(string path, string source)
        {
            List<DateTime> dates = new List<DateTime>();

            Shell32.Shell shell = new Shell32.Shell();
            Shell32.Folder folder = shell.NameSpace(source);
            Shell32.FolderItem item = folder.ParseName(System.IO.Path.GetFileName(path));

            for (int i = 0; i < propIndexes.Length; i++)
            {
                string date = System.Text.RegularExpressions.Regex.Replace(folder.GetDetailsOf(item, propIndexes[i]), @"[^\w\s\/:-]", "",
                    System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace);

                if (!String.IsNullOrEmpty(date))
                {
                    dates.Add(Convert.ToDateTime(date));
                }
            }
            return sort(dates);
        }

        private List<DateTime> sort(List<DateTime> dates) => dates.OrderBy(d => d.Ticks).ToList();

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            isCancelled = true;
        }
    }
}
