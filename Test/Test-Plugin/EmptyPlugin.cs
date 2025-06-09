namespace TestPlugin;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Input;
using System.Windows.Threading;
using Contracts;
using Microsoft.Extensions.Logging;

/// <summary>
/// This sample code demonstrates how to implement a default plugin that does nothing.
/// </summary>
public class EmptyPlugin : TaskbarIconHost.IPluginClient, IDisposable
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

    private Icon? IconInternal;

    public Icon Icon
    {
        get
        {
            if (IconInternal is null)
            {
                System.Reflection.Assembly ExecutingAssembly = System.Reflection.Assembly.GetExecutingAssembly();
                using System.IO.Stream? Stream = ExecutingAssembly.GetManifestResourceStream("TestPlugin.Resources.main.ico");
                IconInternal = new Icon(Contract.AssertNotNull(Stream));
            }

            return IconInternal;
        }
    }

    private Bitmap? SelectionBitmapInternal;

    public Bitmap SelectionBitmap
    {
        get
        {
            SelectionBitmapInternal ??= new(0, 0);
            return SelectionBitmapInternal;
        }
    }

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

    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            if (disposing)
            {
                IconInternal?.Dispose();
                IconInternal = null;
                SelectionBitmapInternal?.Dispose();
                SelectionBitmapInternal = null;
            }

            IsDisposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private bool IsDisposed;

#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
