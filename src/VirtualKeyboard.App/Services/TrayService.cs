using System.Drawing;
using System.Windows.Forms;
using VirtualKeyboard.Core.Settings;

namespace VirtualKeyboard.App.Services;

/// <summary>Tray icon + context menu. Lives for the whole app lifetime.</summary>
public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly Func<string> _getLayout;
    private readonly Func<string> _getTheme;
    private readonly Func<bool> _isAutostart;
    private readonly ToolStripMenuItem _englishItem;
    private readonly ToolStripMenuItem _arabicItem;
    private readonly Dictionary<string, ToolStripMenuItem> _themeItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly ToolStripMenuItem _autostartItem;

    public TrayService(
        Action toggle,
        Action<string> setLayout,
        Action<string> setTheme,
        Action openSettings,
        Action toggleAutostart,
        Action exit,
        Func<string> getLayout,
        Func<string> getTheme,
        Func<bool> isAutostart)
    {
        _getLayout = getLayout;
        _getTheme = getTheme;
        _isAutostart = isAutostart;

        _menu = new ContextMenuStrip();

        var toggleItem = new ToolStripMenuItem("Show / hide keyboard") { Font = new Font(_menu.Font, System.Drawing.FontStyle.Bold) };
        toggleItem.Click += (_, _) => toggle();
        _menu.Items.Add(toggleItem);
        _menu.Items.Add(new ToolStripSeparator());

        var layoutMenu = new ToolStripMenuItem("Layout");
        _englishItem = new ToolStripMenuItem("English");
        _englishItem.Click += (_, _) => setLayout("en");
        _arabicItem = new ToolStripMenuItem("العربية");
        _arabicItem.Click += (_, _) => setLayout("ar");
        layoutMenu.DropDownItems.Add(_englishItem);
        layoutMenu.DropDownItems.Add(_arabicItem);
        _menu.Items.Add(layoutMenu);

        var themeMenu = new ToolStripMenuItem("Theme");
        string? lastCategory = null;
        foreach (var theme in ThemeCatalog.All)
        {
            if (lastCategory is not null && !string.Equals(lastCategory, theme.Category, StringComparison.Ordinal))
            {
                themeMenu.DropDownItems.Add(new ToolStripSeparator());
            }

            lastCategory = theme.Category;
            var capturedId = theme.Id;
            var item = new ToolStripMenuItem(theme.DisplayName);
            item.Click += (_, _) => setTheme(capturedId);
            themeMenu.DropDownItems.Add(item);
            _themeItems[capturedId] = item;
        }
        _menu.Items.Add(themeMenu);

        _autostartItem = new ToolStripMenuItem("Start with Windows");
        _autostartItem.Click += (_, _) => toggleAutostart();
        _menu.Items.Add(_autostartItem);

        _menu.Items.Add(new ToolStripSeparator());
        var settingsItem = new ToolStripMenuItem("Settings…");
        settingsItem.Click += (_, _) => openSettings();
        _menu.Items.Add(settingsItem);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => exit();
        _menu.Items.Add(exitItem);

        _menu.Opening += (_, _) => RefreshChecks();

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "Virtual Keyboard — Ctrl+Alt+K",
            Visible = true,
            ContextMenuStrip = _menu
        };
        _notifyIcon.DoubleClick += (_, _) => toggle();
    }

    public void RefreshChecks()
    {
        _englishItem.Checked = string.Equals(_getLayout(), "en", StringComparison.OrdinalIgnoreCase);
        _arabicItem.Checked = string.Equals(_getLayout(), "ar", StringComparison.OrdinalIgnoreCase);

        var activeTheme = ThemeCatalog.Normalize(_getTheme());
        foreach (var (id, item) in _themeItems)
        {
            item.Checked = string.Equals(id, activeTheme, StringComparison.OrdinalIgnoreCase);
        }

        _autostartItem.Checked = _isAutostart();
        _autostartItem.Enabled = AutostartService.IsSupported;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.Dispose();
        _menu.Dispose();
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/VirtualKeyboard.ico", UriKind.Absolute));
            if (info is not null)
            {
                using (info.Stream)
                {
                    return new Icon(info.Stream, new Size(32, 32));
                }
            }
        }
        catch
        {
            // Fall through to executable icon / system fallback.
        }

        try
        {
            if (!string.IsNullOrEmpty(Environment.ProcessPath))
            {
                var associated = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
                if (associated is not null)
                {
                    return associated;
                }
            }
        }
        catch
        {
            // Ignore and return system fallback.
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}
