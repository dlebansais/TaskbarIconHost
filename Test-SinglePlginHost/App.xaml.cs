namespace TestSinglePluginHost
{
    using Empty;
    using System.Windows;

    public partial class App : Application
    {
        public App()
        {
            EmptyPlugin Empty = new EmptyPlugin();
            new TaskbarIconHost.App(this, Empty, "Test-Plugin");
        }
    }
}
