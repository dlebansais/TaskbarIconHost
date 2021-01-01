namespace TestPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Windows.Input;
    using System.Windows.Threading;

    /// <summary>
    /// This sample code demonstrates how to implement a default plugin that does nothing.
    /// </summary>
    public class EmptyPlugin : TaskbarIconHost.IPluginClient
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
        public string Name
        {
            get { return "Empty"; }
        }

        public Guid Guid
        {
            get { return new Guid("{C54607CF-0D48-4105-BB17-D30C0B0A8A2A}"); } // Do not copy this line, use your own Guid.
        }

        public bool RequireElevated
        {
            get { return false; }
        }

        public bool HasClickHandler
        {
            get { return false; }
        }

        public void Initialize(bool isElevated, Dispatcher dispatcher, RegistryTools.Settings settings, Tracing.ITracer logger)
        {
        }

        public List<ICommand> CommandList
        {
            get
            {
                List<ICommand> Result = new List<ICommand>();
                Result.Add(new RoutedUICommand(Properties.Resources.Test, "test", GetType()));
                return Result;
            }
        }

        public bool GetIsMenuChanged(bool beforeMenuOpening)
        {
            return false;
        }

        public string GetMenuHeader(ICommand command)
        {
            if (command is RoutedUICommand AsRoutedUICommand)
                return AsRoutedUICommand.Text;
            else
                return string.Empty;
        }

        public bool GetMenuIsVisible(ICommand command)
        {
            return true;
        }

        public bool GetMenuIsEnabled(ICommand command)
        {
            return true;
        }

        public bool GetMenuIsChecked(ICommand command)
        {
            return false;
        }

        public Bitmap? GetMenuIcon(ICommand command)
        {
            return null;
        }

        public void OnMenuOpening()
        {
        }

        public void OnExecuteCommand(ICommand command)
        {
        }

        public bool GetIsIconChanged()
        {
            return false;
        }

        public Icon Icon
        {
            get
            {
                System.Reflection.Assembly ExecutingAssembly = System.Reflection.Assembly.GetExecutingAssembly()!;
                using (System.IO.Stream Stream = ExecutingAssembly.GetManifestResourceStream("TestPlugin.Resources.main.ico")!)
                {
                    return new Icon(Stream);
                }
            }
        }

        public Bitmap SelectionBitmap
        {
            get { return new Bitmap(0, 0); }
        }

        public void OnIconClicked()
        {
        }

        public bool GetIsToolTipChanged()
        {
            return false;
        }

        public string ToolTip
        {
            get { return string.Empty; }
        }

        public void OnActivated()
        {
        }

        public void OnDeactivated()
        {
        }

        public bool CanClose(bool canClose)
        {
            return true;
        }

        public void BeginClose()
        {
        }

        public bool IsClosed
        {
            get { return true; }
        }
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
