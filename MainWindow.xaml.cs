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
            if (sourceTextBox.Text != "" && dialog.SelectedPath == "")
            {
                return;
            }
            sourceTextBox.Text = dialog.SelectedPath;
        }

        private void targetPicker_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog();
            dialog.ShowDialog();
            if (targetTextBox.Text != "" && dialog.SelectedPath == "")
            {
                return;
            }
            targetTextBox.Text = dialog.SelectedPath;
        }

        private void organizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (formIsValid())
            {
                organizeButton.IsEnabled = false;

                string sourceDirectory = sourceTextBox.Text;
                string targetDirectory = targetTextBox.Text;
                double progress = 0.0;
                Boolean deleteEmptyDirectories = deleteEmptyFolders.IsEnabled;

                staThread = new Thread(r =>
                {
                    List<string> paths = getPaths(sourceDirectory);
                    List<string> movedFiles = new List<string>();
                    List<string> unmovedFiles = new List<string>();

                    if (deleteEmptyDirectories)
                    {
                        removeEmptyFolders(targetDirectory);
                    }

                    foreach (string path in paths)
                    {
                        if (isCancelled)
                        {
                            break;
                        }

                        List<DateTime> dates = getDates(path);

                        if (dates.Count() > 0)
                        {
                            DateTime dateTimeTaken = dates[0];
                            string newPath = getNewPath(path, dateTimeTaken, targetDirectory);
                            string altPath = getNewPath(path, dateTimeTaken, sourceDirectory);
                            try
                            {
                                moveFile(path, newPath, altPath);
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

                        progress = progress + (100.0 / paths.Count);
                        setProgress(progress);
                    }

                    setProgress(0);
                    enableOrganizeButton();
                });

                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start(organizeButton);
            }
        }

        private bool formIsValid()
        {
            if (sourceTextBox.Text != "" && targetTextBox.Text != ""
                && System.IO.Directory.Exists(sourceTextBox.Text) && System.IO.Directory.Exists(targetTextBox.Text))
            {
                sourceError.Text = "";
                targetError.Text = "";
                return true;
            }

            if (sourceTextBox.Text == "" || !System.IO.Directory.Exists(sourceTextBox.Text))
            {
                sourceError.Text = "Enter valid source folder";
            }
            else
            {
                sourceError.Text = "";
            }

            if (targetTextBox.Text == "" || !System.IO.Directory.Exists(targetTextBox.Text))
            {
                targetError.Text = "Enter valid target folder.";
            }
            else
            {
                targetError.Text = "";
            }

            return false;
        }

        private List<string> getPaths(string source)
        {
            SearchOption searchOption = SearchOption.TopDirectoryOnly;

            this.Dispatcher.Invoke(() =>
            {
                if (includeSubfolders.IsChecked == true)
                {
                    searchOption = SearchOption.AllDirectories;
                }
            });


            var paths = Directory.EnumerateFiles(source, "*.*", searchOption)
                .Where(file => extensions.Contains(System.IO.Path.GetExtension(file).ToLower())).ToList();
            return paths;
        }


        private void removeEmptyFolders(string targetDirectory)
        {
            foreach (string path in Directory.EnumerateDirectories(targetDirectory, "*", SearchOption.AllDirectories).ToList())
            {
                if (!Directory.EnumerateFiles(path).Any() && !Directory.EnumerateDirectories(path).Any())
                {
                    Directory.Delete(path);
                    Console.WriteLine("Deleted " + path);
                }
            }

        }

        private List<DateTime> getDates(string path)
        {
            List<DateTime> dates = new List<DateTime>();

            Shell32.Shell shell = new Shell32.Shell();
            Shell32.Folder folder = shell.NameSpace(System.IO.Path.GetDirectoryName(path));
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

        private void moveFile(string oldPath, string newPath, string altPath)
        {
            if (File.Exists(newPath))
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(altPath));
                File.Move(oldPath, altPath);
                return;
            }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(newPath));
            File.Move(oldPath, newPath);
        }

        public void enableOrganizeButton()
        {
            this.Dispatcher.Invoke(() => organizeButton.IsEnabled = true);
        }

        public void setProgress(double value)
        {
            this.Dispatcher.Invoke(() => progress.Value = value);
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (isCancelled == false)
            {
                isCancelled = true;
                if (staThread != null)
                {
                    staThread.Abort();
                }
                organizeButton.IsEnabled = true;
                isCancelled = false;
            }
        }
    }
}
