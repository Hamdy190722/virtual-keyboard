namespace VirtualKeyboard.Core.Layouts;

public enum ModifierKind
{
    Shift,
    Ctrl,
    Alt,
    Win
}

public enum ModifierState
{
    Off,
    Armed,
    Locked
}

public enum KeyKind
{
    Text,
    VirtualKey,
    Modifier,
    CapsToggle,
    LayoutToggle,
    ClearAll
}
