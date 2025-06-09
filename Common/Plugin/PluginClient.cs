namespace TaskbarIconHost;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using RegistryTools;

/// <summary>
/// Represents an instance of a plugin.
/// </summary>
/// <param name="pluginHandle">The plugin handle.</param>
/// <param name="name">The plugin name.</param>
/// <param name="pluginGuid">The plugin GUID.</param>
/// <param name="requireElevated">True if the plugin require being run as administrator.</param>
/// <param name="hasClickHandler">True if the plugin handles right-click on the taskbar icon.</param>
/// <param name="instanceEvent">The plugin unique instance.</param>
internal class PluginClient(object pluginHandle, string name, Guid pluginGuid, bool requireElevated, bool hasClickHandler, [IDisposableAnalyzers.AcquireOwnership] EventWaitHandle? instanceEvent) : IPluginClient
{
    /// <summary>
    /// Gets the plugin handle.
    /// </summary>
    public object PluginHandle { get; } = pluginHandle;

    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the plugin GUID.
    /// </summary>
    public Guid PluginGuid { get; } = pluginGuid;

    /// <summary>
    /// Gets a value indicating whether the plugin require being run as administrator.
    /// </summary>
    public bool RequireElevated { get; } = requireElevated;

    /// <summary>
    /// Gets a value indicating whether the plugin handles right-click on the taskbar icon.
    /// </summary>
    public bool HasClickHandler { get; } = hasClickHandler;

    /// <summary>
    /// Gets the plugin unique instance.
    /// </summary>
    public EventWaitHandle? InstanceEvent { get; private set; } = instanceEvent;

    /// <summary>
    /// Initializes the plugin.
    /// </summary>
    /// <param name="isElevated">True if the caller is executing in administrator mode.</param>
    /// <param name="dispatcher">A dispatcher that can be used to synchronize with the UI.</param>
    /// <param name="settings">An interface to read and write settings in the registry.</param>
    /// <param name="logger">An interface to log events asynchronously.</param>
    public void Initialize(bool isElevated, Dispatcher dispatcher, Settings settings, ILogger logger)
        => PluginManager.ExecutePluginMethod(PluginHandle, nameof(IPluginClient.Initialize), isElevated, dispatcher, settings, logger);

    /// <summary>
    /// Gets the list of commands that the plugin can receive when an item is clicked in the context menu.
    /// </summary>
    public IReadOnlyCollection<ICommand?> CommandList => PluginManager.PluginProperty<IReadOnlyCollection<ICommand?>>(PluginHandle, nameof(IPluginClient.CommandList));

    /// <summary>
    /// Reads a flag indicating if the state of a menu item has changed. The flag should be reset upon return until another change occurs.
    /// </summary>
    /// <param name="beforeMenuOpening">True if this function is called right before the context menu is opened by the user, false otherwise.</param>
    /// <returns>True if a menu item state has changed since the last call, false otherwise.</returns>
    public bool GetIsMenuChanged(bool beforeMenuOpening) => PluginManager.GetPluginFunctionValue<bool>(PluginHandle, nameof(IPluginClient.GetIsMenuChanged), beforeMenuOpening);

    /// <summary>
    /// Readss the text of a menu item associated to <paramref name="command"/>.
    /// </summary>
    /// <param name="command">The command associated to the menu item.</param>
    /// <returns>The menu text.</returns>
    public string GetMenuHeader(ICommand command) => PluginManager.GetPluginFunctionValue<string>(PluginHandle, nameof(IPluginClient.GetMenuHeader), command);

    /// <summary>
    /// Reads the state of a menu item associated to <paramref name="command"/>.
    /// </summary>
    /// <param name="command">The command associated to the menu item.</param>
    /// <returns>True if the menu item should be visible to the user, false if it should be hidden.</returns>
    public bool GetMenuIsVisible(ICommand command) => PluginManager.GetPluginFunctionValue<bool>(PluginHandle, nameof(IPluginClient.GetMenuIsVisible), command);

    /// <summary>
    /// Reads the state of a menu item associated to <paramref name="command"/>.
    /// </summary>
    /// <param name="command">The command associated to the menu item.</param>
    /// <returns>True if the menu item should appear enabled, false if it should be disabled.</returns>
    public bool GetMenuIsEnabled(ICommand command) => PluginManager.GetPluginFunctionValue<bool>(PluginHandle, nameof(IPluginClient.GetMenuIsEnabled), command);

    /// <summary>
    /// Reads the state of a menu item associated to <paramref name="command"/>.
    /// </summary>
    /// <param name="command">The command associated to the menu item.</param>
    /// <returns>True if the menu item is checked, false otherwise.</returns>
    public bool GetMenuIsChecked(ICommand command) => PluginManager.GetPluginFunctionValue<bool>(PluginHandle, nameof(IPluginClient.GetMenuIsChecked), command);

    /// <summary>
    /// Reads the icon of a menu item associated to <paramref name="command"/>.
    /// </summary>
    /// <param name="command">The command associated to the menu item.</param>
    /// <returns>The icon to display with the menu text, null if none.</returns>
    public Bitmap? GetMenuIcon(ICommand command) => PluginManager.GetPluginFunctionValue<Bitmap>(PluginHandle, nameof(IPluginClient.GetMenuIcon), command);

    /// <summary>
    /// This method is called before the menu is displayed, but after changes in the menu have been evaluated.
    /// If a plugin wants to perform expensive checks that must be reflected in the menu, it should do so when <see cref="GetIsMenuChanged"/> is called, or asynchronously.
    /// </summary>
    public void OnMenuOpening() => PluginManager.ExecutePluginMethod(PluginHandle, nameof(IPluginClient.OnMenuOpening));

    /// <summary>
    /// Request for <paramref name="command"/> to be executed.
    /// The menu state may change as a result of the command (such as a menu item checked or unchecked). Therefore, the plugin should expect a call to <see cref="GetIsMenuChanged"/> soon after this handler returns.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    public void OnExecuteCommand(ICommand command) => PluginManager.ExecutePluginMethod(PluginHandle, nameof(IPluginClient.OnExecuteCommand), command);

    /// <summary>
    /// Reads a flag indicating if the plugin icon, that might reflect the state of the plugin, has changed. The flag should be reset upon return until another change occurs.
    /// Note that this function is called from an asynchronous thread, therefore it should mostly just read and reset a flag.
    /// </summary>
    /// <returns>True if the icon has changed since the last call, false otherwise.</returns>
    public bool GetIsIconChanged() => PluginManager.GetPluginFunctionValue<bool>(PluginHandle, nameof(IPluginClient.GetIsIconChanged));

    /// <summary>
    /// Gets the the icon displayed in the taskbar. Based on the current Windows style at the time of this writing (2018), it should be a mainly white, small (16x16) icon.
    /// </summary>
    public Icon Icon => PluginManager.PluginProperty<Icon>(PluginHandle, nameof(IPluginClient.Icon));

    /// <summary>
    /// Gets the bitmap displayed in the preferred plugin menu. Based on the current Windows style at the time of this writing (2018), it should be a mainly black, small (16x16) bitmap.
    /// </summary>
    public Bitmap SelectionBitmap => PluginManager.PluginProperty<Bitmap>(PluginHandle, nameof(IPluginClient.SelectionBitmap));

    /// <summary>
    /// Requests for the main plugin operation to be executed. This happens when the user left-clicks the taskbar icon.
    /// There is no requirement to perform anything, and only the preferred plugin is called.
    /// If a plugin never handles this request, this should be reflected in the <see cref="HasClickHandler"/> property set to false. The handler can still be called, but the UI is slightly different to allow plugins that do handle it to be in front, unless the user changes it.
    /// </summary>
    public void OnIconClicked() => PluginManager.ExecutePluginMethod(PluginHandle, nameof(IPluginClient.OnIconClicked));

    /// <summary>
    /// Reads a flag indicating if the plugin tooltip, that might reflect the state of the plugin, has changed. The flag should be reset upon return until another change occurs.
    /// Note that this function is called from an asynchronous thread, therefore it should mostly just read and reset a flag.
    /// </summary>
    /// <returns>True if the tooltip has changed since the last call, false otherwise.</returns>
    public bool GetIsToolTipChanged() => PluginManager.GetPluginFunctionValue<bool>(PluginHandle, nameof(IPluginClient.GetIsToolTipChanged));

    /// <summary>
    /// Gets the free text that indicate the state of the plugin. Can be null.
    /// If the text is too large or badly formatted, the caller automatically modifies it before presenting it to the user.
    /// </summary>
    public string ToolTip => PluginManager.PluginProperty<string>(PluginHandle, nameof(IPluginClient.ToolTip));

    /// <summary>
    /// Called when the taskbar is getting the application focus. Happens before the icon is clicked if the taskbar didn't have the focus.
    /// </summary>
    public void OnActivated() => PluginManager.ExecutePluginMethod(PluginHandle, nameof(IPluginClient.OnActivated));

    /// <summary>
    /// Called when the taskbar is loosing the application focus. This can be used to close a UI.
    /// </summary>
    public void OnDeactivated() => PluginManager.ExecutePluginMethod(PluginHandle, nameof(IPluginClient.OnDeactivated));

    /// <summary>
    /// Requests to close and terminate a plugin. All plugins receive this request, even if many of them return false.
    /// If at least one of the plugin returns false, the close request is globally denied. However, the system may eventually force the close, so it's a good time to schedule data to be saved on persistent storage.
    /// Note that even if this is not enforced, plugins are expected to be good citizens and to return immediately.
    /// </summary>
    /// <param name="canClose">True if no plugin called before this one has returned false, false if one of them has.</param>
    /// <returns>True if the plugin can be safely terminated, false if the request is denied.</returns>
    public bool CanClose(bool canClose) => PluginManager.GetPluginFunctionValue<bool>(PluginHandle, nameof(IPluginClient.CanClose), canClose);

    /// <summary>
    /// Requests to begin closing the plugin. This request is sent if all plugins have returned true to the <see cref="CanClose"/> request.
    /// Note that even if this is not enforced, plugins are expected to be good citizens and to return immediately. A plugin notifies that all closing operations are complete by setting the <see cref="IsClosed"/> property to true.
    /// </summary>
    public void BeginClose()
    {
        InstanceEvent?.Dispose();
        InstanceEvent = null;

        PluginManager.ExecutePluginMethod(PluginHandle, nameof(IPluginClient.BeginClose));
    }

    /// <summary>
    /// Gets a value indicating whether the plugin is closed. See <see cref="CanClose"/> and <see cref="BeginClose"/>.
    /// </summary>
    public bool IsClosed => PluginManager.PluginProperty<bool>(PluginHandle, nameof(IPluginClient.IsClosed));
}
