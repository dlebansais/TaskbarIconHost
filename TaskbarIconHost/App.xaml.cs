#pragma warning disable CA1515 // Consider making public types internal

namespace TaskbarIconHost;

using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Contracts;
using SchedulerTools;
using TaskbarTools;
using static TaskbarIconHost.Properties.Resources;

/// <summary>
/// Represents an application that can manage plugins having an icon in the taskbar.
/// </summary>
public partial class App : Application, IDisposable
{
    #region Init
    static App()
    {
        Logger.AddLog("Starting");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// </summary>
    public App()
    {
        ParseArguments();

        // IsCreatedNew is updated during static fields initialization.
        if (!IsCreatedNew)
        {
            Logger.AddLog("Another instance is already running");

            // Optionally tell the other instance to stop.
            if (IsExitRequested)
                _ = InstanceEvent.Set();

            UpdateLogger();
            Shutdown();
            return;
        }

        // This code is here mostly to make sure that the Taskbar static class is initialized ASAP.
        // The taskbar rectangle is never empty. And if it is, we have no purpose.
        Rectangle ScreenBounds = TaskbarLocation.ScreenBounds;
        Debug.Assert(!ScreenBounds.IsEmpty);

        Startup += OnStartup;

        // Make sure we stop only on a call to Shutdown. This is for plugins that have a main window, we don't want to exit when it's closed.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    private static EventWaitHandle InitializeInstanceEvent()
    {
        // Ensure only one instance is running at a time.
        Logger.AddLog("Checking uniqueness");

        // Try to create a global named event with a unique name. If we can create it we are first, otherwise there is another instance.
        // In that case, we will just abort.
        return new EventWaitHandle(false, EventResetMode.ManualReset, GetAppUniqueId(), out IsCreatedNew);
    }

    private static string GetAppUniqueId()
    {
        Guid AppGuid = PluginDetails.PluginGuid;
        if (AppGuid == Guid.Empty)
        {
            // In case the guid is provided by the project settings and not source code.
            Contract.RequireNotNull(Assembly.GetExecutingAssembly(), out Assembly ExecutingAssembly);
            Contract.RequireNotNull(ExecutingAssembly.GetCustomAttribute<GuidAttribute>(), out GuidAttribute AppGuidAttribute);
            AppGuid = Guid.Parse(AppGuidAttribute.Value);
        }

        string AppUniqueId = AppGuid.ToString("B", CultureInfo.InvariantCulture).ToUpper(CultureInfo.InvariantCulture);

        return AppUniqueId;
    }

    private void ParseArguments()
    {
        string[] Arguments = Environment.GetCommandLineArgs();

        foreach (string Argument in Arguments)
            ParseArgument(Argument);
    }

    private void ParseArgument(string argument)
    {
        switch (argument)
        {
            case "Exit":
                IsExitRequested = true;
                break;

            case "FailSigned":
                // Add an unsupported certificate feature to the list of mandatory features.
                _ = OidCheckList.Add(new Oid("1.3.6.1.5.5.7.3.3"));
                SignatureAlertTimeout = TimeSpan.Zero;
                break;

            case "bad":
            default:
                break;
        }
    }

    private void OnStartup(object sender, StartupEventArgs e)
    {
        Logger.AddLog("OnStartup");

        InitTimer();

        // The plugin manager can fail for various reasons. If it does, we just abort.
        if (InitPlugInManager(OidCheckList, out int ExitCode, out bool IsBadSignature))
        {
            // Install the taskbar icon and create its menu.
            InitTaskbarIcon();

            // Get a notification when we get or loose the focus.
            Activated += OnActivated;
            Deactivated += OnDeactivated;
            Exit += OnExit;

            if (IsBadSignature)
                ShowBadSignatureAlert();
        }
        else
        {
            CleanupTimer();

            if (IsBadSignature)
                ShowBadSignatureAlert();

            Shutdown(ExitCode);
        }
    }

    // Display a notification to signal that one of the plugins has a bad digital signature.
    private void ShowBadSignatureAlert()
    {
        SignatureAlertTimer = new Timer(SignatureAlertTimerCallback);
        _ = SignatureAlertTimer.Change(SignatureAlertTimeout, Timeout.InfiniteTimeSpan);
    }

    private void SignatureAlertTimerCallback(object? parameter) => TaskbarBalloon.Show(InvalidSignatureAlert, TimeSpan.FromSeconds(15));

    private bool IsAnotherInstanceRequestingExit => InstanceEvent.WaitOne(0);

    private void ScheduleShutdown() => _ = Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(OnExitRequested));

    private void OnExitRequested() => Shutdown();

    private Application Owner => this;

    // Someone called Exit on the application. Time to clean things up.
    private void OnExit(object sender, ExitEventArgs e)
    {
        Logger.AddLog("Exiting application");

        // Set this flag to minimize asynchronous activities.
        IsExiting = true;

        StopPlugInManager();
        CleanupTaskbarIcon();
        CleanupTimer();
        CleanupInstanceEvent();
        CleanupSignatureAlertTimer();

        // Explicit display of the last message since timed debug is not running anymore.
        Logger.AddLog("Done");
        UpdateLogger();
    }

    private void CleanupInstanceEvent()
    {
        using (InstanceEvent)
        {
        }
    }

    private void CleanupSignatureAlertTimer()
    {
        using (SignatureAlertTimer)
        {
        }
    }

    private readonly string AssemblyName = "TaskbarIconHost";
    private readonly Guid AppGuid = Guid.Empty;
    private bool IsExitRequested;
    private readonly OidCollection OidCheckList = [];
    private Timer SignatureAlertTimer = new((object? parameter) => { });
    private TimeSpan SignatureAlertTimeout = TimeSpan.FromSeconds(40);
    private bool IsExiting;
    private readonly EventWaitHandle InstanceEvent = InitializeInstanceEvent();
    private static bool IsCreatedNew;
    #endregion

    #region Properties
    /// <summary>
    /// Gets a value indicating whether the application is running with elevated privileges.
    /// </summary>
    public bool IsElevated
    {
        get
        {
            // Evaluate this property once, and return the same value after that.
            // This elevated status is the administrator mode, it never changes during the lifetime of the application.
            if (!IsElevatedInternal.HasValue)
            {
                WindowsIdentity wi = WindowsIdentity.GetCurrent();
                WindowsPrincipal wp = new(wi);
                IsElevatedInternal = wp.IsInRole(WindowsBuiltInRole.Administrator);

                Logger.AddLog($"IsElevated={IsElevatedInternal}");
            }

            return IsElevatedInternal.Value;
        }
    }

    private bool? IsElevatedInternal;
    #endregion

    #region Events

    // The taskbar got the focus.
    private void OnActivated(object? sender, EventArgs e) => PluginManager.OnActivated();

    // The taskbar lost the focus.
    private void OnDeactivated(object? sender, EventArgs e) => PluginManager.OnDeactivated();

    private void OnCommandLoadAtStartup(object sender, ExecutedRoutedEventArgs e)
    {
        Logger.AddLog("OnCommandLoadAtStartup");

        if (IsElevated)
        {
            // The user is changing the state.
            TaskbarIcon.ToggleMenuCheck(LoadAtStartupCommand, out bool Install);
            InstallLoad(Install, PluginDetails.Name);
        }
        else
        {
            // The user would like to change the state, we show to them a dialog box that explains how to do it.
            string ExeName = Assembly.GetExecutingAssembly().Location;

            if (Scheduler.IsTaskActive(ExeName))
            {
                RemoveFromStartupWindow Dlg = new(PluginDetails.Name);
                _ = Dlg.ShowDialog();
            }
            else
            {
                LoadAtStartupWindow Dlg = new(PluginManager.RequireElevated, PluginDetails.Name);
                _ = Dlg.ShowDialog();
            }
        }
    }

    private bool GetIsIconOrToolTipChanged()
    {
        IsIconChanged |= PluginManager.GetIsIconChanged();
        IsToolTipChanged |= PluginManager.GetIsToolTipChanged();

        return IsIconChanged || IsToolTipChanged;
    }

    private void UpdateIconAndToolTip()
    {
        if (IsIconChanged)
        {
            IsIconChanged = false;
            Icon? Icon = PluginManager.Icon;
            if (Icon is not null)
                AppTaskbarIcon.UpdateIcon(Icon);
        }

        if (IsToolTipChanged)
        {
            IsToolTipChanged = false;
            string? ToolTip = PluginManager.ToolTip;
            AppTaskbarIcon.UpdateToolTipText(ToolTip);
        }
    }

    private void OnPluginCommand(object sender, ExecutedRoutedEventArgs e)
    {
        Logger.AddLog("OnPluginCommand");

        PluginManager.OnExecuteCommand(e.Command);

        // After a command is executed, update the menu with the new state.
        // This allows us to do it in the background, instead of when the menu is being opened. It's smoother (or should be).
        if (GetIsIconOrToolTipChanged())
            UpdateIconAndToolTip();

        UpdateMenu();
    }

    private void OnCommandSelectPreferred(object sender, ExecutedRoutedEventArgs e)
    {
        Logger.AddLog("OnCommandSelectPreferred");

        RoutedUICommand SubmenuCommand = (RoutedUICommand)e.Command;

        Guid NewSelectedGuid = new(SubmenuCommand.Text);
        Guid OldSelectedGuid = PluginManager.PreferredPluginGuid;
        if (NewSelectedGuid != OldSelectedGuid)
        {
            PluginManager.PreferredPluginGuid = NewSelectedGuid;

            // If the preferred plugin changed, make sure the icon and tooltip reflect the change.
            IsIconChanged = true;
            IsToolTipChanged = true;
            UpdateIconAndToolTip();

            // Check the new plugin in the menu, and uncheck the previous one.
            if (IconSelectionTable.TryGetValue(NewSelectedGuid, out ICommand? NewValue))
                TaskbarIcon.SetMenuCheck(NewValue, true);

            if (IconSelectionTable.TryGetValue(OldSelectedGuid, out ICommand? OldValue))
                TaskbarIcon.SetMenuCheck(OldValue, false);
        }
    }

    private void OnCommandExit(object sender, ExecutedRoutedEventArgs e)
    {
        Logger.AddLog("OnCommandExit");

        Shutdown();
    }
    #endregion

    #region Logger
    private static void UpdateLogger() => Logger.PrintLog();

    private static readonly PluginLogger Logger = new();
    #endregion

    #region Load at startup
    private static void InstallLoad(bool isInstalled, string appName)
    {
        string ExeName = Assembly.GetExecutingAssembly().Location;

        // Create or delete a task in the Task Scheduler.
        if (isInstalled)
        {
            TaskRunLevel RunLevel = PluginManager.RequireElevated ? TaskRunLevel.Highest : TaskRunLevel.LUA;

            try
            {
                _ = Scheduler.AddTask(appName, ExeName, RunLevel);
            }
            catch (AddTaskFailedException e)
            {
                Logger.AddLog($"(from Scheduler.AddTask) {e.Message}");
            }
        }
        else
        {
            Scheduler.RemoveTask(ExeName, out _); // Ignore it if the task was not found.
        }
    }
    #endregion

    #region Implementation of IDisposable
    /// <summary>
    /// Called when an object should release its resources.
    /// </summary>
    /// <param name="isDisposing">Indicates if resources must be disposed now.</param>
    protected virtual void Dispose(bool isDisposing)
    {
        if (!IsDisposed)
        {
            IsDisposed = true;

            if (isDisposing)
                DisposeNow();
        }
    }

    /// <summary>
    /// Called when an object should release its resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="App"/> class.
    /// </summary>
    ~App()
    {
        Dispose(false);
    }

    /// <summary>
    /// True after <see cref="Dispose(bool)"/> has been invoked.
    /// </summary>
    private bool IsDisposed;

    /// <summary>
    /// Disposes of every reference that must be cleaned up.
    /// </summary>
    private void DisposeNow()
    {
        CleanupTimer();
        CleanupPlugInManager();
        CleanupInstanceEvent();
        CleanupSignatureAlertTimer();
    }
    #endregion
}
