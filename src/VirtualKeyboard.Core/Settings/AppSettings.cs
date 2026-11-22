using System.Text.Json;

namespace VirtualKeyboard.Core.Settings;

public sealed class AppSettings
{
    public string Theme { get; set; } = "dark";
    public string LayoutId { get; set; } = Layouts.LayoutCatalog.EnglishId;
    public double KeyHeight { get; set; } = 52;
    public bool ShowSuggestions { get; set; } = true;
    public int SuggestionCount { get; set; } = 5;
    public bool AddSpaceAfterSuggestion { get; set; }
    public bool ClickSound { get; set; } = true;
    public bool AutoShowOnTextFocus { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double WindowWidth { get; set; } = 1100;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}

/// <summary>Portable settings stored next to the executable.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SettingsStore(string? directory = null)
    {
        DirectoryPath = directory ?? AppContext.BaseDirectory;
    }

    public string DirectoryPath { get; }
    public string SettingsPath => Path.Combine(DirectoryPath, "settings.json");
    public string LearnedWordsPath => Path.Combine(DirectoryPath, "learned-words.json");
    public string WordlistsDirectory => Path.Combine(DirectoryPath, "wordlists");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            return Validate(settings);
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var json = JsonSerializer.Serialize(Validate(settings), JsonOptions);
            var temp = SettingsPath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, SettingsPath, overwrite: true);
        }
        catch
        {
            // Settings persistence is best-effort.
        }
    }

    public static AppSettings Validate(AppSettings settings)
    {
        settings.Theme = ThemeCatalog.Normalize(settings.Theme);

        settings.LayoutId = Layouts.LayoutCatalog.Get(settings.LayoutId).Id;
        settings.KeyHeight = Math.Clamp(settings.KeyHeight, 34, 96);
        settings.SuggestionCount = Math.Clamp(settings.SuggestionCount, 0, 9);
        settings.WindowWidth = Math.Clamp(settings.WindowWidth, 640, 4000);
        return settings;
    }
}
