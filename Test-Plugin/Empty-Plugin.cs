using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Input;
using System.Windows.Threading;

/// <summary>
/// This sample code demonstrates how to implement a default plugin that does nothing.
/// </summary>
namespace Empty
{
    public class EmptyPlugin : TaskbarIconHost.IPluginClient
    {
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
                Result.Add(new RoutedUICommand());
                Result.Add(new RoutedUICommand(TestPlugin.Properties.Resources.Test, "test", GetType()));
                return Result;
            }
        }

        public bool GetIsMenuChanged(bool beforeMenuOpening)
        {
            return false;
        }

        public string GetMenuHeader(ICommand Command)
        {
            return string.Empty;
        }

        public bool GetMenuIsVisible(ICommand Command)
        {
            return true;
        }

        public bool GetMenuIsEnabled(ICommand Command)
        {
            return true;
        }

        public bool GetMenuIsChecked(ICommand Command)
        {
            return false;
        }

        public Bitmap? GetMenuIcon(ICommand Command)
        {
            return null;
        }

        public void OnMenuOpening()
        {
        }

        public void OnExecuteCommand(ICommand Command)
        {
        }

        public bool GetIsIconChanged()
        {
            return false;
        }

        public Icon Icon
        {
            get { return new Icon(string.Empty); }
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
    }
}
