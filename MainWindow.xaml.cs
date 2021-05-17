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

        /** Indexes for Date Taken, Date Acquired, Media Created. */
        private static readonly int[] propIndexes = new int[] { 12, 136, 208 };

        Thread staThread;

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
                string targetDirectory = targetTextBox.Text;

                Action enableButton = () => organizeButton.IsEnabled = true;

                staThread = new Thread(r =>
                {
                    List<string> paths = getPaths(sourceDirectory);
                    List<string> movedFiles = new List<string>();
                    List<string> unmovedFiles = new List<string>();

                    foreach (string path in paths)
                    {
                        if (isCancelled)
                        {
                            break;
                        }

                        List<DateTime> dates = getDates(path, sourceDirectory);

                        if (dates.Count() > 0)
                        {
                            DateTime dateTimeTaken = dates[0];
                            string newPath = getNewPath(path, dateTimeTaken, targetDirectory);

                            try
                            {
                                moveFile(path, newPath);
                                Console.WriteLine("Moved " + path + " to " + newPath);
                            }
                            catch
                            {
                                unmovedFiles.Add(path);
                            }
                        }
                        else
                        {
                            unmovedFiles.Add(path);
                        }
                    }
                    enableOrganizeButton();
                });
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start(organizeButton);
            }
        }

        private bool formIsValid()
        {
            return sourceTextBox.Text != "" && targetTextBox.Text != "" && sourceTextBox.Text != targetTextBox.Text;
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

        private string getNewPath(string path, DateTime dt, string targetDirectory)
        {
            string month = "" + dt.Month;
            if (month.Length == 1)
            {
                month = "0" + month;
            }
            return System.IO.Path.Combine(targetDirectory, dt.Year + "\\" + month + "\\" + System.IO.Path.GetFileName(path));
        }

        private void moveFile(string oldPath, string newPath)
        {
            if (File.Exists(newPath))
            {
                throw new IOException("File already exists");
            }

            File.Move(oldPath, newPath);
        }

        public void enableOrganizeButton()
        {
            this.Dispatcher.Invoke(() => organizeButton.IsEnabled = true);
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (isCancelled == false)
            {
                isCancelled = true;
                staThread.Abort();
                organizeButton.IsEnabled = true;
                isCancelled = false;
            }
        }
    }
}
