namespace TaskbarIconHost
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using Contracts;
    using Tracing;

    /// <summary>
    /// Represents an object that can write log trace on the disk.
    /// </summary>
    public class PluginLogger : ITracer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginLogger"/> class.
        /// </summary>
        public PluginLogger()
        {
#if DEBUG
            IsLogOn = true;
#endif

            try
            {
                Contract.RequireNotNull(Assembly.GetEntryAssembly(), out Assembly EntryAssembly);
                string Location = EntryAssembly.Location;
                Contract.RequireNotNull(Path.GetDirectoryName(Location), out string DirectoryName);
                string SettingFilePath = Path.Combine(DirectoryName, "settings.txt");

                if (File.Exists(SettingFilePath))
                {
                    using (FileStream fs = new FileStream(SettingFilePath, FileMode.Open, FileAccess.Read))
                    {
                        using (StreamReader sr = new StreamReader(fs))
                        {
                            TraceFilePath = sr.ReadLine();
                        }
                    }
                }

                if (TraceFilePath != null && TraceFilePath.Length > 0)
                {
                    bool IsFirstTraceWritten = false;

                    using (FileStream fs = new FileStream(TraceFilePath, FileMode.Append, FileAccess.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(fs))
                        {
                            sw.WriteLine("** Log started **");
                            IsFirstTraceWritten = true;
                        }
                    }

                    if (IsFirstTraceWritten)
                    {
                        IsLogOn = true;
                        IsFileLogOn = true;
                    }
                }
            }
            catch (Exception e)
            {
                PrintLine("Unable to start logging traces.");
                PrintLine(e.Message);
            }
        }

        /// <summary>
        /// Writes a log message.
        /// </summary>
        /// <param name="category">The message category.</param>
        /// <param name="message">The message text.</param>
        /// <param name="arguments">Arguments used when for formatting the final message.</param>
        public void Write(Category category, string message, params object[] arguments)
        {
            if (arguments.Length > 0)
                AddLog(string.Format(CultureInfo.InvariantCulture, message, arguments));
            else
                AddLog(message);
        }

        /// <summary>
        /// Writes a log message associated to an exception.
        /// </summary>
        /// <param name="category">The message category.</param>
        /// <param name="exception">The exception.</param>
        /// <param name="message">The message text.</param>
        /// <param name="arguments">Arguments used when for formatting the final message.</param>
        public void Write(Category category, Exception exception, string message, params object[] arguments)
        {
            if (arguments.Length > 0)
                AddLog(string.Format(CultureInfo.InvariantCulture, message, arguments));
            else
                AddLog(message);

            if (exception != null)
                AddLog(exception.Message);
        }

        /// <summary>
        /// Adds a simple log message.
        /// </summary>
        /// <param name="logText">The text message.</param>
        public void AddLog(string logText)
        {
            AddLog(logText, false);
        }

        /// <summary>
        /// Adds a simple log message and flush logs on the disk.
        /// </summary>
        /// <param name="logText">The text message.</param>
        /// <param name="showNow">True if logs should be flushed on this disk now.</param>
        public void AddLog(string logText, bool showNow)
        {
            if (IsLogOn)
            {
                lock (GlobalLock)
                {
                    DateTime UtcNow = DateTime.UtcNow;
                    string TimeLog = UtcNow.ToString(CultureInfo.InvariantCulture) + UtcNow.Millisecond.ToString("D3", CultureInfo.InvariantCulture);

                    string Line = $"TaskbarIconHost - {TimeLog}: {logText}\n";

                    if (LogLines == null)
                        LogLines = Line;
                    else
                        LogLines += Line;
                }
            }

            if (showNow)
                PrintLog();
        }

        /// <summary>
        /// Prints log traces on the debugging terminal.
        /// </summary>
        public void PrintLog()
        {
            if (IsLogOn)
            {
                lock (GlobalLock)
                {
                    if (LogLines != null)
                    {
                        string[] Lines = LogLines.Split('\n');
                        foreach (string Line in Lines)
                            PrintLine(Line);

                        LogLines = null;
                    }
                }
            }
        }

        private void PrintLine(string line)
        {
            NativeMethods.OutputDebugString(line);

            if (IsFileLogOn)
                WriteLineToTraceFile(line);
        }

        private void WriteLineToTraceFile(string line)
        {
            try
            {
                if (line.Length == 0 || TraceFilePath == null)
                    return;

                using (FileStream fs = new FileStream(TraceFilePath, FileMode.Append, FileAccess.Write))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        sw.WriteLine(line);
                    }
                }
            }
            catch
            {
            }
        }

        private string? LogLines;
        private object GlobalLock = string.Empty;
        private bool IsLogOn;
        private bool IsFileLogOn;
        private string? TraceFilePath;
    }
}
