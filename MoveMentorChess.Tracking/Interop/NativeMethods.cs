using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MoveMentorChess.Tracking;

internal static class NativeMethods
{
    public static WindowCaptureInfo? TryGetForegroundWindowInfo()
    {
        nint handle = GetForegroundWindow();
        if (handle == 0)
        {
            return null;
        }

        char[] titleBuffer = new char[512];
        int titleLen = GetWindowText(handle, titleBuffer, titleBuffer.Length);
        string title = new string(titleBuffer, 0, titleLen).Trim();
        return string.IsNullOrWhiteSpace(title) ? null : new WindowCaptureInfo(handle, title);
    }

    public static bool WindowExists(nint handle) => handle != 0 && IsWindow(handle);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(nint hWnd, [Out] char[] lpString, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hWnd);
}
