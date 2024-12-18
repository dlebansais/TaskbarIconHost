namespace TestPlugin;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

/// <summary>
/// This sample code demonstrates how to implement a default plugin that does nothing.
/// </summary>
public class EmptyPlugin : TaskbarIconHost.IPluginClient
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public string Name => "Empty";

    public Guid PluginGuid => new("{C54607CF-0D48-4105-BB17-D30C0B0A8A2A}");

    public bool RequireElevated => false;

    public bool HasClickHandler => false;

    public void Initialize(bool isElevated, Dispatcher dispatcher, RegistryTools.Settings settings, ILogger logger)
    {
    }

    public IReadOnlyCollection<ICommand?> CommandList
    {
        get
        {
            List<ICommand> Result = [new RoutedUICommand(Properties.Resources.Test, "test", GetType())];
            return Result.AsReadOnly();
        }
    }

    public bool GetIsMenuChanged(bool beforeMenuOpening) => false;

    public string GetMenuHeader(ICommand command) => command is RoutedUICommand AsRoutedUICommand ? AsRoutedUICommand.Text : string.Empty;

    public bool GetMenuIsVisible(ICommand command) => true;

    public bool GetMenuIsEnabled(ICommand command) => true;

    public bool GetMenuIsChecked(ICommand command) => false;

    public Bitmap? GetMenuIcon(ICommand command) => null;

    public void OnMenuOpening()
    {
    }

    public void OnExecuteCommand(ICommand command)
    {
    }

    public bool GetIsIconChanged() => false;

    public Icon Icon
    {
        get
        {
            System.Reflection.Assembly ExecutingAssembly = System.Reflection.Assembly.GetExecutingAssembly()!;
            using System.IO.Stream Stream = ExecutingAssembly.GetManifestResourceStream("TestPlugin.Resources.main.ico")!;
            return new Icon(Stream);
        }
    }

    public Bitmap SelectionBitmap => new(0, 0);

    public void OnIconClicked()
    {
    }

    public bool GetIsToolTipChanged() => false;

    public string ToolTip => string.Empty;

    public void OnActivated()
    {
    }

    public void OnDeactivated()
    {
    }

    public bool CanClose(bool canClose) => true;

    public void BeginClose()
    {
    }

    public bool IsClosed => true;
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
