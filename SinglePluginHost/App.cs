namespace TaskbarIconHost
{
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.Principal;
    using System.Threading;
    using System.Windows;
    using System.Windows.Input;
    using SchedulerTools;
    using TaskbarTools;
    using Tracing;

    /// <summary>
    /// Represents an application that can manage a plugin having an icon in the taskbar.
    /// </summary>
    public partial class App : IDisposable
    {
        #region Init
        static App()
        {
            Logger.Write(Category.Debug, "Starting");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// </summary>
        /// <param name="owner">The owner program.</param>
        /// <param name="plugin">The plugin.</param>
        /// <param name="assemblyName">The assembly containg the plugin code and resources.</param>
        public App(Application owner, IPluginClient plugin, string assemblyName)
        {
            Owner = owner;
            Plugin = plugin;
            AssemblyName = assemblyName;

            // Ensure only one instance is running at a time.
            Logger.Write(Category.Debug, "Checking uniqueness");

            try
            {
                Guid AppGuid = Plugin.Guid;
                if (AppGuid == Guid.Empty)
                {
                    // In case the guid is provided by the project settings and not source code.
                    GuidAttribute AppGuidAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<GuidAttribute>();
                    AppGuid = Guid.Parse(AppGuidAttribute.Value);
                }

                string AppUniqueId = AppGuid.ToString("B").ToUpperInvariant();

                // Try to create a global named event with a unique name. If we can create it we are first, otherwise there is another instance.
                // In that case, we just abort.
                bool createdNew;
                InstanceEvent = new EventWaitHandle(false, EventResetMode.ManualReset, AppUniqueId, out createdNew);
                if (!createdNew)
                {
                    Logger.Write(Category.Warning, "Another instance is already running");
                    InstanceEvent.Close();
                    InstanceEvent = null;
                    Owner.Shutdown();
                    return;
                }
            }
            catch (Exception e)
            {
                Logger.Write(Category.Error, $"(from App) {e.Message}");

                Owner.Shutdown();
                return;
            }

            // This code is here mostly to make sure that the Taskbar static class is initialized ASAP.
            // The taskbar rectangle is never empty. And if it is, we have no purpose.
            if (TaskbarLocation.ScreenBounds.IsEmpty)
                Owner.Shutdown();
            else
            {
                Owner.Startup += OnStartup;

                // Make sure we stop only on a call to Shutdown. This is for plugins that have a main window, we don't want to exit when it's closed.
                Owner.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            Logger.Write(Category.Debug, "OnStartup");

            InitTimer();

            // The plugin manager can fail for various reasons. If it does, we just abort.
            if (InitPlugInManager())
            {
                // Install the taskbar icon and create its menu.
                InitTaskbarIcon();

                // Get a notification when we get or loose the focus.
                Owner.Activated += OnActivated;
                Owner.Deactivated += OnDeactivated;
                Owner.Exit += OnExit;
            }
            else
            {
                CleanupTimer();
                Owner.Shutdown();
            }
        }

        // The taskbar got the focus.
        private void OnActivated(object sender, EventArgs e)
        {
            PluginManager.OnActivated();
        }

        // The taskbar lost the focus.
        private void OnDeactivated(object sender, EventArgs e)
        {
            PluginManager.OnDeactivated();
        }

        // Someone called Exit on the application. Time to clean things up.
        private void OnExit(object sender, ExitEventArgs e)
        {
            Logger.Write(Category.Debug, "Exiting application");

            // Set this flag to minimize asynchronous activities.
            IsExiting = true;

            StopPlugInManager();
            CleanupTaskbarIcon();
            CleanupTimer();
            CleanupInstanceEvent();

            // Explicit display of the last message since timed debug is not running anymore.
            Logger.Write(Category.Debug, "Done");
            UpdateLogger();
        }

        private void CleanupInstanceEvent()
        {
            using (EventWaitHandle? Event = InstanceEvent)
            {
                InstanceEvent = null;
            }
        }

        private Application Owner;
        private IPluginClient Plugin;
        private string AssemblyName;
        private bool IsExiting;
        private EventWaitHandle? InstanceEvent;
        #endregion

        #region Properties
        /// <summary>
        /// Gets a value indicating whether the plugin is running as administrator.
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
                    if (wi != null)
                    {
                        WindowsPrincipal wp = new WindowsPrincipal(wi);
                        if (wp != null)
                            IsElevatedInternal = wp.IsInRole(WindowsBuiltInRole.Administrator);
                        else
                            IsElevatedInternal = false;
                    }
                    else
                        IsElevatedInternal = false;

                    Logger.Write(Category.Information, $"IsElevated={IsElevatedInternal}");
                }

                return IsElevatedInternal.Value;
            }
        }

        private bool? IsElevatedInternal;
        #endregion

        #region Events
        private void OnCommandLoadAtStartup(object sender, ExecutedRoutedEventArgs e)
        {
            Logger.Write(Category.Debug, "OnCommandLoadAtStartup");

            if (IsElevated)
            {
                // The user is changing the state.
                TaskbarIcon.ToggleMenuCheck(LoadAtStartupCommand, out bool Install);
                InstallLoad(Install, Plugin.Name);
            }
            else
            {
                // The user would like to change the state, we show to them a dialog box that explains how to do it.
                string ExeName = Assembly.GetEntryAssembly().Location;

                if (Scheduler.IsTaskActive(ExeName))
                {
                    RemoveFromStartupWindow Dlg = new RemoveFromStartupWindow(Plugin.Name);
                    Dlg.ShowDialog();
                }
                else
                {
                    LoadAtStartupWindow Dlg = new LoadAtStartupWindow(PluginManager.RequireElevated, Plugin.Name);
                    Dlg.ShowDialog();
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
                Debug.Assert(Icon != null);

                if (Icon != null)
                    AppTaskbarIcon.UpdateIcon(Icon);
            }

            if (IsToolTipChanged)
            {
                IsToolTipChanged = false;
                string? ToolTip = PluginManager.ToolTip;
                Debug.Assert(ToolTip != null);

                if (ToolTip != null)
                    AppTaskbarIcon.UpdateToolTipText(ToolTip);
            }
        }

        private void OnPluginCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Logger.Write(Category.Debug, "OnPluginCommand");

            PluginManager.OnExecuteCommand(e.Command);

            // After a command is executed, update the menu with the new state.
            // This allows us to do it in the background, instead of when the menu is being opened. It's smoother (or should be).
            if (GetIsIconOrToolTipChanged())
                UpdateIconAndToolTip();

            UpdateMenu();
        }

        private void OnCommandSelectPreferred(object sender, ExecutedRoutedEventArgs e)
        {
            Logger.Write(Category.Debug, "OnCommandSelectPreferred");

            RoutedUICommand SubmenuCommand = (RoutedUICommand)e.Command;

            Guid NewSelectedGuid = new Guid(SubmenuCommand.Text);
            Guid OldSelectedGuid = PluginManager.PreferredPluginGuid;
            if (NewSelectedGuid != OldSelectedGuid)
            {
                PluginManager.PreferredPluginGuid = NewSelectedGuid;

                // If the preferred plugin changed, make sure the icon and tooltip reflect the change.
                IsIconChanged = true;
                IsToolTipChanged = true;
                UpdateIconAndToolTip();

                // Check the new plugin in the menu, and uncheck the previous one.
                if (IconSelectionTable.ContainsKey(NewSelectedGuid))
                    TaskbarIcon.SetMenuCheck(IconSelectionTable[NewSelectedGuid], true);

                if (IconSelectionTable.ContainsKey(OldSelectedGuid))
                    TaskbarIcon.SetMenuCheck(IconSelectionTable[OldSelectedGuid], false);
            }
        }

        private void OnCommandExit(object sender, ExecutedRoutedEventArgs e)
        {
            Logger.Write(Category.Debug, "OnCommandExit");

            Owner.Shutdown();
        }
        #endregion

        #region Logger
        private static void UpdateLogger()
        {
            Logger.PrintLog();
        }

        private static PluginLogger Logger = new PluginLogger();
        #endregion

        #region Load at startup
        private static void InstallLoad(bool isInstalled, string appName)
        {
            string ExeName = Assembly.GetEntryAssembly().Location;

            // Create or delete a task in the Task Scheduler.
            if (isInstalled)
            {
                TaskRunLevel RunLevel = PluginManager.RequireElevated ? TaskRunLevel.Highest : TaskRunLevel.LUA;
                Scheduler.AddTask(appName, ExeName, RunLevel);
            }
            else
                Scheduler.RemoveTask(ExeName, out bool IsFound); // Ignore it if the task was not found.
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
        }
        #endregion
    }
}
