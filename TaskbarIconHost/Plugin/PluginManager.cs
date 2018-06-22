﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Input;
using System.Windows.Threading;

namespace TaskbarIconHost
{
    public static class PluginManager
    {
        public static bool Init(bool isElevated, Dispatcher dispatcher, IPluginLogger logger)
        {
            Assembly CurrentAssembly = Assembly.GetExecutingAssembly();

            if (!IsReferencingSharedAssembly(CurrentAssembly, out AssemblyName SharedAssemblyName))
                throw new PluginException("The main assembly is missing a reference to the " + SharedPluginAssemblyName + " assembly");

            Assembly SharedAssembly = Assembly.ReflectionOnlyLoad(SharedAssemblyName.FullName);
            PluginInterfaceType = SharedAssembly.GetType(typeof(IPluginClient).FullName);

            string Location = CurrentAssembly.Location;
            string AppFolder = Path.GetDirectoryName(Location);
            int AssemblyCount = 0;
            int CompatibleAssemblyCount = 0;

            string[] Assemblies = Directory.GetFiles(AppFolder, "*.dll");
            foreach (string AssemblyPath in Assemblies)
            {
                AssemblyCount++;

                FindPluginClientTypes(AssemblyPath, out Assembly PluginAssembly, out List<Type> PluginClientTypeList);
                if (PluginClientTypeList.Count > 0)
                {
                    CompatibleAssemblyCount++;

                    CreatePluginList(PluginAssembly, PluginClientTypeList, logger, out List<IPluginClient> PluginList);
                    if (PluginList.Count > 0)
                        LoadedPluginTable.Add(PluginAssembly, PluginList);
                }
            }

            if (LoadedPluginTable.Count > 0)
            {
                foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                    foreach (IPluginClient Plugin in Entry.Value)
                    {
                        if (PreferredPlugin == null)
                            PreferredPlugin = Plugin;

                        IPluginSettings Settings = new PluginSettings(GuidToString(Plugin.Guid), logger);
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
                            FullCommandList.Add(new List<ICommand>());

                            foreach (ICommand Command in PluginCommandList)
                            {
                                FullCommandList[FullCommandList.Count - 1].Add(Command);

                                if (Command != null)
                                    CommandTable.Add(Command, Plugin);
                            }
                        }
                    }

                return true;
            }
            else
            {
                logger.AddLog($"Could not load plugins, {AssemblyCount} assemblies found, {CompatibleAssemblyCount} are compatible.");
                return false;
            }
        }

        private static bool IsReferencingSharedAssembly(Assembly assembly, out AssemblyName SharedAssemblyName)
        {
            AssemblyName[] AssemblyNames = assembly.GetReferencedAssemblies();
            foreach (AssemblyName AssemblyName in AssemblyNames)
                if (AssemblyName.Name == SharedPluginAssemblyName)
                {
                    SharedAssemblyName = AssemblyName;
                    return true;
                }

            SharedAssemblyName = null;
            return false;
        }

        private static void FindPluginClientTypes(string assemblyPath, out Assembly PluginAssembly, out List<Type> PluginClientTypeList)
        {
            PluginClientTypeList = new List<Type>();

            try
            {
                PluginAssembly = Assembly.LoadFrom(assemblyPath);
                if (IsReferencingSharedAssembly(PluginAssembly, out AssemblyName SharedAssemblyName))
                {
                    Type[] AssemblyTypes;
                    try
                    {
                        AssemblyTypes = PluginAssembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException LoaderException)
                    {
                        AssemblyTypes = LoaderException.Types;
                    }
                    catch
                    {
                        AssemblyTypes = new Type[0];
                    }

                    foreach (Type ClientType in AssemblyTypes)
                    {
                        if (!ClientType.IsPublic || ClientType.IsInterface || !ClientType.IsClass || ClientType.IsAbstract)
                            continue;

                        Type InterfaceType = ClientType.GetInterface(PluginInterfaceType.FullName);
                        if (InterfaceType != null)
                            PluginClientTypeList.Add(ClientType);
                    }
                }
            }
            catch
            {
                PluginAssembly = null;
            }
        }

        private static void CreatePluginList(Assembly pluginAssembly, List<Type> PluginClientTypeList, IPluginLogger logger, out List<IPluginClient> PluginList)
        {
            PluginList = new List<IPluginClient>();

            foreach (Type ClientType in PluginClientTypeList)
            {
                try
                {
                    object PluginHandle = pluginAssembly.CreateInstance(ClientType.FullName);
                    if (PluginHandle != null)
                    {
                        string PluginName = PluginHandle.GetType().InvokeMember(nameof(IPluginClient.Name), BindingFlags.Default | BindingFlags.GetProperty, null, PluginHandle, null) as string;
                        Guid PluginGuid = (Guid)PluginHandle.GetType().InvokeMember(nameof(IPluginClient.Guid), BindingFlags.Default | BindingFlags.GetProperty, null, PluginHandle, null);
                        bool PluginRequireElevated = (bool)PluginHandle.GetType().InvokeMember(nameof(IPluginClient.RequireElevated), BindingFlags.Default | BindingFlags.GetProperty, null, PluginHandle, null);
                        if (!string.IsNullOrEmpty(PluginName) && PluginGuid != Guid.Empty)
                        {
                            bool createdNew;
                            EventWaitHandle InstanceEvent = new EventWaitHandle(false, EventResetMode.ManualReset, GuidToString(PluginGuid), out createdNew);
                            if (createdNew)
                            {
                                IPluginClient NewPlugin = new PluginClient(PluginHandle, PluginName, PluginGuid, PluginRequireElevated, InstanceEvent);
                                PluginList.Add(NewPlugin);
                            }
                            else
                            {
                                logger.AddLog("Another instance of a plugin is already running");
                                InstanceEvent.Close();
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

        public static T PluginProperty<T>(object pluginHandle, string propertyName)
        {
            return (T)pluginHandle.GetType().InvokeMember(propertyName, BindingFlags.Default | BindingFlags.GetProperty, null, pluginHandle, null);
        }

        public static void ExecutePluginMethod(object pluginHandle, string methodName, params object[] args)
        {
            pluginHandle.GetType().InvokeMember(methodName, BindingFlags.Default | BindingFlags.InvokeMethod, null, pluginHandle, args);
        }

        public static T GetPluginFunctionValue<T>(object pluginHandle, string functionName, params object[] args)
        {
            return (T)pluginHandle.GetType().InvokeMember(functionName, BindingFlags.Default | BindingFlags.InvokeMethod, null, pluginHandle, args);
        }

        public static bool RequireElevated { get; private set; }
        public static Dictionary<ICommand, IPluginClient> CommandTable { get; } = new Dictionary<ICommand, IPluginClient>();
        public static List<List<ICommand>> FullCommandList { get; } = new List<List<ICommand>>();

        public static List<ICommand> GetChangedCommands()
        {
            List<ICommand> Result = new List<ICommand>();

            foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                foreach (IPluginClient Plugin in Entry.Value)
                    if (Plugin.GetIsMenuChanged())
                    {
                        foreach (KeyValuePair<ICommand, IPluginClient> CommandEntry in CommandTable)
                            if (CommandEntry.Value == Plugin)
                                Result.Add(CommandEntry.Key);
                    }

            return Result;
        }

        public static string GetMenuHeader(ICommand Command)
        {
            IPluginClient Plugin = CommandTable[Command];
            return Plugin.GetMenuHeader(Command);
        }

        public static bool GetMenuIsVisible(ICommand Command)
        {
            IPluginClient Plugin = CommandTable[Command];
            return Plugin.GetMenuIsVisible(Command);
        }

        public static bool GetMenuIsEnabled(ICommand Command)
        {
            IPluginClient Plugin = CommandTable[Command];
            return Plugin.GetMenuIsEnabled(Command);
        }

        public static bool GetMenuIsChecked(ICommand Command)
        {
            IPluginClient Plugin = CommandTable[Command];
            return Plugin.GetMenuIsChecked(Command);
        }

        public static Bitmap GetMenuIcon(ICommand Command)
        {
            IPluginClient Plugin = CommandTable[Command];
            return Plugin.GetMenuIcon(Command);
        }

        public static void OnMenuOpening()
        {
            foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                foreach (IPluginClient Plugin in Entry.Value)
                    Plugin.OnMenuOpening();
        }

        public static void ExecuteCommandHandler(ICommand Command)
        {
            IPluginClient Plugin = CommandTable[Command];
            Plugin.ExecuteCommandHandler(Command);
        }

        public static bool GetIsIconChanged()
        {
            return PreferredPlugin != null ? PreferredPlugin.GetIsIconChanged() : false;
        }

        public static Icon Icon
        {
            get { return PreferredPlugin != null ? PreferredPlugin.Icon : null; }
        }

        public static bool GetIsToolTipChanged()
        {
            return PreferredPlugin != null ? PreferredPlugin.GetIsToolTipChanged() : false;
        }

        public static string ToolTip
        {
            get { return PreferredPlugin != null ? PreferredPlugin.ToolTip : null; }
        }

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

            foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
            {
                Entry.Value.Clear();
                //AppDomain.Unload(Entry.Key);
            }

            LoadedPluginTable.Clear();
        }

        private static string GuidToString(Guid guid)
        {
            return guid.ToString("B").ToUpper();
        }

        private static readonly string SharedPluginAssemblyName = "TaskbarIconShared";
        private static Type PluginInterfaceType;
        private static Dictionary<Assembly, List<IPluginClient>> LoadedPluginTable = new Dictionary<Assembly, List<IPluginClient>>();
        private static IPluginClient PreferredPlugin;
    }
}