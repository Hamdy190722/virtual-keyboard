using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VirtualKeyboard.App.Controls;
using VirtualKeyboard.App.Services;
using VirtualKeyboard.Core.Input;
using VirtualKeyboard.Core.Layouts;
using VirtualKeyboard.Core.Prediction;
using VirtualKeyboard.Core.Settings;
using VirtualKeyboard.Core.Typing;

namespace VirtualKeyboard.App;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly TypingSession _session;
    private readonly ClickSound _sound;
    private readonly List<KeyButton> _keyButtons = new();
    private readonly DispatcherTimer _foregroundTimer;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _alternatesTimer;
    private readonly System.Threading.Timer _focusTimer;

    private KeyboardLayout _layout;
    private HotkeyService? _hotkey;
    private IntPtr _hwnd;
    private IntPtr _lastForeground;
    private bool _allowClose;
    private bool _dragging;
    private volatile bool _autoShow;
    private volatile bool _collapsed;
    private string? _lastFocusKey;
    private string? _suppressedFocusKey;
    private Point _dragStartScreen;
    private double _dragStartLeft;
    private double _dragStartTop;

    public MainWindow(AppSettings settings, WordPredictor predictor)
    {
        _settings = settings;
        _layout = LayoutCatalog.Get(settings.LayoutId);

        InitializeComponent();

        _session = new TypingSession(new SendInputInjector(), predictor)
        {
            Language = _layout.Id,
            SuggestionCount = settings.SuggestionCount,
            AddSpaceAfterSuggestion = settings.AddSpaceAfterSuggestion
        };
        _session.Changed += (_, _) => UpdateSuggestions();

        _sound = new ClickSound { Enabled = settings.ClickSound };

        _foregroundTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _foregroundTimer.Tick += ForegroundTimer_Tick;

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveState();
        };

        _alternatesTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _alternatesTimer.Tick += (_, _) =>
        {
            _alternatesTimer.Stop();
            HideAlternates();
        };

        _autoShow = settings.AutoShowOnTextFocus;
        _focusTimer = new System.Threading.Timer(CheckForTextFocus, null, Timeout.Infinite, Timeout.Infinite);

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
        StateChanged += OnStateChanged;
        LocationChanged += (_, _) => ScheduleSave();
        SizeChanged += (_, _) => ScheduleSave();

        BuildLayout();
    }

    public string LayoutId => _layout.Id;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        WindowEffects.MakeNonActivating(this, RestoreFromTaskbar);
        WindowEffects.RoundCorners(this);

        _hotkey = new HotkeyService(this);
        _hotkey.Pressed += (_, _) => ToggleVisibility();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var work = SystemParameters.WorkArea;
        Width = Math.Clamp(_settings.WindowWidth, MinWidth, Math.Max(MinWidth, work.Width - 24));

        Dispatcher.BeginInvoke(new Action(PositionInitial), DispatcherPriority.Loaded);

        _collapsed = false;
        _foregroundTimer.Start();
        _focusTimer.Change(900, 650);
        UpdateStatus();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            MinimizeToTaskbar();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _foregroundTimer.Stop();
        _saveTimer.Stop();
        _alternatesTimer.Stop();
        _focusTimer.Dispose();
        _hotkey?.Dispose();
        _sound.Dispose();
    }

    public void AllowClose() => _allowClose = true;

    public void ToggleVisibility()
    {
        if (IsCollapsed)
        {
            RestoreFromTaskbar();
        }
        else
        {
            MinimizeToTaskbar();
        }
    }

    /// <summary>True when the keyboard is minimized to the taskbar or hidden.</summary>
    public bool IsCollapsed => WindowState == WindowState.Minimized || !IsVisible;

    /// <summary>Collapses to a taskbar button instead of vanishing from the screen.</summary>
    public void MinimizeToTaskbar()
    {
        if (WindowState == WindowState.Minimized)
        {
            return;
        }

        HideThemePicker();
        HideAlternates();

        // Probe the focus target right now instead of trusting the timer's cached
        // key: the cache can be stale (or null), which would let auto-show undo
        // this collapse a fraction of a second later.
        _suppressedFocusKey = FocusProbe.TryGetFocusedTextTarget() ?? _lastFocusKey;
        _collapsed = true;
        WindowEffects.MinimizeNoActivate(this);
    }

    public void RestoreFromTaskbar()
    {
        if (!IsVisible)
        {
            Show();
        }

        _suppressedFocusKey = null;
        _collapsed = false;
        WindowEffects.RestoreNoActivate(this);
        EnsureOnScreen();
        WindowEffects.BringToFrontNoActivate(this);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        _collapsed = IsCollapsed;

        if (WindowState == WindowState.Normal)
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    EnsureOnScreen();
                    WindowEffects.BringToFrontNoActivate(this);
                }),
                DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// Runs on a timer thread: when the keyboard is collapsed and the user
    /// focuses a text field in another app, bring the keyboard back.
    /// </summary>
    private void CheckForTextFocus(object? state)
    {
        if (!_autoShow)
        {
            return;
        }

        var key = FocusProbe.TryGetFocusedTextTarget();
        if (key is null)
        {
            return;
        }

        _lastFocusKey = key;

        if (!_collapsed || key == _suppressedFocusKey)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                if (_autoShow && IsCollapsed && key != _suppressedFocusKey)
                {
                    RestoreFromTaskbar();
                }
            }),
            DispatcherPriority.Normal);
    }

    public void SetLayout(string id)
    {
        var layout = LayoutCatalog.Get(id);
        if (layout.Id == _layout.Id)
        {
            return;
        }

        _layout = layout;
        _settings.LayoutId = layout.Id;
        _session.Language = layout.Id;
        _session.ResetWordContext();
        BuildLayout();
        SaveState();
    }

    public void OpenSettings()
    {
        HideThemePicker();
        var originalTheme = _settings.Theme;
        var dialog = new SettingsWindow(
            _settings.Clone(),
            AutostartService.IsSupported,
            previewTheme: theme => App.Instance.PreviewTheme(theme))
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            ApplySettings(dialog.Settings);
        }
        else
        {
            App.Instance.PreviewTheme(originalTheme);
        }
    }

    public void ToggleAutostart()
    {
        if (!AutostartService.IsSupported)
        {
            return;
        }

        _settings.StartWithWindows = !_settings.StartWithWindows;
        AutostartService.SetEnabled(_settings.StartWithWindows);
        SaveState();
    }

    public void RefreshTheme()
    {
        foreach (var button in _keyButtons)
        {
            button.UpdateVisual();
        }

        UpdateThemeBadge();
        if (ThemePickerBorder.Visibility == Visibility.Visible)
        {
            PopulateThemePicker();
        }

        UpdateSuggestions();
        UpdateStatus();
    }

    private void ApplySettings(AppSettings updated)
    {
        var layoutChanged = !string.Equals(_settings.LayoutId, updated.LayoutId, StringComparison.OrdinalIgnoreCase);
        var themeChanged = _settings.Theme != updated.Theme;
        var heightChanged = Math.Abs(_settings.KeyHeight - updated.KeyHeight) > 0.1;

        _settings.Theme = updated.Theme;
        _settings.LayoutId = updated.LayoutId;
        _settings.KeyHeight = updated.KeyHeight;
        _settings.ShowSuggestions = updated.ShowSuggestions;
        _settings.SuggestionCount = updated.SuggestionCount;
        _settings.AddSpaceAfterSuggestion = updated.AddSpaceAfterSuggestion;
        _settings.ClickSound = updated.ClickSound;
        _settings.StartWithWindows = updated.StartWithWindows;
        _settings.AutoShowOnTextFocus = updated.AutoShowOnTextFocus;

        _autoShow = _settings.AutoShowOnTextFocus;

        _session.SuggestionCount = _settings.SuggestionCount;
        _session.AddSpaceAfterSuggestion = _settings.AddSpaceAfterSuggestion;
        _sound.Enabled = _settings.ClickSound;

        if (layoutChanged)
        {
            _layout = LayoutCatalog.Get(_settings.LayoutId);
            _session.Language = _layout.Id;
            _session.ResetWordContext();
        }

        if (layoutChanged || heightChanged)
        {
            BuildLayout();
            Dispatcher.BeginInvoke(new Action(ClampPosition), DispatcherPriority.Loaded);
        }
        else
        {
            UpdateKeyStates();
            UpdateSuggestions();
            UpdateStatus();
        }

        if (themeChanged)
        {
            ThemeService.Apply(_settings.Theme);
            RefreshTheme();
        }

        if (AutostartService.IsSupported)
        {
            AutostartService.SetEnabled(_settings.StartWithWindows);
        }

        SaveState();
    }

    private void BuildLayout()
    {
        RowsHost.Children.Clear();
        _keyButtons.Clear();
        RowsHost.FlowDirection = _layout.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        for (var rowIndex = 0; rowIndex < _layout.Rows.Count; rowIndex++)
        {
            var row = _layout.Rows[rowIndex];
            var isUtilityRow = rowIndex == _layout.Rows.Count - 1;
            var rowHeight = isUtilityRow
                ? Math.Max(30, Math.Round(_settings.KeyHeight * 0.78))
                : _settings.KeyHeight;

            var grid = new Grid
            {
                Margin = isUtilityRow
                    ? new Thickness(0, 6, 0, 1)
                    : new Thickness(0, 2, 0, 2)
            };
            for (var i = 0; i < row.Count; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(row[i].Width, GridUnitType.Star)
                });
            }

            for (var i = 0; i < row.Count; i++)
            {
                var button = new KeyButton(row[i], rowHeight);
                button.Pressed += OnKeyPressed;
                button.LongPressed += OnKeyLongPressed;
                button.RepeatTick += OnKeyRepeat;
                Grid.SetColumn(button, i);
                grid.Children.Add(button);
                _keyButtons.Add(button);
            }

            RowsHost.Children.Add(grid);
        }

        UpdateThemeBadge();
        UpdateKeyStates();
        UpdateSuggestions();
        UpdateStatus();
    }

    private void OnKeyPressed(object? sender, KeyDef key)
    {
        HideAlternates();
        HideThemePicker();

        if (key.Kind == KeyKind.LayoutToggle)
        {
            SetLayout(_layout.Id == LayoutCatalog.EnglishId ? LayoutCatalog.ArabicId : LayoutCatalog.EnglishId);
            return;
        }

        _session.PressKey(key);
        _sound.Play();
        UpdateKeyStates();
        UpdateStatus();
    }

    private void OnKeyRepeat(object? sender, KeyDef key)
    {
        _session.PressKey(key);
        UpdateKeyStates();
        UpdateStatus();
    }

    private void OnKeyLongPressed(object? sender, KeyDef key)
    {
        ShowAlternates(key);
    }

    private void ShowAlternates(KeyDef key)
    {
        AlternatesHost.Children.Clear();

        var hint = new TextBlock
        {
            Text = key.Label + " →",
            Foreground = TryFindResource("SubtleTextBrush") as Brush ?? Brushes.Gray,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 10, 0)
        };
        AlternatesHost.Children.Add(hint);

        foreach (var alternate in key.Alternates)
        {
            var captured = alternate;
            var button = new Button
            {
                Content = alternate,
                Style = (Style)FindResource("SuggestionButton"),
                FontSize = 17
            };
            button.Click += (_, _) =>
            {
                _session.PressText(captured);
                _sound.Play();
                HideAlternates();
                UpdateKeyStates();
            };
            AlternatesHost.Children.Add(button);
        }

        AlternatesBorder.Visibility = Visibility.Visible;
        _alternatesTimer.Stop();
        _alternatesTimer.Start();
    }

    private void HideAlternates()
    {
        _alternatesTimer.Stop();
        AlternatesBorder.Visibility = Visibility.Collapsed;
    }

    private void UpdateKeyStates()
    {
        var shifted = _session.Modifiers.IsActive(ModifierKind.Shift);

        foreach (var button in _keyButtons)
        {
            button.IsShifted = shifted;
            button.ModifierState = button.Key.Kind switch
            {
                KeyKind.Modifier when button.Key.Modifier is { } modifier =>
                    _session.Modifiers.GetState(modifier),
                KeyKind.CapsToggle => _session.Modifiers.IsLocked(ModifierKind.Shift)
                    ? ModifierState.Locked
                    : ModifierState.Off,
                _ => ModifierState.Off
            };
            button.UpdateVisual();
        }
    }

    private void UpdateSuggestions()
    {
        SuggestionsHost.Children.Clear();

        if (!_settings.ShowSuggestions)
        {
            SuggestionsBorder.Visibility = Visibility.Collapsed;
            return;
        }

        SuggestionsBorder.Visibility = Visibility.Visible;

        if (_session.Suggestions.Count == 0)
        {
            SuggestionsIdleHint.Visibility = Visibility.Visible;
            SuggestionsScroll.Visibility = Visibility.Collapsed;
            return;
        }

        SuggestionsIdleHint.Visibility = Visibility.Collapsed;
        SuggestionsScroll.Visibility = Visibility.Visible;
        SuggestionsHost.FlowDirection = _layout.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;

        for (var i = 0; i < _session.Suggestions.Count; i++)
        {
            var word = _session.Suggestions[i];
            var captured = word;
            var isTop = i == 0;
            var button = new Button
            {
                Content = word,
                Style = (Style)FindResource("SuggestionButton"),
                FontWeight = isTop ? FontWeights.SemiBold : FontWeights.Normal,
                BorderBrush = isTop
                    ? TryFindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue
                    : TryFindResource("KeyBorderBrush") as Brush ?? Brushes.Gray
            };
            button.Click += (_, _) =>
            {
                _session.ApplySuggestion(captured);
                _sound.Play();
                UpdateKeyStates();
            };
            SuggestionsHost.Children.Add(button);
        }
    }

    private void UpdateStatus()
    {
        LayoutNameText.Text = _layout.DisplayName;
        LayoutButton.Content = _layout.Id == LayoutCatalog.ArabicId ? "EN" : "ع";

        StatusPillsHost.Children.Clear();
        var active = new List<(string Label, bool Locked)>();

        if (_session.Modifiers.IsActive(ModifierKind.Shift))
        {
            active.Add((_session.Modifiers.IsLocked(ModifierKind.Shift) ? "Shift Locked" : "Shift", _session.Modifiers.IsLocked(ModifierKind.Shift)));
        }

        if (_session.Modifiers.IsActive(ModifierKind.Ctrl))
        {
            active.Add((_session.Modifiers.IsLocked(ModifierKind.Ctrl) ? "Ctrl Locked" : "Ctrl", _session.Modifiers.IsLocked(ModifierKind.Ctrl)));
        }

        if (_session.Modifiers.IsActive(ModifierKind.Alt))
        {
            active.Add((_session.Modifiers.IsLocked(ModifierKind.Alt) ? "Alt Locked" : "Alt", _session.Modifiers.IsLocked(ModifierKind.Alt)));
        }

        if (_session.Modifiers.IsActive(ModifierKind.Win))
        {
            active.Add((_session.Modifiers.IsLocked(ModifierKind.Win) ? "Win Locked" : "Win", _session.Modifiers.IsLocked(ModifierKind.Win)));
        }

        if (active.Count == 0)
        {
            StatusText.Visibility = Visibility.Visible;
            StatusText.Text = "Click keys to type · hold a letter for accents";
        }
        else
        {
            StatusText.Visibility = Visibility.Collapsed;
            foreach (var (label, locked) in active)
            {
                var pill = new Border
                {
                    Margin = new Thickness(0, 0, 6, 0),
                    Padding = new Thickness(7, 1, 7, 1),
                    CornerRadius = new CornerRadius(4),
                    Background = locked
                        ? TryFindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue
                        : TryFindResource("ArmedBackgroundBrush") as Brush ?? Brushes.Transparent,
                    BorderBrush = locked
                        ? TryFindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue
                        : TryFindResource("ArmedBorderBrush") as Brush ?? Brushes.DodgerBlue,
                    BorderThickness = new Thickness(1)
                };
                pill.Child = new TextBlock
                {
                    Text = label,
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = locked
                        ? TryFindResource("AccentTextBrush") as Brush ?? Brushes.White
                        : TryFindResource("TextBrush") as Brush ?? Brushes.White
                };
                StatusPillsHost.Children.Add(pill);
            }
        }

        HotkeyText.Text = _hotkey is null || _hotkey.IsRegistered
            ? "Ctrl+Alt+K"
            : "Ctrl+Alt+K unavailable";
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        HideAlternates();
        if (ThemePickerBorder.Visibility == Visibility.Visible)
        {
            HideThemePicker();
        }
        else
        {
            PopulateThemePicker();
            ThemePickerBorder.Visibility = Visibility.Visible;
        }
    }

    private void PopulateThemePicker()
    {
        ThemePickerHost.Children.Clear();
        var activeId = ThemeCatalog.Normalize(_settings.Theme);

        foreach (var theme in ThemeCatalog.All)
        {
            var captured = theme.Id;
            var isCurrent = string.Equals(theme.Id, activeId, StringComparison.OrdinalIgnoreCase);

            var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (var hex in new[] { theme.SurfaceHex, theme.KeyHex, theme.AccentHex })
            {
                content.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 9,
                    Height = 9,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                    Stroke = TryFindResource("BorderBrush") as Brush ?? Brushes.Gray,
                    StrokeThickness = 0.75,
                    Margin = new Thickness(0, 0, 3, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            content.Children.Add(new TextBlock
            {
                Text = theme.DisplayName,
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            var button = new Button
            {
                Content = content,
                Style = (Style)FindResource("SuggestionButton"),
                Margin = new Thickness(0, 3, 6, 3),
                Padding = new Thickness(10, 5, 11, 5),
                FontSize = 12,
                FontWeight = isCurrent ? FontWeights.SemiBold : FontWeights.Normal,
                Background = isCurrent
                    ? TryFindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue
                    : TryFindResource("KeyBrush") as Brush ?? Brushes.DimGray,
                Foreground = isCurrent
                    ? TryFindResource("AccentTextBrush") as Brush ?? Brushes.White
                    : TryFindResource("TextBrush") as Brush ?? Brushes.White,
                BorderBrush = isCurrent
                    ? TryFindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue
                    : TryFindResource("KeyBorderBrush") as Brush ?? Brushes.Gray
            };
            button.Click += (_, _) =>
            {
                App.Instance.ApplyTheme(captured);
                HideThemePicker();
            };
            ThemePickerHost.Children.Add(button);
        }
    }

    private void HideThemePicker() => ThemePickerBorder.Visibility = Visibility.Collapsed;

    private void UpdateThemeBadge()
    {
        var badge = TryFindResource("ThemeBadgeText") as string;
        if (string.IsNullOrWhiteSpace(badge))
        {
            ThemeBadgeBorder.Visibility = Visibility.Collapsed;
        }
        else
        {
            ThemeBadgeText.Text = badge;
            ThemeBadgeBorder.Visibility = Visibility.Visible;
        }
    }

    private void LayoutButton_Click(object sender, RoutedEventArgs e) =>
        SetLayout(_layout.Id == LayoutCatalog.EnglishId ? LayoutCatalog.ArabicId : LayoutCatalog.EnglishId);

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void HideButton_Click(object sender, RoutedEventArgs e) => MinimizeToTaskbar();

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        SaveState();
        App.Instance.ShutdownApp();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is not null)
        {
            return;
        }

        _dragging = true;
        _dragStartScreen = PointToScreen(e.GetPosition(this));
        _dragStartLeft = Left;
        _dragStartTop = Top;
        HeaderBorder.CaptureMouse();
        e.Handled = true;
    }

    private void Header_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var current = PointToScreen(e.GetPosition(this));
        var dpi = VisualTreeHelper.GetDpi(this);
        Left = _dragStartLeft + (current.X - _dragStartScreen.X) / dpi.DpiScaleX;
        Top = _dragStartTop + (current.Y - _dragStartScreen.Y) / dpi.DpiScaleY;
    }

    private void Header_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        HeaderBorder.ReleaseMouseCapture();
        ClampPosition();
        ScheduleSave();
    }

    private static T? FindAncestor<T>(DependencyObject node) where T : DependencyObject
    {
        var current = node;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void ForegroundTimer_Tick(object? sender, EventArgs e)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var foreground = GetForegroundWindow();
        if (foreground == _lastForeground)
        {
            return;
        }

        _lastForeground = foreground;
        if (foreground != IntPtr.Zero && foreground != _hwnd)
        {
            _session.ResetWordContext();
        }
    }

    private void PositionInitial()
    {
        var work = SystemParameters.WorkArea;
        var height = ActualHeight > 0 ? ActualHeight : 340;

        if (_settings.WindowLeft is { } left
            && _settings.WindowTop is { } top
            && IsOnScreen(left, top, Width, height))
        {
            Left = left;
            Top = top;
        }
        else
        {
            Left = work.Left + Math.Max(0, (work.Width - Width) / 2);
            Top = work.Bottom - height - 12;
        }

        ClampPosition();
    }

    private void EnsureOnScreen()
    {
        var height = ActualHeight > 0 ? ActualHeight : 340;
        if (double.IsNaN(Left) || double.IsNaN(Top) || !IsOnScreen(Left, Top, Width, height))
        {
            var work = SystemParameters.WorkArea;
            Left = work.Left + Math.Max(0, (work.Width - Width) / 2);
            Top = work.Bottom - height - 12;
        }

        ClampPosition();
    }

    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualWidth = SystemParameters.VirtualScreenWidth;
        var virtualHeight = SystemParameters.VirtualScreenHeight;

        return left + width > virtualLeft + 80
               && left < virtualLeft + virtualWidth - 80
               && top + height > virtualTop + 40
               && top < virtualTop + virtualHeight - 40;
    }

    private void ClampPosition()
    {
        var work = SystemParameters.WorkArea;
        var height = ActualHeight > 0 ? ActualHeight : 340;

        Left = Math.Max(work.Left - Width + 140, Math.Min(Left, work.Right - 140));
        Top = Math.Max(work.Top, Math.Min(Top, work.Bottom - height - 4));
    }

    private void ScheduleSave()
    {
        if (WindowState == WindowState.Minimized)
        {
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveState()
    {
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        _settings.WindowWidth = Width;
        App.Instance.SaveSettings();
        _session.SaveLearnedIfDirty();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
