using System.Runtime.InteropServices;

namespace VirtualKeyboard.Core.Input;

/// <summary>Windows SendInput backend. All events of one key press go out in a single call.</summary>
public sealed class SendInputInjector : IKeyInjector
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const uint KeyEventExtendedKey = 0x0001;

    public void Send(IReadOnlyList<InputEvent> events)
    {
        if (events.Count == 0)
        {
            return;
        }

        var inputs = new List<INPUT>(events.Count * 2);
        foreach (var evt in events)
        {
            switch (evt.Type)
            {
                case InputEventType.UnicodeChar:
                    inputs.Add(MakeKeyboardInput(0, evt.Character, KeyEventUnicode));
                    inputs.Add(MakeKeyboardInput(0, evt.Character, KeyEventUnicode | KeyEventKeyUp));
                    break;

                case InputEventType.VirtualKeyDown:
                    inputs.Add(MakeKeyboardInput(evt.VirtualKey, '\0', evt.Extended ? KeyEventExtendedKey : 0));
                    break;

                case InputEventType.VirtualKeyUp:
                    inputs.Add(MakeKeyboardInput(
                        evt.VirtualKey,
                        '\0',
                        KeyEventKeyUp | (evt.Extended ? KeyEventExtendedKey : 0)));
                    break;
            }
        }

        var array = inputs.ToArray();
        var sent = SendInput((uint)array.Length, array, Marshal.SizeOf<INPUT>());
        if (sent == 0)
        {
            // Nothing useful to do here; the caller's UI stays responsive either way.
            _ = Marshal.GetLastWin32Error();
        }
    }

    private static INPUT MakeKeyboardInput(ushort vk, char scan, uint flags)
    {
        var scanCode = vk != 0 ? (ushort)MapVirtualKey(vk, 0) : scan;
        return new INPUT
        {
            type = InputKeyboard,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = scanCode,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }
}
