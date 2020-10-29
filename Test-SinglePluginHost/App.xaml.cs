namespace TestSinglePluginHost
{
    using System.Windows;
    using TestPlugin;

#pragma warning disable SA1601 // Partial elements should be documented
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public partial class App : Application
    {
        public App()
        {
            Plugin = new EmptyPlugin();
            PluginApp = new TaskbarIconHost.App(this, Plugin, "Test-Plugin");
        }

        private EmptyPlugin Plugin;
        private TaskbarIconHost.App PluginApp;
    }
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
#pragma warning restore SA1601 // Partial elements should be documented
}
