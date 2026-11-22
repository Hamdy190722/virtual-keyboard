using VirtualKeyboard.Core.Input;
using VirtualKeyboard.Core.Layouts;
using VirtualKeyboard.Core.Prediction;
using VirtualKeyboard.Core.Sticky;

namespace VirtualKeyboard.Core.Typing;

/// <summary>
/// Orchestrates one key press: resolve shift, inject, update sticky state,
/// track the current word and refresh suggestions.
/// </summary>
public sealed class TypingSession
{
    private readonly IKeyInjector _injector;
    private readonly WordPredictor _predictor;

    public TypingSession(IKeyInjector injector, WordPredictor predictor)
    {
        _injector = injector;
        _predictor = predictor;
    }

    public StickyModifiers Modifiers { get; } = new();
    public WordBuffer Buffer { get; } = new();

    public string Language { get; set; } = LayoutCatalog.EnglishId;
    public bool AddSpaceAfterSuggestion { get; set; }
    public int SuggestionCount { get; set; } = 5;

    public IReadOnlyList<string> Suggestions { get; private set; } = Array.Empty<string>();

    public event EventHandler? Changed;

    public void PressKey(KeyDef key)
    {
        switch (key.Kind)
        {
            case KeyKind.Modifier when key.Modifier is { } modifier:
                Modifiers.Tap(modifier);
                Refresh();
                return;

            case KeyKind.CapsToggle:
                Modifiers.SetLocked(ModifierKind.Shift, !Modifiers.IsLocked(ModifierKind.Shift));
                Refresh();
                return;

            case KeyKind.LayoutToggle:
                return;

            case KeyKind.ClearAll:
                ClearAll();
                return;

            case KeyKind.VirtualKey when key.VirtualKey is { } vk:
            {
                var snapshot = Modifiers.Snapshot();
                _injector.Send(InjectionPlanner.Plan(null, vk, key.Extended, snapshot));
                Modifiers.Consume();
                AfterVirtualKey(vk, snapshot);
                Refresh();
                return;
            }

            case KeyKind.Text:
            {
                var snapshot = Modifiers.Snapshot();
                var text = snapshot.Shift && key.ShiftText is not null
                    ? key.ShiftText
                    : key.Text ?? key.Label;

                _injector.Send(InjectionPlanner.Plan(text, null, false, snapshot));
                Modifiers.Consume();
                AfterText(text, snapshot);
                Refresh();
                return;
            }
        }
    }

    /// <summary>Inserts a long-press alternate without applying sticky Shift.</summary>
    public void PressText(string text)
    {
        _injector.Send(InjectionPlanner.Plan(text, null, false, default));
        Modifiers.Consume();
        var committed = Buffer.Append(text);
        if (committed is not null)
        {
            _predictor.Learn(committed);
        }

        Refresh();
    }

    public void ApplySuggestion(string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return;
        }

        var events = new List<InputEvent>(Buffer.Length * 2 + word.Length + 2);
        for (var i = 0; i < Buffer.Length; i++)
        {
            events.Add(InputEvent.KeyDown(VirtualKeys.Back));
            events.Add(InputEvent.KeyUp(VirtualKeys.Back));
        }

        foreach (var ch in word)
        {
            events.Add(InputEvent.Unicode(ch));
        }

        _injector.Send(events);
        _predictor.Learn(word);

        if (AddSpaceAfterSuggestion)
        {
            _injector.Send(InjectionPlanner.Plan(" ", null, false, default));
            Buffer.Clear();
        }
        else
        {
            Buffer.Set(word);
        }

        Modifiers.Consume();
        Refresh();
    }

    public void ResetWordContext()
    {
        Buffer.Clear();
        Refresh();
    }

    /// <summary>Selects everything in the focused field and deletes it (Ctrl+A, then Backspace).</summary>
    public void ClearAll()
    {
        _injector.Send(InjectionPlanner.Plan(null, VirtualKeys.A, false, new StickySnapshot(false, Ctrl: true, false, false)));
        _injector.Send(InjectionPlanner.Plan(null, VirtualKeys.Back, false, default));
        Modifiers.Consume();
        Buffer.Clear();
        Refresh();
    }

    public void SaveLearnedIfDirty() => _predictor.SaveLearnedIfDirty();

    private void AfterText(string text, StickySnapshot snapshot)
    {
        if (snapshot.HasCtrlLike)
        {
            // A shortcut, not text: commit the pending word and start clean.
            var pending = Buffer.TakeWord();
            if (pending is not null)
            {
                _predictor.Learn(pending);
            }

            return;
        }

        var committed = Buffer.Append(text);
        if (committed is not null)
        {
            _predictor.Learn(committed);
        }
    }

    private void AfterVirtualKey(ushort vk, StickySnapshot snapshot)
    {
        if (vk == VirtualKeys.Back)
        {
            if (snapshot.HasCtrlLike)
            {
                Buffer.Clear();
            }
            else
            {
                Buffer.Backspace();
            }

            return;
        }

        if (vk is VirtualKeys.Enter or VirtualKeys.Tab or VirtualKeys.Escape
            or VirtualKeys.Home or VirtualKeys.End or VirtualKeys.Delete
            or VirtualKeys.Left or VirtualKeys.Up or VirtualKeys.Right or VirtualKeys.Down
            or VirtualKeys.PageUp or VirtualKeys.PageDown)
        {
            var pending = Buffer.TakeWord();
            if (pending is not null)
            {
                _predictor.Learn(pending);
            }
        }
    }

    private void Refresh()
    {
        Suggestions = _predictor.Predict(Buffer.Text, Language, SuggestionCount);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
