using System.Windows;
using VirtualKeyboard.Core.Settings;

namespace VirtualKeyboard.App.Services;

public static class ThemeService
{
    public static void Apply(string? theme)
    {
        var id = ThemeCatalog.Normalize(theme);
        var source = id switch
        {
            ThemeCatalog.LightId => "Themes/Light.xaml",
            ThemeCatalog.HighContrastId => "Themes/HighContrast.xaml",
            ThemeCatalog.DoomId => "Themes/Doom.xaml",
            ThemeCatalog.SpiderManId => "Themes/SpiderMan.xaml",
            ThemeCatalog.BlackPantherId => "Themes/BlackPanther.xaml",
            ThemeCatalog.NeonId => "Themes/Neon.xaml",
            ThemeCatalog.SakuraId => "Themes/Sakura.xaml",
            _ => "Themes/Dark.xaml"
        };

        var dictionary = new ResourceDictionary
        {
            Source = new Uri(source, UriKind.Relative)
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d =>
            d.Source is not null && d.Source.OriginalString.Contains("Themes/", StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            var index = merged.IndexOf(existing);
            merged[index] = dictionary;
        }
        else
        {
            merged.Add(dictionary);
        }
    }
}
