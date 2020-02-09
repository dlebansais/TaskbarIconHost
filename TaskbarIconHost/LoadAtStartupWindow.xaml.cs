using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace TaskbarIconHost
{
    public partial class LoadAtStartupWindow : Window
    {
        #region Init
        public LoadAtStartupWindow(bool requireElevated, string appName)
        {
            InitializeComponent();
            DataContext = this;

            RequireElevated = requireElevated;
            Title = appName;

            try
            {
                string TaskSelectionTextFormat = (string)FindResource("TaskSelectionTextFormat");
                TaskSelectionText = string.Format(CultureInfo.CurrentCulture, TaskSelectionTextFormat, appName);

                // Create a script in the plugin folder. This script can be imported to create a new task.
                string ApplicationFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);

                if (!Directory.Exists(ApplicationFolder))
                    Directory.CreateDirectory(ApplicationFolder);

                TaskFile = Path.Combine(ApplicationFolder, appName + ".xml");

                if (!File.Exists(TaskFile))
                    CreateTaskFile();
            }
            catch (Exception e)
            {
                TaskSelectionText = string.Empty;
                MessageBox.Show(e.Message, string.Empty, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateTaskFile()
        {
            Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();

            // The TaskbarIconHost.xml file must be added to the project has an "Embedded Resource".
            foreach (string ResourceName in ExecutingAssembly.GetManifestResourceNames())
                if (ResourceName.EndsWith("TaskbarIconHost.xml", StringComparison.InvariantCulture))
                {
                    using (Stream rs = ExecutingAssembly.GetManifestResourceStream(ResourceName))
                    {
                        using (FileStream fs = new FileStream(TaskFile, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            using (StreamReader sr = new StreamReader(rs))
                            {
                                using (StreamWriter sw = new StreamWriter(fs))
                                {
                                    string Content = sr.ReadToEnd();

                                    // Use the complete path to the plugin.
                                    Content = Content.Replace("%PATH%", ExecutingAssembly.Location);
                                    sw.WriteLine(Content);
                                }
                            }
                        }
                    }

                    break;
                }
        }

        public bool RequireElevated { get; }
        public string TaskSelectionText { get; }

        private string TaskFile = string.Empty;
        #endregion

        #region Events
        private void OnLaunch(object sender, ExecutedRoutedEventArgs e)
        {
            // Launch the Windows Task Scheduler.
            using Process ControlProcess = new Process();
            ControlProcess.StartInfo.FileName = "control.exe";
            ControlProcess.StartInfo.Arguments = "schedtasks";
            ControlProcess.StartInfo.UseShellExecute = true;

            ControlProcess.Start();
        }

        private void OnCopy(object sender, ExecutedRoutedEventArgs e)
        {
            // Copy to the clipboard the full path to the script to import.
            Clipboard.SetText(TaskFile);
        }

        private void OnClose(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }
        #endregion
    }
}
