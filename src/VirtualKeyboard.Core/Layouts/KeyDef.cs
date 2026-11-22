namespace VirtualKeyboard.Core.Layouts;

/// <summary>Virtual-key codes used by the injection layer.</summary>
public static class VirtualKeys
{
    public const ushort Back = 0x08;
    public const ushort Tab = 0x09;
    public const ushort Enter = 0x0D;
    public const ushort Shift = 0x10;
    public const ushort Ctrl = 0x11;
    public const ushort Alt = 0x12;
    public const ushort CapsLock = 0x14;
    public const ushort Escape = 0x1B;
    public const ushort Space = 0x20;
    public const ushort PageUp = 0x21;
    public const ushort PageDown = 0x22;
    public const ushort End = 0x23;
    public const ushort Home = 0x24;
    public const ushort Left = 0x25;
    public const ushort Up = 0x26;
    public const ushort Right = 0x27;
    public const ushort Down = 0x28;
    public const ushort Delete = 0x2E;
    public const ushort A = 0x41;
    public const ushort Win = 0x5B;
}

/// <summary>One key on the on-screen keyboard.</summary>
public sealed record KeyDef
{
    public required string Label { get; init; }
    public KeyKind Kind { get; init; } = KeyKind.Text;

    /// <summary>Text injected when pressed (unshifted).</summary>
    public string? Text { get; init; }

    /// <summary>Text injected when Shift is sticky. Falls back to <see cref="Text"/>.</summary>
    public string? ShiftText { get; init; }

    public ushort? VirtualKey { get; init; }
    public bool Extended { get; init; }
    public ModifierKind? Modifier { get; init; }

    /// <summary>Relative width in a row (star sizing).</summary>
    public double Width { get; init; } = 1;

    public bool Repeatable { get; init; }

    /// <summary>Extra strings offered on long-press.</summary>
    public IReadOnlyList<string> Alternates { get; init; } = Array.Empty<string>();

    public static KeyDef Letter(string label, string? shift = null, params string[] alternates) => new()
    {
        Label = label,
        Text = label,
        ShiftText = shift,
        Alternates = alternates
    };

    public static KeyDef Symbol(string label, string? shift = null, double width = 1) => new()
    {
        Label = label,
        Text = label,
        ShiftText = shift,
        Width = width
    };

    public static KeyDef SpaceKey(double width = 6) => new()
    {
        Label = "Space",
        Text = " ",
        Width = width,
        Repeatable = true
    };

    public static KeyDef Key(string label, ushort virtualKey, double width = 1, bool repeatable = false, bool extended = false) => new()
    {
        Label = label,
        Kind = KeyKind.VirtualKey,
        VirtualKey = virtualKey,
        Width = width,
        Repeatable = repeatable,
        Extended = extended
    };

    public static KeyDef Mod(string label, ModifierKind kind, double width = 1) => new()
    {
        Label = label,
        Kind = KeyKind.Modifier,
        Modifier = kind,
        Width = width
    };

    public static KeyDef CapsKey(double width = 1.75) => new()
    {
        Label = "Caps",
        Kind = KeyKind.CapsToggle,
        Width = width
    };

    public static KeyDef LayoutToggle(string label, double width = 1.5) => new()
    {
        Label = label,
        Kind = KeyKind.LayoutToggle,
        Width = width
    };

    public static KeyDef ClearAllKey(string label = "Clear All", double width = 2) => new()
    {
        Label = label,
        Kind = KeyKind.ClearAll,
        Width = width
    };
}

public sealed record KeyboardLayout(
    string Id,
    string DisplayName,
    string CultureName,
    bool IsRightToLeft,
    IReadOnlyList<IReadOnlyList<KeyDef>> Rows);
