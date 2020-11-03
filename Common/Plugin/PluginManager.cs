namespace TaskbarIconHost
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.Reflection;
    using System.Threading;
    using System.Windows.Input;

    /// <summary>
    /// Represents an object that manages plugins.
    /// </summary>
    public static partial class PluginManager
    {
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
        /// Gets the tooltip to display in the taskbar.
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
