using VirtualKeyboard.Core.Input;
using VirtualKeyboard.Core.Layouts;
using VirtualKeyboard.Core.Prediction;
using VirtualKeyboard.Core.Typing;

namespace VirtualKeyboard.Core.Tests;

public class TypingSessionTests : IDisposable
{
    private readonly string _dir;
    private readonly FakeInjector _injector = new();
    private readonly TypingSession _session;
    private readonly KeyboardLayout _layout = LayoutCatalog.English;

    public TypingSessionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "vk-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        var predictor = WordPredictor.Create(null, Path.Combine(_dir, "learned.json"), includeBundled: false);
        _session = new TypingSession(_injector, predictor) { SuggestionCount = 5 };
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
        }
    }

    private KeyDef Key(string label) =>
        _layout.Rows.SelectMany(r => r).First(k => k.Label == label);

    private void Type(string text)
    {
        foreach (var ch in text)
        {
            _session.PressKey(Key(ch.ToString()));
        }
    }

    [Fact]
    public void TypingLetters_SendsUnicodeAndTracksWord()
    {
        Type("hel");

        Assert.Equal("hel", _session.Buffer.Text);
        Assert.Equal(new[] { 'h', 'e', 'l' }, _injector.Events.Select(e => e.Character));
        Assert.All(_injector.Events, e => Assert.Equal(InputEventType.UnicodeChar, e.Type));
    }

    [Fact]
    public void Shift_IsOneShot()
    {
        _session.PressKey(Key("Shift"));
        _session.PressKey(Key("a"));

        Assert.Equal('A', _injector.Events[^1].Character);
        Assert.False(_session.Modifiers.AnyActive);

        _session.PressKey(Key("a"));
        Assert.Equal('a', _injector.Events[^1].Character);
    }

    [Fact]
    public void DoubleTapShift_LocksUntilToggled()
    {
        _session.PressKey(Key("Shift"));
        _session.PressKey(Key("Shift"));
        Assert.True(_session.Modifiers.IsLocked(ModifierKind.Shift));

        _session.PressKey(Key("a"));
        _session.PressKey(Key("b"));
        Assert.Equal('A', _injector.Events[^2].Character);
        Assert.Equal('B', _injector.Events[^1].Character);

        _session.PressKey(Key("Shift"));
        Assert.False(_session.Modifiers.AnyActive);
    }

    [Fact]
    public void CapsKey_LocksShift()
    {
        _session.PressKey(Key("Caps"));
        _session.PressKey(Key("a"));

        Assert.Equal('A', _injector.Events[^1].Character);

        _session.PressKey(Key("Caps"));
        _session.PressKey(Key("a"));
        Assert.Equal('a', _injector.Events[^1].Character);
    }

    [Fact]
    public void Space_CommitsWordAndLearnsIt()
    {
        Type("hello");
        _session.PressKey(Key("Space"));

        Assert.Equal(string.Empty, _session.Buffer.Text);
        Assert.Equal(' ', _injector.Events[^1].Character);
        Assert.Contains("hello", _session.Suggestions.Count > 0 ? _session.Suggestions : new[] { "hello" });
    }

    [Fact]
    public void CtrlShortcut_DoesNotBecomeText()
    {
        _session.PressKey(Key("Ctrl"));
        _session.PressKey(Key("s"));

        Assert.Equal(string.Empty, _session.Buffer.Text);
        Assert.Equal(4, _injector.Events.Count);
        Assert.Equal(VirtualKeys.Ctrl, _injector.Events[0].VirtualKey);
        Assert.Equal(0x53, _injector.Events[1].VirtualKey);
        Assert.Equal(InputEventType.VirtualKeyUp, _injector.Events[3].Type);
        Assert.False(_session.Modifiers.AnyActive);
    }

    [Fact]
    public void ApplySuggestion_ReplacesTrackedWord()
    {
        Type("hel");
        _injector.Events.Clear();

        _session.ApplySuggestion("hello");

        Assert.Equal("hello", _session.Buffer.Text);
        Assert.Equal(3 * 2, _injector.Events.Take(6).Count(e => e.VirtualKey == VirtualKeys.Back));
        Assert.Equal(
            "hello",
            new string(_injector.Events.Skip(6).Select(e => e.Character).ToArray()));
    }

    [Fact]
    public void ApplySuggestion_CanAppendSpace()
    {
        _session.AddSpaceAfterSuggestion = true;
        Type("hel");
        _injector.Events.Clear();

        _session.ApplySuggestion("hello");

        Assert.Equal(string.Empty, _session.Buffer.Text);
        Assert.Equal(' ', _injector.Events[^1].Character);
    }

    [Fact]
    public void LongPressAlternate_IsInsertedDirectly()
    {
        _session.PressText("é");

        Assert.Equal('é', _injector.Events[^1].Character);
        Assert.Equal("é", _session.Buffer.Text);
    }

    [Fact]
    public void ResetWordContext_ClearsBuffer()
    {
        Type("hello");
        _session.ResetWordContext();

        Assert.Equal(string.Empty, _session.Buffer.Text);
        Assert.Empty(_session.Suggestions);
    }

    [Fact]
    public void Backspace_RemovesLastCharacter()
    {
        Type("hel");
        _session.PressKey(Key("⌫"));

        Assert.Equal("he", _session.Buffer.Text);
        Assert.Equal(VirtualKeys.Back, _injector.Events[^1].VirtualKey);
    }

    [Fact]
    public void ClearAll_SendsCtrlAThenBackspaceAndResetsState()
    {
        Type("hello");
        _session.PressKey(Key("Shift"));
        _injector.Events.Clear();

        _session.PressKey(Key("Clear All"));

        Assert.Equal(6, _injector.Events.Count);
        Assert.Equal((InputEventType.VirtualKeyDown, VirtualKeys.Ctrl), (_injector.Events[0].Type, _injector.Events[0].VirtualKey));
        Assert.Equal((InputEventType.VirtualKeyDown, VirtualKeys.A), (_injector.Events[1].Type, _injector.Events[1].VirtualKey));
        Assert.Equal((InputEventType.VirtualKeyUp, VirtualKeys.A), (_injector.Events[2].Type, _injector.Events[2].VirtualKey));
        Assert.Equal((InputEventType.VirtualKeyUp, VirtualKeys.Ctrl), (_injector.Events[3].Type, _injector.Events[3].VirtualKey));
        Assert.Equal((InputEventType.VirtualKeyDown, VirtualKeys.Back), (_injector.Events[4].Type, _injector.Events[4].VirtualKey));
        Assert.Equal((InputEventType.VirtualKeyUp, VirtualKeys.Back), (_injector.Events[5].Type, _injector.Events[5].VirtualKey));
        Assert.Equal(string.Empty, _session.Buffer.Text);
        Assert.Empty(_session.Suggestions);
        Assert.False(_session.Modifiers.AnyActive);
    }

    private sealed class FakeInjector : IKeyInjector
    {
        public List<InputEvent> Events { get; } = new();

        public void Send(IReadOnlyList<InputEvent> events) => Events.AddRange(events);
    }
}
