namespace VirtualKeyboard.Core.Settings;

public sealed record ThemeInfo(
    string Id,
    string DisplayName,
    string Category,
    string SurfaceHex,
    string KeyHex,
    string AccentHex);

/// <summary>Built-in keyboard themes shared by Core validation, WPF UI, and the tray menu.</summary>
public static class ThemeCatalog
{
    public const string DarkId = "dark";
    public const string LightId = "light";
    public const string HighContrastId = "highcontrast";
    public const string DoomId = "doom";
    public const string SpiderManId = "spiderman";
    public const string BlackPantherId = "blackpanther";
    public const string NeonId = "neon";
    public const string SakuraId = "sakura";

    public static IReadOnlyList<ThemeInfo> All { get; } = new ThemeInfo[]
    {
        new(DarkId, "Dark", "Standard", "#171A23", "#2A3040", "#3B82F6"),
        new(LightId, "Light", "Standard", "#ECF0F6", "#FFFFFF", "#2563EB"),
        new(HighContrastId, "High Contrast", "Standard", "#000000", "#FFFFFF", "#FFD400"),
        new(DoomId, "Doctor Doom", "Marvel", "#0C1813", "#2D3734", "#10B981"),
        new(SpiderManId, "Spider-Man", "Marvel", "#0D162B", "#1B2A4A", "#EF233C"),
        new(BlackPantherId, "Black Panther (Vibranium)", "Marvel", "#0C0A14", "#1B162B", "#A855F7"),
        new(NeonId, "Neon Cyberpunk", "Special", "#090716", "#14102E", "#FF2A85"),
        new(SakuraId, "Sakura Blossom", "Special", "#FEF2F6", "#FFFFFF", "#DB2777")
    };

    public static bool IsValid(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        All.Any(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    public static string Normalize(string? id) =>
        All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase))?.Id ?? DarkId;

    public static ThemeInfo Get(string? id) =>
        All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];
}
