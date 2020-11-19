namespace TaskbarIconHost
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Windows.Input;
    using System.Windows.Threading;
    using Contracts;
    using Tracing;

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
        public static bool Init(bool isElevated, string embeddedPluginName, Guid embeddedPluginGuid, Dispatcher dispatcher, ITracer logger, OidCollection oidCheckList, out int exitCode, out bool isBadSignature)
        {
            Contract.RequireNotNull(logger, out ITracer Logger);

            exitCode = 0;
            isBadSignature = false;

            FindPluginCandidates(embeddedPluginName, out Dictionary<Assembly, List<Type>> PluginClientTypeTable, oidCheckList, ref exitCode, ref isBadSignature);
            AddExternalPlugins(PluginClientTypeTable, oidCheckList, ref exitCode, ref isBadSignature);

            LoadEnumeratedPlugins(PluginClientTypeTable, embeddedPluginGuid, logger, out int AssemblyCount, out int CompatibleAssemblyCount);

            if (LoadedPluginTable.Count > 0)
            {
                InitializeAllPlugins(isElevated, dispatcher, logger);
                InitializeCommandsAndIcons();
                InitializePreferredPlugin();
                return true;
            }
            else
            {
                if (exitCode == 0)
                    exitCode = -2;

                Logger.Write(Category.Warning, $"Could not load plugins, {AssemblyCount} assemblies found, {CompatibleAssemblyCount} are compatible.");
                return false;
            }
        }

        private static void FindPluginCandidates(string embeddedPluginName, out Dictionary<Assembly, List<Type>> pluginClientTypeTable, OidCollection oidCheckList, ref int exitCode, ref bool isBadSignature)
        {
            pluginClientTypeTable = new Dictionary<Assembly, List<Type>>();

            if (embeddedPluginName != null)
            {
                Assembly CurrentAssembly = Assembly.GetExecutingAssembly();

                AssemblyName[] AssemblyNames = CurrentAssembly.GetReferencedAssemblies();
                foreach (AssemblyName Name in AssemblyNames)
                    if (Name.Name == embeddedPluginName)
                    {
                        if (FindPluginClientTypesByName(Name, oidCheckList, out Assembly PluginAssembly, out List<Type> PluginClientTypeList, ref exitCode, ref isBadSignature))
                            pluginClientTypeTable.Add(PluginAssembly, PluginClientTypeList);
                    }
            }
        }

        private static void AddExternalPlugins(Dictionary<Assembly, List<Type>> pluginClientTypeTable, OidCollection oidCheckList, ref int exitCode, ref bool isBadSignature)
        {
            Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
            string Location = CurrentAssembly.Location;
            Contract.RequireNotNull(Path.GetDirectoryName(Location), out string AppFolder);

            string[] Assemblies = Directory.GetFiles(AppFolder, "*.dll");
            foreach (string AssemblyPath in Assemblies)
            {
                if (FindPluginClientTypesByPath(AssemblyPath, oidCheckList, out Assembly PluginAssembly, out List<Type> PluginClientTypeList, ref exitCode, ref isBadSignature))
                    pluginClientTypeTable.Add(PluginAssembly, PluginClientTypeList);
            }
        }
    }
}
