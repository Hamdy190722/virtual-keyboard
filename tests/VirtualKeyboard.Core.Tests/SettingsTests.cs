using VirtualKeyboard.Core.Settings;

namespace VirtualKeyboard.Core.Tests;

public class SettingsTests
{
    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vk-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(dir);
            var settings = new AppSettings
            {
                Theme = "light",
                LayoutId = "ar",
                KeyHeight = 64,
                ShowSuggestions = false,
                SuggestionCount = 7,
                AddSpaceAfterSuggestion = true,
                ClickSound = false,
                StartWithWindows = true,
                WindowLeft = 120,
                WindowTop = 800,
                WindowWidth = 1400
            };

            store.Save(settings);
            var loaded = store.Load();

            Assert.Equal("light", loaded.Theme);
            Assert.Equal("ar", loaded.LayoutId);
            Assert.Equal(64, loaded.KeyHeight);
            Assert.False(loaded.ShowSuggestions);
            Assert.Equal(7, loaded.SuggestionCount);
            Assert.True(loaded.AddSpaceAfterSuggestion);
            Assert.False(loaded.ClickSound);
            Assert.True(loaded.StartWithWindows);
            Assert.Equal(120, loaded.WindowLeft);
            Assert.Equal(800, loaded.WindowTop);
            Assert.Equal(1400, loaded.WindowWidth);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void InvalidValues_AreClamped()
    {
        var settings = new AppSettings
        {
            Theme = "not-a-theme",
            KeyHeight = 5,
            SuggestionCount = 99,
            WindowWidth = 10
        };

        var validated = SettingsStore.Validate(settings);

        Assert.Equal("dark", validated.Theme);
        Assert.Equal(34, validated.KeyHeight);
        Assert.Equal(9, validated.SuggestionCount);
        Assert.Equal(640, validated.WindowWidth);
    }

    [Fact]
    public void AllCatalogThemes_ValidateAndRoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vk-themes-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(dir);
            foreach (var theme in ThemeCatalog.All)
            {
                var settings = new AppSettings { Theme = theme.Id };
                store.Save(settings);
                var loaded = store.Load();
                Assert.Equal(theme.Id, loaded.Theme);
            }
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void ThemeCatalog_NormalizesCaseInsensitively()
    {
        Assert.Equal(ThemeCatalog.DoomId, ThemeCatalog.Normalize("DOOM"));
        Assert.Equal(ThemeCatalog.SpiderManId, ThemeCatalog.Normalize("SpiderMan"));
        Assert.Equal(ThemeCatalog.BlackPantherId, ThemeCatalog.Normalize("blackpanther"));
        Assert.Equal(ThemeCatalog.NeonId, ThemeCatalog.Normalize("NEON"));
        Assert.Equal(ThemeCatalog.SakuraId, ThemeCatalog.Normalize("Sakura"));
        Assert.Equal(ThemeCatalog.DarkId, ThemeCatalog.Normalize("unknown"));
    }

    [Fact]
    public void MissingFile_ReturnsDefaults()
    {
        var store = new SettingsStore(Path.Combine(Path.GetTempPath(), "vk-missing-" + Guid.NewGuid().ToString("N")));
        var settings = store.Load();

        Assert.Equal("dark", settings.Theme);
        Assert.Equal("en", settings.LayoutId);
    }
}
