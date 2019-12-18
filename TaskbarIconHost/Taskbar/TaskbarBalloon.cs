namespace TaskbarTools
{
    using System.Drawing;
    using System.Threading;
    using System.Windows.Forms;

    /// <summary>
    /// This class provides an API to display notifications to the user.
    /// </summary>
    public static class TaskbarBalloon
    {
        #region Client Interface
        /// <summary>
        /// Display a notification in a taskbar balloon.
        /// </summary>
        /// <param name="text">The text to show.</param>
        /// <param name="delay">The delay, in milliseconds.</param>
        public static void Show(string text, short delay = 5000)
        {
            try
            {
                using (NotifyIcon notification = new NotifyIcon() { Visible = true, Icon = SystemIcons.Shield, Text = text, BalloonTipText = text })
                {
                    notification.ShowBalloonTip(delay);
                    Thread.Sleep(delay);
                }
            }
            catch
            {
            }
        }
        #endregion
    }
}
