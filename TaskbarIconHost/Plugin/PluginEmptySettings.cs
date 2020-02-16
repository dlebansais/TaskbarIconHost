namespace TaskbarIconHost
{
    using System;

    internal class PluginEmptySettings : IPluginSettings
    {
        #region Init
        public PluginEmptySettings(IPluginLogger logger)
        {
            Logger = logger;
        }

        private IPluginLogger Logger;
        #endregion

        #region Properties
#pragma warning disable CA1822 // Mark members as static
        public string PluginName { get; } = string.Empty;
#pragma warning restore CA1822 // Mark members as static
        #endregion

        #region Settings
        public void RenewKey()
        {
        }

        public bool IsBoolKeySet(string valueName)
        {
            return false;
        }

        public bool GetSettingBool(string valueName, bool defaultValue)
        {
            return defaultValue;
        }

        public void SetSettingBool(string valueName, bool value)
        {
        }

        public int GetSettingInt(string valueName, int defaultValue)
        {
            return defaultValue;
        }

        public void SetSettingInt(string valueName, int value)
        {
        }

        public string GetSettingString(string valueName, string defaultValue)
        {
            return defaultValue;
        }

        public void SetSettingString(string valueName, string value)
        {
        }

        public double GetSettingDouble(string valueName, double defaultValue)
        {
            return defaultValue;
        }

        public void SetSettingDouble(string valueName, double value)
        {
        }
        #endregion

        #region Implementation of IDisposable
        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
                DisposeNow();
        }

        private void DisposeNow()
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~PluginEmptySettings()
        {
            Dispose(false);
        }
        #endregion
    }
}
