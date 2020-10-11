namespace TaskbarIconHost
{
    using System.Runtime.InteropServices;

#pragma warning disable SA1600 // Elements should be documented
    internal class NativeMethods
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        public static extern void OutputDebugString([In][MarshalAs(UnmanagedType.LPWStr)] string message);
    }
#pragma warning restore SA1600 // Elements should be documented
}