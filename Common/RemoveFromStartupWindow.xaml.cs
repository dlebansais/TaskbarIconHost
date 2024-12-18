namespace TaskbarIconHost;

using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

/// <summary>
/// Represents an interface with instructions on how to manually disable loading as administrator.
/// </summary>
internal partial class RemoveFromStartupWindow : Window
{
    #region Init
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveFromStartupWindow"/> class.
    /// </summary>
    /// <param name="appName">The application name.</param>
    public RemoveFromStartupWindow(string appName)
    {
        InitializeComponent();
        DataContext = this;

        Title = appName;
        TaskSelectiontext = $"From the Task Scheduler, select 'Task Scheduler Library' and search for the task called '{appName}'.";
    }

    /// <summary>
    /// Gets the text with task selection.
    /// </summary>
    public string TaskSelectiontext { get; private set; }
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

    private void OnClose(object sender, ExecutedRoutedEventArgs e) => Close();
    #endregion
}
