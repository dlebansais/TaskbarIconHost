namespace TaskbarIconHost
{
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

                Logger.Write(Category.Warning, $"Could not load plugins, {AssemblyCount} assemblies found, {CompatibleAssemblyCount} are compatible.");
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

            if (embeddedPluginName != null)
            {
                AssemblyName[] AssemblyNames = CurrentAssembly.GetReferencedAssemblies();
                foreach (AssemblyName name in AssemblyNames)
                    if (name.Name == embeddedPluginName)
                    {
                        FindPluginClientTypesByName(name, oidCheckList, out PluginAssembly, out PluginClientTypeList, ref exitCode, ref isBadSignature);
                        if (PluginAssembly != null && PluginClientTypeList != null && PluginClientTypeList.Count > 0)
                            pluginClientTypeTable.Add(PluginAssembly, PluginClientTypeList);
                    }
            }

            string[] Assemblies = Directory.GetFiles(AppFolder, "*.dll");
            foreach (string AssemblyPath in Assemblies)
            {
                FindPluginClientTypesByPath(AssemblyPath, oidCheckList, out PluginAssembly, out PluginClientTypeList, ref exitCode, ref isBadSignature);
                if (PluginAssembly != null && PluginClientTypeList != null)
                    if (!pluginClientTypeTable.ContainsKey(PluginAssembly))
                        pluginClientTypeTable.Add(PluginAssembly, PluginClientTypeList);
            }
        }
    }
}
