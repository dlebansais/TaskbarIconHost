namespace TaskbarIconHost
{
    using System.Globalization;
    using Microsoft.Win32.TaskScheduler;
    using SchedulerTools;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.Principal;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Threading;
    using System.Diagnostics;
    using System.Security.Cryptography;
    using TaskbarTools;
    using static TaskbarIconHost.Properties.Resources;


    public partial class App : Application, IDisposable
    {
        #region Init
        static App()
        {
            Logger.AddLog("Starting");
        }

        public App()
        {
            ParseArguments();

            // IsCreatedNew is updated during static fields initialization.
            if (!IsCreatedNew)
            {
                Logger.AddLog("Another instance is already running");

                // Optionally tell the other instance to stop.
                if (IsExitRequested)
                    InstanceEvent.Set();

                UpdateLogger();
                Shutdown();
                return;
            }

            // This code is here mostly to make sure that the Taskbar static class is initialized ASAP.
            // The taskbar rectangle is never empty. And if it is, we have no purpose.
            Rectangle ScreenBounds = Taskbar.ScreenBounds;
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
            Guid AppGuid = PluginDetails.Guid;
            if (AppGuid == Guid.Empty)
            {
                // In case the guid is provided by the project settings and not source code.
                GuidAttribute AppGuidAttribute = Assembly.GetExecutingAssembly().GetCustomAttribute<GuidAttribute>();
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
                    OidCheckList.Add(new Oid("1.3.6.1.5.5.7.3.3"));
                    SignatureAlertTimeout = TimeSpan.Zero;
                    break;

                case "bad":
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
            SignatureAlertTimer.Change(SignatureAlertTimeout, Timeout.InfiniteTimeSpan);
        }

        private void SignatureAlertTimerCallback(object parameter)
        {
            TaskbarBalloon.Show(InvalidSignatureAlert, 15000);
        }

        private bool IsAnotherInstanceRequestingExit
        {
            get { return InstanceEvent.WaitOne(0); }
        }

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

        private bool IsExitRequested;
        private OidCollection OidCheckList = new OidCollection();
        private Timer SignatureAlertTimer = new Timer((object parameter) => { });
        private TimeSpan SignatureAlertTimeout = TimeSpan.FromSeconds(40);
        private bool IsExiting;
        private readonly EventWaitHandle InstanceEvent = InitializeInstanceEvent();
        private static bool IsCreatedNew;
        #endregion

        #region Properties
        public bool IsElevated
        {
            get
            {
                // Evaluate this property once, and return the same value after that.
                // This elevated status is the administrator mode, it never changes during the lifetime of the application.
                if (!_IsElevated.HasValue)
                {
                    WindowsIdentity wi = WindowsIdentity.GetCurrent();
                    WindowsPrincipal wp = new WindowsPrincipal(wi);
                    _IsElevated = wp.IsInRole(WindowsBuiltInRole.Administrator);

                    Logger.AddLog($"IsElevated={_IsElevated}");
                }

                return _IsElevated.Value;
            }
        }
        private bool? _IsElevated;
        #endregion

        #region Plugin Manager
        private bool InitPlugInManager(OidCollection oidCheckList, out int exitCode, out bool isBadSignature)
        {
            if (!PluginManager.Init(IsElevated, PluginDetails.AssemblyName, PluginDetails.Guid, Dispatcher, Logger, oidCheckList, out exitCode, out isBadSignature))
                return false;

            // In the case of a single plugin version, this code won't do anything.
            // However, if several single plugin versions run concurrently, the last one to run will be the preferred one for another plugin host.
            GlobalSettings = new PluginSettings(string.Empty, Logger);

            // Assign the guid with a value taken from the registry.
            PluginManager.PreferredPluginGuid = GlobalSettings.GetSettingGuid(PreferredPluginSettingName, Guid.Empty);
            exitCode = 0;

            return true;
        }

        private void StopPlugInManager()
        {
            // Save this plugin guid so that the last saved will be the preferred one if there is another plugin host.
            GlobalSettings.SetSettingString(PreferredPluginSettingName, PluginManager.GuidToString(PluginManager.PreferredPluginGuid));
            PluginManager.Shutdown();

            CleanupPlugInManager();
        }

        private void CleanupPlugInManager()
        {
            using (GlobalSettings)
            {
            }
        }

        private const string PreferredPluginSettingName = "PreferredPlugin";
        private IPluginSettings GlobalSettings = new PluginEmptySettings(new PluginEmptyLogger());
        #endregion

        #region Taskbar Icon
        private void InitTaskbarIcon()
        {
            Logger.AddLog("InitTaskbarIcon starting");

            // Bind the load at startup/remove from startup command.
            AddMenuCommand(LoadAtStartupCommand, OnCommandLoadAtStartup);

            // Bind the exit command.
            AddMenuCommand(ExitCommand, OnCommandExit);

            // Do the same with all plugin commands.
            foreach (KeyValuePair<List<ICommand>, string> Entry in PluginManager.FullCommandList)
            {
                List<ICommand> FullPluginCommandList = Entry.Key;
                foreach (ICommand Command in FullPluginCommandList)
                    AddMenuCommand(Command, OnPluginCommand);
            }

            // Get the preferred icon and tooltip, and build the taskbar context menu.
            Icon Icon = PluginManager.Icon;
            string ToolTip = PluginManager.ToolTip;
            ContextMenu ContextMenu = LoadContextMenu();

            // Install the taskbar icon.
            TaskbarIcon = TaskbarIcon.Create(Icon, ToolTip, ContextMenu, ContextMenu);
            TaskbarIcon.MenuOpening += OnMenuOpening;
            TaskbarIcon.IconClicked += OnIconClicked;

            Logger.AddLog("InitTaskbarIcon done");
        }

        private void CleanupTaskbarIcon()
        {
            using (TaskbarIcon)
            {
            }
        }

        private static void AddMenuCommand(ICommand command, ExecutedRoutedEventHandler executed)
        {
            // Bind the command to the corresponding handler. Requires the menu to be the target of notifications in TaskbarIcon.
            if (!IsSeparatorCommand(command))
                CommandManager.RegisterClassCommandBinding(typeof(ContextMenu), new CommandBinding(command, executed));
        }

        private static bool IsSeparatorCommand(ICommand command)
        {
            return command is RoutedUICommand AsRoutedUiCommand && AsRoutedUiCommand.Text.Length == 0;
        }

        private ContextMenu LoadContextMenu()
        {
            // Create the taskbar context menu and populate it with menu items, submenus and separators.
            ContextMenu Result = new ContextMenu();
            ItemCollection Items = Result.Items;

            MenuItem LoadAtStartup = LoadContextMenuFromActive();

            TaskbarIcon.PrepareMenuItem(LoadAtStartup, true, true);
            Items.Add(LoadAtStartup);

            // Below load at startup, we add plugin menus.
            // Separate them in two categories, small and large. Small menus are always directly visible in the main context menu.
            // Large plugin menus, if there is more than one, have their own submenu. If there is just one plugin with a large menu we don't bother.
            AddPluginMenuToContextMenu(out Dictionary<List<MenuItem?>, string> FullPluginMenuList, out int LargePluginMenuCount);

            // Add small menus, then large menus.
            AddPluginMenuItems(Items, FullPluginMenuList, false, false);
            AddPluginMenuItems(Items, FullPluginMenuList, true, LargePluginMenuCount > 1);

            // If there are more than one plugin capable of receiving click notification, we must give the user a way to choose which.
            // For this purpose, create a "Icons" menu with a choice of plugins, with their name and preferred icon.
            SelectPreferredPluginContextMenu(Items);

            AddExitMenuToContextMenu(Items);

            Logger.AddLog("Menu created");

            return Result;
        }

        private MenuItem LoadContextMenuFromActive()
        {
            MenuItem Result;
            string ExeName = Assembly.GetExecutingAssembly().Location;

            // Create a menu item for the load at startup/remove from startup command, depending on the current situation.
            // UAC-16.png is the recommended 'shield' icon to indicate administrator mode is required for the operation.
            if (Scheduler.IsTaskActive(ExeName))
            {
                if (IsElevated)
                    Result = CreateMenuItem(LoadAtStartupCommand, LoadAtStartupHeader, true, null);
                else
                    Result = CreateMenuItem(LoadAtStartupCommand, RemoveFromStartupHeader, false, ResourceTools.LoadEmbeddedResource<Bitmap>("UAC-16.png"));
            }
            else
            {
                if (IsElevated)
                    Result = CreateMenuItem(LoadAtStartupCommand, LoadAtStartupHeader, false, null);
                else
                    Result = CreateMenuItem(LoadAtStartupCommand, LoadAtStartupHeader, false, ResourceTools.LoadEmbeddedResource<Bitmap>("UAC-16.png"));
            }

            return Result;
        }

        private static void AddPluginMenuToContextMenu(out Dictionary<List<MenuItem?>, string> fullPluginMenuList, out int largePluginMenuCount)
        {
            fullPluginMenuList = new Dictionary<List<MenuItem?>, string>();
            largePluginMenuCount = 0;

            foreach (KeyValuePair<List<ICommand>, string> Entry in PluginManager.FullCommandList)
            {
                List<ICommand> FullPluginCommandList = Entry.Key;
                string PluginName = Entry.Value;

                List<MenuItem?> PluginMenuList = new List<MenuItem?>();
                int VisiblePluginMenuCount = 0;

                foreach (ICommand Command in FullPluginCommandList)
                    if (Command == null)
                        PluginMenuList.Add(null); // This will result in the creation of a separator.
                    else
                    {
                        string MenuHeader = PluginManager.GetMenuHeader(Command);
                        bool MenuIsVisible = PluginManager.GetMenuIsVisible(Command);
                        bool MenuIsEnabled = PluginManager.GetMenuIsEnabled(Command);
                        bool MenuIsChecked = PluginManager.GetMenuIsChecked(Command);
                        Bitmap? MenuIcon = PluginManager.GetMenuIcon(Command);

                        MenuItem PluginMenu = CreateMenuItem(Command, MenuHeader, MenuIsChecked, MenuIcon);
                        TaskbarIcon.PrepareMenuItem(PluginMenu, MenuIsVisible, MenuIsEnabled);

                        PluginMenuList.Add(PluginMenu);

                        // Count how many visible items to decide if the menu is large or small.
                        if (MenuIsVisible)
                            VisiblePluginMenuCount++;
                    }

                if (VisiblePluginMenuCount > 1)
                    largePluginMenuCount++;

                fullPluginMenuList.Add(PluginMenuList, PluginName);
            }
        }

        private void SelectPreferredPluginContextMenu(ItemCollection items)
        {
            if (PluginManager.ConsolidatedPluginList.Count > 1)
            {
                items.Add(new Separator());

                MenuItem IconSubmenu = new MenuItem();
                IconSubmenu.Header = "Icons";

                foreach (IPluginClient Plugin in PluginManager.ConsolidatedPluginList)
                {
                    Guid SubmenuGuid = Plugin.Guid;
                    if (!IconSelectionTable.ContainsKey(SubmenuGuid)) // Protection against plugins reusing a guid...
                    {
                        RoutedUICommand SubmenuCommand = new RoutedUICommand();
                        SubmenuCommand.Text = PluginManager.GuidToString(SubmenuGuid);

                        string SubmenuHeader = Plugin.Name;
                        bool SubmenuIsChecked = (SubmenuGuid == PluginManager.PreferredPluginGuid); // The currently preferred plugin will be checked as so.
                        Bitmap SubmenuIcon = Plugin.SelectionBitmap;

                        AddMenuCommand(SubmenuCommand, OnCommandSelectPreferred);
                        MenuItem PluginMenu = CreateMenuItem(SubmenuCommand, SubmenuHeader, SubmenuIsChecked, SubmenuIcon);
                        TaskbarIcon.PrepareMenuItem(PluginMenu, true, true);
                        IconSubmenu.Items.Add(PluginMenu);

                        IconSelectionTable.Add(SubmenuGuid, SubmenuCommand);
                    }
                }

                // Add this "Icons" menu to the main context menu.
                items.Add(IconSubmenu);
            }
        }

        private static void AddPluginMenuItems(ItemCollection items, Dictionary<List<MenuItem?>, string> fullPluginMenuList, bool largeSubmenu, bool useSubmenus)
        {
            bool AddSeparator = true;

            foreach (KeyValuePair<List<MenuItem?>, string> Entry in fullPluginMenuList)
            {
                List<MenuItem?> PluginMenuList = Entry.Key;

                // Only add the category of plugin menu targeted by this call.
                if ((PluginMenuList.Count <= 1 && largeSubmenu) || (PluginMenuList.Count > 1 && !largeSubmenu))
                    continue;

                string PluginName = Entry.Value;

                if (AddSeparator)
                    items.Add(new Separator());

                ItemCollection SubmenuItems;
                if (useSubmenus)
                {
                    AddSeparator = false;
                    MenuItem PluginSubmenu = new MenuItem();
                    PluginSubmenu.Header = PluginName;
                    SubmenuItems = PluginSubmenu.Items;
                    items.Add(PluginSubmenu);
                }
                else
                {
                    AddSeparator = true;
                    SubmenuItems = items;
                }

                // null in the plugin menu means separator.
                foreach (MenuItem? MenuItem in PluginMenuList)
                    if (MenuItem != null)
                        SubmenuItems.Add(MenuItem);
                    else
                        SubmenuItems.Add(new Separator());
            }
        }

        private void AddExitMenuToContextMenu(ItemCollection items)
        {
            // Always add a separator above the exit menu.
            items.Add(new Separator());

            MenuItem ExitMenu = CreateMenuItem(ExitCommand, "Exit", false, null);
            TaskbarIcon.PrepareMenuItem(ExitMenu, true, true);
            items.Add(ExitMenu);
        }

        private static MenuItem CreateMenuItem(ICommand command, string header, bool isChecked, Bitmap? icon)
        {
            MenuItem Result = new MenuItem();
            Result.Header = header;
            Result.Command = command;
            Result.IsChecked = isChecked;
            Result.Icon = icon;

            return Result;
        }

        private void OnMenuOpening(object sender, EventArgs e)
        {
            Logger.AddLog("OnMenuOpening");

            string ExeName = Assembly.GetExecutingAssembly().Location;

            // Update the load at startup menu with the current state (the user can change it directly in the Task Scheduler at any time).
            if (IsElevated)
                TaskbarIcon.SetMenuCheck(LoadAtStartupCommand, Scheduler.IsTaskActive(ExeName));
            else
            {
                if (Scheduler.IsTaskActive(ExeName))
                    TaskbarIcon.SetMenuText(LoadAtStartupCommand, RemoveFromStartupHeader);
                else
                    TaskbarIcon.SetMenuText(LoadAtStartupCommand, LoadAtStartupHeader);
            }

            // Update the menu with latest news from plugins.
            UpdateMenu();

            PluginManager.OnMenuOpening();
        }

        private void UpdateMenu()
        {
            List<ICommand> ChangedCommandList = PluginManager.GetChangedCommands();

            foreach (ICommand Command in ChangedCommandList)
            {
                // Update changed menus with their new state.
                bool MenuIsVisible = PluginManager.GetMenuIsVisible(Command);
                if (MenuIsVisible)
                {
                    TaskbarIcon.SetMenuIsVisible(Command, true);
                    TaskbarIcon.SetMenuText(Command, PluginManager.GetMenuHeader(Command));
                    TaskbarIcon.SetMenuIsEnabled(Command, PluginManager.GetMenuIsEnabled(Command));

                    Bitmap? MenuIcon = PluginManager.GetMenuIcon(Command);
                    if (MenuIcon != null)
                    {
                        TaskbarIcon.SetMenuCheck(Command, false);
                        TaskbarIcon.SetMenuIcon(Command, MenuIcon);
                    }
                    else
                        TaskbarIcon.SetMenuCheck(Command, PluginManager.GetMenuIsChecked(Command));
                }
                else
                    TaskbarIcon.SetMenuIsVisible(Command, false);
            }
        }

        private void OnIconClicked(object sender, EventArgs e)
        {
            PluginManager.OnIconClicked();
        }

        internal TaskbarIcon TaskbarIcon { get; private set; } = TaskbarIcon.Empty;
        private ICommand LoadAtStartupCommand = new RoutedUICommand();
        private ICommand ExitCommand = new RoutedUICommand();
        private Dictionary<Guid, ICommand> IconSelectionTable = new Dictionary<Guid, ICommand>();
        private bool IsIconChanged;
        private bool IsToolTipChanged;
        #endregion

        #region Events
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
                    RemoveFromStartupWindow Dlg = new RemoveFromStartupWindow(PluginDetails.Name);
                    Dlg.ShowDialog();
                }
                else
                {
                    LoadAtStartupWindow Dlg = new LoadAtStartupWindow(PluginManager.RequireElevated, PluginDetails.Name);
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
                Icon Icon = PluginManager.Icon;
                TaskbarIcon.UpdateIcon(Icon);
            }

            if (IsToolTipChanged)
            {
                IsToolTipChanged = false;
                string ToolTip = PluginManager.ToolTip;
                TaskbarIcon.UpdateToolTipText(ToolTip);
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
            Logger.AddLog("OnCommandExit");

            Shutdown();
        }
        #endregion

        #region Timer
        private void InitTimer()
        {
            // Create a timer to display traces asynchronously.
            AppTimer = new Timer(AppTimerCallback);
            AppTimer.Change(CheckInterval, CheckInterval);
        }

        private void AppTimerCallback(object parameter)
        {
            // If a shutdown is started, don't show traces anymore so the shutdown can complete smoothly.
            if (IsExiting)
                return;

            // If another instance is requesting exit, schedule a task to do it.
            if (IsAnotherInstanceRequestingExit)
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new System.Action(OnExitRequested));
            else
            {
                // Print traces asynchronously from the timer thread.
                UpdateLogger();

                // Also, schedule an update of the icon and tooltip if they changed, or the first time.
                if (AppTimerOperation == null || (AppTimerOperation.Status == DispatcherOperationStatus.Completed && GetIsIconOrToolTipChanged()))
                    AppTimerOperation = Dispatcher.BeginInvoke(DispatcherPriority.Normal, new System.Action(OnAppTimer));
            }
        }

        private void OnExitRequested()
        {
            Shutdown();
        }

        private void OnAppTimer()
        {
            // If a shutdown is started, don't update the taskbar anymore so the shutdown can complete smoothly.
            if (IsExiting)
                return;

            UpdateIconAndToolTip();
        }

        private void CleanupTimer()
        {
            using (AppTimer)
            {
            }
        }

        private Timer AppTimer = new Timer((object parameter) => { });
        private DispatcherOperation? AppTimerOperation;
        private TimeSpan CheckInterval = TimeSpan.FromSeconds(0.1);
        #endregion

        #region Logger
        private static void UpdateLogger()
        {
            Logger.PrintLog();
        }

        private static readonly PluginLogger Logger = new PluginLogger();
        #endregion

        #region Load at startup
        private void InstallLoad(bool isInstalled, string appName)
        {
            string ExeName = Assembly.GetExecutingAssembly().Location;

            // Create or delete a task in the Task Scheduler.
            if (isInstalled)
            {
                TaskRunLevel RunLevel = PluginManager.RequireElevated ? TaskRunLevel.Highest : TaskRunLevel.LUA;
                Scheduler.AddTask(appName, ExeName, RunLevel, Logger);
            }
            else
                Scheduler.RemoveTask(ExeName, out bool IsFound); // Ignore it if the task was not found.
        }
        #endregion

        #region Implementation of IDisposable
        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
                DisposeNow();
        }

        private void DisposeNow()
        {
            CleanupTimer();
            CleanupPlugInManager();
            CleanupInstanceEvent();
            CleanupSignatureAlertTimer();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~App()
        {
            Dispose(false);
        }
        #endregion
    }
}
