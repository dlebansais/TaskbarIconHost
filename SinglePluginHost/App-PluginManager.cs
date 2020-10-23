namespace TaskbarIconHost
{
    using System;
    using RegistryTools;

    /// <summary>
    /// Represents an application that can manage a plugin having an icon in the taskbar.
    /// </summary>
    public partial class App
    {
        private bool InitPlugInManager()
        {
            if (!PluginManager.Init(IsElevated, AssemblyName, Plugin.Guid, Owner.Dispatcher, Logger))
                return false;

            // In the case of a single plugin version, this code won't do anything.
            // However, if several single plugin versions run concurrently, the last one to run will be the preferred one for another plugin host.
            GlobalSettings = new RegistryTools.Settings("TaskbarIconHost", "Main Settings", Logger);

            try
            {
                // Assign the guid with a value taken from the registry. The try/catch blocks allows us to ignore invalid ones.
                GlobalSettings.GetString(PreferredPluginSettingName, PluginManager.GuidToString(Guid.Empty), out string PreferredPluginGuid);
                PluginManager.PreferredPluginGuid = new Guid(PreferredPluginGuid);
            }
            catch
            {
            }

            return true;
        }

        private void StopPlugInManager()
        {
            // Save this plugin guid so that the last saved will be the preferred one if there is another plugin host.
            GlobalSettings?.SetString(PreferredPluginSettingName, PluginManager.GuidToString(PluginManager.PreferredPluginGuid));
            PluginManager.Shutdown();

            CleanupPlugInManager();
        }

        private void CleanupPlugInManager()
        {
            using (Settings? Settings = GlobalSettings)
            {
                GlobalSettings = null;
            }
        }

        private const string PreferredPluginSettingName = "PreferredPlugin";
        private Settings? GlobalSettings;
    }
}
