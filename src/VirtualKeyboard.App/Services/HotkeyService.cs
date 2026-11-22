using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace VirtualKeyboard.App.Services;

/// <summary>Global Ctrl+Alt+K hotkey registered against the keyboard window.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0xB001;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkK = 0x4B;

    private readonly IntPtr _hwnd;
    private readonly HwndSource? _source;
    private readonly bool _registered;

    public HotkeyService(Window window)
    {
        _hwnd = new WindowInteropHelper(window).Handle;
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        _registered = RegisterHotKey(_hwnd, HotkeyId, ModControl | ModAlt | ModNoRepeat, VkK);
    }

    public event EventHandler? Pressed;

    public bool IsRegistered => _registered;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
        }

        _source?.RemoveHook(WndProc);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
