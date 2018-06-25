namespace TaskbarIconHost
{
    public interface IPluginLogger
    {
        /// <summary>
        /// Add some text to a log that is displayed asynchronously in real time.
        /// </summary>
        /// <param name="text">Text to add, without ending line feed or carriage return</param>
        void AddLog(string text);
    }
}
