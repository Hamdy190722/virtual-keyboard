using VirtualKeyboard.Core.Layouts;

namespace VirtualKeyboard.Core.Input;

/// <summary>Active sticky modifiers at the moment a key is pressed.</summary>
public readonly record struct StickySnapshot(bool Shift, bool Ctrl, bool Alt, bool Win)
{
    public bool HasCtrlLike => Ctrl || Alt || Win;
    public bool HasAny => Shift || Ctrl || Alt || Win;
    public bool IsActive(ModifierKind kind) => kind switch
    {
        ModifierKind.Shift => Shift,
        ModifierKind.Ctrl => Ctrl,
        ModifierKind.Alt => Alt,
        ModifierKind.Win => Win,
        _ => false
    };
}
