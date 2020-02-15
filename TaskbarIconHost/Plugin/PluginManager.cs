namespace TaskbarIconHost
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Reflection;
    using System.Threading;
    using System.Windows.Input;
    using System.Diagnostics;
    using System.Globalization;

    internal static partial class PluginManager
    {
        static void WriteDebug(string s)
        {
            Debug.WriteLine(s);

            if (AccumulatedDebugString.Length < 10000)
                AccumulatedDebugString += s;
        }

        public static string AccumulatedDebugString { get; private set; } = string.Empty;

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

        public static List<IPluginClient> ConsolidatedPluginList { get; } = new List<IPluginClient>();
        private static IPluginClient? PreferredPlugin;

        public static T PluginProperty<T>(object pluginHandle, string propertyName)
        {
            return (T)pluginHandle.GetType().InvokeMember(propertyName, BindingFlags.Default | BindingFlags.GetProperty, null, pluginHandle, null, CultureInfo.InvariantCulture);
        }

        public static void ExecutePluginMethod(object pluginHandle, string methodName, params object[] args)
        {
            pluginHandle.GetType().InvokeMember(methodName, BindingFlags.Default | BindingFlags.InvokeMethod, null, pluginHandle, args, CultureInfo.InvariantCulture);
        }

        public static T GetPluginFunctionValue<T>(object pluginHandle, string functionName, params object[] args)
        {
            return (T)pluginHandle.GetType().InvokeMember(functionName, BindingFlags.Default | BindingFlags.InvokeMethod, null, pluginHandle, args, CultureInfo.InvariantCulture);
        }

        public static bool RequireElevated { get; private set; }
        public static Dictionary<ICommand, IPluginClient> CommandTable { get; } = new Dictionary<ICommand, IPluginClient>();
        public static Dictionary<List<ICommand>, string> FullCommandList { get; } = new Dictionary<List<ICommand>, string>();

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

        public static Bitmap? GetMenuIcon(ICommand Command)
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

        public static void OnExecuteCommand(ICommand Command)
        {
            IPluginClient Plugin = CommandTable[Command];
            Plugin.OnExecuteCommand(Command);
        }

        public static bool GetIsIconChanged()
        {
            return PreferredPlugin != null ? PreferredPlugin.GetIsIconChanged() : false;
        }

        public static Icon Icon
        {
            get
            {
                if (PreferredPlugin != null)
                    return PreferredPlugin.Icon;

                using Bitmap Bitmap = new Bitmap(16, 16);
                Bitmap.MakeTransparent(Color.White);
                return Icon.FromHandle(Bitmap.GetHicon());
            }
        }

        public static void OnIconClicked()
        {
            if (PreferredPlugin != null)
                PreferredPlugin.OnIconClicked();
        }

        public static bool GetIsToolTipChanged()
        {
            return PreferredPlugin != null ? PreferredPlugin.GetIsToolTipChanged() : false;
        }

        public static string ToolTip
        {
            get { return PreferredPlugin != null ? PreferredPlugin.ToolTip : string.Empty; }
        }

        public static void OnActivated()
        {
            foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                foreach (IPluginClient Plugin in Entry.Value)
                    Plugin.OnActivated();
        }

        public static void OnDeactivated()
        {
            foreach (KeyValuePair<Assembly, List<IPluginClient>> Entry in LoadedPluginTable)
                foreach (IPluginClient Plugin in Entry.Value)
                    Plugin.OnDeactivated();
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
            LoadedPluginTable.Clear();
        }

        public static string GuidToString(Guid guid)
        {
            return guid.ToString("B", CultureInfo.InvariantCulture).ToUpper(CultureInfo.InvariantCulture);
        }

        private const string SharedPluginAssemblyName = "TaskbarIconShared";
        private static Type PluginInterfaceType = typeof(PluginManager);
        private static Dictionary<Assembly, List<IPluginClient>> LoadedPluginTable = new Dictionary<Assembly, List<IPluginClient>>();
        private static bool IsWakeUpDelayElapsed;
    }
}
