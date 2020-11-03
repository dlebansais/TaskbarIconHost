namespace TaskbarIconHost
{
    using System;
    using System.Security.Cryptography;
    using RegistryTools;

    /// <summary>
    /// Represents an application that can manage plugins having an icon in the taskbar.
    /// </summary>
    public partial class App
    {
        private bool InitPlugInManager(OidCollection oidCheckList, out int exitCode, out bool isBadSignature)
        {
            if (!PluginManager.Init(IsElevated, AssemblyName, AppGuid, Owner.Dispatcher, Logger, oidCheckList, out exitCode, out isBadSignature))
                return false;

            // In the case of a single plugin version, this code won't do anything.
            // However, if several single plugin versions run concurrently, the last one to run will be the preferred one for another plugin host.
            GlobalSettings = new Settings("TaskbarIconHost", "Main Settings", Logger);

            try
            {
                // Assign the guid with a value taken from the registry. The try/catch blocks allows us to ignore invalid ones.
                GlobalSettings.GetGuid(PreferredPluginSettingName, Guid.Empty, out Guid PreferredPluginGuid);
                PluginManager.PreferredPluginGuid = PreferredPluginGuid;
            }
            catch
            {
            }

            exitCode = 0;

            return true;
        }

        private void StopPlugInManager()
        {
            // Save this plugin guid so that the last saved will be the preferred one if there is another plugin host.
            GlobalSettings?.SetGuid(PreferredPluginSettingName, PluginManager.PreferredPluginGuid);
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
