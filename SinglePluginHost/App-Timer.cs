namespace TaskbarIconHost
{
    using System;
    using System.Threading;
    using System.Windows.Threading;

    /// <summary>
    /// Represents an application that can manage a plugin having an icon in the taskbar.
    /// </summary>
    public partial class App : IDisposable
    {
        private void InitTimer()
        {
            // Create a timer to display traces asynchrousnously.
            AppTimer = new Timer(new TimerCallback(AppTimerCallback));
            AppTimer.Change(CheckInterval, CheckInterval);
        }

        private void AppTimerCallback(object parameter)
        {
            // If a shutdown is started, don't show traces anymore so the shutdown can complete smoothly.
            if (IsExiting)
                return;

            // Print traces asynchronously from the timer thread.
            UpdateLogger();

            // Also, schedule an update of the icon and tooltip if they changed, or the first time.
            if (AppTimerOperation == null || (AppTimerOperation.Status == DispatcherOperationStatus.Completed && GetIsIconOrToolTipChanged()))
                AppTimerOperation = Owner.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new System.Action(OnAppTimer));
        }

        private void OnAppTimer()
        {
            // If a shutdown is started, don't update the taskbar anymore so the shutdown can complete smoothly.
            if (IsExiting)
                return;

            UpdateIconAndToolTip();
        }

        private void CleanupTimer()
        {
            using (Timer? Timer = AppTimer)
            {
                AppTimer = null;
            }
        }

        private Timer? AppTimer;
        private DispatcherOperation? AppTimerOperation;
        private TimeSpan CheckInterval = TimeSpan.FromSeconds(0.1);
    }
}
