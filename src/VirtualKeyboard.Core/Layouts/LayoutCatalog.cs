using static VirtualKeyboard.Core.Layouts.KeyDef;

namespace VirtualKeyboard.Core.Layouts;

/// <summary>Built-in keyboard layouts. Layout data is code so it can be extended safely.</summary>
public static class LayoutCatalog
{
    public const string EnglishId = "en";
    public const string ArabicId = "ar";

    public static IReadOnlyList<KeyboardLayout> All => new[] { English, Arabic };

    public static KeyboardLayout Get(string? id) =>
        All.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase)) ?? English;

    public static KeyboardLayout English { get; } = new(
        EnglishId,
        "English",
        "en-US",
        IsRightToLeft: false,
        new IReadOnlyList<KeyDef>[]
        {
            new KeyDef[]
            {
                Symbol("`", "~"), Symbol("1", "!"), Symbol("2", "@"), Symbol("3", "#"),
                Symbol("4", "$"), Symbol("5", "%"), Symbol("6", "^"), Symbol("7", "&"),
                Symbol("8", "*"), Symbol("9", "("), Symbol("0", ")"), Symbol("-", "_"),
                Symbol("=", "+"), Key("⌫", VirtualKeys.Back, 2, repeatable: true)
            },
            new KeyDef[]
            {
                Key("Tab", VirtualKeys.Tab, 1.5),
                Letter("q", "Q"), Letter("w", "W"), Letter("e", "E", "é", "è", "ê", "ë"),
                Letter("r", "R", "ř"), Letter("t", "T", "þ"), Letter("y", "Y", "ý", "ÿ"),
                Letter("u", "U", "ú", "ù", "û", "ü"), Letter("i", "I", "í", "ì", "î", "ï"),
                Letter("o", "O", "ó", "ò", "ô", "ö", "õ", "ø"), Letter("p", "P"),
                Symbol("[", "{"), Symbol("]", "}"), Symbol("\\", "|", 1.5)
            },
            new KeyDef[]
            {
                CapsKey(1.75),
                Letter("a", "A", "à", "á", "â", "ä", "ã", "å"), Letter("s", "S", "ß"),
                Letter("d", "D", "ð"), Letter("f", "F"), Letter("g", "G", "ğ"),
                Letter("h", "H"), Letter("j", "J"), Letter("k", "K"),
                Letter("l", "L", "ł"), Symbol(";", ":"), Symbol("'", "\""),
                Key("Enter", VirtualKeys.Enter, 2.25)
            },
            new KeyDef[]
            {
                Mod("Shift", ModifierKind.Shift, 2.25),
                Letter("z", "Z", "ž"), Letter("x", "X"), Letter("c", "C", "ç"),
                Letter("v", "V"), Letter("b", "B"), Letter("n", "N", "ñ"),
                Letter("m", "M"), Symbol(",", "<"), Symbol(".", ">"), Symbol("/", "?"),
                Mod("Shift", ModifierKind.Shift, 2.75)
            },
            new KeyDef[]
            {
                Mod("Ctrl", ModifierKind.Ctrl, 1.25), Mod("Win", ModifierKind.Win, 1.25),
                Mod("Alt", ModifierKind.Alt, 1.25), SpaceKey(6),
                Mod("Alt", ModifierKind.Alt, 1.25), Mod("Win", ModifierKind.Win, 1.25),
                Mod("Ctrl", ModifierKind.Ctrl, 1.25), LayoutToggle("ع", 1.5)
            },
            new KeyDef[]
            {
                Key("Esc", VirtualKeys.Escape, 1.25), Key("Home", VirtualKeys.Home, 1.25),
                Key("End", VirtualKeys.End, 1.25), Key("Del", VirtualKeys.Delete, 1.25),
                ClearAllKey("Clear All", 2),
                Key("PgUp", VirtualKeys.PageUp, 1.25), Key("PgDn", VirtualKeys.PageDown, 1.25),
                Key("←", VirtualKeys.Left, 1.375, repeatable: true, extended: true),
                Key("↑", VirtualKeys.Up, 1.375, repeatable: true, extended: true),
                Key("↓", VirtualKeys.Down, 1.375, repeatable: true, extended: true),
                Key("→", VirtualKeys.Right, 1.375, repeatable: true, extended: true)
            }
        });

    public static KeyboardLayout Arabic { get; } = new(
        ArabicId,
        "العربية",
        "ar-SA",
        IsRightToLeft: true,
        new IReadOnlyList<KeyDef>[]
        {
            new KeyDef[]
            {
                Symbol("ذ", "ّ"), Symbol("١", "!"), Symbol("٢", "@"), Symbol("٣", "#"),
                Symbol("٤", "$"), Symbol("٥", "%"), Symbol("٦", "^"), Symbol("٧", "&"),
                Symbol("٨", "*"), Symbol("٩", ")"), Symbol("٠", "("), Symbol("-", "_"),
                Symbol("=", "+"), Key("⌫", VirtualKeys.Back, 2, repeatable: true)
            },
            new KeyDef[]
            {
                Key("Tab", VirtualKeys.Tab, 1.5),
                Letter("ض", "َ"), Letter("ص", "ً"), Letter("ث", "ُ"), Letter("ق", "ٌ"),
                Letter("ف", "ِ"), Letter("غ", "ٍ"), Letter("ع", "ْ"), Letter("ه", "ّ"),
                Letter("خ", "]"), Letter("ح", "["), Letter("ج", "}"), Letter("د", "{"),
                Symbol("\\", "|", 1.5)
            },
            new KeyDef[]
            {
                CapsKey(1.75),
                Letter("ش", "أ"), Letter("س", "إ"), Letter("ي", "آ"), Letter("ب", "ؤ"),
                Letter("ل", "ئ"), Letter("ا", "ء", "أ", "إ", "آ", "ٱ"), Letter("ت", "ة"),
                Letter("ن", "ى"), Letter("م", "ـ"), Letter("ك", "،"), Letter("ط", "؟"),
                Key("Enter", VirtualKeys.Enter, 2.25)
            },
            new KeyDef[]
            {
                Mod("Shift", ModifierKind.Shift, 2.25),
                Letter("ئ", "َ"), Letter("ء", "ُ"), Letter("ؤ", "ِ"), Letter("ر", "ْ"),
                Letter("لا", "ّ"), Letter("ى", "ً"), Letter("ة", "ٌ"), Letter("و", "ٍ"),
                Letter("ز", "ٰ"), Letter("ظ", "ـ"),
                Mod("Shift", ModifierKind.Shift, 2.75)
            },
            new KeyDef[]
            {
                Mod("Ctrl", ModifierKind.Ctrl, 1.25), Mod("Win", ModifierKind.Win, 1.25),
                Mod("Alt", ModifierKind.Alt, 1.25), SpaceKey(6),
                Mod("Alt", ModifierKind.Alt, 1.25), Mod("Win", ModifierKind.Win, 1.25),
                Mod("Ctrl", ModifierKind.Ctrl, 1.25), LayoutToggle("EN", 1.5)
            },
            new KeyDef[]
            {
                Key("Esc", VirtualKeys.Escape, 1.25), Key("Home", VirtualKeys.Home, 1.25),
                Key("End", VirtualKeys.End, 1.25), Key("Del", VirtualKeys.Delete, 1.25),
                ClearAllKey("Clear All", 2),
                Key("PgUp", VirtualKeys.PageUp, 1.25), Key("PgDn", VirtualKeys.PageDown, 1.25),
                Key("←", VirtualKeys.Left, 1.375, repeatable: true, extended: true),
                Key("↑", VirtualKeys.Up, 1.375, repeatable: true, extended: true),
                Key("↓", VirtualKeys.Down, 1.375, repeatable: true, extended: true),
                Key("→", VirtualKeys.Right, 1.375, repeatable: true, extended: true)
            }
        });
}
