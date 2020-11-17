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
#pragma warning disable CA1801 // Review unused parameters
        public static bool Init(bool isElevated, string embeddedPluginName, Guid embeddedPluginGuid, Dispatcher dispatcher, ITracer logger, OidCollection oidCheckList, out int exitCode, out bool isBadSignature)
#pragma warning restore CA1801 // Review unused parameters
        {
            Contract.RequireNotNull(logger, out ITracer Logger);

            exitCode = 0;
            isBadSignature = false;

            FindPluginCandidates(embeddedPluginName, out Dictionary<Assembly, List<Type>> PluginClientTypeTable);
            FindLoadablePlugins(embeddedPluginGuid,  PluginClientTypeTable, Logger, out int AssemblyCount, out int CompatibleAssemblyCount);

            if (LoadedPluginTable.Count > 0)
            {
                InitializePlugins(isElevated, dispatcher, Logger);
                FindPluginsWithIcon();
                SetPreferredPlugin();
                return true;
            }
            else
            {
                Logger.Write(Category.Warning, $"Could not load plugins, {AssemblyCount} assemblies found, {CompatibleAssemblyCount} are compatible.");
                return false;
            }
        }

        private static void FindPluginCandidates(string embeddedPluginName, out Dictionary<Assembly, List<Type>> pluginClientTypeTable)
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
                        FindPluginClientTypesByName(name, out PluginAssembly, out PluginClientTypeList);
                        if (PluginAssembly != null && PluginClientTypeList != null && PluginClientTypeList.Count > 0)
                            pluginClientTypeTable.Add(PluginAssembly, PluginClientTypeList);
                    }
            }

            string[] Assemblies = Directory.GetFiles(AppFolder, "*.dll");
            foreach (string AssemblyPath in Assemblies)
            {
                FindPluginClientTypesByPath(AssemblyPath, out PluginAssembly, out PluginClientTypeList);
                if (PluginAssembly != null && PluginClientTypeList != null)
                    if (!pluginClientTypeTable.ContainsKey(PluginAssembly))
                        pluginClientTypeTable.Add(PluginAssembly, PluginClientTypeList);
            }
        }

        private static void FindLoadablePlugins(Guid embeddedPluginGuid, Dictionary<Assembly, List<Type>> pluginClientTypeTable, ITracer logger, out int assemblyCount, out int compatibleAssemblyCount)
        {
            Assembly? PluginAssembly;
            List<Type>? PluginClientTypeList;

            assemblyCount = 0;
            compatibleAssemblyCount = 0;

            foreach (KeyValuePair<Assembly, List<Type>> Entry in pluginClientTypeTable)
            {
                assemblyCount++;

                PluginAssembly = Entry.Key;
                PluginClientTypeList = Entry.Value;

                if (PluginClientTypeList.Count > 0)
                {
                    compatibleAssemblyCount++;

                    CreatePluginList(PluginAssembly, PluginClientTypeList, embeddedPluginGuid, logger, out List<IPluginClient> PluginList);
                    if (PluginList.Count > 0)
                        LoadedPluginTable.Add(PluginAssembly, PluginList);
                }
            }
        }

        private static void InitializePlugins(bool isElevated, Dispatcher dispatcher, ITracer logger)
        {
            foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                foreach (IPluginClient Plugin in Entry.Value)
                {
#pragma warning disable CA2000 // Dispose objects before losing scope: ownership is transfered
                    RegistryTools.Settings Settings = new RegistryTools.Settings("TaskbarIconHost", GuidToString(Plugin.Guid), logger);
#pragma warning restore CA2000 // Dispose objects before losing scope
                    Plugin.Initialize(isElevated, dispatcher, Settings, logger);

                    if (Plugin.RequireElevated)
                        RequireElevated = true;
                }
        }

        private static void FindPluginsWithIcon()
        {
            foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                foreach (IPluginClient Plugin in Entry.Value)
                {
                    List<ICommand> PluginCommandList = Plugin.CommandList;
                    if (PluginCommandList != null)
                    {
                        List<ICommand> FullPluginCommandList = new List<ICommand>();
                        FullCommandList.Add(FullPluginCommandList, Plugin.Name);

                        foreach (ICommand Command in PluginCommandList)
                        {
                            FullPluginCommandList.Add(Command);

                            if (Command != null)
                                CommandTable.Add(Command, Plugin);
                        }
                    }

                    Icon PluginIcon = Plugin.Icon;
                    if (PluginIcon != null)
                        ConsolidatedPluginList.Add(Plugin);
                }
        }

        private static void SetPreferredPlugin()
        {
            foreach (IPluginClient Plugin in ConsolidatedPluginList)
                if (Plugin.HasClickHandler)
                {
                    PreferredPlugin = Plugin;
                    break;
                }

            if (PreferredPlugin == null && ConsolidatedPluginList.Count > 0)
                PreferredPlugin = ConsolidatedPluginList[0];
        }

        private static bool IsReferencingSharedAssembly(Assembly assembly, out AssemblyName sharedAssemblyName)
        {
            AssemblyName[] AssemblyNames = assembly.GetReferencedAssemblies();
            foreach (AssemblyName AssemblyName in AssemblyNames)
                if (AssemblyName.Name == SharedPluginAssemblyName)
                {
                    sharedAssemblyName = AssemblyName;
                    return true;
                }

            sharedAssemblyName = null !;
            return false;
        }

        private static void FindPluginClientTypesByPath(string assemblyPath, out Assembly? pluginAssembly, out List<Type>? pluginClientTypeList)
        {
            try
            {
                pluginAssembly = Assembly.LoadFrom(assemblyPath);
                FindPluginClientTypes(pluginAssembly, out pluginClientTypeList);
            }
            catch
            {
                pluginAssembly = null;
                pluginClientTypeList = null;
            }
        }

        private static void FindPluginClientTypesByName(AssemblyName name, out Assembly? pluginAssembly, out List<Type>? pluginClientTypeList)
        {
            try
            {
                pluginAssembly = Assembly.Load(name);
                FindPluginClientTypes(pluginAssembly, out pluginClientTypeList);
            }
            catch
            {
                pluginAssembly = null;
                pluginClientTypeList = null;
            }
        }

        private static void FindPluginClientTypes(Assembly assembly, out List<Type>? pluginClientTypeList)
        {
            pluginClientTypeList = null;

            try
            {
#if !DEBUG
                if (!string.IsNullOrEmpty(assembly.Location) && !IsAssemblySigned(assembly))
                    return;
#endif
                pluginClientTypeList = new List<Type>();

                if (IsReferencingSharedAssembly(assembly, out AssemblyName SharedAssemblyName))
                {
                    Type?[] AssemblyTypes;
                    try
                    {
                        AssemblyTypes = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException LoaderException)
                    {
                        AssemblyTypes = LoaderException.Types;
                    }
                    catch
                    {
                        AssemblyTypes = Array.Empty<Type>();
                    }

                    foreach (Type? ClientType in AssemblyTypes)
                        if (ClientType != null)
                        {
                            if (!ClientType.IsPublic || ClientType.IsInterface || !ClientType.IsClass || ClientType.IsAbstract)
                                continue;

                            Contract.RequireNotNull(PluginInterfaceType.FullName, out string FullName);
                            Type? InterfaceType = ClientType.GetInterface(FullName);
                            if (InterfaceType != null)
                                pluginClientTypeList.Add(ClientType);
                        }
                }
            }
            catch
            {
            }
        }

        private static bool IsAssemblySigned(Assembly assembly)
        {
            foreach (Module Module in assembly.GetModules())
                return IsModuleSigned(Module);

            return false;
        }

        private static bool IsModuleSigned(Module module)
        {
            for (int i = 0; i < 3; i++)
                if (IsModuleSignedOneTry(module))
                    return true;
                else if (!IsWakeUpDelayElapsed)
                {
                    Thread.Sleep(5000);
                    IsWakeUpDelayElapsed = true;
                }
                else
                    return false;

            return false;
        }

        private static bool IsModuleSignedOneTry(Module module)
        {
            try
            {
                using X509Certificate certificate = GetSignerCertificate(module);
                if (certificate == null)
                {
                    // File is not signed.
                    return false;
                }

                using X509Certificate2 certificate2 = new X509Certificate2(certificate);
                using X509Chain CertificateChain = X509Chain.Create();
                CertificateChain.ChainPolicy.RevocationFlag = X509RevocationFlag.EndCertificateOnly;
                CertificateChain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(60);
                CertificateChain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                bool IsEndCertificateValid = CertificateChain.Build(certificate2);

                if (!IsEndCertificateValid)
                    return false;

                CertificateChain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                CertificateChain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(60);
                CertificateChain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                CertificateChain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreCertificateAuthorityRevocationUnknown;
                bool IsCertificateChainValid = CertificateChain.Build(certificate2);

                if (!IsCertificateChainValid)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static X509Certificate GetSignerCertificate(Module module)
        {
#if NET48
            return module.GetSignerCertificate();
#else
            string FileName = module.FullyQualifiedName;
            return new X509Certificate(FileName);
#endif
        }

        private static void CreatePluginList(Assembly pluginAssembly, List<Type> pluginClientTypeList, Guid embeddedPluginGuid, ITracer logger, out List<IPluginClient> pluginList)
        {
            pluginList = new List<IPluginClient>();

            foreach (Type ClientType in pluginClientTypeList)
            {
                try
                {
                    Contract.RequireNotNull(ClientType.FullName, out string FullName);
                    object? PluginHandle = pluginAssembly.CreateInstance(FullName);
                    if (PluginHandle != null)
                    {
                        string? PluginName = PluginProperty<string?>(PluginHandle, nameof(IPluginClient.Name));
                        Guid PluginGuid = PluginProperty<Guid>(PluginHandle, nameof(IPluginClient.Guid));
                        bool PluginRequireElevated = PluginProperty<bool>(PluginHandle, nameof(IPluginClient.RequireElevated));
                        bool PluginHasClickHandler = PluginProperty<bool>(PluginHandle, nameof(IPluginClient.HasClickHandler));

                        if (PluginName != null && PluginName.Length > 0 && PluginGuid != Guid.Empty)
                        {
                            bool createdNew;
                            EventWaitHandle? InstanceEvent;

                            if (PluginGuid != embeddedPluginGuid)
                                InstanceEvent = new EventWaitHandle(false, EventResetMode.ManualReset, GuidToString(PluginGuid), out createdNew);
                            else
                            {
                                createdNew = true;
                                InstanceEvent = null;
                            }

                            if (createdNew)
                            {
                                IPluginClient NewPlugin = new PluginClient(PluginHandle, PluginName, PluginGuid, PluginRequireElevated, PluginHasClickHandler, InstanceEvent);
                                pluginList.Add(NewPlugin);
                            }
                            else
                            {
                                logger.Write(Category.Warning, "Another instance of a plugin is already running");

                                InstanceEvent?.Close();
                                InstanceEvent = null;
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }
}
