namespace TaskbarIconHost;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Input;
using Contracts;
using ResourceTools;
using SchedulerTools;
using TaskbarTools;
using static TaskbarIconHost.Properties.Resources;

/// <summary>
/// Represents an application that can manage plugins having an icon in the taskbar.
/// </summary>
public partial class App
{
    private void InitTaskbarIcon()
    {
        Logger.AddLog("InitTaskbarIcon starting");

        // Bind the load at startup/remove from startup command.
        AddMenuCommand(LoadAtStartupCommand, OnCommandLoadAtStartup);

        // Bind the exit command.
        AddMenuCommand(ExitCommand, OnCommandExit);

        // Do the same with all plugin commands.
        foreach (KeyValuePair<IReadOnlyCollection<ICommand?>, string> Entry in PluginManager.FullCommandList)
        {
            IReadOnlyCollection<ICommand?> FullPluginCommandList = Entry.Key;
            foreach (ICommand? Command in FullPluginCommandList)
                AddMenuCommand(Command, OnPluginCommand);
        }

        // Get the preferred icon and tooltip, and build the taskbar context menu.
        Icon? Icon = PluginManager.Icon;
        string? ToolTip = PluginManager.ToolTip;
        ContextMenu ContextMenu = LoadContextMenu();

        Icon = Contract.AssertNotNull(Icon);
        ToolTip = Contract.AssertNotNull(ToolTip);

        // Install the taskbar icon.
        AppTaskbarIcon = TaskbarIcon.Create(Icon, ToolTip, ContextMenu, ContextMenu);
        AppTaskbarIcon.MenuOpening += OnMenuOpening;
        AppTaskbarIcon.IconClicked += OnIconClicked;

        Logger.AddLog("InitTaskbarIcon done");
    }

    private void CleanupTaskbarIcon()
    {
        using TaskbarIcon Icon = AppTaskbarIcon;
        AppTaskbarIcon = TaskbarIcon.Empty;
    }

    private static void AddMenuCommand(ICommand? command, ExecutedRoutedEventHandler executed)
    {
        // Bind the command to the corresponding handler. Requires the menu to be the target of notifications in AppTaskbarIcon.
        if (command is not null && !PluginManager.IsSeparatorCommand(command))
            CommandManager.RegisterClassCommandBinding(typeof(ContextMenu), new CommandBinding(command, executed));
    }

    private ContextMenu LoadContextMenu()
    {
        // Create the taskbar context menu and populate it with menu items, submenus and separators.
        ContextMenu Result = new();
        ItemCollection Items = Result.Items;

        MenuItem LoadAtStartup = LoadContextMenuFromActive();

        TaskbarIcon.PrepareMenuItem(LoadAtStartup, true, true);
        _ = Items.Add(LoadAtStartup);

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

        Contract.RequireNotNull(Assembly.GetEntryAssembly(), out Assembly EntryAssembly);
        string ExeName = EntryAssembly.Location;

        // Create a menu item for the load at startup/remove from startup command, depending on the current situation.
        // UAC-16.png is the recommended 'shield' icon to indicate administrator mode is required for the operation.
        if (Scheduler.IsTaskActive(ExeName))
        {
            if (IsElevated)
            {
                Result = CreateMenuItem(LoadAtStartupCommand, LoadAtStartupHeader, true, null);
            }
            else
            {
                _ = ResourceLoader.Load("UAC-16.png", string.Empty, out Bitmap UACBitmap);
                Result = CreateMenuItem(LoadAtStartupCommand, RemoveFromStartupHeader, false, UACBitmap);
            }
        }
        else
        {
            if (IsElevated)
            {
                Result = CreateMenuItem(LoadAtStartupCommand, LoadAtStartupHeader, false, null);
            }
            else
            {
                _ = ResourceLoader.Load("UAC-16.png", string.Empty, out Bitmap UACBitmap);
                Result = CreateMenuItem(LoadAtStartupCommand, LoadAtStartupHeader, false, UACBitmap);
            }
        }

        return Result;
    }

    private static void AddPluginMenuToContextMenu(out Dictionary<List<MenuItem?>, string> fullPluginMenuList, out int largePluginMenuCount)
    {
        fullPluginMenuList = [];
        largePluginMenuCount = 0;

        foreach (KeyValuePair<IReadOnlyCollection<ICommand?>, string> Entry in PluginManager.FullCommandList)
            AddPluginMenu(Entry.Key, Entry.Value, fullPluginMenuList, ref largePluginMenuCount);
    }

    private static void AddPluginMenu(IReadOnlyCollection<ICommand?> fullPluginCommandList, string pluginName, Dictionary<List<MenuItem?>, string> fullPluginMenuList, ref int largePluginMenuCount)
    {
        List<MenuItem?> PluginMenuList = [];
        int VisiblePluginMenuCount = 0;

        foreach (ICommand? Command in fullPluginCommandList)
        {
            if (Command is null || PluginManager.IsSeparatorCommand(Command))
            {
                PluginMenuList.Add(null); // This will result in the creation of a separator.
            }
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
        }

        if (VisiblePluginMenuCount > 1)
            largePluginMenuCount++;

        fullPluginMenuList.Add(PluginMenuList, pluginName);
    }

    private void SelectPreferredPluginContextMenu(ItemCollection items)
    {
        if (PluginManager.ConsolidatedPluginList.Count > 1)
        {
            _ = items.Add(new Separator());

            MenuItem IconSubmenu = new()
            {
                Header = "Icons",
            };

            foreach (IPluginClient Plugin in PluginManager.ConsolidatedPluginList)
                AddPluginIcon(IconSubmenu, Plugin);

            // Add this "Icons" menu to the main context menu.
            _ = items.Add(IconSubmenu);
        }
    }

    private void AddPluginIcon(MenuItem iconSubmenu, IPluginClient plugin)
    {
        Guid SubmenuGuid = plugin.PluginGuid;

        // Protection against plugins reusing a guid...
        if (!IconSelectionTable.ContainsKey(SubmenuGuid))
        {
            RoutedUICommand SubmenuCommand = new()
            {
                Text = PluginManager.GuidToString(SubmenuGuid),
            };

            // The currently preferred plugin will be checked as so.
            string SubmenuHeader = plugin.Name;
            bool SubmenuIsChecked = SubmenuGuid == PluginManager.PreferredPluginGuid;
            Bitmap SubmenuIcon = plugin.SelectionBitmap;

            AddMenuCommand(SubmenuCommand, OnCommandSelectPreferred);
            MenuItem PluginMenu = CreateMenuItem(SubmenuCommand, SubmenuHeader, SubmenuIsChecked, SubmenuIcon);
            TaskbarIcon.PrepareMenuItem(PluginMenu, true, true);
            _ = iconSubmenu.Items.Add(PluginMenu);

            IconSelectionTable.Add(SubmenuGuid, SubmenuCommand);
        }
    }

    private void AddExitMenuToContextMenu(ItemCollection items)
    {
        // Add a separator above the exit menu if appropriate.
        if (items.Count > 0 && items[^1] is not Separator)
            _ = items.Add(new Separator());

        MenuItem ExitMenu = CreateMenuItem(ExitCommand, "Exit", false, null);
        TaskbarIcon.PrepareMenuItem(ExitMenu, true, true);
        _ = items.Add(ExitMenu);
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
                _ = items.Add(new Separator());

            ItemCollection SubmenuItems;
            if (useSubmenus)
            {
                AddSeparator = false;

                MenuItem PluginSubmenu = new()
                {
                    Header = PluginName,
                };

                SubmenuItems = PluginSubmenu.Items;
                _ = items.Add(PluginSubmenu);
            }
            else
            {
                AddSeparator = true;
                SubmenuItems = items;
            }

            // null in the plugin menu means separator.
            foreach (MenuItem? MenuItem in PluginMenuList)
                _ = SubmenuItems.Add(MenuItem is not null ? MenuItem : new Separator());
        }
    }

    private static MenuItem CreateMenuItem(ICommand command, string header, bool isChecked, Bitmap? icon)
    {
        MenuItem Result = new()
        {
            Header = header,
            Command = command,
            IsChecked = isChecked,
            Icon = icon,
        };

        return Result;
    }

    private void OnMenuOpening(object? sender, EventArgs e)
    {
        Logger.AddLog("OnMenuOpening");

        Contract.RequireNotNull(Assembly.GetEntryAssembly(), out Assembly EntryAssembly);
        string ExeName = EntryAssembly.Location;

        // Update the load at startup menu with the current state (the user can change it directly in the Task Scheduler at any time).
        if (IsElevated)
        {
            TaskbarIcon.SetMenuCheck(LoadAtStartupCommand, Scheduler.IsTaskActive(ExeName));
        }
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
        IReadOnlyCollection<ICommand> ChangedCommandList = PluginManager.GetChangedCommands();

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
                if (MenuIcon is not null)
                {
                    TaskbarIcon.SetMenuCheck(Command, false);
                    TaskbarIcon.SetMenuIcon(Command, MenuIcon);
                }
                else
                {
                    TaskbarIcon.SetMenuCheck(Command, PluginManager.GetMenuIsChecked(Command));
                }
            }
            else
            {
                TaskbarIcon.SetMenuIsVisible(Command, false);
            }
        }
    }

    private void OnIconClicked(object? sender, EventArgs e) => PluginManager.OnIconClicked();

    /// <summary>
    /// Gets the icon to display in the taskbar.
    /// </summary>
    public TaskbarIcon AppTaskbarIcon { get; private set; } = TaskbarIcon.Empty;

    private readonly ICommand LoadAtStartupCommand = new RoutedUICommand("{2E4589C5-620C-42C2-B68D-0E3AA9F9E362}", "LoadAtStartup", typeof(App));
    private readonly ICommand ExitCommand = new RoutedUICommand("{FA8D16C1-16F0-461B-BF10-082DB4D76208}", "Exit", typeof(App));
    private readonly Dictionary<Guid, ICommand> IconSelectionTable = [];
    private bool IsIconChanged;
    private bool IsToolTipChanged;
}
