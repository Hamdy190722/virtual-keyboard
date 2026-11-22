using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace VirtualKeyboard.App.Services;

/// <summary>
/// Win32 window plumbing: keep the keyboard from ever taking focus, ensure a
/// taskbar button via WS_EX_APPWINDOW, round its corners, and show/restore
/// without activation.
/// </summary>
public static class WindowEffects
{
    private const int GwlExStyle = -20;
    private const long WsExNoActivate = 0x08000000;
    private const long WsExAppWindow = 0x00040000;
    private const long WsExToolWindow = 0x00000080;

    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;
    private const int WmSysCommand = 0x0112;
    private const int ScRestore = 0xF120;

    private const int SwShowNoActivate = 4;
    private const int SwMinimize = 6;

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);

    public static void MakeNonActivating(Window window, Action? onTaskbarRestore = null)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        // WS_EX_NOACTIVATE suppresses the taskbar button unless WS_EX_APPWINDOW
        // is also set and WS_EX_TOOLWINDOW is cleared.
        var style = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        var updatedStyle = (style | WsExNoActivate | WsExAppWindow) & ~WsExToolWindow;
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(updatedStyle));

        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook((IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (msg == WmMouseActivate)
            {
                handled = true;
                return new IntPtr(MaNoActivate);
            }

            if (msg == WmSysCommand && (wParam.ToInt64() & 0xFFF0) == ScRestore && onTaskbarRestore is not null)
            {
                handled = true;
                window.Dispatcher.BeginInvoke(onTaskbarRestore);
                return IntPtr.Zero;
            }

            return IntPtr.Zero;
        });
    }

    public static void RoundCorners(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2 (Windows 11 only; ignored elsewhere).
        var preference = 2;
        try
        {
            DwmSetWindowAttribute(hwnd, 33, ref preference, sizeof(int));
        }
        catch
        {
            // Windows 10 has no rounded-corner API.
        }
    }

    public static void MinimizeNoActivate(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        ShowWindow(hwnd, SwMinimize);
    }

    public static void RestoreNoActivate(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        ShowWindow(hwnd, SwShowNoActivate);
        BringToFrontNoActivate(window);
    }

    public static void BringToFrontNoActivate(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
