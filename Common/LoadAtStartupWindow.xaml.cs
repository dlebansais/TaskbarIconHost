namespace TaskbarIconHost;

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
#if !NETFRAMEWORK
using System.Text;
#endif
using System.Windows;
using System.Windows.Input;
using Contracts;
using ResourceTools;
using static TaskbarIconHost.Properties.Resources;

/// <summary>
/// Represents an interface with instructions on how to manually enable loading as administrator.
/// </summary>
internal partial class LoadAtStartupWindow : Window
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

        try
        {
#if NETFRAMEWORK
            TaskSelectionText = string.Format(CultureInfo.CurrentCulture, TaskSelectionTextFormat, appName);
#else
            CompositeFormat TaskSelectionTextCompositeFormat = CompositeFormat.Parse(TaskSelectionTextFormat);
            TaskSelectionText = string.Format(CultureInfo.CurrentCulture, TaskSelectionTextCompositeFormat, appName);
#endif

            // Create a script in the plugin folder. This script can be imported to create a new task.
            string ApplicationFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appName);

            if (!Directory.Exists(ApplicationFolder))
                _ = Directory.CreateDirectory(ApplicationFolder);

            TaskFile = Path.Combine(ApplicationFolder, appName + ".xml");

            if (!File.Exists(TaskFile))
                CreateTaskFile();
        }
        catch (Exception e)
        {
            TaskSelectionText = string.Empty;
            _ = MessageBox.Show(e.Message, string.Empty, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateTaskFile()
    {
        Contract.RequireNotNull(Assembly.GetEntryAssembly(), out Assembly ExecutingAssembly);

        // The TaskbarIconHost.xml file must be added to the project has an "Embedded Reource".
        if (ResourceLoader.LoadStream("TaskbarIconHost.xml", string.Empty, out Stream ResourceStream))
        {
            using Stream SourceStream = ResourceStream;
            using FileStream DestinationStream = new(TaskFile, FileMode.Create, FileAccess.Write, FileShare.None);
            using StreamReader Reader = new(SourceStream);
            using StreamWriter Writer = new(DestinationStream);

            string Content = Reader.ReadToEnd();

            // Use the complete path to the plugin.
#if NETFRAMEWORK
            Content = Content.Replace("%PATH%", ExecutingAssembly.Location);
#else
            Content = Content.Replace("%PATH%", ExecutingAssembly.Location, StringComparison.InvariantCulture);
#endif
            Writer.WriteLine(Content);
        }
    }

    /// <summary>
    /// Gets a value indicating whether the application requires being run as administrator.
    /// </summary>
    public bool RequireElevated { get; }

    /// <summary>
    /// Gets the text with task selection.
    /// </summary>
    public string TaskSelectionText { get; }

    private readonly string TaskFile = string.Empty;
    #endregion

    #region Events
    private void OnLaunch(object sender, ExecutedRoutedEventArgs e)
    {
        // Launch the Windows Task Scheduler.
        using Process ControlProcess = new();
        ControlProcess.StartInfo.FileName = "control.exe";
        ControlProcess.StartInfo.Arguments = "schedtasks";
        ControlProcess.StartInfo.UseShellExecute = true;

        _ = ControlProcess.Start();
    }

    // Copy to the clipboard the full path to the script to import.
    private void OnCopy(object sender, ExecutedRoutedEventArgs e) => Clipboard.SetText(TaskFile);

    private void OnClose(object sender, ExecutedRoutedEventArgs e) => Close();
    #endregion
}
