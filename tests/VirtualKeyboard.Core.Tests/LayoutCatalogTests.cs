using VirtualKeyboard.Core.Layouts;

namespace VirtualKeyboard.Core.Tests;

public class LayoutCatalogTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ar")]
    public void Layouts_HaveSixRowsAndFullWidth(string id)
    {
        var layout = LayoutCatalog.Get(id);

        Assert.Equal(6, layout.Rows.Count);
        foreach (var row in layout.Rows)
        {
            Assert.NotEmpty(row);
            var total = row.Sum(k => k.Width);
            Assert.InRange(total, 14.9, 15.1);
        }
    }

    [Fact]
    public void EveryTextKey_HasText()
    {
        foreach (var layout in LayoutCatalog.All)
        {
            foreach (var key in layout.Rows.SelectMany(r => r))
            {
                if (key.Kind == KeyKind.Text)
                {
                    Assert.False(string.IsNullOrEmpty(key.Text), $"{layout.Id}:{key.Label}");
                }
            }
        }
    }

    [Fact]
    public void Arabic_IsRightToLeft_WithArabicIndicDigits()
    {
        var arabic = LayoutCatalog.Get("ar");

        Assert.True(arabic.IsRightToLeft);
        Assert.Contains(arabic.Rows[0], k => k.Label == "١");
        Assert.Contains(arabic.Rows[0], k => k.ShiftText == "!");
        Assert.Contains(arabic.Rows.SelectMany(r => r), k => k.Text == "لا");
    }

    [Fact]
    public void English_HasShiftAndAlternates()
    {
        var english = LayoutCatalog.Get("en");
        var keys = english.Rows.SelectMany(r => r).ToList();

        var a = Assert.Single(keys.Where(k => k.Text == "a"));
        Assert.Equal("A", a.ShiftText);
        Assert.Contains("á", a.Alternates);

        var one = Assert.Single(keys.Where(k => k.Text == "1"));
        Assert.Equal("!", one.ShiftText);

        Assert.Contains(keys, k => k.Kind == KeyKind.LayoutToggle);
        Assert.Contains(keys, k => k.Kind == KeyKind.CapsToggle);
        Assert.Equal(6, keys.Count(k => k.Kind == KeyKind.Modifier && k.Modifier != ModifierKind.Win));
    }

    [Fact]
    public void UnknownLayout_FallsBackToEnglish()
    {
        Assert.Equal("en", LayoutCatalog.Get("xx").Id);
        Assert.Equal("en", LayoutCatalog.Get(null).Id);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ar")]
    public void Layouts_IncludeClearAllKey(string id)
    {
        var layout = LayoutCatalog.Get(id);
        Assert.Contains(layout.Rows.SelectMany(r => r), k => k.Kind == KeyKind.ClearAll);
    }
}
