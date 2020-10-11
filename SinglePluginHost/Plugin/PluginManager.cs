namespace TaskbarIconHost
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Windows.Input;
    using System.Windows.Threading;
    using Tracing;

    /// <summary>
    /// Represents an object that manages plugins.
    /// </summary>
    public static class PluginManager
    {
        /// <summary>
        /// Initializes the plug in manager.
        /// </summary>
        /// <param name="isElevated">True if the application is running as adiminstrator.</param>
        /// <param name="embeddedPluginName">Name of the embedded plugin.</param>
        /// <param name="embeddedPluginGuid">GUID of the embedded plugin.</param>
        /// <param name="dispatcher">An instance of a dispatcher.</param>
        /// <param name="logger">An instance of a logger.</param>
        /// <returns>True if successful; otherwise, false.</returns>
        public static bool Init(bool isElevated, string embeddedPluginName, Guid embeddedPluginGuid, Dispatcher dispatcher, ITracer logger)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            Assembly CurrentAssembly = Assembly.GetEntryAssembly();
            string Location = CurrentAssembly.Location;
            string AppFolder = Path.GetDirectoryName(Location);
            int AssemblyCount = 0;
            int CompatibleAssemblyCount = 0;

            Dictionary<Assembly, List<Type>> PluginClientTypeTable = new Dictionary<Assembly, List<Type>>();
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
                            PluginClientTypeTable.Add(PluginAssembly, PluginClientTypeList);
                    }
            }

            string[] Assemblies = Directory.GetFiles(AppFolder, "*.dll");
            foreach (string AssemblyPath in Assemblies)
            {
                FindPluginClientTypesByPath(AssemblyPath, out PluginAssembly, out PluginClientTypeList);
                if (PluginAssembly != null && PluginClientTypeList != null)
                    PluginClientTypeTable.Add(PluginAssembly, PluginClientTypeList);
            }

            foreach (KeyValuePair<Assembly, List<Type>> Entry in PluginClientTypeTable)
            {
                AssemblyCount++;

                PluginAssembly = Entry.Key;
                PluginClientTypeList = Entry.Value;

                if (PluginClientTypeList.Count > 0)
                {
                    CompatibleAssemblyCount++;

                    CreatePluginList(PluginAssembly, PluginClientTypeList, embeddedPluginGuid, logger, out List<IPluginClient> PluginList);
                    if (PluginList.Count > 0)
                        LoadedPluginTable.Add(PluginAssembly, PluginList);
                }
            }

            if (LoadedPluginTable.Count > 0)
            {
                foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                    foreach (IPluginClient Plugin in Entry.Value)
                    {
                        using RegistryTools.Settings Settings = new RegistryTools.Settings("TaskbarIconHost", GuidToString(Plugin.Guid), logger);
                        Plugin.Initialize(isElevated, dispatcher, Settings, logger);

                        if (Plugin.RequireElevated)
                            RequireElevated = true;
                    }

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

                foreach (IPluginClient Plugin in ConsolidatedPluginList)
                    if (Plugin.HasClickHandler)
                    {
                        PreferredPlugin = Plugin;
                        break;
                    }

                if (PreferredPlugin == null && ConsolidatedPluginList.Count > 0)
                    PreferredPlugin = ConsolidatedPluginList[0];

                return true;
            }
            else
            {
                logger.Write(Category.Warning, $"Could not load plugins, {AssemblyCount} assemblies found, {CompatibleAssemblyCount} are compatible.");
                return false;
            }
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
                    Type[] AssemblyTypes;
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

                    foreach (Type ClientType in AssemblyTypes)
                    {
                        if (!ClientType.IsPublic || ClientType.IsInterface || !ClientType.IsClass || ClientType.IsAbstract)
                            continue;

                        Type InterfaceType = ClientType.GetInterface(PluginInterfaceType.FullName);
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
                X509Certificate certificate = module.GetSignerCertificate();
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

        private static void CreatePluginList(Assembly pluginAssembly, List<Type> pluginClientTypeList, Guid embeddedPluginGuid, ITracer logger, out List<IPluginClient> pluginList)
        {
            pluginList = new List<IPluginClient>();

            foreach (Type ClientType in pluginClientTypeList)
            {
                try
                {
                    object PluginHandle = pluginAssembly.CreateInstance(ClientType.FullName);
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

        /// <summary>
        /// Gets or sets the GUID of the preferred plugin.
        /// </summary>
        public static Guid PreferredPluginGuid
        {
            get { return PreferredPlugin != null ? PreferredPlugin.Guid : Guid.Empty; }
            set
            {
                foreach (IPluginClient Plugin in ConsolidatedPluginList)
                    if (Plugin.Guid == value)
                    {
                        PreferredPlugin = Plugin;
                        break;
                    }
            }
        }

        /// <summary>
        /// Gets the list of plugins that are deemed valid.
        /// </summary>
        public static List<IPluginClient> ConsolidatedPluginList { get; } = new List<IPluginClient>();
        private static IPluginClient? PreferredPlugin;

        /// <summary>
        /// Gets the value of plugin property.
        /// </summary>
        /// <typeparam name="T">The property type.</typeparam>
        /// <param name="pluginHandle">The plugin handle.</param>
        /// <param name="propertyName">The property name.</param>
        /// <returns>The property value.</returns>
        public static T PluginProperty<T>(object pluginHandle, string propertyName)
        {
            if (pluginHandle == null)
                throw new ArgumentNullException(nameof(pluginHandle));
            if (propertyName == null)
                throw new ArgumentNullException(nameof(propertyName));

            return (T)pluginHandle.GetType().InvokeMember(propertyName, BindingFlags.Default | BindingFlags.GetProperty, null, pluginHandle, null, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Executes a plugin method.
        /// </summary>
        /// <param name="pluginHandle">The plugin handle.</param>
        /// <param name="methodName">The method name.</param>
        /// <param name="args">Arguments for the method.</param>
        public static void ExecutePluginMethod(object pluginHandle, string methodName, params object[] args)
        {
            if (pluginHandle == null)
                throw new ArgumentNullException(nameof(pluginHandle));
            if (methodName == null)
                throw new ArgumentNullException(nameof(methodName));

            pluginHandle.GetType().InvokeMember(methodName, BindingFlags.Default | BindingFlags.InvokeMethod, null, pluginHandle, args, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Calls a plugin function.
        /// </summary>
        /// <typeparam name="T">The plugin function return type.</typeparam>
        /// <param name="pluginHandle">The plugin handle.</param>
        /// <param name="functionName">The plugin function name.</param>
        /// <param name="args">Arguments for the function.</param>
        /// <returns>The function return value.</returns>
        public static T GetPluginFunctionValue<T>(object pluginHandle, string functionName, params object[] args)
        {
            if (pluginHandle == null)
                throw new ArgumentNullException(nameof(pluginHandle));
            if (functionName == null)
                throw new ArgumentNullException(nameof(functionName));

            return (T)pluginHandle.GetType().InvokeMember(functionName, BindingFlags.Default | BindingFlags.InvokeMethod, null, pluginHandle, args, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets a value indicating whether the plugin must run as administrator.
        /// </summary>
        public static bool RequireElevated { get; private set; }

        /// <summary>
        /// Gets the table associating a command to a plugin.
        /// </summary>
        public static Dictionary<ICommand, IPluginClient> CommandTable { get; } = new Dictionary<ICommand, IPluginClient>();

        /// <summary>
        /// Gets the table associating a list of commands to a plugin name.
        /// </summary>
        public static Dictionary<List<ICommand>, string> FullCommandList { get; } = new Dictionary<List<ICommand>, string>();

        /// <summary>
        /// Gets the list of commands for which some visual has changed.
        /// </summary>
        /// <returns>The list of commands.</returns>
        public static List<ICommand> GetChangedCommands()
        {
            List<ICommand> Result = new List<ICommand>();

            foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                foreach (IPluginClient Plugin in Entry.Value)
                    if (Plugin.GetIsMenuChanged(false))
                    {
                        foreach (KeyValuePair<ICommand, IPluginClient> CommandEntry in CommandTable)
                            if (CommandEntry.Value == Plugin)
                                Result.Add(CommandEntry.Key);
                    }

            return Result;
        }

        /// <summary>
        /// Gets the menu header associated to a command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>The menu header.</returns>
        public static string GetMenuHeader(ICommand command)
        {
            IPluginClient Plugin = CommandTable[command];
            return Plugin.GetMenuHeader(command);
        }

        /// <summary>
        /// Gets the command visibility status.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>The visibility status.</returns>
        public static bool GetMenuIsVisible(ICommand command)
        {
            IPluginClient Plugin = CommandTable[command];
            return Plugin.GetMenuIsVisible(command);
        }

        /// <summary>
        /// Gets the command enabled status.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>The enabled status.</returns>
        public static bool GetMenuIsEnabled(ICommand command)
        {
            IPluginClient Plugin = CommandTable[command];
            return Plugin.GetMenuIsEnabled(command);
        }

        /// <summary>
        /// Gets the command checked status.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>The checked status.</returns>
        public static bool GetMenuIsChecked(ICommand command)
        {
            IPluginClient Plugin = CommandTable[command];
            return Plugin.GetMenuIsChecked(command);
        }

        /// <summary>
        /// Gets the command icon as a bitmap.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>The command icon.</returns>
        public static Bitmap? GetMenuIcon(ICommand command)
        {
            IPluginClient Plugin = CommandTable[command];
            return Plugin.GetMenuIcon(command);
        }

        /// <summary>
        /// Called when the context menu is opening.
        /// </summary>
        public static void OnMenuOpening()
        {
            foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                foreach (IPluginClient Plugin in Entry.Value)
                    Plugin.OnMenuOpening();
        }

        /// <summary>
        /// Called when a command must be executed.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        public static void OnExecuteCommand(ICommand command)
        {
            IPluginClient Plugin = CommandTable[command];
            Plugin.OnExecuteCommand(command);
        }

        /// <summary>
        /// Gets a value indicating whether the icon associated to a command has changed.
        /// </summary>
        /// <returns>True if the icon has changed; otherwise, false.</returns>
        public static bool GetIsIconChanged()
        {
            return PreferredPlugin != null ? PreferredPlugin.GetIsIconChanged() : false;
        }

        /// <summary>
        /// Gets the icon to display in the taskbar.
        /// </summary>
        public static Icon? Icon
        {
            get { return PreferredPlugin != null ? PreferredPlugin.Icon : null; }
        }

        /// <summary>
        /// Called when the icon is clicked.
        /// </summary>
        public static void OnIconClicked()
        {
            if (PreferredPlugin != null)
                PreferredPlugin.OnIconClicked();
        }

        /// <summary>
        /// Gets a value indicating whether the tooltip associated to a command has changed.
        /// </summary>
        /// <returns>True if the tooltip has changed; otherwise, false.</returns>
        public static bool GetIsToolTipChanged()
        {
            return PreferredPlugin != null ? PreferredPlugin.GetIsToolTipChanged() : false;
        }

        /// <summary>
        /// Gets the icon to display in the taskbar.
        /// </summary>
        public static string? ToolTip
        {
            get { return PreferredPlugin != null ? PreferredPlugin.ToolTip : null; }
        }

        /// <summary>
        /// Called when the alplication is activated.
        /// </summary>
        public static void OnActivated()
        {
            foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                foreach (IPluginClient Plugin in Entry.Value)
                    Plugin.OnActivated();
        }

        /// <summary>
        /// Called when the alplication is deactivated.
        /// </summary>
        public static void OnDeactivated()
        {
            foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                foreach (IPluginClient Plugin in Entry.Value)
                    Plugin.OnDeactivated();
        }

        /// <summary>
        /// Called when the alplication is shut down.
        /// </summary>
        public static void Shutdown()
        {
            bool CanClose = true;
            foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                foreach (IPluginClient Plugin in Entry.Value)
                    CanClose &= Plugin.CanClose(CanClose);

            if (!CanClose)
                return;

            foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                foreach (IPluginClient Plugin in Entry.Value)
                    Plugin.BeginClose();

            bool IsClosed;

            do
            {
                IsClosed = true;

                foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                    foreach (IPluginClient Plugin in Entry.Value)
                        IsClosed &= Plugin.IsClosed;

                if (!IsClosed)
                    Thread.Sleep(100);
            }
            while (!IsClosed);

            FullCommandList.Clear();
            LoadedPluginTable.Clear();
        }

        /// <summary>
        /// Converts a GUID to a well-formatted string.
        /// </summary>
        /// <param name="guid">The GUID to convert.</param>
        /// <returns>The converted value.</returns>
        public static string GuidToString(Guid guid)
        {
            return guid.ToString("B", CultureInfo.InvariantCulture).ToUpperInvariant();
        }

        private const string SharedPluginAssemblyName = "TaskbarIconShared";
        private static Type PluginInterfaceType = typeof(IPluginClient);
        private static Dictionary<Assembly, List<IPluginClient>> LoadedPluginTable = new Dictionary<Assembly, List<IPluginClient>>();
        private static bool IsWakeUpDelayElapsed;
    }
}
