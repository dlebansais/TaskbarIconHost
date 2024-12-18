namespace TaskbarIconHost;

using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Contracts;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents an object that can write log trace on the disk.
/// </summary>
internal class PluginLogger : ILogger
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
                using FileStream fs = new(SettingFilePath, FileMode.Open, FileAccess.Read);
                using StreamReader sr = new(fs);
                TraceFilePath = sr.ReadLine();
            }

            if (TraceFilePath is not null && TraceFilePath.Length > 0)
            {
                bool IsFirstTraceWritten = false;

                using FileStream fs = new(TraceFilePath, FileMode.Append, FileAccess.Write);
                using StreamWriter sw = new(fs);
                sw.WriteLine("** Log started **");
                IsFirstTraceWritten = true;

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

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Contract.RequireNotNull(formatter, out Func<TState, Exception?, string> Formatter);

        Write(logLevel, Formatter(state, null));
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <summary>
    /// Writes a log message.
    /// </summary>
    /// <param name="logLevel">The message category.</param>
    /// <param name="message">The message text.</param>
    /// <param name="arguments">Arguments used when for formatting the final message.</param>
#pragma warning disable IDE0060 // Remove unused parameter
    public void Write(LogLevel logLevel, string message, params object[] arguments)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        Contract.RequireNotNull(arguments, out object[] Arguments);

        if (Arguments.Length > 0)
            AddLog(string.Format(CultureInfo.InvariantCulture, message, Arguments));
        else
            AddLog(message);
    }

    /// <summary>
    /// Writes a log message associated to an exception.
    /// </summary>
    /// <param name="logLevel">The message category.</param>
    /// <param name="exception">The exception.</param>
    /// <param name="message">The message text.</param>
    /// <param name="arguments">Arguments used when for formatting the final message.</param>
#pragma warning disable IDE0060 // Remove unused parameter
    public void Write(LogLevel logLevel, Exception exception, string message, params object[] arguments)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        Contract.RequireNotNull(arguments, out object[] Arguments);

        if (Arguments.Length > 0)
            AddLog(string.Format(CultureInfo.InvariantCulture, message, Arguments));
        else
            AddLog(message);

        if (exception is not null)
            AddLog(exception.Message);
    }

    /// <summary>
    /// Adds a simple log message.
    /// </summary>
    /// <param name="logText">The text message.</param>
    public void AddLog(string logText) => AddLog(logText, false);

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

                if (LogLines is null)
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
                if (LogLines is not null)
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
            if (line.Length == 0 || TraceFilePath is null)
                return;

            using FileStream fs = new(TraceFilePath, FileMode.Append, FileAccess.Write);
            using StreamWriter sw = new(fs);
            sw.WriteLine(line);
        }
        catch
        {
        }
    }

    private string? LogLines;
    private readonly object GlobalLock = string.Empty;
    private readonly bool IsLogOn;
    private readonly bool IsFileLogOn;
    private readonly string? TraceFilePath;
}
