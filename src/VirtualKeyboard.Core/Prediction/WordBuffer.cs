using System.Text;

namespace VirtualKeyboard.Core.Prediction;

/// <summary>Tracks the word currently being typed so suggestions can replace it.</summary>
public sealed class WordBuffer
{
    private readonly StringBuilder _sb = new();

    public string Text => _sb.ToString();
    public int Length => _sb.Length;

    /// <summary>
    /// Appends typed text. Any non-word character ends the current word; the committed
    /// word (length >= 2) is returned so it can be learned.
    /// </summary>
    public string? Append(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        string? committed = null;
        foreach (var ch in text)
        {
            if (IsWordChar(ch))
            {
                _sb.Append(ch);
            }
            else
            {
                committed = TakeWord() ?? committed;
            }
        }

        return committed;
    }

    public void Backspace()
    {
        if (_sb.Length > 0)
        {
            _sb.Length--;
        }
    }

    /// <summary>Returns the current word and clears it when it is long enough to learn.</summary>
    public string? TakeWord()
    {
        if (_sb.Length < 2)
        {
            _sb.Clear();
            return null;
        }

        var word = _sb.ToString();
        _sb.Clear();
        return word;
    }

    public void Set(string text)
    {
        _sb.Clear();
        _sb.Append(text);
    }

    public void Clear() => _sb.Clear();

    public static bool IsWordChar(char ch) => char.IsLetterOrDigit(ch) || ch is '\'' or '\u2019';
}
