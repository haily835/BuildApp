using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack.Dialogs;
using Ionic.Zip;
using Ionic.Zlib;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BuildApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Read entire text file content in one string
            string settingFile = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "settingApp.txt");

            try
            {
                string text = File.ReadAllText(settingFile);
                settings = JsonSerializer.Deserialize<Dictionary<string, string>>(text);
                debugPathTb.Text = settings["debugPath"];
                destPathTb.Text = settings["destPath"];
                fileNameTb.Text = settings["fileName"];
                GetContent(debugPathTb.Text);
            } catch(Exception e)
            {
                settings = new Dictionary<string, string>();
            }
        }


        public List<string> fileTypes { get; set; }
        public List<string> excludedfileTypes { get; set; }

        public List<string> subfolders { get; set; }
        public List<string> excludedSubfolders { get; set; }

        public Dictionary<string, string> settings;

        #region Events

        private void destPathBtn_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = "C:\\Users";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                destPathTb.Text = dialog.FileName;
            }


        }

        private void debugPathBtn_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = "C:\\Users";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                debugPathTb.Text = dialog.FileName;
                GetContent(dialog.FileName);
            }
        }

        public void GetContent(string fileName)
        {
            var dir = new DirectoryInfo(fileName);

            fileTypes = new List<string>();
            excludedfileTypes = new List<string>();
            subfolders = new List<string>();
            excludedSubfolders = new List<string>();
            try
            {
                foreach (var file in dir.GetFiles())
                {
                    if (!fileTypes.Contains(file.Extension))
                    {
                        fileTypes.Add(file.Extension);
                    }
                }


                foreach (var folder in dir.GetDirectories())
                {
                    subfolders.Add(folder.Name);
                }

                typeList.ItemsSource = fileTypes;
                folderList.ItemsSource = subfolders;

            } catch(Exception e)
            {
                MessageBox.Show("Check your path to debug folder!");
            }

            this.DataContext = this;
        }

        private void typeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            string fileExtension = (string)((CheckBox)sender).Tag;
            excludedfileTypes.Add(fileExtension);
        }

        private void typeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            string fileExtension = (string)((CheckBox)sender).Tag;
            excludedfileTypes.Remove(fileExtension);
        }

        private void folderCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            string folderName = (string)((CheckBox)sender).Tag;
            excludedSubfolders.Add(folderName);
        }

        private void folderCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            string folderName = (string)((CheckBox)sender).Tag;
            excludedSubfolders.Remove(folderName);
        }


        private void BuildBtn_Click(object sender, RoutedEventArgs e)
        {

            CopyAndRemove(debugPathTb.Text, destPathTb.Text);
            string tempPath = System.IO.Path.Combine(destPathTb.Text, "temp");
            string fileName = fileNameTb.Text + "_" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-ffffff") +".zip";
            string finalPath = System.IO.Path.Combine(destPathTb.Text, fileName);
            Zip(tempPath, finalPath);
            Directory.Delete(tempPath, true);
            MessageBox.Show("Successful "+ finalPath);
        }

        private async void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            settings = new Dictionary<string, string>();
            try
            {
                settings.Add("debugPath", debugPathTb.Text);
                settings.Add("destPath", destPathTb.Text);
                settings.Add("fileName", fileNameTb.Text);
                string jsonString = JsonSerializer.Serialize(settings);
                await WriteFile(jsonString);
            } catch(Exception ex)
            {
                MessageBox.Show("Some error when saving your settings!");
            }
            
        }
        #endregion

        #region File and directory

        private void Zip(string source, string destination)
        {
            using (ZipFile zip = new ZipFile
            {
                CompressionLevel = CompressionLevel.BestCompression
            })
            {
                var files = Directory.GetFiles(source, "*",
                    SearchOption.AllDirectories).
                    Where(f => System.IO.Path.GetExtension(f).
                        ToLowerInvariant() != ".zip").ToArray();

                foreach (var f in files)
                {
                    zip.AddFile(f, GetCleanFolderName(source, f));
                }

                var destinationFilename = destination;

                if (Directory.Exists(destination) && !destination.EndsWith(".zip"))
                {
                    destinationFilename += $"\\{new DirectoryInfo(source).Name}-{DateTime.Now:yyyy-MM-dd-HH-mm-ss-ffffff}.zip";
                }
                
                zip.Save(destinationFilename);
            }

        }

        private string GetCleanFolderName(string source, string filepath)
        {
            if (string.IsNullOrWhiteSpace(filepath))
            {
                return string.Empty;
            }

            var result = filepath.Substring(source.Length);

            if (result.StartsWith("\\"))
            {
                result = result.Substring(1);
            }

            result = result.Substring(0, result.Length - new FileInfo(filepath).Name.Length);

            return result;
        }

        private void CopyAndRemove (string debugDir, string destDir)
        {
            // create a temporary folder store the copy
            string tempPath = System.IO.Path.Combine(destDir, "temp");
            DirectoryCopy(debugDir, tempPath, true);

            
            DirectoryInfo dir = new DirectoryInfo(tempPath);

            // delete unwanted folder
            foreach (var f in excludedSubfolders)
            {
                string f_path = System.IO.Path.Combine(tempPath, f);
                Directory.Delete(f_path, true);
            }

            // delete all unwanted files

            foreach(var t in excludedfileTypes)
            {
                dir.EnumerateFiles("*" + t).ToList().ForEach(f => f.Delete());
            }
        }

        // source: https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
        private void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the destination directory doesn't exist, create it.       
            Directory.CreateDirectory(destDirName);

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = System.IO.Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = System.IO.Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }

        public static async Task WriteFile(string jsonString)
        {
            string settingFile = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "settingApp.txt");
            await File.WriteAllTextAsync(settingFile, jsonString);
            MessageBox.Show(settingFile);
        }

        #endregion

    }
}
