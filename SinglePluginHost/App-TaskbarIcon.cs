namespace TaskbarIconHost
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.IO.Compression;
    using System.Reflection;
    using System.Windows.Controls;
    using System.Windows.Input;
    using SchedulerTools;
    using TaskbarTools;
    using Tracing;

    /// <summary>
    /// Represents an application that can manage a plugin having an icon in the taskbar.
    /// </summary>
    public partial class App
    {
        private void InitTaskbarIcon()
        {
            Logger.Write(Category.Debug, "InitTaskbarIcon starting");

            // Create and bind the load at startip/remove from startup command.
            LoadAtStartupCommand = new RoutedUICommand();
            AddMenuCommand(LoadAtStartupCommand, OnCommandLoadAtStartup);

            // Create and bind the exit command.
            ExitCommand = new RoutedUICommand();
            AddMenuCommand(ExitCommand, OnCommandExit);

            // Do the same with all plugin commands.
            foreach (KeyValuePair<List<ICommand>, string> Entry in PluginManager.FullCommandList)
            {
                List<ICommand> FullPluginCommandList = Entry.Key;
                foreach (ICommand Command in FullPluginCommandList)
                    AddMenuCommand(Command, OnPluginCommand);
            }

            // Get the preferred icon and tooltip, and build the taskbar context menu.
            Icon? Icon = PluginManager.Icon;
            string? ToolTip = PluginManager.ToolTip;
            ContextMenu ContextMenu = LoadContextMenu();

            Debug.Assert(Icon != null);
            Debug.Assert(ToolTip != null);

            // Install the taskbar icon.
            if (Icon != null && ToolTip != null)
            {
                AppTaskbarIcon = TaskbarIcon.Create(Icon, ToolTip, ContextMenu, ContextMenu);
                AppTaskbarIcon.MenuOpening += OnMenuOpening;
                AppTaskbarIcon.IconClicked += OnIconClicked;
            }

            Logger.Write(Category.Debug, "InitTaskbarIcon done");
        }

        private void CleanupTaskbarIcon()
        {
            using (TaskbarIcon Icon = AppTaskbarIcon)
            {
                AppTaskbarIcon = TaskbarIcon.Empty;
            }
        }

        private static void AddMenuCommand(ICommand command, ExecutedRoutedEventHandler executed)
        {
            // The command can be null if a separator is intended.
            if (command == null)
                return;

            // Bind the command to the corresponding handler. Requires the menu to be the target of notifications in TaskbarIcon.
            CommandManager.RegisterClassCommandBinding(typeof(ContextMenu), new CommandBinding(command, executed));
        }

        private T LoadEmbeddedResource<T>(string resourceName)
        {
            if (DecompressedAssembly == null)
                DecompressedAssembly = LoadEmbeddedAssemblyStream();

            Assembly UsingAssembly = DecompressedAssembly != null ? DecompressedAssembly : Assembly.GetExecutingAssembly();
            string ResourcePath = string.Empty;

            // Loads an "Embedded Resource" of type T (ex: Bitmap for a PNG file).
            // Make sure the resource is tagged as such in the resource properties.
            string[] ResourceNames = UsingAssembly.GetManifestResourceNames();
            foreach (string Item in ResourceNames)
                if (Item.EndsWith(resourceName, StringComparison.InvariantCulture))
                {
                    ResourcePath = Item;
                    break;
                }

            // If not found, it could be because it's not tagged as "Embedded Resource".
            if (ResourcePath.Length == 0)
                Logger.Write(Category.Error, $"Resource {resourceName} not found");

            using Stream ResourceStream = UsingAssembly.GetManifestResourceStream(ResourcePath);

            T Result = (T)Activator.CreateInstance(typeof(T), ResourceStream);
            Logger.Write(Category.Debug, $"Resource {resourceName} loaded");

            return Result;
        }

        private Assembly? LoadEmbeddedAssemblyStream()
        {
            Assembly assembly = Assembly.GetEntryAssembly();

            string EmbeddedAssemblyResourcePath = $"costura.{AssemblyName}.dll.compressed";
#pragma warning disable CA1308 // Normalize strings to uppercase
            EmbeddedAssemblyResourcePath = EmbeddedAssemblyResourcePath.ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

            using Stream CompressedStream = assembly.GetManifestResourceStream(EmbeddedAssemblyResourcePath);
            if (CompressedStream == null)
                return null;

            using Stream UncompressedStream = new DeflateStream(CompressedStream, CompressionMode.Decompress);
            using MemoryStream TemporaryStream = new MemoryStream();

            int Count;
            var Buffer = new byte[81920];
            while ((Count = UncompressedStream.Read(Buffer, 0, Buffer.Length)) != 0)
                TemporaryStream.Write(Buffer, 0, Count);

            TemporaryStream.Position = 0;

            byte[] array = new byte[TemporaryStream.Length];
            TemporaryStream.Read(array, 0, array.Length);

            return Assembly.Load(array);
        }

        private Assembly? DecompressedAssembly;

        private ContextMenu LoadContextMenu()
        {
            // Create the taskbar context menu and populate it with menu items, submenus and separators.
            ContextMenu Result = new ContextMenu();
            ItemCollection Items = Result.Items;

            MenuItem LoadAtStartup;
            string ExeName = Assembly.GetEntryAssembly().Location;

            // Create a menu item for the load at startup/remove from startup command, depending on the current situation.
            // UAC-16.png is the recommended 'shield' icon to indicate administrator mode is required for the operation.
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

            TaskbarIcon.PrepareMenuItem(LoadAtStartup, true, true);
            Items.Add(LoadAtStartup);

            // Below load at startup, we add plugin menus.
            // Separate them in two categories, small and large. Small menus are always directly visible in the main context menu.
            // Large plugin menus, if there is more than one, have their own submenu. If there is just one plugin with a large menu we don't bother.
            Dictionary<List<MenuItem?>, string> FullPluginMenuList = new Dictionary<List<MenuItem?>, string>();
            int LargePluginMenuCount = 0;

            foreach (KeyValuePair<List<ICommand>, string> Entry in PluginManager.FullCommandList)
                AddPluginMenu(Entry.Key, Entry.Value, FullPluginMenuList, ref LargePluginMenuCount);

            // Add small menus, then large menus.
            AddPluginMenuItems(Items, FullPluginMenuList, false, false);
            AddPluginMenuItems(Items, FullPluginMenuList, true, LargePluginMenuCount > 1);

            // If there are more than one plugin capable of receiving click notification, we must give the user a way to choose which.
            // For this purpose, create a "Icons" menu with a choice of plugins, with their name and preferred icon.
            if (PluginManager.ConsolidatedPluginList.Count > 1)
            {
                Items.Add(new Separator());

                MenuItem IconSubmenu = new MenuItem();
                IconSubmenu.Header = "Icons";

                foreach (IPluginClient Plugin in PluginManager.ConsolidatedPluginList)
                    AddPluginIcon(IconSubmenu, Plugin);

                // Add this "Icons" menu to the main context menu.
                Items.Add(IconSubmenu);
            }

            // Always add a separator above the exit menu.
            Items.Add(new Separator());

            MenuItem ExitMenu = CreateMenuItem(ExitCommand, "Exit", false, null);
            TaskbarIcon.PrepareMenuItem(ExitMenu, true, true);
            Items.Add(ExitMenu);

            Logger.Write(Category.Debug, "Menu created");

            return Result;
        }

        private static void AddPluginMenu(List<ICommand> fullPluginCommandList, string pluginName, Dictionary<List<MenuItem?>, string> fullPluginMenuList, ref int largePluginMenuCount)
        {
            List<MenuItem?> PluginMenuList = new List<MenuItem?>();
            int VisiblePluginMenuCount = 0;

            foreach (ICommand Command in fullPluginCommandList)
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

            fullPluginMenuList.Add(PluginMenuList, pluginName);
        }

        private void AddPluginIcon(MenuItem iconSubmenu, IPluginClient plugin)
        {
            Guid SubmenuGuid = plugin.Guid;

            // Protection against plugins reusing a guid...
            if (!IconSelectionTable.ContainsKey(SubmenuGuid))
            {
                RoutedUICommand SubmenuCommand = new RoutedUICommand();
                SubmenuCommand.Text = PluginManager.GuidToString(SubmenuGuid);

                // The currently preferred plugin will be checked as so.
                string SubmenuHeader = Plugin.Name;
                bool SubmenuIsChecked = SubmenuGuid == PluginManager.PreferredPluginGuid;
                Bitmap SubmenuIcon = Plugin.SelectionBitmap;

                AddMenuCommand(SubmenuCommand, OnCommandSelectPreferred);
                MenuItem PluginMenu = CreateMenuItem(SubmenuCommand, SubmenuHeader, SubmenuIsChecked, SubmenuIcon);
                TaskbarIcon.PrepareMenuItem(PluginMenu, true, true);
                iconSubmenu.Items.Add(PluginMenu);

                IconSelectionTable.Add(SubmenuGuid, SubmenuCommand);
            }
        }

        private static void AddPluginMenuItems(ItemCollection items, Dictionary<List<MenuItem?>, string> fullPluginMenuList, bool largeSubmenu, bool useSubmenus)
        {
            bool AddSeparator = true;

            foreach (KeyValuePair<List<MenuItem?>, string> Entry in fullPluginMenuList)
            {
                List<MenuItem?> PluginMenuList = Entry.Key;

                // Only add the category of plugin menu targetted by this call.
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
            Logger.Write(Category.Debug, "OnMenuOpening");

            string ExeName = Assembly.GetEntryAssembly().Location;

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

        private static void UpdateMenu()
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

        /// <summary>
        /// Gets the icon to display in the taskbar.
        /// </summary>
        public TaskbarIcon AppTaskbarIcon { get; private set; } = TaskbarIcon.Empty;

        /// <summary>
        /// Gets the text to display in the plugin menu to load at startup.
        /// </summary>
        private const string LoadAtStartupHeader = "Load at startup";

        /// <summary>
        /// Gets the text to display in the plugin menu to not load at startup.
        /// </summary>
        private const string RemoveFromStartupHeader = "Remove from startup";
        private ICommand LoadAtStartupCommand = new RoutedCommand();
        private ICommand ExitCommand = new RoutedCommand();
        private Dictionary<Guid, ICommand> IconSelectionTable = new Dictionary<Guid, ICommand>();
        private bool IsIconChanged;
        private bool IsToolTipChanged;
    }
}
