﻿namespace TaskbarIconHost
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Windows;
    using System.Windows.Input;

    /// <summary>
    /// Represents an interface with instructions on how to manually enable loading as administrator.
    /// </summary>
    public partial class LoadAtStartupWindow : Window
    {
        #region Init
        /// <summary>
        /// Initializes a new instance of the <see cref="LoadAtStartupWindow"/> class.
        /// </summary>
        /// <param name="requireElevated">True if the application must be run as administrator.</param>
        /// <param name="appName">The application name.</param>
        public LoadAtStartupWindow(bool requireElevated, string appName)
        {
            InitializeComponent();
            DataContext = this;

            RequireElevated = requireElevated;
            Title = appName;
            TaskSelectiontext = $"Select the task called '{appName}.xml' (this is a simple text file that you can inspect).";

            try
            {
                // Create a script in the plugin folder. This script can be imported to create a new task.
                string ApplicationFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);

                if (!Directory.Exists(ApplicationFolder))
                    Directory.CreateDirectory(ApplicationFolder);

                TaskFile = Path.Combine(ApplicationFolder, appName + ".xml");

                if (!File.Exists(TaskFile))
                {
                    Assembly ExecutingAssembly = Assembly.GetEntryAssembly();

                    // The TaskbarIconHost.xml file must be added to the project has an "Embedded Reource".
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
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the application requires being run as administrator.
        /// </summary>
        public bool RequireElevated { get; private set; }

        /// <summary>
        /// Gets the text with task selection.
        /// </summary>
        public string TaskSelectiontext { get; private set; }

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