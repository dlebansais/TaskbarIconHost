namespace TaskbarIconHost;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;
using Contracts;
using Microsoft.Extensions.Logging;

/// <summary>
/// Represents an object that manages plugins.
/// </summary>
public static partial class PluginManager
{
    /// <summary>
    /// Initializes the plug in manager.
    /// </summary>
    /// <param name="isElevated">True if the application is running as adiminstrator.</param>
    /// <param name="embeddedPluginName">Name of the embedded plugin.</param>
    /// <param name="embeddedPluginGuid">GUID of the embedded plugin.</param>
    /// <param name="dispatcher">An instance of a dispatcher.</param>
    /// <param name="logger">An instance of a logger.</param>
    /// <param name="oidCheckList">A list of OIDs.</param>
    /// <param name="exitCode">The exit code to report in case of error.</param>
    /// <param name="isBadSignature">True upon return if the error is because of a bad signature.</param>
    /// <returns>True if successful; otherwise, false.</returns>
    public static bool Init(bool isElevated, string embeddedPluginName, Guid embeddedPluginGuid, Dispatcher dispatcher, ILogger logger, OidCollection oidCheckList, out int exitCode, out bool isBadSignature)
    {
        Contract.RequireNotNull(logger, out ILogger Logger);

        exitCode = 0;
        isBadSignature = false;

        FindPluginCandidates(embeddedPluginName, out Dictionary<Assembly, List<Type>> PluginClientTypeTable, oidCheckList, ref exitCode, ref isBadSignature);

        LoadEnumeratedPlugins(PluginClientTypeTable, embeddedPluginGuid, Logger, out int AssemblyCount, out int CompatibleAssemblyCount);

        if (LoadedPluginTable.Count > 0)
        {
            InitializeAllPlugins(isElevated, dispatcher, Logger);
            InitializeCommandsAndIcons();
            InitializePreferredPlugin();
            return true;
        }
        else
        {
            if (exitCode == 0)
                exitCode = -2;

            LoggerMessage.Define(LogLevel.Warning, 0, $"Could not load plugins, {AssemblyCount} assemblies found, {CompatibleAssemblyCount} are compatible.")(Logger, null);
            return false;
        }
    }

    private static void FindPluginCandidates(string embeddedPluginName, out Dictionary<Assembly, List<Type>> pluginClientTypeTable, OidCollection oidCheckList, ref int exitCode, ref bool isBadSignature)
    {
        Contract.RequireNotNull(Assembly.GetEntryAssembly(), out Assembly CurrentAssembly);
        string Location = CurrentAssembly.Location;
        Contract.RequireNotNull(Path.GetDirectoryName(Location), out string AppFolder);

        pluginClientTypeTable = new Dictionary<Assembly, List<Type>>();
        Assembly? PluginAssembly;
        List<Type>? PluginClientTypeList;

        if (embeddedPluginName is not null)
        {
            AssemblyName[] AssemblyNames = CurrentAssembly.GetReferencedAssemblies();
            foreach (AssemblyName Name in AssemblyNames)
            {
                if (Name.Name == embeddedPluginName)
                {
                    _ = FindPluginClientTypesByName(Name, oidCheckList, out PluginAssembly, out PluginClientTypeList, ref exitCode, ref isBadSignature);
                    if (PluginAssembly is not null && PluginClientTypeList is not null && PluginClientTypeList.Count > 0)
                        pluginClientTypeTable.Add(PluginAssembly, PluginClientTypeList);
                }
            }
        }

        string[] Assemblies = Directory.GetFiles(AppFolder, "*.dll");
        foreach (string AssemblyPath in Assemblies)
        {
            _ = FindPluginClientTypesByPath(AssemblyPath, oidCheckList, out PluginAssembly, out PluginClientTypeList, ref exitCode, ref isBadSignature);
            if (PluginAssembly is not null && PluginClientTypeList is not null)
            {
                if (!pluginClientTypeTable.ContainsKey(PluginAssembly))
                    pluginClientTypeTable.Add(PluginAssembly, PluginClientTypeList);
            }
        }
    }
}
