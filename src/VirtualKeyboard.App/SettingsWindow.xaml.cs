using System.Windows;
using System.Windows.Controls;
using VirtualKeyboard.Core.Settings;

namespace VirtualKeyboard.App;

public partial class SettingsWindow : Window
{
    private readonly Action<string>? _previewTheme;
    private readonly bool _initialized;

    public SettingsWindow(AppSettings settings, bool autostartSupported, Action<string>? previewTheme = null)
    {
        InitializeComponent();
        Settings = settings;
        _previewTheme = previewTheme;

        foreach (var theme in ThemeCatalog.All)
        {
            ThemeCombo.Items.Add(new ComboBoxItem
            {
                Content = theme.Category == "Standard"
                    ? theme.DisplayName
                    : $"{theme.DisplayName} ({theme.Category})",
                Tag = theme.Id
            });
        }

        SelectByTag(ThemeCombo, ThemeCatalog.Normalize(settings.Theme));
        SelectByTag(LayoutCombo, settings.LayoutId);
        KeyHeightSlider.Value = settings.KeyHeight;
        KeyHeightValue.Text = ((int)Math.Round(settings.KeyHeight)).ToString();

        SuggestionsCheck.IsChecked = settings.ShowSuggestions;
        SelectByTag(SuggestionCountCombo, settings.SuggestionCount.ToString());
        AddSpaceCheck.IsChecked = settings.AddSpaceAfterSuggestion;
        SoundCheck.IsChecked = settings.ClickSound;
        AutoShowCheck.IsChecked = settings.AutoShowOnTextFocus;
        AutostartCheck.IsChecked = settings.StartWithWindows;
        AutostartCheck.IsEnabled = autostartSupported;
        if (!autostartSupported)
        {
            AutostartCheck.Content = "Start with Windows (available in the published exe)";
        }

        _initialized = true;
    }

    public AppSettings Settings { get; }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        _previewTheme?.Invoke(SelectedTag(ThemeCombo, ThemeCatalog.DarkId));
    }

    private void KeyHeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (KeyHeightValue is not null)
        {
            KeyHeightValue.Text = ((int)Math.Round(e.NewValue)).ToString();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Settings.Theme = SelectedTag(ThemeCombo, ThemeCatalog.DarkId);
        Settings.LayoutId = SelectedTag(LayoutCombo, "en");
        Settings.KeyHeight = Math.Round(KeyHeightSlider.Value);
        Settings.ShowSuggestions = SuggestionsCheck.IsChecked == true;
        Settings.SuggestionCount = int.TryParse(SelectedTag(SuggestionCountCombo, "5"), out var count)
            ? count
            : 5;
        Settings.AddSpaceAfterSuggestion = AddSpaceCheck.IsChecked == true;
        Settings.ClickSound = SoundCheck.IsChecked == true;
        Settings.AutoShowOnTextFocus = AutoShowCheck.IsChecked == true;
        Settings.StartWithWindows = AutostartCheck.IsChecked == true;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private static void SelectByTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem { Tag: string itemTag }
                && string.Equals(itemTag, tag, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }

        combo.SelectedIndex = 0;
    }

    private static string SelectedTag(ComboBox combo, string fallback) =>
        combo.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : fallback;
}
