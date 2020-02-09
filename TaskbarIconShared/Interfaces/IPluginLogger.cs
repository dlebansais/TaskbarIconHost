namespace TaskbarIconHost
{
    /// <summary>
    /// Interface describing the log feature available to plugins.
    /// </summary>
    public interface IPluginLogger
    {
        /// <summary>
        /// Add some text to a log that is displayed asynchronously in real time.
        /// </summary>
        /// <param name="logText">Text to add, without ending line feed or carriage return</param>
        void AddLog(string logText);
    }
}
