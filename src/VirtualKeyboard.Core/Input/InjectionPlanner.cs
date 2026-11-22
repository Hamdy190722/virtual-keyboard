using VirtualKeyboard.Core.Layouts;

namespace VirtualKeyboard.Core.Input;

/// <summary>
/// Turns a logical key press into the exact sequence of low-level events.
/// Rules:
///  - Printable characters are sent as Unicode (layout independent, Arabic safe).
///  - Control keys and Ctrl/Alt/Win combos are sent as virtual keys.
///  - Shift is resolved by the layout data, never synthesized for characters.
/// </summary>
public static class InjectionPlanner
{
    public static IReadOnlyList<InputEvent> Plan(
        string? text,
        ushort? virtualKey,
        bool extended,
        StickySnapshot modifiers)
    {
        var events = new List<InputEvent>(8);

        if (virtualKey is { } vk)
        {
            AppendModifierDowns(events, modifiers);
            events.Add(InputEvent.KeyDown(vk, extended));
            events.Add(InputEvent.KeyUp(vk, extended));
            AppendModifierUps(events, modifiers);
            return events;
        }

        if (string.IsNullOrEmpty(text))
        {
            return events;
        }

        if (modifiers.HasCtrlLike && text.Length == 1 && TryGetAsciiVirtualKey(text[0], out var comboVk))
        {
            AppendModifierDowns(events, modifiers);
            events.Add(InputEvent.KeyDown(comboVk));
            events.Add(InputEvent.KeyUp(comboVk));
            AppendModifierUps(events, modifiers);
            return events;
        }

        foreach (var ch in text)
        {
            events.Add(InputEvent.Unicode(ch));
        }

        return events;
    }

    private static void AppendModifierDowns(List<InputEvent> events, StickySnapshot mods)
    {
        if (mods.Ctrl) events.Add(InputEvent.KeyDown(VirtualKeys.Ctrl));
        if (mods.Shift) events.Add(InputEvent.KeyDown(VirtualKeys.Shift));
        if (mods.Alt) events.Add(InputEvent.KeyDown(VirtualKeys.Alt));
        if (mods.Win) events.Add(InputEvent.KeyDown(VirtualKeys.Win));
    }

    private static void AppendModifierUps(List<InputEvent> events, StickySnapshot mods)
    {
        if (mods.Win) events.Add(InputEvent.KeyUp(VirtualKeys.Win));
        if (mods.Alt) events.Add(InputEvent.KeyUp(VirtualKeys.Alt));
        if (mods.Shift) events.Add(InputEvent.KeyUp(VirtualKeys.Shift));
        if (mods.Ctrl) events.Add(InputEvent.KeyUp(VirtualKeys.Ctrl));
    }

    /// <summary>Maps ASCII letters, digits and common punctuation to virtual-key codes.</summary>
    public static bool TryGetAsciiVirtualKey(char ch, out ushort virtualKey)
    {
        virtualKey = ch switch
        {
            >= 'a' and <= 'z' => (ushort)(0x41 + (ch - 'a')),
            >= 'A' and <= 'Z' => (ushort)(0x41 + (ch - 'A')),
            >= '0' and <= '9' => (ushort)(0x30 + (ch - '0')),
            ' ' => 0x20,
            ';' => 0xBA,
            '=' => 0xBB,
            ',' => 0xBC,
            '-' => 0xBD,
            '.' => 0xBE,
            '/' => 0xBF,
            '`' => 0xC0,
            '[' => 0xDB,
            '\\' => 0xDC,
            ']' => 0xDD,
            '\'' => 0xDE,
            _ => 0
        };
        return virtualKey != 0;
    }
}
