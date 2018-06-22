using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;

namespace TaskbarIconHost
{
    public static class Taskbar
    {
        #region Init
        static Taskbar()
        {
            UpdateLocation();

            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        private static void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            UpdateLocation();
        }

        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.Desktop)
                UpdateLocation();
        }

        private static void UpdateLocation()
        {
            TaskbarHandle = IntPtr.Zero;
            CurrentScreen = null;

            IntPtr hwnd;
            if (GetSystemTrayHandle(out hwnd))
            {
                TaskbarHandle = hwnd;

                RECT TrayRect, NotificationAreaRect, IconAreaRect;
                if (GetSystemTrayRect(out TrayRect, out NotificationAreaRect, out IconAreaRect))
                {
                    System.Drawing.Rectangle TrayDrawingRect = new System.Drawing.Rectangle(TrayRect.Left, TrayRect.Top, TrayRect.Right - TrayRect.Left, TrayRect.Bottom - TrayRect.Top);
                    Dictionary<Screen, int> AreaTable = new Dictionary<Screen, int>();

                    foreach (Screen s in Screen.AllScreens)
                    {
                        System.Drawing.Rectangle ScreenDrawingRect = s.Bounds;
                        ScreenDrawingRect.Intersect(TrayDrawingRect);
                        int IntersectionArea = ScreenDrawingRect.Width * ScreenDrawingRect.Height;

                        //System.Diagnostics.Debug.Print(s.DeviceName + ": " + IntersectionArea);
                        AreaTable.Add(s, IntersectionArea);
                    }

                    Screen SelectedScreen = null;
                    int SmallestPositiveArea = 0;

                    foreach (KeyValuePair<Screen, int> Entry in AreaTable)
                    {
                        if (SelectedScreen == null || (Entry.Value > 0 && (SmallestPositiveArea == 0 || SmallestPositiveArea > Entry.Value)))
                        {
                            SelectedScreen = Entry.Key;
                            SmallestPositiveArea = Entry.Value;
                        }
                    }

                    CurrentScreen = SelectedScreen;
                    //System.Diagnostics.Debug.Print("Selected: " + CurrentScreen.DeviceName);
                }
            }
        }

        private static IntPtr TaskbarHandle;
        private static Screen CurrentScreen;
        #endregion

        #region User Interface
        // Bounds of the current screen used to display the taskbar.
        public static System.Drawing.Rectangle ScreenBounds
        {
            get { return CurrentScreen == null ? System.Drawing.Rectangle.Empty : CurrentScreen.Bounds; }
        }

        // Return the position a FrameworkElement should take to be on the edge of the task bar. In screen coordinates.
        public static Point GetRelativePosition(FrameworkElement element)
        {
            if (element == null || double.IsNaN(element.ActualWidth) || double.IsNaN(element.ActualHeight) || ScreenBounds.IsEmpty)
                return new Point(double.NaN, double.NaN);

            System.Drawing.Point FormsMousePosition = Control.MousePosition;
            Point MousePosition = new Point(FormsMousePosition.X, FormsMousePosition.Y);

            Rect WorkArea = SystemParameters.WorkArea;

            double WorkScreenWidth = WorkArea.Right - WorkArea.Left;
            double WorkScreenHeight = WorkArea.Bottom - WorkArea.Top;
            double CurrentScreenWidth = ScreenBounds.Right - ScreenBounds.Left;
            double CurrentScreenHeight = ScreenBounds.Bottom - ScreenBounds.Top;

            double RatioX = WorkScreenWidth / CurrentScreenWidth;
            double RatioY = WorkScreenHeight / CurrentScreenHeight;

            Size PopupSize = new Size((int)(element.ActualWidth / RatioX), (int)(element.ActualHeight / RatioY));
            Point RelativePosition = Taskbar.GetRelativePosition(MousePosition, PopupSize);

            RelativePosition = new Point(RelativePosition.X * RatioX, RelativePosition.Y * RatioY);

            return RelativePosition;
        }

        // From a position, and a window size, all in screen coordinates, return the position the window should take
        // to be on the edge of the task bar. In screen coordinates.
        private static Point GetRelativePosition(Point Position, Size Size)
        {
            RECT TrayRect, NotificationAreaRect, IconAreaRect;
            if (CurrentScreen == null ||!GetSystemTrayRect(out TrayRect, out NotificationAreaRect, out IconAreaRect))
                return new Point(0, 0);

            // Use the full taskbar rectangle.
            RECT TaskbarRect = TrayRect;

            double X;
            double Y;

            // If the potion isn't within the taskbar (shouldn't happen), default to bottom.
            if (!(Position.X >= TaskbarRect.Left && Position.X < TaskbarRect.Right && Position.Y >= TaskbarRect.Top && Position.Y < TaskbarRect.Bottom))
                AlignedToBottom(Position, Size, TaskbarRect, out X, out Y);

            else
            {
                // Otherwise, check where the taskbar is, and calculate an aligned position.
                switch (GetTaskBarLocation(TaskbarRect))
                {
                    case TaskBarLocation.Top:
                        AlignedToTop(Position, Size, TaskbarRect, out X, out Y);
                        break;

                    default:
                    case TaskBarLocation.Bottom:
                        AlignedToBottom(Position, Size, TaskbarRect, out X, out Y);
                        break;

                    case TaskBarLocation.Left:
                        AlignedToLeft(Position, Size, TaskbarRect, out X, out Y);
                        break;

                    case TaskBarLocation.Right:
                        AlignedToRight(Position, Size, TaskbarRect, out X, out Y);
                        break;
                }
            }

            return new Point(X, Y);
        }
        #endregion

        #region Implementation
        private enum TaskBarLocation { Top, Bottom, Left, Right }

        private static TaskBarLocation GetTaskBarLocation(RECT TaskbarRect)
        {
            Point TaskbarCenter = new Point((TaskbarRect.Left + TaskbarRect.Right) / 2, (TaskbarRect.Top + TaskbarRect.Bottom) / 2);

            bool IsTop = (TaskbarCenter.Y < CurrentScreen.WorkingArea.Top + (CurrentScreen.WorkingArea.Bottom - CurrentScreen.WorkingArea.Top) / 4);
            bool IsBottom = (TaskbarCenter.Y >= CurrentScreen.WorkingArea.Bottom - (CurrentScreen.WorkingArea.Bottom - CurrentScreen.WorkingArea.Top) / 4);
            bool IsLeft = (TaskbarCenter.X < CurrentScreen.WorkingArea.Left + (CurrentScreen.WorkingArea.Right - CurrentScreen.WorkingArea.Left) / 4);
            bool IsRight = (TaskbarCenter.X >= CurrentScreen.WorkingArea.Right - (CurrentScreen.WorkingArea.Right - CurrentScreen.WorkingArea.Left) / 4);

            if (IsTop && !IsLeft && !IsRight)
                return TaskBarLocation.Top;

            else if (IsBottom && !IsLeft && !IsRight)
                return TaskBarLocation.Bottom;

            else if (IsLeft && !IsTop && !IsBottom)
                return TaskBarLocation.Left;

            else if (IsRight && !IsTop && !IsBottom)
                return TaskBarLocation.Right;

            else
                return TaskBarLocation.Bottom;
        }

        private static void AlignedToLeft(Point Position, Size Size, RECT TaskbarRect, out double X, out double Y)
        {
            X = TaskbarRect.Right;
            Y = Position.Y - Size.Height / 2;
        }

        private static void AlignedToRight(Point Position, Size Size, RECT TaskbarRect, out double X, out double Y)
        {
            X = TaskbarRect.Left - Size.Width;
            Y = Position.Y - Size.Height / 2;
        }

        private static void AlignedToTop(Point Position, Size Size, RECT TaskbarRect, out double X, out double Y)
        {
            X = Position.X - Size.Width / 2;
            Y = TaskbarRect.Bottom;
        }

        private static void AlignedToBottom(Point Position, Size Size, RECT TaskbarRect, out double X, out double Y)
        {
            X = Position.X - Size.Width / 2;
            Y = TaskbarRect.Top - Size.Height;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, ref RECT rect);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        private static bool GetSystemTrayHandle(out IntPtr hwnd)
        {
            hwnd = IntPtr.Zero;

            IntPtr hWndTray = FindWindow("Shell_TrayWnd", null);
            if (hWndTray != IntPtr.Zero)
            {
                hwnd = hWndTray;

                hWndTray = FindWindowEx(hWndTray, IntPtr.Zero, "TrayNotifyWnd", null);
                if (hWndTray != IntPtr.Zero)
                {
                    hWndTray = FindWindowEx(hWndTray, IntPtr.Zero, "SysPager", null);
                    if (hWndTray != IntPtr.Zero)
                    {
                        hWndTray = FindWindowEx(hWndTray, IntPtr.Zero, "ToolbarWindow32", null);
                        if (hWndTray != IntPtr.Zero)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool GetSystemTrayRect(out RECT TrayRect, out RECT NotificationAreaRect, out RECT IconAreaRect)
        {
            TrayRect = new RECT() { Left = 0, Top = 0, Right = 0, Bottom = 0 };
            NotificationAreaRect = new RECT() { Left = 0, Top = 0, Right = 0, Bottom = 0 };
            IconAreaRect = new RECT() { Left = 0, Top = 0, Right = 0, Bottom = 0 };

            IntPtr hWndTray = FindWindow("Shell_TrayWnd", null);
            if (hWndTray != IntPtr.Zero)
            {
                GetWindowRect(hWndTray, ref TrayRect);

                hWndTray = FindWindowEx(hWndTray, IntPtr.Zero, "TrayNotifyWnd", null);
                if (hWndTray != IntPtr.Zero)
                {
                    GetWindowRect(hWndTray, ref NotificationAreaRect);

                    hWndTray = FindWindowEx(hWndTray, IntPtr.Zero, "SysPager", null);
                    if (hWndTray != IntPtr.Zero)
                    {
                        hWndTray = FindWindowEx(hWndTray, IntPtr.Zero, "ToolbarWindow32", null);
                        if (hWndTray != IntPtr.Zero)
                        {
                            GetWindowRect(hWndTray, ref IconAreaRect);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool ToScreen(ref Point Position)
        {
            POINT p1 = new POINT() { X = 0, Y = 0 };
            POINT p2 = new POINT() { X = 1000, Y = 1000 };

            if (TaskbarHandle != IntPtr.Zero && ClientToScreen(TaskbarHandle, ref p1) && ClientToScreen(TaskbarHandle, ref p2))
            {
                double RatioX = (double)(p2.X - p1.X) / 1000;
                double RatioY = (double)(p2.Y - p1.Y) / 1000;

                Position = new Point(Position.X * RatioX, Position.Y * RatioY);
                return true;
            }

            return false;
        }

        private static bool ToScreen(ref Size Size)
        {
            POINT p1 = new POINT() { X = 0, Y = 0 };
            POINT p2 = new POINT() { X = 1000, Y = 1000 };

            if (TaskbarHandle != IntPtr.Zero && ClientToScreen(TaskbarHandle, ref p1) && ClientToScreen(TaskbarHandle, ref p2))
            {
                double RatioX = (double)(p2.X - p1.X) / 1000;
                double RatioY = (double)(p2.Y - p1.Y) / 1000;

                Size = new Size(Size.Width * RatioX, Size.Height * RatioY);
                return true;
            }

            return false;
        }

        private static bool ToClient(ref Point Position)
        {
            POINT p1 = new POINT() { X = 0, Y = 0 };
            POINT p2 = new POINT() { X = 1000, Y = 1000 };

            if (TaskbarHandle != IntPtr.Zero && ScreenToClient(TaskbarHandle, ref p1) && ScreenToClient(TaskbarHandle, ref p2))
            {
                double RatioX = (double)(p2.X - p1.X) / 1000;
                double RatioY = (double)(p2.Y - p1.Y) / 1000;

                Position = new Point(Position.X * RatioX, Position.Y * RatioY);
                return true;
            }

            return false;
        }

        private static bool ToClient(ref Size Size)
        {
            POINT p1 = new POINT() { X = 0, Y = 0 };
            POINT p2 = new POINT() { X = 1000, Y = 1000 };

            if (TaskbarHandle != IntPtr.Zero && ScreenToClient(TaskbarHandle, ref p1) && ScreenToClient(TaskbarHandle, ref p2))
            {
                double RatioX = (double)(p2.X - p1.X) / 1000;
                double RatioY = (double)(p2.Y - p1.Y) / 1000;

                Size = new Size(Size.Width * RatioX, Size.Height * RatioY);
                return true;
            }

            return false;
        }
        #endregion
    }
}
