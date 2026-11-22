using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using VirtualKeyboard.Core.Layouts;

namespace VirtualKeyboard.App.Controls;

/// <summary>
/// A single tactile mechanical keycap on the on-screen keyboard.
/// Owns its bevel compression, press animation, auto-repeat, long-press timing,
/// Armed/Locked modifier visuals, live shifted cap text, and theme glow.
/// Never takes keyboard focus, so the target app stays active.
/// </summary>
public sealed class KeyButton : Border
{
    private readonly Border _capBorder;
    private readonly Border _topHighlight;
    private readonly TextBlock _label;
    private readonly TextBlock? _shiftLabel;
    private readonly Border? _spaceBarIndicator;
    private readonly DispatcherTimer _longPressTimer;
    private readonly DispatcherTimer _repeatTimer;
    private readonly ScaleTransform _scale = new(1, 1);

    private bool _isDown;
    private bool _longPressFired;
    private bool _repeating;
    private bool _hover;
    private bool _isShifted;

    public KeyButton(KeyDef key, double height)
    {
        Key = key;
        Height = height;
        Margin = new Thickness(2.5);
        CornerRadius = new CornerRadius(7);
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0, 0, 0, 2.5);
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        Cursor = Cursors.Hand;
        Focusable = false;
        RenderTransformOrigin = new Point(0.5, 0.5);
        RenderTransform = _scale;

        if (key.Kind == KeyKind.ClearAll)
        {
            ToolTip = "Select all and delete (Ctrl+A, Backspace)";
        }

        var fontSize = Math.Clamp(height * 0.32, 11, 21);
        if (key.Label.Length > 2)
        {
            fontSize *= 0.78;
        }

        var isModifier = key.Kind != KeyKind.Text;
        var isSpace = key.Kind == KeyKind.Text && key.Text == " ";

        _label = new TextBlock
        {
            Text = isSpace ? string.Empty : key.Label,
            FontSize = fontSize,
            FontFamily = new FontFamily("Segoe UI, Arial"),
            FontWeight = isModifier ? FontWeights.SemiBold : FontWeights.Medium,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsHitTestVisible = false
        };

        var faceGrid = new Grid();

        _topHighlight = new Border
        {
            Height = 1,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(3, 0, 3, 0),
            IsHitTestVisible = false
        };
        faceGrid.Children.Add(_topHighlight);

        if (isSpace)
        {
            _spaceBarIndicator = new Border
            {
                Width = 44,
                Height = 3.5,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.72,
                IsHitTestVisible = false
            };
            faceGrid.Children.Add(_spaceBarIndicator);
        }
        else
        {
            faceGrid.Children.Add(_label);
        }

        if (key.Kind == KeyKind.Text
            && !string.IsNullOrEmpty(key.ShiftText)
            && key.ShiftText != key.Label)
        {
            _shiftLabel = new TextBlock
            {
                Text = key.ShiftText,
                FontSize = Math.Clamp(fontSize * 0.54, 8, 12),
                FontFamily = new FontFamily("Segoe UI, Arial"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 3, 6, 0),
                Opacity = 0.72,
                IsHitTestVisible = false
            };
            faceGrid.Children.Add(_shiftLabel);
        }

        _capBorder = new Border
        {
            Child = faceGrid,
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(1),
            SnapsToDevicePixels = true
        };
        Child = _capBorder;

        _longPressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _longPressTimer.Tick += (_, _) =>
        {
            _longPressTimer.Stop();
            if (!_isDown || Key.Alternates.Count == 0)
            {
                return;
            }

            _longPressFired = true;
            LongPressed?.Invoke(this, Key);
        };

        _repeatTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _repeatTimer.Tick += (_, _) =>
        {
            if (!_isDown)
            {
                _repeatTimer.Stop();
                return;
            }

            if (!_repeating)
            {
                _repeating = true;
                _repeatTimer.Interval = TimeSpan.FromMilliseconds(45);
            }

            RepeatTick?.Invoke(this, Key);
        };

        MouseEnter += (_, _) =>
        {
            _hover = true;
            UpdateVisual();
        };
        MouseLeave += (_, _) =>
        {
            _hover = false;
            UpdateVisual();
        };

        UpdateVisual();
    }

    public KeyDef Key { get; }

    public ModifierState ModifierState { get; set; } = ModifierState.Off;

    public bool IsShifted
    {
        get => _isShifted;
        set
        {
            if (_isShifted == value)
            {
                return;
            }

            _isShifted = value;
            UpdateCapLabels();
        }
    }

    public event EventHandler<KeyDef>? Pressed;
    public event EventHandler<KeyDef>? LongPressed;
    public event EventHandler<KeyDef>? RepeatTick;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!IsEnabled)
        {
            return;
        }

        e.Handled = true;
        _isDown = true;
        _longPressFired = false;
        _repeating = false;
        CaptureMouse();
        UpdateVisual();
        Animate(0.97, 50);

        if (Key.Alternates.Count > 0)
        {
            _longPressTimer.Start();
        }

        if (Key.Repeatable)
        {
            Pressed?.Invoke(this, Key);
            _repeatTimer.Start();
        }
        else if (Key.Alternates.Count == 0)
        {
            Pressed?.Invoke(this, Key);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_isDown)
        {
            return;
        }

        e.Handled = true;
        var wasLongPress = _longPressFired;
        EndPress();

        if (!wasLongPress && Key.Alternates.Count > 0)
        {
            Pressed?.Invoke(this, Key);
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_isDown)
        {
            EndPress();
        }
    }

    private void EndPress()
    {
        _isDown = false;
        _longPressTimer.Stop();
        _repeatTimer.Stop();
        _repeatTimer.Interval = TimeSpan.FromMilliseconds(400);
        _repeating = false;

        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        UpdateVisual();
        Animate(1.0, 100);
    }

    private void Animate(double target, int milliseconds)
    {
        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private void UpdateCapLabels()
    {
        if (Key.Kind != KeyKind.Text || string.IsNullOrEmpty(Key.ShiftText) || Key.ShiftText == Key.Label)
        {
            return;
        }

        _label.Text = _isShifted ? Key.ShiftText : Key.Label;
        if (_shiftLabel is not null)
        {
            _shiftLabel.Text = _isShifted ? Key.Label : Key.ShiftText;
        }
    }

    public void UpdateVisual()
    {
        var corner = ResourceValue("KeyCornerRadius", new CornerRadius(7));
        var baseThickness = ResourceValue("KeyBorderThickness", new Thickness(1));
        var isEnter = Key.Kind == KeyKind.VirtualKey && Key.VirtualKey == VirtualKeys.Enter;
        var isClearAll = Key.Kind == KeyKind.ClearAll;

        CornerRadius = corner;
        _capBorder.CornerRadius = corner;
        _topHighlight.CornerRadius = new CornerRadius(corner.TopLeft, corner.TopRight, 0, 0);
        _topHighlight.Background = Resource("KeyTopHighlightBrush", Brushes.Transparent);

        // Depress keycap physically inside its bevel cradle when pressed.
        Padding = _isDown ? new Thickness(0, 1.5, 0, 1) : new Thickness(0, 0, 0, 2.5);
        Background = Resource("KeyBevelBrush", Brushes.Black);

        var normal = isEnter
            ? Resource("EnterKeyBrush", Resource("ModifierBrush", Brushes.DodgerBlue))
            : Key.Kind == KeyKind.Text
                ? Resource("KeyBrush", Brushes.DimGray)
                : Resource("ModifierBrush", Brushes.DimGray);

        _capBorder.Background = ModifierState switch
        {
            ModifierState.Locked => Resource("AccentBrush", Brushes.DodgerBlue),
            ModifierState.Armed => Resource("ArmedBackgroundBrush", Resource("ModifierBrush", Brushes.DimGray)),
            _ when _isDown && isClearAll => new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C)),
            _ when _isDown && isEnter => Resource("EnterKeyBrush", Resource("KeyPressedBrush", Brushes.SlateGray)),
            _ when _isDown => Resource("KeyPressedBrush", Brushes.SlateGray),
            _ when _hover && isClearAll => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
            _ when _hover && isEnter => Resource("EnterKeyHoverBrush", Resource("KeyHoverBrush", Brushes.Gray)),
            _ when _hover => Resource("KeyHoverBrush", Brushes.Gray),
            _ => normal
        };

        var foreground = ModifierState == ModifierState.Locked
            ? Resource("AccentTextBrush", Brushes.Black)
            : (_hover || _isDown) && isClearAll
                ? Brushes.White
                : isEnter
                    ? Resource("EnterKeyTextBrush", Brushes.White)
                    : Resource("TextBrush", Brushes.White);

        _label.Foreground = foreground;
        if (_shiftLabel is not null)
        {
            _shiftLabel.Foreground = ModifierState == ModifierState.Locked
                ? foreground
                : Resource("SubtleTextBrush", foreground);
        }

        if (_spaceBarIndicator is not null)
        {
            _spaceBarIndicator.Background = Resource("SpaceBarAccentBrush", Resource("AccentBrush", Brushes.DodgerBlue));
        }

        _capBorder.BorderBrush = ModifierState switch
        {
            ModifierState.Locked => Resource("AccentBrush", Brushes.DodgerBlue),
            ModifierState.Armed => Resource("ArmedBorderBrush", Resource("AccentBrush", Brushes.DodgerBlue)),
            _ when _isDown && isClearAll => new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)),
            _ when _hover && isClearAll => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
            _ when isEnter => Resource("EnterKeyBrush", Resource("KeyBorderBrush", Brushes.Gray)),
            _ => Resource("KeyBorderBrush", Resource("BorderBrush", Brushes.Gray))
        };

        _capBorder.BorderThickness = ModifierState == ModifierState.Armed
            ? new Thickness(Math.Max(2, baseThickness.Left))
            : baseThickness;

        var glowOpacity = ResourceValue("KeyGlowOpacity", 0.0);
        if ((_hover || ModifierState != ModifierState.Off) && glowOpacity > 0.01)
        {
            var glowColor = ResourceValue("KeyGlowColor", Colors.Transparent);
            var glowBlur = ResourceValue("KeyGlowBlur", 10.0);
            _capBorder.Effect = new DropShadowEffect
            {
                Color = glowColor,
                BlurRadius = glowBlur,
                ShadowDepth = 0,
                Opacity = ModifierState != ModifierState.Off ? Math.Min(0.95, glowOpacity + 0.15) : glowOpacity
            };
        }
        else
        {
            _capBorder.Effect = null;
        }
    }

    private static Brush Resource(string key, Brush fallback) =>
        Application.Current?.TryFindResource(key) as Brush ?? fallback;

    private static T ResourceValue<T>(string key, T fallback) =>
        Application.Current?.TryFindResource(key) is T value ? value : fallback;
}
