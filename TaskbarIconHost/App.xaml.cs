using Microsoft.Win32.TaskScheduler;
using SchedulerTools;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TaskbarTools;

namespace TaskbarIconHost
{
    public partial class App : Application, IDisposable
    {
        #region Init
        static App()
        {
            InitLogger();
            Logger.AddLog("Starting");
        }

        public App()
        {
            // Ensure only one instance is running at a time.
            Logger.AddLog("Checking uniqueness");

            try
            {
                bool createdNew;
                InstanceEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "{E4801722-7212-4C7D-8949-87012B5E1B5B}", out createdNew);
                if (!createdNew)
                {
                    Logger.AddLog("Another instance is already running");
                    InstanceEvent.Close();
                    InstanceEvent = null;
                    Shutdown();
                    return;
                }
            }
            catch (Exception e)
            {
                Logger.AddLog($"(from App) {e.Message}");

                Shutdown();
                return;
            }

            // This code is here mostly to make sure that the Taskbar static class is initialized ASAP.
            if (Taskbar.ScreenBounds.IsEmpty)
                Shutdown();
            else
            {
                Startup += OnStartup;
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
            }
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            Logger.AddLog("OnStartup");

            InitTimer();

            if (InitPlugInManager())
            {
                InitTaskbarIcon();

                Exit += OnExit;
            }
            else
            {
                CleanupTimer();
                Shutdown();
            }
        }

        private void OnExit(object sender, ExitEventArgs e)
        {
            Logger.AddLog("Exiting application");

            StopPlugInManager();
            CleanupTaskbarIcon();
            CleanupTimer();

            if (InstanceEvent != null)
            {
                InstanceEvent.Close();
                InstanceEvent = null;
            }

            Logger.AddLog("Done");
            UpdateLogger();
        }

        private EventWaitHandle InstanceEvent;
        #endregion

        #region Properties
        public bool IsElevated
        {
            get
            {
                if (_IsElevated == null)
                {
                    WindowsIdentity wi = WindowsIdentity.GetCurrent();
                    if (wi != null)
                    {
                        WindowsPrincipal wp = new WindowsPrincipal(wi);
                        if (wp != null)
                            _IsElevated = wp.IsInRole(WindowsBuiltInRole.Administrator);
                        else
                            _IsElevated = false;
                    }
                    else
                        _IsElevated = false;

                    Logger.AddLog($"IsElevated={_IsElevated}");
                }

                return _IsElevated.Value;
            }
        }
        private bool? _IsElevated;
        #endregion

        #region Plugin Manager
        private bool InitPlugInManager()
        {
            return PluginManager.Init(IsElevated, Dispatcher, Logger);
        }

        private void StopPlugInManager()
        {
            PluginManager.Shutdown();
        }
        #endregion

        #region Taskbar Icon
        private void InitTaskbarIcon()
        {
            Logger.AddLog("InitTaskbarIcon starting");

            LoadAtStartupCommand = FindResource("LoadAtStartupCommand") as ICommand;
            AddMenuCommand(LoadAtStartupCommand, OnCommandLoadAtStartup);

            ExitCommand = FindResource("ExitCommand") as ICommand;
            AddMenuCommand(ExitCommand, OnCommandExit);

            foreach (List<ICommand> CommandList in PluginManager.FullCommandList)
                foreach (ICommand Command in CommandList)
                    AddMenuCommand(Command, OnPluginCommand);

            Icon Icon = PluginManager.Icon;
            string ToolTip = PluginManager.ToolTip;
            ContextMenu ContextMenu = LoadContextMenu();

            TaskbarIcon = TaskbarIcon.Create(Icon, ToolTip, ContextMenu, ContextMenu);
            TaskbarIcon.MenuOpening += OnMenuOpening;

            Logger.AddLog("InitTaskbarIcon done");
        }

        private void CleanupTaskbarIcon()
        {
            using (TaskbarIcon Icon = TaskbarIcon)
            {
                TaskbarIcon = null;
            }
        }

        private void AddMenuCommand(ICommand Command, ExecutedRoutedEventHandler executed)
        {
            if (Command == null)
                return;

            // Bind the command to the corresponding handler. Requires the menu to be the target of notifications in TaskbarIcon.
            CommandManager.RegisterClassCommandBinding(typeof(ContextMenu), new CommandBinding(Command, executed));
        }

        private T LoadEmbeddedResource<T>(string resourceName)
        {
            // Loads an "Embedded Resource" of type T (ex: Bitmap for a PNG file).
            foreach (string ResourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                if (ResourceName.EndsWith(resourceName))
                {
                    using (Stream rs = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
                    {
                        T Result = (T)Activator.CreateInstance(typeof(T), rs);
                        Logger.AddLog($"Resource {resourceName} loaded");

                        return Result;
                    }
                }

            Logger.AddLog($"Resource {resourceName} not found");
            return default(T);
        }

        private ContextMenu LoadContextMenu()
        {
            ContextMenu Result = new ContextMenu();

            MenuItem LoadAtStartup;
            string ExeName = Assembly.GetExecutingAssembly().Location;

            if (Scheduler.IsTaskActive(ExeName))
            {
                if (IsElevated)
                    LoadAtStartup = CreateMenuItem(LoadAtStartupCommand, LoadAtStartupHeader, true, null);
                else
                    LoadAtStartup = CreateMenuItem(LoadAtStartupCommand, RemoveFromStartupHeader, false, LoadEmbeddedResource<Bitmap>("UAC-16.png"));
            }
            else
            {
                if (IsElevated)
                    LoadAtStartup = CreateMenuItem(LoadAtStartupCommand, LoadAtStartupHeader, false, null);
                else
                    LoadAtStartup = CreateMenuItem(LoadAtStartupCommand, LoadAtStartupHeader, false, LoadEmbeddedResource<Bitmap>("UAC-16.png"));
            }

            TaskbarIcon.PrepareMenuItem(LoadAtStartup, true, IsElevated);
            Result.Items.Add(LoadAtStartup);

            List<List<MenuItem>> FullPluginMenuList = new List<List<MenuItem>>();
            int VisiblePluginMenuCount = 0;

            foreach (List<ICommand> CommandList in PluginManager.FullCommandList)
            {
                List<MenuItem> PluginMenuList = new List<MenuItem>();

                foreach (ICommand Command in CommandList)
                    if (Command == null)
                        PluginMenuList.Add(null);
                    else
                    {
                        string MenuHeader = PluginManager.GetMenuHeader(Command);
                        bool MenuIsVisible = PluginManager.GetMenuIsVisible(Command);
                        bool MenuIsEnabled = PluginManager.GetMenuIsEnabled(Command);
                        bool MenuIsChecked = PluginManager.GetMenuIsChecked(Command);
                        Bitmap MenuIcon = PluginManager.GetMenuIcon(Command);

                        MenuItem PluginMenu = CreateMenuItem(Command, MenuHeader, MenuIsChecked, MenuIcon);
                        TaskbarIcon.PrepareMenuItem(PluginMenu, MenuIsVisible, MenuIsEnabled);

                        PluginMenuList.Add(PluginMenu);

                        if (MenuIsVisible)
                            VisiblePluginMenuCount++;
                    }

                FullPluginMenuList.Add(PluginMenuList);
            }

            bool AddSeparator = VisiblePluginMenuCount > 1;

            foreach (List<MenuItem> MenuList in FullPluginMenuList)
            {
                if (AddSeparator)
                    Result.Items.Add(new Separator());

                AddSeparator = true;

                ContextMenu CurrentMenu;
                if (MenuList.Count > 1)
                    CurrentMenu = new ContextMenu();
                else
                    CurrentMenu = Result;

                foreach (MenuItem MenuItem in MenuList)
                    if (MenuItem != null)
                        CurrentMenu.Items.Add(MenuItem);
                    else
                        CurrentMenu.Items.Add(new Separator());
            }

            Result.Items.Add(new Separator());

            MenuItem ExitMenu = CreateMenuItem(ExitCommand, "Exit", false, null);
            Result.Items.Add(ExitMenu);

            Logger.AddLog("Menu created");

            return Result;
        }

        private MenuItem CreateMenuItem(ICommand command, string header, bool isChecked, Bitmap icon)
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

            TaskbarIcon SenderIcon = sender as TaskbarIcon;
            string ExeName = Assembly.GetExecutingAssembly().Location;

            if (IsElevated)
                SenderIcon.SetMenuIsChecked(LoadAtStartupCommand, Scheduler.IsTaskActive(ExeName));
            else
            {
                if (Scheduler.IsTaskActive(ExeName))
                    SenderIcon.SetMenuHeader(LoadAtStartupCommand, RemoveFromStartupHeader);
                else
                    SenderIcon.SetMenuHeader(LoadAtStartupCommand, LoadAtStartupHeader);
            }

            UpdateMenu();

            PluginManager.OnMenuOpening();
        }

        private void UpdateMenu()
        {
            foreach (ICommand Command in PluginManager.GetChangedCommands())
            {
                bool MenuIsVisible = PluginManager.GetMenuIsVisible(Command);
                if (MenuIsVisible)
                {
                    TaskbarIcon.SetMenuIsVisible(Command, true);
                    TaskbarIcon.SetMenuHeader(Command, PluginManager.GetMenuHeader(Command));
                    TaskbarIcon.SetMenuIsEnabled(Command, PluginManager.GetMenuIsEnabled(Command));

                    Bitmap MenuIcon = PluginManager.GetMenuIcon(Command);
                    if (MenuIcon != null)
                    {
                        TaskbarIcon.SetMenuIsChecked(Command, false);
                        TaskbarIcon.SetMenuIcon(Command, MenuIcon);
                    }
                    else
                        TaskbarIcon.SetMenuIsChecked(Command, PluginManager.GetMenuIsChecked(Command));
                }
                else
                    TaskbarIcon.SetMenuIsVisible(Command, false);
            }
        }

        public TaskbarIcon TaskbarIcon { get; private set; }
        private static readonly string LoadAtStartupHeader = "Load at startup";
        private static readonly string RemoveFromStartupHeader = "Remove from startup";
        private ICommand LoadAtStartupCommand;
        private ICommand ExitCommand;
        private bool IsIconChanged;
        private bool IsToolTipChanged;
        #endregion

        #region Events
        private void OnCommandLoadAtStartup(object sender, ExecutedRoutedEventArgs e)
        {
            Logger.AddLog("OnCommandLoadAtStartup");

            TaskbarIcon.ToggleMenuIsChecked(LoadAtStartupCommand, out bool Install);
            InstallLoad(Install);
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
                TaskbarIcon.UpdateToolTip(ToolTip);
            }
        }

        private void OnPluginCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Logger.AddLog("OnPluginCommand");

            PluginManager.ExecuteCommandHandler(e.Command);

            if (GetIsIconOrToolTipChanged())
                UpdateIconAndToolTip();

            UpdateMenu();
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
            AppTimer = new Timer(new TimerCallback(AppTimerCallback));
            AppTimer.Change(CheckInterval, CheckInterval);
        }

        private void AppTimerCallback(object parameter)
        {
            UpdateLogger();

            if (AppTimerOperation == null || (AppTimerOperation.Status == DispatcherOperationStatus.Completed && GetIsIconOrToolTipChanged()))
                AppTimerOperation = Dispatcher.BeginInvoke(DispatcherPriority.Normal, new System.Action(OnAppTimer));
        }

        private void OnAppTimer()
        {
            UpdateIconAndToolTip();
        }

        private void CleanupTimer()
        {
            if (AppTimer != null)
            {
                AppTimer.Dispose();
                AppTimer = null;
            }
        }

        private Timer AppTimer;
        private DispatcherOperation AppTimerOperation;
        private TimeSpan CheckInterval = TimeSpan.FromSeconds(0.1);
        #endregion

        #region Logger
        private static void InitLogger()
        {
            Logger = new PluginLogger();
        }

        private static void UpdateLogger()
        {
            Logger.PrintLog();
        }

        private static PluginLogger Logger;
        #endregion

        #region Load at startup
        private void InstallLoad(bool isInstalled)
        {
            string ExeName = Assembly.GetExecutingAssembly().Location;

            if (isInstalled)
            {
                TaskRunLevel RunLevel = PluginManager.RequireElevated ? TaskRunLevel.Highest : TaskRunLevel.LUA;
                Scheduler.AddTask("Taskbar Icon Host", ExeName, RunLevel, Logger);
            }
            else
                Scheduler.RemoveTask(ExeName, out bool IsFound);
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
