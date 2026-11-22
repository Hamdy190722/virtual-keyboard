namespace VirtualKeyboard.Core.Input;

public enum InputEventType
{
    VirtualKeyDown,
    VirtualKeyUp,
    UnicodeChar
}

/// <summary>A single low-level input action. Pure data so planning can be unit-tested.</summary>
public readonly record struct InputEvent(
    InputEventType Type,
    ushort VirtualKey = 0,
    ushort ScanCode = 0,
    char Character = '\0',
    bool Extended = false)
{
    public static InputEvent KeyDown(ushort vk, bool extended = false) =>
        new(InputEventType.VirtualKeyDown, vk, Extended: extended);

    public static InputEvent KeyUp(ushort vk, bool extended = false) =>
        new(InputEventType.VirtualKeyUp, vk, Extended: extended);

    public static InputEvent Unicode(char ch) =>
        new(InputEventType.UnicodeChar, Character: ch);
}

public interface IKeyInjector
{
    void Send(IReadOnlyList<InputEvent> events);
}
