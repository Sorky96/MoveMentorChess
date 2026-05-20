using System.Runtime.InteropServices;

namespace MoveMentorChess.Tracking;

internal static partial class NativeMethods
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

    [LibraryImport("user32.dll")]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial int GetWindowText(nint hWnd, [Out] char[] lpString, int nMaxCount);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindow(nint hWnd);
}
