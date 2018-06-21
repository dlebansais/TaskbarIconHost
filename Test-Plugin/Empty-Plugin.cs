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
    public class EmptyPlugin : System.MarshalByRefObject, TaskbarIconHost.IPluginClient
    {
        public string Name
        {
            get { return "Empty"; }
        }

        public Guid Guid
        {
            get { return Guid.Empty; }
        }

        public void Initialize(bool isElevated, Dispatcher dispatcher, TaskbarIconHost.IPluginSettings settings, TaskbarIconHost.IPluginLogger logger)
        {
        }

        public List<ICommand> CommandList
        {
            get { return null; }
        }

        public bool GetIsMenuChanged()
        {
            return false;
        }

        public string GetMenuHeader(ICommand Command)
        {
            return null;
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

        public Bitmap GetMenuIcon(ICommand Command)
        {
            return null;
        }

        public void OnMenuOpening()
        {
        }

        public void ExecuteCommandHandler(ICommand Command)
        {
        }

        public bool IsIconChanged
        {
            get { return false; }
        }

        public Icon Icon
        {
            get { return null; }
        }

        public bool IsToolTipChanged
        {
            get { return false; }
        }

        public string ToolTip
        {
            get { return null; }
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
