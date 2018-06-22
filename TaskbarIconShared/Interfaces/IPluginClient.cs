using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Input;
using System.Windows.Threading;

namespace TaskbarIconHost
{
    public interface IPluginClient
    {
        string Name { get; }
        Guid Guid { get; }
        bool RequireElevated { get; }
        void Initialize(bool isElevated, Dispatcher dispatcher, IPluginSettings settings, IPluginLogger logger);

        List<ICommand> CommandList { get; }
        bool GetIsMenuChanged();
        string GetMenuHeader(ICommand Command);
        bool GetMenuIsVisible(ICommand Command);
        bool GetMenuIsEnabled(ICommand Command);
        bool GetMenuIsChecked(ICommand Command);
        Bitmap GetMenuIcon(ICommand Command);
        void OnMenuOpening();
        void ExecuteCommandHandler(ICommand Command);

        bool GetIsIconChanged();
        Icon Icon { get; }

        bool GetIsToolTipChanged();
        string ToolTip { get; }

        bool CanClose(bool canClose);
        void BeginClose();
        bool IsClosed { get; }
    }
}
