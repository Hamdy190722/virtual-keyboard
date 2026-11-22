using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VirtualKeyboard.Core.Prediction;

/// <summary>
/// Offline prefix predictor. Bundled frequency lists plus per-user learned words.
/// No network access, ever.
/// </summary>
public sealed class WordPredictor
{
    private const int LearnedBoost = 2000;

    private readonly Dictionary<string, int> _baseEn = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _baseAr = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _learnedEn = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _learnedAr = new(StringComparer.Ordinal);
    private readonly string? _learnedPath;
    private bool _dirty;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private WordPredictor(string? learnedPath)
    {
        _learnedPath = learnedPath;
    }

    public int BaseWordCount => _baseEn.Count + _baseAr.Count;
    public int LearnedWordCount => _learnedEn.Count + _learnedAr.Count;

    public static WordPredictor Create(
        string? wordlistsDirectory = null,
        string? learnedPath = null,
        bool includeBundled = true)
    {
        var predictor = new WordPredictor(learnedPath);
        if (includeBundled)
        {
            predictor.LoadEmbedded("words-en.txt", "words-ar.txt");
        }

        predictor.LoadUserWordlists(wordlistsDirectory);
        predictor.LoadLearned();
        return predictor;
    }

    public IReadOnlyList<string> Predict(string? prefix, string language, int count)
    {
        if (count <= 0 || string.IsNullOrWhiteSpace(prefix))
        {
            return Array.Empty<string>();
        }

        var isArabic = string.Equals(language, Layouts.LayoutCatalog.ArabicId, StringComparison.OrdinalIgnoreCase);
        var normalizedPrefix = Normalize(prefix.Trim(), isArabic);
        if (normalizedPrefix.Length == 0)
        {
            return Array.Empty<string>();
        }

        var baseWords = isArabic ? _baseAr : _baseEn;
        var learned = isArabic ? _learnedAr : _learnedEn;

        var scores = new Dictionary<string, int>(isArabic ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

        foreach (var (word, freq) in baseWords)
        {
            if (word.Length <= normalizedPrefix.Length)
            {
                continue;
            }

            if (!Normalize(word, isArabic).StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            scores[word] = freq;
        }

        foreach (var (word, learnedCount) in learned)
        {
            if (word.Length <= normalizedPrefix.Length)
            {
                continue;
            }

            if (!Normalize(word, isArabic).StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            scores[word] = scores.TryGetValue(word, out var existing)
                ? existing + learnedCount * LearnedBoost
                : learnedCount * LearnedBoost;
        }

        return scores
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(count)
            .Select(pair => pair.Key)
            .ToArray();
    }

    public void Learn(string? word)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            return;
        }

        var trimmed = word.Trim();
        if (trimmed.Length < 2)
        {
            return;
        }

        if (IsArabic(trimmed))
        {
            var normalized = Normalize(trimmed, isArabic: true);
            if (normalized.Length < 2)
            {
                return;
            }

            _learnedAr[normalized] = _learnedAr.GetValueOrDefault(normalized) + 1;
        }
        else
        {
            var normalized = trimmed.ToLowerInvariant();
            _learnedEn[normalized] = _learnedEn.GetValueOrDefault(normalized) + 1;
        }

        _dirty = true;
    }

    public bool SaveLearnedIfDirty()
    {
        if (!_dirty || string.IsNullOrEmpty(_learnedPath))
        {
            return false;
        }

        try
        {
            var directory = Path.GetDirectoryName(_learnedPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = new LearnedFile
            {
                En = _learnedEn.Count > 0 ? new Dictionary<string, int>(_learnedEn) : null,
                Ar = _learnedAr.Count > 0 ? new Dictionary<string, int>(_learnedAr) : null
            };

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var temp = _learnedPath + ".tmp";
            File.WriteAllText(temp, json, Encoding.UTF8);
            File.Move(temp, _learnedPath, overwrite: true);
            _dirty = false;
            return true;
        }
        catch
        {
            // Learning is best-effort; never break typing because a file could not be written.
            return false;
        }
    }

    private void LoadEmbedded(string englishResource, string arabicResource)
    {
        var assembly = Assembly.GetExecutingAssembly();
        LoadEmbeddedResource(assembly, englishResource, isArabic: false);
        LoadEmbeddedResource(assembly, arabicResource, isArabic: true);
    }

    private void LoadEmbeddedResource(Assembly assembly, string resourceName, bool isArabic)
    {
        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            ParseLines(reader, isArabic, explicitRankOffset: true);
        }
        catch
        {
            // Missing resources degrade to learning-only prediction.
        }
    }

    private void LoadUserWordlists(string? directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.txt"))
            {
                using var reader = new StreamReader(file, Encoding.UTF8);
                ParseLines(reader, isArabic: null, explicitRankOffset: false);
            }
        }
        catch
        {
            // A malformed user wordlist must not prevent startup.
        }
    }

    private void ParseLines(StreamReader reader, bool? isArabic, bool explicitRankOffset)
    {
        var index = 0;
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var parts = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var word = parts[0].Trim();
            if (word.Length < 2)
            {
                continue;
            }

            var frequency = explicitRankOffset
                ? Math.Max(1, 100_000 - index * 25)
                : Math.Max(1, 10_000 - index);

            if (parts.Length > 1 && int.TryParse(parts[1], out var explicitFrequency) && explicitFrequency > 0)
            {
                frequency = explicitFrequency;
            }

            var arabic = isArabic ?? IsArabic(word);
            var target = arabic ? _baseAr : _baseEn;
            var normalized = Normalize(word, arabic);
            if (normalized.Length < 2)
            {
                continue;
            }

            target[normalized] = target.GetValueOrDefault(normalized) + frequency;
            index++;
        }
    }

    private void LoadLearned()
    {
        if (string.IsNullOrEmpty(_learnedPath) || !File.Exists(_learnedPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_learnedPath, Encoding.UTF8);
            var payload = JsonSerializer.Deserialize<LearnedFile>(json, JsonOptions);
            if (payload?.En is not null)
            {
                foreach (var (word, count) in payload.En)
                {
                    if (!string.IsNullOrWhiteSpace(word) && count > 0)
                    {
                        _learnedEn[word.ToLowerInvariant()] = count;
                    }
                }
            }

            if (payload?.Ar is not null)
            {
                foreach (var (word, count) in payload.Ar)
                {
                    if (!string.IsNullOrWhiteSpace(word) && count > 0)
                    {
                        _learnedAr[word] = count;
                    }
                }
            }
        }
        catch
        {
            // Corrupt learned file: start fresh.
        }
    }

    public static bool IsArabic(string text) => text.Any(ch => ch is >= '\u0600' and <= '\u06FF' or >= '\u0750' and <= '\u077F');

    /// <summary>Strips Arabic diacritics/tatweel and lowercases Latin so prefixes match cleanly.</summary>
    public static string Normalize(string text, bool isArabic)
    {
        if (!isArabic)
        {
            return text.ToLowerInvariant();
        }

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch is >= '\u064B' and <= '\u065F')
            {
                continue;
            }

            if (ch is '\u0640' or '\u0670')
            {
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    private sealed class LearnedFile
    {
        [JsonPropertyName("en")] public Dictionary<string, int>? En { get; set; }
        [JsonPropertyName("ar")] public Dictionary<string, int>? Ar { get; set; }
    }
}
