using VirtualKeyboard.Core.Prediction;

namespace VirtualKeyboard.Core.Tests;

public class PredictionTests : IDisposable
{
    private readonly string _dir;

    public PredictionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "vk-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_dir, "wordlists"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // Temp cleanup is best-effort.
        }
    }

    [Fact]
    public void BundledLists_AreEmbeddedAndNonEmpty()
    {
        var predictor = WordPredictor.Create();

        Assert.True(predictor.BaseWordCount > 1000, $"only {predictor.BaseWordCount} words");
        Assert.Contains("hello", predictor.Predict("hel", "en", 10));
        Assert.Contains("كتاب", predictor.Predict("كت", "ar", 10));
    }

    [Fact]
    public void Predict_RanksByFrequency()
    {
        File.WriteAllText(Path.Combine(_dir, "wordlists", "test.txt"), "hello 100\nhelp 90\nheld 80\n");
        var predictor = WordPredictor.Create(Path.Combine(_dir, "wordlists"), null, includeBundled: false);

        var results = predictor.Predict("hel", "en", 3);

        Assert.Equal(new[] { "hello", "help", "held" }, results);
    }

    [Fact]
    public void Learn_BoostsWordToTop_AndPersists()
    {
        File.WriteAllText(Path.Combine(_dir, "wordlists", "test.txt"), "hello 100\nhelp 90\n");
        var learnedPath = Path.Combine(_dir, "learned.json");
        var predictor = WordPredictor.Create(Path.Combine(_dir, "wordlists"), learnedPath, includeBundled: false);

        predictor.Learn("help");
        Assert.Equal("help", predictor.Predict("hel", "en", 2)[0]);
        Assert.True(predictor.SaveLearnedIfDirty());

        var reloaded = WordPredictor.Create(Path.Combine(_dir, "wordlists"), learnedPath, includeBundled: false);
        Assert.Equal("help", reloaded.Predict("hel", "en", 2)[0]);
    }

    [Fact]
    public void Predict_IsLanguageFiltered()
    {
        File.WriteAllText(Path.Combine(_dir, "wordlists", "ar.txt"), "كتاب 100\nكتابة 90\n");
        var predictor = WordPredictor.Create(Path.Combine(_dir, "wordlists"), null, includeBundled: false);

        Assert.Equal(2, predictor.Predict("كت", "ar", 5).Count);
        Assert.Empty(predictor.Predict("كت", "en", 5));
    }

    [Fact]
    public void Predict_ExcludesExactMatchAndEmptyPrefix()
    {
        File.WriteAllText(Path.Combine(_dir, "wordlists", "test.txt"), "hello 100\n");
        var predictor = WordPredictor.Create(Path.Combine(_dir, "wordlists"), null, includeBundled: false);

        Assert.Empty(predictor.Predict("hello", "en", 5));
        Assert.Empty(predictor.Predict("", "en", 5));
    }

    [Fact]
    public void WordBuffer_CommitsOnBoundary()
    {
        var buffer = new WordBuffer();
        Assert.Null(buffer.Append("hel"));
        Assert.Equal("hel", buffer.Text);

        Assert.Equal("hel", buffer.Append(" "));
        Assert.Equal(string.Empty, buffer.Text);

        buffer.Append("a");
        Assert.Null(buffer.Append("!"));
        Assert.Equal(string.Empty, buffer.Text);
    }

    [Fact]
    public void WordBuffer_BackspaceAndArabic()
    {
        var buffer = new WordBuffer();
        buffer.Append("كتاب");
        Assert.Equal(4, buffer.Length);

        buffer.Backspace();
        Assert.Equal("كتا", buffer.Text);

        Assert.True(WordBuffer.IsWordChar('ش'));
        Assert.True(WordBuffer.IsWordChar('7'));
        Assert.False(WordBuffer.IsWordChar(' '));
    }
}
