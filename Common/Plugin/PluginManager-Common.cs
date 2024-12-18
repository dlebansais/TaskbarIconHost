namespace TaskbarIconHost;

using System;
using System.Collections.Generic;
#if DEBUG
using System.Diagnostics;
#endif
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection;
using System.Security.Cryptography;
#if !DEBUG
using System.Security.Cryptography.X509Certificates;
#endif
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
    private static void LoadEnumeratedPlugins(Dictionary<Assembly, List<Type>> pluginClientTypeTable, Guid embeddedPluginGuid, ILogger logger, out int assemblyCount, out int compatibleAssemblyCount)
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

    private static void InitializeAllPlugins(bool isElevated, Dispatcher dispatcher, ILogger logger)
    {
        foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
        {
            foreach (IPluginClient Plugin in Entry.Value)
            {
                InitializePlugin(Plugin, isElevated, dispatcher, logger);

                if (Plugin.RequireElevated)
                    RequireElevated = true;
            }
        }
    }

    [SuppressMessage("Microsoft.Reliability", "CA2000: Dispose objects before losing scope", Justification = "Disposable object is stored")]
    private static void InitializePlugin(IPluginClient plugin, bool isElevated, Dispatcher dispatcher, ILogger logger)
    {
        RegistryTools.Settings Settings = new("TaskbarIconHost", GuidToString(plugin.PluginGuid), logger);
        plugin.Initialize(isElevated, dispatcher, Settings, logger);
    }

    private static void InitializeCommandsAndIcons()
    {
        foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
        {
            foreach (IPluginClient Plugin in Entry.Value)
            {
                IReadOnlyCollection<ICommand?> PluginCommandList = Plugin.CommandList;
                if (PluginCommandList is not null)
                {
                    List<ICommand?> FullPluginCommandList = [];
                    FullCommandList.Add(FullPluginCommandList, Plugin.Name);

                    foreach (ICommand? Command in PluginCommandList)
                    {
                        FullPluginCommandList.Add(Command);

                        if (Command is not null && !IsSeparatorCommand(Command))
                            CommandTable.Add(Command, Plugin);
                    }
                }

                Icon PluginIcon = Plugin.Icon;
                if (PluginIcon is not null)
                    ConsolidatedPluginListInternal.Add(Plugin);
            }
        }
    }

    private static void InitializePreferredPlugin()
    {
        foreach (IPluginClient Plugin in ConsolidatedPluginListInternal)
        {
            if (Plugin.HasClickHandler)
            {
                PreferredPlugin = Plugin;
                break;
            }
        }

        if (PreferredPlugin is null && ConsolidatedPluginListInternal.Count > 0)
            PreferredPlugin = ConsolidatedPluginListInternal[0];
    }

    private static bool IsReferencingSharedAssembly(Assembly assembly, out AssemblyName sharedAssemblyName)
    {
        AssemblyName[] AssemblyNames = assembly.GetReferencedAssemblies();
        foreach (AssemblyName AssemblyName in AssemblyNames)
        {
            if (AssemblyName.Name == SharedPluginAssemblyName)
            {
                sharedAssemblyName = AssemblyName;
                return true;
            }
        }

        Contract.Unused(out sharedAssemblyName);
        return false;
    }

    private static bool FindPluginClientTypesByPath(string assemblyPath, OidCollection oidCheckList, out Assembly pluginAssembly, out List<Type> pluginClientTypeList, ref int exitCode, ref bool isBadSignature)
    {
        try
        {
            pluginAssembly = Assembly.LoadFrom(assemblyPath);
            return FindPluginClientTypes(pluginAssembly, oidCheckList, out pluginClientTypeList, ref exitCode, ref isBadSignature);
        }
        catch
        {
            if (exitCode == 0)
                exitCode = -3;

            pluginAssembly = Assembly.GetExecutingAssembly();
            pluginClientTypeList = [];
            return false;
        }
    }

    private static bool FindPluginClientTypesByName(AssemblyName name, OidCollection oidCheckList, out Assembly pluginAssembly, out List<Type> pluginClientTypeList, ref int exitCode, ref bool isBadSignature)
    {
        try
        {
            pluginAssembly = Assembly.Load(name);
            return FindPluginClientTypes(pluginAssembly, oidCheckList, out pluginClientTypeList, ref exitCode, ref isBadSignature);
        }
        catch
        {
            if (exitCode == 0)
                exitCode = -4;

            Contract.Unused(out pluginAssembly);
            Contract.Unused(out pluginClientTypeList);
            return false;
        }
    }

    private static bool FindPluginClientTypes(Assembly assembly, OidCollection oidCheckList, out List<Type> pluginClientTypeList, ref int exitCode, ref bool isBadSignature)
    {
        pluginClientTypeList = [];

        try
        {
            if (!string.IsNullOrEmpty(assembly.Location) && !IsAssemblySigned(assembly, oidCheckList, ref exitCode))
            {
                isBadSignature = true;
                return false;
            }

            if (IsReferencingSharedAssembly(assembly, out _))
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
                    AssemblyTypes = [];
                }

                foreach (Type? ClientType in AssemblyTypes)
                {
                    if (ClientType is not null)
                    {
                        if (!ClientType.IsPublic || ClientType.IsInterface || !ClientType.IsClass || ClientType.IsAbstract)
                            continue;

                        Contract.RequireNotNull(PluginInterfaceType.FullName, out string FullName);
                        Type? InterfaceType = ClientType.GetInterface(FullName);
                        if (InterfaceType is not null)
                            pluginClientTypeList.Add(ClientType);
                    }
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

    private static bool IsAssemblySigned(Assembly assembly, OidCollection oidCheckList, ref int exitCode)
    {
        if (!string.IsNullOrEmpty(assembly.Location))
            return false;

#if DEBUG
        Debug.Assert(oidCheckList is not null);
        exitCode = 0;
#else
        foreach (Module Module in assembly.GetModules())
            return IsModuleSigned(Module, oidCheckList, ref exitCode);
#endif

        return false;
    }

#if !DEBUG
    private static bool IsModuleSigned(Module module, OidCollection oidCheckList, ref int exitCode)
    {
        for (int i = 0; i < 3; i++)
        {
            if (IsModuleSignedOneTry(module, oidCheckList, out int SignedExitCode))
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

    private static bool IsModuleSignedOneTry(Module module, OidCollection oidCheckList, out int signedExitCode)
    {
        bool Success = false;

        signedExitCode = 0;

        try
        {
            using X509Certificate Certificate = GetSignerCertificate(module);
            if (Certificate is null)
            {
                // File is not signed.
                signedExitCode = -7;
                return Success;
            }

            using X509Certificate2 Certificate2 = new(Certificate);
            using X509Chain CertificateChain = X509Chain.Create();

            Success = true;
            CheckCertificateChain(Certificate2, CertificateChain, oidCheckList, X509RevocationFlag.EndCertificateOnly, X509VerificationFlags.IgnoreEndRevocationUnknown, -8, ref Success, ref signedExitCode);
            CheckCertificateChain(Certificate2, CertificateChain, oidCheckList, X509RevocationFlag.EntireChain, X509VerificationFlags.IgnoreEndRevocationUnknown | X509VerificationFlags.IgnoreCertificateAuthorityRevocationUnknown, -9, ref Success, ref signedExitCode);
        }
        catch
        {
            signedExitCode = -10;
        }

        return Success;
    }

    private static X509Certificate GetSignerCertificate(Module module)
    {
        string FileName = module.FullyQualifiedName;
        return new X509Certificate(FileName);
    }

    private static void CheckCertificateChain(X509Certificate2 certificate, X509Chain certificateChain, OidCollection oidCheckList, X509RevocationFlag revocationFlags, X509VerificationFlags verificationFlags, int exitCode, ref bool success, ref int signedExitCode)
    {
        TimeSpan UrlRetrievalTimeout = TimeSpan.FromSeconds(60);

        // Oid CodeSigningOid = new("1.3.6.1.5.5.7.3.3");
        certificateChain.ChainPolicy.RevocationFlag = revocationFlags;
        certificateChain.ChainPolicy.UrlRetrievalTimeout = UrlRetrievalTimeout;
        certificateChain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
        certificateChain.ChainPolicy.VerificationFlags = verificationFlags;

        foreach (Oid Item in oidCheckList)
            _ = certificateChain.ChainPolicy.CertificatePolicy.Add(Item);

        bool IsEndCertificateValid = certificateChain.Build(certificate);

        if (!IsEndCertificateValid)
        {
            signedExitCode = exitCode;
            success = false;
        }
    }
#endif

    private static void CreatePluginList(Assembly pluginAssembly, List<Type> pluginClientTypeList, Guid embeddedPluginGuid, ILogger logger, out List<IPluginClient> pluginList)
    {
        pluginList = [];

        foreach (Type ClientType in pluginClientTypeList)
        {
            try
            {
                Contract.RequireNotNull(ClientType.FullName, out string FullName);
                object? PluginHandle = pluginAssembly.CreateInstance(FullName);
                if (PluginHandle is not null)
                {
                    string? PluginName = PluginProperty<string?>(PluginHandle, nameof(IPluginClient.Name));
                    Guid PluginGuid = PluginProperty<Guid>(PluginHandle, nameof(IPluginClient.PluginGuid));
                    bool PluginRequireElevated = PluginProperty<bool>(PluginHandle, nameof(IPluginClient.RequireElevated));
                    bool PluginHasClickHandler = PluginProperty<bool>(PluginHandle, nameof(IPluginClient.HasClickHandler));

                    if (PluginName is not null && PluginName.Length > 0 && PluginGuid != Guid.Empty)
                    {
                        bool createdNew;
                        EventWaitHandle? InstanceEvent = null;

                        try
                        {
                            if (PluginGuid != embeddedPluginGuid)
                                InstanceEvent = new EventWaitHandle(false, EventResetMode.ManualReset, GuidToString(PluginGuid), out createdNew);
                            else
                                createdNew = true;

                            if (createdNew)
                            {
                                IPluginClient NewPlugin = new PluginClient(PluginHandle, PluginName, PluginGuid, PluginRequireElevated, PluginHasClickHandler, InstanceEvent);
                                pluginList.Add(NewPlugin);

                                InstanceEvent = null;
                            }
                            else
                            {
                                LoggerMessage.Define(LogLevel.Warning, 0, "Another instance of a plugin is already running")(logger, null);
                            }
                        }
                        finally
                        {
                            InstanceEvent?.Dispose();
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
    /// Checks if a command is not associated to a menu but to a separator.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>True is associated to a separator; otherwise, false.</returns>
    public static bool IsSeparatorCommand(ICommand? command) => command is not RoutedUICommand AsRoutedUiCommand || AsRoutedUiCommand.Text.Length == 0;
}
