namespace TestSinglePluginHost;

using System;
using System.Windows;
using TestPlugin;

#pragma warning disable SA1601 // Partial elements should be documented
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1515 // Consider making public types internal
public partial class App : Application, IDisposable
{
    public App()
    {
        Plugin = new EmptyPlugin();
        PluginApp = new TaskbarIconHost.App(this, Plugin, "Test-Plugin");
    }

    private readonly EmptyPlugin Plugin;
    private readonly TaskbarIconHost.App PluginApp;
    private bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                PluginApp.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
#pragma warning restore CA1515 // Consider making public types internal
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
#pragma warning restore SA1601 // Partial elements should be documented

