namespace TaskbarIconHost;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

/// <summary>
/// This interface describes how a plugin is called by the host, and how the plugin should behave.
/// All plugins must implement this interface to be loaded.
/// </summary>
public interface IPluginClient
{
    /// <summary>
    /// Gets the plugin name. This value is read once before <see cref="Initialize"/> is called, and cached. Null or an empty string is not allowed, in this case the plugin will not load.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the plugin unique ID. This value is read once before <see cref="Initialize"/> is called, and cached. The empty guid is not allowed, in this case the plugin will not load.
    /// </summary>
    Guid PluginGuid { get; }

    /// <summary>
    /// Gets a value indicating whether the plugin require elevated (administrator) mode to operate. This value is read once before <see cref="Initialize"/> is called, and cached.
    /// </summary>
    bool RequireElevated { get; }

    /// <summary>
    /// Gets a value indicating whether the plugin want to handle clicks on the taskbar icon. This value is read once before <see cref="Initialize"/> is called, and cached. This setting only has a minor impact on the UI, since the <see cref="OnIconClicked"/> event handler is called nevertheless.
    /// </summary>
    bool HasClickHandler { get; }

    /// <summary>
    /// Called once at startup, to initialize the plugin.
    /// The <paramref name="isElevated"/> parameter indicates if the plugin is started in administrator mode. A plugin with <see cref="RequireElevated"/> set should reflect this runtime value in the UI, for example with disabled menu items for operations that require administrator mode. A plugin that doesn't require administrator mode can safely ignore it.
    /// Upon return, the plugin is expected to have filled the following information in properties:
    /// <see cref="CommandList"/>, a list of commands used to create the plugin menu.
    /// <see cref="Icon"/>, the icon to display in the taskbar.
    /// <see cref="SelectionBitmap"/>, a bitmap displayed in the menu that enables the user to select their preferred plugin.
    /// <see cref="ToolTip"/>, a tooltip displayed when the user hovers with the mouse over the taskbatr icon.
    /// <see cref="IsClosed"/>, should be false.
    /// </summary>
    /// <param name="isElevated">True if the caller is executing in administrator mode.</param>
    /// <param name="dispatcher">A dispatcher that can be used to synchronize with the UI.</param>
    /// <param name="settings">An interface to read and write settings in the registry.</param>
    /// <param name="logger">An interface to log events asynchronously.</param>
    void Initialize(bool isElevated, Dispatcher dispatcher, RegistryTools.Settings settings, ILogger logger);

    /// <summary>
    /// Gets the list of commands that the plugin can receive when an item is clicked in the context menu.
    /// Null is allowed, even more than once, and is a request to insert a separator between menu items associated to commands.
    /// This property is read once after <see cref="Initialize"/> returns, and the value is cached.
    /// </summary>
    IReadOnlyCollection<ICommand?> CommandList { get; }

    /// <summary>
    /// Reads a flag indicating if the state of a menu item has changed. The flag should be reset upon return until another change occurs.
    /// </summary>
    /// <returns>True if a menu item state has changed since the last call, false otherwise.</returns>
    /// <param name="beforeMenuOpening">True if this function is called right before the context menu is opened by the user, false otherwise.</param>
    bool GetIsMenuChanged(bool beforeMenuOpening);

    /// <summary>
    /// Reads the text of a menu item associated to <paramref name="command"/>.
    /// The text is cached until <see cref="GetIsMenuChanged"/> returns true, then it is read again.
    /// </summary>
    /// <param name="command">The command associated to the menu item.</param>
    /// <returns>The menu text.</returns>
    string GetMenuHeader(ICommand command);

    /// <summary>
    /// Reads the state of a menu item associated to <paramref name="command"/>.
    /// The state is cached until <see cref="GetIsMenuChanged"/> returns true, then it is read again.
    /// Note that the context menu is organized around the state after the call to <see cref="Initialize"/>. If items get shown/hidden afterward, it can give raise to a weird menu displayed, such as two separators one upon the other.
    /// </summary>
    /// <param name="command">The command associated to the menu item.</param>
    /// <returns>True if the menu item should be visible to the user, false if it should be hidden.</returns>
    bool GetMenuIsVisible(ICommand command);

    /// <summary>
    /// Reads the state of a menu item associated to <paramref name="command"/>.
    /// The state is cached until <see cref="GetIsMenuChanged"/> returns true, then it is read again.
    /// </summary>
    /// <param name="command">The command associated to the menu item.</param>
    /// <returns>True if the menu item should appear enabled, false if it should be disabled.</returns>
    bool GetMenuIsEnabled(ICommand command);

    /// <summary>
    /// Reads the state of a menu item associated to <paramref name="command"/>.
    /// The state is cached until <see cref="GetIsMenuChanged"/> returns true, then it is read again.
    /// </summary>
    /// <param name="command">The command associated to the menu item.</param>
    /// <returns>True if the menu item is checked, false otherwise.</returns>
    bool GetMenuIsChecked(ICommand command);

    /// <summary>
    /// Reads the icon of a menu item associated to <paramref name="command"/>.
    /// The icon is cached until <see cref="GetIsMenuChanged"/> returns true, then it is read again.
    /// </summary>
    /// <param name="command">The command associated to the menu item.</param>
    /// <returns>The icon to display with the menu text, null if none.</returns>
    Bitmap? GetMenuIcon(ICommand command);

    /// <summary>
    /// This method is called before the menu is displayed, but after changes in the menu have been evaluated.
    /// If a plugin wants to perform expensive checks that must be reflected in the menu, it should do so when <see cref="GetIsMenuChanged"/> is called, or asynchronously.
    /// </summary>
    void OnMenuOpening();

    /// <summary>
    /// Requests for <paramref name="command"/> to be executed.
    /// The menu state may change as a result of the command (such as a menu item checked or unchecked). Therefore, the plugin should expect a call to <see cref="GetIsMenuChanged"/> soon after this handler returns.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    void OnExecuteCommand(ICommand command);

    /// <summary>
    /// Reads a flag indicating if the plugin icon, that might reflect the state of the plugin, has changed. The flag should be reset upon return until another change occurs.
    /// Note that this function is called from an asynchronous thread, therefore it should mostly just read and reset a flag.
    /// </summary>
    /// <returns>True if the icon has changed since the last call, false otherwise.</returns>
    bool GetIsIconChanged();

    /// <summary>
    /// Gets the icon displayed in the taskbar. Based on the current Windows style at the time of this writing (2018), it should be a mainly white, small (16x16) icon.
    /// </summary>
    Icon Icon { get; }

    /// <summary>
    /// Gets the bitmap displayed in the preferred plugin menu. Based on the current Windows style at the time of this writing (2018), it should be a mainly black, small (16x16) bitmap.
    /// </summary>
    Bitmap SelectionBitmap { get; }

    /// <summary>
    /// Requests for the main plugin operation to be executed. This happens when the user left-clicks the taskbar icon.
    /// There is no requirement to perform anything, and only the preferred plugin is called.
    /// If a plugin never handles this request, this should be reflected in the <see cref="HasClickHandler"/> property set to false. The handler can still be called, but the UI is slightly different to allow plugins that do handle it to be in front, unless the user changes it.
    /// </summary>
    void OnIconClicked();

    /// <summary>
    /// Reads a flag indicating if the plugin tooltip, that might reflect the state of the plugin, has changed. The flag should be reset upon return until another change occurs.
    /// Note that this function is called from an asynchronous thread, therefore it should mostly just read and reset a flag.
    /// </summary>
    /// <returns>True if the tooltip has changed since the last call, false otherwise.</returns>
    bool GetIsToolTipChanged();

    /// <summary>
    /// Gets the free text that indicate the state of the plugin. Can be null.
    /// If the text is too large or badly formatted, the caller automatically modifies it before presenting it to the user.
    /// </summary>
    string ToolTip { get; }

    /// <summary>
    /// Called when the taskbar is getting the application focus. Happens before the icon is clicked if the taskbar didn't have the focus.
    /// </summary>
    void OnActivated();

    /// <summary>
    /// Called when the taskbar is loosing the application focus. This can be used to close a UI.
    /// </summary>
    void OnDeactivated();

    /// <summary>
    /// Requests to close and terminate a plugin. All plugins receive this request, even if many of them return false.
    /// If at least one of the plugin returns false, the close request is globally denied. However, the system may eventually force the close, so it's a good time to schedule data to be saved on persistent storage.
    /// Note that even if this is not enforced, plugins are expected to be good citizens and to return immediately.
    /// </summary>
    /// <param name="canClose">True if no plugin called before this one has returned false, false if one of them has.</param>
    /// <returns>True if the plugin can be safely terminated, false if the request is denied.</returns>
    bool CanClose(bool canClose);

    /// <summary>
    /// Requests to begin closing the plugin. This request is sent if all plugins have returned true to the <see cref="CanClose"/> request.
    /// Note that even if this is not enforced, plugins are expected to be good citizens and to return immediately. A plugin notifies that all closing operations are complete by setting the <see cref="IsClosed"/> property to true.
    /// </summary>
    void BeginClose();

    /// <summary>
    /// Gets a value indicating whether the plugin is closed. See <see cref="CanClose"/> and <see cref="BeginClose"/>.
    /// </summary>
    bool IsClosed { get; }
}
