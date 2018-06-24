namespace TaskbarIconHost
{
    public interface IPluginSettings
    {
        bool IsBoolKeySet(string valueName);
        bool GetSettingBool(string valueName, bool defaultValue);
        void SetSettingBool(string valueName, bool value);
        int GetSettingInt(string valueName, int defaultValue);
        void SetSettingInt(string valueName, int value);
        string GetSettingString(string valueName, string defaultValue);
        void SetSettingString(string valueName, string value);
        double GetSettingDouble(string valueName, double defaultValue);
        void SetSettingDouble(string valueName, double value);
    }
}
