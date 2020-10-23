namespace TaskbarIconHost
{
    using System;
    using System.Security.Cryptography;

    /// <summary>
    /// Represents an application that can manage plugins having an icon in the taskbar.
    /// </summary>
    public partial class App
    {
        private bool InitPlugInManager(OidCollection oidCheckList, out int exitCode, out bool isBadSignature)
        {
            if (!PluginManager.Init(IsElevated, PluginDetails.AssemblyName, PluginDetails.Guid, Dispatcher, Logger, oidCheckList, out exitCode, out isBadSignature))
                return false;

            // Assign the guid with a value taken from the registry.
            GlobalSettings.GetGuid(PreferredPluginSettingName, Guid.Empty, out Guid PreferredPluginGuid);
            PluginManager.PreferredPluginGuid = PreferredPluginGuid;
            exitCode = 0;

            return true;
        }

        private void StopPlugInManager()
        {
            // Save this plugin guid so that the last saved will be the preferred one if there is another plugin host.
            GlobalSettings.SetString(PreferredPluginSettingName, PluginManager.GuidToString(PluginManager.PreferredPluginGuid));
            PluginManager.Shutdown();

            CleanupPlugInManager();
        }

        private void CleanupPlugInManager()
        {
            using (GlobalSettings)
            {
            }
        }

        private const string PreferredPluginSettingName = "PreferredPlugin";

        // In the case of a single plugin version, this code won't do anything.
        // However, if several single plugin versions run concurrently, the last one to run will be the preferred one for another plugin host.
        private RegistryTools.Settings GlobalSettings = new RegistryTools.Settings("TaskbarIconHost", "Main Settings", Logger);
    }
}
