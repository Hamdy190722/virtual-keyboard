using VirtualKeyboard.Core.Input;
using VirtualKeyboard.Core.Layouts;

namespace VirtualKeyboard.Core.Sticky;

/// <summary>
/// One-shot / locked sticky modifiers.
/// Tap cycles Off -> Armed -> Locked -> Off. Armed is consumed by the next key.
/// </summary>
public sealed class StickyModifiers
{
    private readonly Dictionary<ModifierKind, ModifierState> _states =
        Enum.GetValues<ModifierKind>().ToDictionary(k => k, _ => ModifierState.Off);

    public ModifierState GetState(ModifierKind kind) => _states[kind];

    public bool IsActive(ModifierKind kind) => _states[kind] != ModifierState.Off;

    public bool IsLocked(ModifierKind kind) => _states[kind] == ModifierState.Locked;

    public bool AnyActive => _states.Values.Any(s => s != ModifierState.Off);

    public void Tap(ModifierKind kind)
    {
        _states[kind] = _states[kind] switch
        {
            ModifierState.Off => ModifierState.Armed,
            ModifierState.Armed => ModifierState.Locked,
            _ => ModifierState.Off
        };
    }

    public void SetLocked(ModifierKind kind, bool locked)
    {
        _states[kind] = locked ? ModifierState.Locked : ModifierState.Off;
    }

    public StickySnapshot Snapshot() => new(
        Shift: IsActive(ModifierKind.Shift),
        Ctrl: IsActive(ModifierKind.Ctrl),
        Alt: IsActive(ModifierKind.Alt),
        Win: IsActive(ModifierKind.Win));

    /// <summary>Clears one-shot (armed) modifiers after a key was typed. Locked ones stay.</summary>
    public bool Consume()
    {
        var changed = false;
        foreach (var kind in Enum.GetValues<ModifierKind>())
        {
            if (_states[kind] == ModifierState.Armed)
            {
                _states[kind] = ModifierState.Off;
                changed = true;
            }
        }

        return changed;
    }

    public void Reset()
    {
        foreach (var kind in Enum.GetValues<ModifierKind>())
        {
            _states[kind] = ModifierState.Off;
        }
    }
}
