namespace TaskbarIconHost
{
    public interface IPluginSettings
    {
        /// <summary>
        /// Check if a boolean value exists in the registry.
        /// </summary>
        /// <param name="valueName">Value name</param>
        /// <returns>True if the value exists (and can be read), false otherwise</returns>
        bool IsBoolKeySet(string valueName);

        /// <summary>
        /// Read a boolean value from the registry.
        /// </summary>
        /// <param name="valueName">Value name</param>
        /// <param name="defaultValue">Default if the value doesn't exist</param>
        /// <returns>The value in the registry, or <paramref name="defaultValue"/> if it doesn't</returns>
        bool GetSettingBool(string valueName, bool defaultValue);

        /// <summary>
        /// Set a boolean value in the registry.
        /// </summary>
        /// <param name="valueName">Value name</param>
        /// <param name="value">The value to set</param>
        void SetSettingBool(string valueName, bool value);

        /// <summary>
        /// Read an integer value from the registry.
        /// </summary>
        /// <param name="valueName">Value name</param>
        /// <param name="defaultValue">Default if the value doesn't exist</param>
        /// <returns>The value in the registry, or <paramref name="defaultValue"/> if it doesn't</returns>
        int GetSettingInt(string valueName, int defaultValue);

        /// <summary>
        /// Set an integer value in the registry.
        /// </summary>
        /// <param name="valueName">Value name</param>
        /// <param name="value">The value to set</param>
        void SetSettingInt(string valueName, int value);

        /// <summary>
        /// Read a string value from the registry.
        /// </summary>
        /// <param name="valueName">Value name</param>
        /// <param name="defaultValue">Default if the value doesn't exist</param>
        /// <returns>The value in the registry, or <paramref name="defaultValue"/> if it doesn't</returns>
        string GetSettingString(string valueName, string defaultValue);

        /// <summary>
        /// Set a string value in the registry.
        /// </summary>
        /// <param name="valueName">Value name</param>
        /// <param name="value">The value to set</param>
        void SetSettingString(string valueName, string value);

        /// <summary>
        /// Read a double value from the registry.
        /// </summary>
        /// <param name="valueName">Value name</param>
        /// <param name="defaultValue">Default if the value doesn't exist</param>
        /// <returns>The value in the registry, or <paramref name="defaultValue"/> if it doesn't</returns>
        double GetSettingDouble(string valueName, double defaultValue);

        /// <summary>
        /// Set a double value in the registry.
        /// </summary>
        /// <param name="valueName">Value name</param>
        /// <param name="value">The value to set</param>
        void SetSettingDouble(string valueName, double value);
    }
}
