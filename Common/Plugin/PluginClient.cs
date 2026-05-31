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

    /// <inheritdoc cref="IPluginClient.Name" />
    public string Name { get; } = name;

    /// <inheritdoc cref="IPluginClient.PluginGuid" />
    public Guid PluginGuid { get; } = pluginGuid;

    /// <inheritdoc cref="IPluginClient.RequireElevated" />
    public bool RequireElevated { get; } = requireElevated;

    /// <inheritdoc cref="IPluginClient.HasClickHandler" />
    public bool HasClickHandler { get; } = hasClickHandler;

    /// <summary>
    /// Gets the plugin unique instance.
    /// </summary>
    public EventWaitHandle? InstanceEvent { get; private set; } = instanceEvent;

    /// <inheritdoc cref="IPluginClient.Initialize(bool, Dispatcher, Settings, ILogger)" />
    public void Initialize(bool isElevated, Dispatcher dispatcher, Settings settings, ILogger logger)
        => PluginManager.ExecutePluginMethod(PluginHandle, nameof(IPluginClient.Initialize), isElevated, dispatcher, settings, logger);

    /// <inheritdoc cref="IPluginClient.CommandList" />
    public IReadOnlyCollection<ICommand?> CommandList => PluginManager.PluginProperty<IReadOnlyCollection<ICommand?>>(PluginHandle, nameof(IPluginClient.CommandList));

    /// <inheritdoc cref="IPluginClient.GetIsMenuChanged(bool)" />
    public bool GetIsMenuChanged(bool beforeMenuOpening) => PluginManager.GetPluginFunctionValue<bool>(PluginHandle, nameof(IPluginClient.GetIsMenuChanged), [beforeMenuOpening]);

    /// <inheritdoc cref="IPluginClient.GetMenuHeader(ICommand)" />
    public string GetMenuHeader(ICommand command) => PluginManager.GetPluginFunctionValue<string>(PluginHandle, nameof(IPluginClient.GetMenuHeader), [command]);

    /// <inheritdoc cref="IPluginClient.GetMenuIsVisible(ICommand)" />
    public bool GetMenuIsVisible(ICommand command) => PluginManager.GetPluginFunctionValue<bool>(PluginHandle, nameof(IPluginClient.GetMenuIsVisible), [command]);

    /// <inheritdoc cref="IPluginClient.GetMenuIsEnabled(ICommand)" />
    public bool GetMenuIsEnabled(ICommand command) => PluginManager.GetPluginFunctionValue<bool>(PluginHandle, nameof(IPluginClient.GetMenuIsEnabled), [command]);

    /// <inheritdoc cref="IPluginClient.GetMenuIsChecked(ICommand)" />
    public bool GetMenuIsChecked(ICommand command) => PluginManager.GetPluginFunctionValue<bool>(PluginHandle, nameof(IPluginClient.GetMenuIsChecked), [command]);

    /// <inheritdoc cref="IPluginClient.GetMenuIcon(ICommand)" />
    public Bitmap? GetMenuIcon(ICommand command) => PluginManager.GetPluginFunctionValue<Bitmap>(PluginHandle, nameof(IPluginClient.GetMenuIcon), [command]);

    /// <inheritdoc cref="IPluginClient.OnMenuOpening" />
    public void OnMenuOpening() => PluginManager.ExecutePluginMethod(PluginHandle, nameof(IPluginClient.OnMenuOpening));

    /// <inheritdoc cref="IPluginClient.OnExecuteCommand" />
    public void OnExecuteCommand(ICommand command) => PluginManager.ExecutePluginMethod(PluginHandle, nameof(IPluginClient.OnExecuteCommand), command);

    /// <inheritdoc cref="IPluginClient.GetIsIconChanged" />
    public bool GetIsIconChanged() => PluginManager.GetPluginFunctionValue<bool>(PluginHandle, nameof(IPluginClient.GetIsIconChanged));

    /// <inheritdoc cref="IPluginClient.Icon" />
    public Icon Icon => PluginManager.PluginProperty<Icon>(PluginHandle, nameof(IPluginClient.Icon));

    /// <inheritdoc cref="IPluginClient.SelectionBitmap" />
    public Bitmap SelectionBitmap => PluginManager.PluginProperty<Bitmap>(PluginHandle, nameof(IPluginClient.SelectionBitmap));

    /// <inheritdoc cref="IPluginClient.OnIconClicked" />
    public void OnIconClicked() => PluginManager.ExecutePluginMethod(PluginHandle, nameof(IPluginClient.OnIconClicked));

    /// <inheritdoc cref="IPluginClient.GetIsToolTipChanged" />
    public bool GetIsToolTipChanged() => PluginManager.GetPluginFunctionValue<bool>(PluginHandle, nameof(IPluginClient.GetIsToolTipChanged));

    /// <inheritdoc cref="IPluginClient.ToolTip" />
    public string ToolTip => PluginManager.PluginProperty<string>(PluginHandle, nameof(IPluginClient.ToolTip));

    /// <inheritdoc cref="IPluginClient.OnActivated" />
    public void OnActivated() => PluginManager.ExecutePluginMethod(PluginHandle, nameof(IPluginClient.OnActivated));

    /// <inheritdoc cref="IPluginClient.OnDeactivated" />
    public void OnDeactivated() => PluginManager.ExecutePluginMethod(PluginHandle, nameof(IPluginClient.OnDeactivated));

    /// <inheritdoc cref="IPluginClient.CanClose(bool)" />
    public bool CanClose(bool canClose) => PluginManager.GetPluginFunctionValue<bool>(PluginHandle, nameof(IPluginClient.CanClose), canClose);

    /// <inheritdoc cref="IPluginClient.BeginClose" />
    public void BeginClose()
    {
        InstanceEvent?.Dispose();
        InstanceEvent = null;

        PluginManager.ExecutePluginMethod(PluginHandle, nameof(IPluginClient.BeginClose));
    }

    /// <inheritdoc cref="IPluginClient.IsClosed" />
    public bool IsClosed => PluginManager.PluginProperty<bool>(PluginHandle, nameof(IPluginClient.IsClosed));
}
