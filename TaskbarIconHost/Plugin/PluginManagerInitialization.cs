﻿namespace TaskbarIconHost
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using System.Windows.Input;
    using System.Windows.Threading;
    using System.Security.Cryptography.X509Certificates;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;

    internal static partial class PluginManager
    {
        public static bool Init(bool isElevated, string embeddedPluginName, Guid embeddedPluginGuid, Dispatcher dispatcher, IPluginLogger logger, out int exitCode, out bool isBadSignature)
        {
            exitCode = 0;
            isBadSignature = false;
            PluginInterfaceType = typeof(IPluginClient);

            Assembly CurrentAssembly = Assembly.GetExecutingAssembly();
            string Location = CurrentAssembly.Location;
            string AppFolder = GetDirectoryName(Location);

            Dictionary<Assembly, List<Type>> PluginClientTypeTable = new Dictionary<Assembly, List<Type>>();

            AddEmbeddedPlugins(embeddedPluginName, PluginClientTypeTable, ref exitCode, ref isBadSignature);
            AddExternalPlugins(AppFolder, PluginClientTypeTable, ref exitCode, ref isBadSignature);

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

                logger.AddLog($"Could not load plugins, {AssemblyCount} assemblies found, {CompatibleAssemblyCount} are compatible.");
                return false;
            }
        }

        private static void AddEmbeddedPlugins(string embeddedPluginName, Dictionary<Assembly, List<Type>> pluginClientTypeTable, ref int exitCode, ref bool isBadSignature)
        {
            if (embeddedPluginName != null)
            {
                Assembly CurrentAssembly = Assembly.GetExecutingAssembly();

                AssemblyName[] AssemblyNames = CurrentAssembly.GetReferencedAssemblies();
                foreach (AssemblyName Name in AssemblyNames)
                    if (Name.Name == embeddedPluginName)
                    {
                        if (FindPluginClientTypesByName(Name, out Assembly PluginAssembly, out List<Type> PluginClientTypeList, ref exitCode, ref isBadSignature))
                            pluginClientTypeTable.Add(PluginAssembly, PluginClientTypeList);
                    }
            }
        }

        private static void AddExternalPlugins(string appFolder, Dictionary<Assembly, List<Type>> pluginClientTypeTable, ref int exitCode, ref bool isBadSignature)
        {
            string[] Assemblies = Directory.GetFiles(appFolder, "*.dll");
            foreach (string AssemblyPath in Assemblies)
            {
                if (FindPluginClientTypesByPath(AssemblyPath, out Assembly PluginAssembly, out List<Type> PluginClientTypeList, ref exitCode, ref isBadSignature))
                    pluginClientTypeTable.Add(PluginAssembly, PluginClientTypeList);
            }
        }

        private static void LoadEnumeratedPlugins(Dictionary<Assembly, List<Type>> pluginClientTypeTable, Guid embeddedPluginGuid, IPluginLogger logger, out int assemblyCount, out int compatibleAssemblyCount)
        {
            assemblyCount = 0;
            compatibleAssemblyCount = 0;

            foreach (KeyValuePair<Assembly, List<Type>> Entry in pluginClientTypeTable)
            {
                assemblyCount++;

                Assembly PluginAssembly = Entry.Key;
                List<Type> PluginClientTypeList = Entry.Value;

                if (PluginClientTypeList.Count > 0)
                {
                    compatibleAssemblyCount++;

                    CreatePluginList(PluginAssembly, PluginClientTypeList, embeddedPluginGuid, logger, out List<IPluginClient> PluginList);
                    if (PluginList.Count > 0)
                        LoadedPluginTable.Add(PluginAssembly, PluginList);
                }
            }
        }

        private static void InitializeAllPlugins(bool isElevated, Dispatcher dispatcher, IPluginLogger logger)
        {
            foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                foreach (IPluginClient Plugin in Entry.Value)
                {
                    InitializePlugin(Plugin, isElevated, dispatcher, logger);

                    if (Plugin.RequireElevated)
                        RequireElevated = true;
                }
        }

        private static void InitializeCommandsAndIcons()
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

        private static void InitializePreferredPlugin()
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

        [SuppressMessage("Microsoft.Reliability", "CA2000: Dispose objects before losing scope")]
        private static void InitializePlugin(IPluginClient plugin, bool isElevated, Dispatcher dispatcher, IPluginLogger logger)
        {
            IPluginSettings Settings = new PluginSettings(GuidToString(plugin.Guid), logger);
            plugin.Initialize(isElevated, dispatcher, Settings, logger);
        }

        private static string GetDirectoryName(string path)
        {
            return Path.GetDirectoryName(path);
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

            sharedAssemblyName = Assembly.GetExecutingAssembly().GetName();
            return false;
        }

        private static bool FindPluginClientTypesByPath(string assemblyPath, out Assembly pluginAssembly, out List<Type> pluginClientTypeList, ref int exitCode, ref bool isBadSignature)
        {
            try
            {
                pluginAssembly = Assembly.LoadFrom(assemblyPath);
                return FindPluginClientTypes(pluginAssembly, out pluginClientTypeList, ref exitCode, ref isBadSignature);
            }
            catch
            {
                if (exitCode == 0)
                    exitCode = -3;

                pluginAssembly = Assembly.GetExecutingAssembly();
                pluginClientTypeList = new List<Type>();
                return false;
            }
        }

        private static bool FindPluginClientTypesByName(AssemblyName name, out Assembly pluginAssembly, out List<Type> pluginClientTypeList, ref int exitCode, ref bool isBadSignature)
        {
            try
            {
                pluginAssembly = Assembly.Load(name);
                return FindPluginClientTypes(pluginAssembly, out pluginClientTypeList, ref exitCode, ref isBadSignature);
            }
            catch
            {
                if (exitCode == 0)
                    exitCode = -4;

                pluginAssembly = Assembly.GetExecutingAssembly();
                pluginClientTypeList = new List<Type>();
                return false;
            }
        }

        private static bool FindPluginClientTypes(Assembly assembly, out List<Type> pluginClientTypeList, ref int exitCode, ref bool isBadSignature)
        {
            pluginClientTypeList = new List<Type>();

            try
            {
                if (!string.IsNullOrEmpty(assembly.Location) && !IsAssemblySigned(assembly, ref exitCode))
                {
                    isBadSignature = true;
                    return false;
                }

                if (IsReferencingSharedAssembly(assembly, out _))
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

                    return pluginClientTypeList.Count > 0;
                }
            }
            catch
            {
                if (exitCode == 0)
                    exitCode = -6;
            }

            return false;
        }

        private static bool IsAssemblySigned(Assembly assembly, ref int exitCode)
        {
            foreach (Module Module in assembly.GetModules())
                return IsModuleSigned(Module, ref exitCode);

            return false;
        }

        private static bool IsModuleSigned(Module module, ref int exitCode)
        {
            for (int i = 0; i < 3; i++)
            {
                if (IsModuleSignedOneTry(module, out int SignedExitCode))
                    return true;

                if (!IsWakeUpDelayElapsed)
                {
                    Thread.Sleep(5000);
                    IsWakeUpDelayElapsed = true;
                }
                else
                {
                    if (exitCode == 0)
                        exitCode = SignedExitCode;

                    return false;
                }
            }

            return false;
        }

        private static bool IsModuleSignedOneTry(Module module, out int signedExitCode)
        {
#if DEBUG
            bool Success = true;
#else
            bool Success = false;
#endif
            signedExitCode = 0;

            try
            {
                X509Certificate certificate = module.GetSignerCertificate();
                if (certificate == null)
                {
                    // File is not signed.
                    WriteDebug("File not signed");
                    signedExitCode = -7;
                    return Success;
                }
                else
                {
                    using (X509Certificate2 Certificate2 = new X509Certificate2(certificate))
                    {
                        using (X509Chain CertificateChain = X509Chain.Create())
                        {
                            CertificateChain.ChainPolicy.RevocationFlag = X509RevocationFlag.EndCertificateOnly;
                            CertificateChain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(60);
                            CertificateChain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                            CertificateChain.ChainPolicy.VerificationFlags =
                                X509VerificationFlags.IgnoreEndRevocationUnknown;
                            bool IsEndCertificateValid = CertificateChain.Build(Certificate2);

                            if (!IsEndCertificateValid)
                            {
                                WriteDebug("End certificate not valid, " + CertificateChain.ToString());
                                foreach (X509ChainStatus Item in CertificateChain.ChainStatus)
                                    WriteDebug(": " + Item.Status + ", " + Item.StatusInformation);

                                signedExitCode = -8;
                                return Success;
                            }
                            else
                            {
                                CertificateChain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                                CertificateChain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(60);
                                CertificateChain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                                CertificateChain.ChainPolicy.VerificationFlags =
                                    X509VerificationFlags.IgnoreEndRevocationUnknown | X509VerificationFlags
                                        .IgnoreCertificateAuthorityRevocationUnknown;
                                bool IsCertificateChainValid = CertificateChain.Build(Certificate2);

                                if (!IsCertificateChainValid)
                                {
                                    WriteDebug("Certificate chain not valid, " + CertificateChain.ToString());
                                    foreach (X509ChainStatus Item in CertificateChain.ChainStatus)
                                        WriteDebug(": " + Item.Status + ", " + Item.StatusInformation);

                                    signedExitCode = -9;
                                    return Success;
                                }
                                else
                                    Success = true;
                            }
                        }
                    }
                }
            }
            catch
            {
                signedExitCode = -10;
            }

            return Success;
        }

        private static void CreatePluginList(Assembly pluginAssembly, List<Type> PluginClientTypeList, Guid embeddedPluginGuid, IPluginLogger logger, out List<IPluginClient> PluginList)
        {
            PluginList = new List<IPluginClient>();

            foreach (Type ClientType in PluginClientTypeList)
            {
                try
                {
                    object PluginHandle = pluginAssembly.CreateInstance(ClientType.FullName);
                    if (PluginHandle != null)
                    {
                        string? PluginName = PluginHandle.GetType().InvokeMember(nameof(IPluginClient.Name), BindingFlags.Default | BindingFlags.GetProperty, null, PluginHandle, null, CultureInfo.InvariantCulture) as string;
                        Guid PluginGuid = (Guid)PluginHandle.GetType().InvokeMember(nameof(IPluginClient.Guid), BindingFlags.Default | BindingFlags.GetProperty, null, PluginHandle, null, CultureInfo.InvariantCulture);
                        bool PluginRequireElevated = (bool)PluginHandle.GetType().InvokeMember(nameof(IPluginClient.RequireElevated), BindingFlags.Default | BindingFlags.GetProperty, null, PluginHandle, null, CultureInfo.InvariantCulture);
                        bool PluginHasClickHandler = (bool)PluginHandle.GetType().InvokeMember(nameof(IPluginClient.HasClickHandler), BindingFlags.Default | BindingFlags.GetProperty, null, PluginHandle, null, CultureInfo.InvariantCulture);

                        if (!string.IsNullOrEmpty(PluginName) && PluginGuid != Guid.Empty)
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

                            if (createdNew && PluginName != null)
                            {
                                IPluginClient NewPlugin = new PluginClient(PluginHandle, PluginName, PluginGuid, PluginRequireElevated, PluginHasClickHandler, InstanceEvent);
                                PluginList.Add(NewPlugin);
                            }
                            else
                            {
                                logger.AddLog("Another instance of a plugin is already running");
                                InstanceEvent?.Close();
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