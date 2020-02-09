namespace TaskbarIconHost
{
    using System.Runtime.InteropServices;

    internal class NativeMethods
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        public static extern void OutputDebugString([In][MarshalAs(UnmanagedType.LPWStr)] string message);
    }
}