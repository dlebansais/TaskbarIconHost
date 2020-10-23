﻿namespace TaskbarIconHost
{
    using System;
    using System.Threading;
    using System.Windows.Threading;

    /// <summary>
    /// Represents an application that can manage plugins having an icon in the taskbar.
    /// </summary>
    public partial class App
    {
        private void InitTimer()
        {
            // Create a timer to display traces asynchronously.
            AppTimer = new Timer(AppTimerCallback);
            AppTimer.Change(CheckInterval, CheckInterval);
        }

        private void AppTimerCallback(object parameter)
        {
            // If a shutdown is started, don't show traces anymore so the shutdown can complete smoothly.
            if (IsExiting)
                return;

            // If another instance is requesting exit, schedule a task to do it.
            if (IsAnotherInstanceRequestingExit)
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new System.Action(OnExitRequested));
            else
            {
                // Print traces asynchronously from the timer thread.
                UpdateLogger();

                // Also, schedule an update of the icon and tooltip if they changed, or the first time.
                if (AppTimerOperation == null || (AppTimerOperation.Status == DispatcherOperationStatus.Completed && GetIsIconOrToolTipChanged()))
                    AppTimerOperation = Dispatcher.BeginInvoke(DispatcherPriority.Normal, new System.Action(OnAppTimer));
            }
        }

        private void OnExitRequested()
        {
            Shutdown();
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
            using (AppTimer)
            {
            }
        }

        private Timer AppTimer = new Timer((object parameter) => { });
        private DispatcherOperation? AppTimerOperation;
        private TimeSpan CheckInterval = TimeSpan.FromSeconds(0.1);
    }
}
