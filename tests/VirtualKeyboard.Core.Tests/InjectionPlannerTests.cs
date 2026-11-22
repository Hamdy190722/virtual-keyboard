using VirtualKeyboard.Core.Input;
using VirtualKeyboard.Core.Layouts;

namespace VirtualKeyboard.Core.Tests;

public class InjectionPlannerTests
{
    [Fact]
    public void Text_IsSentAsUnicode()
    {
        var events = InjectionPlanner.Plan("a", null, false, default);

        var single = Assert.Single(events);
        Assert.Equal(InputEventType.UnicodeChar, single.Type);
        Assert.Equal('a', single.Character);
    }

    [Fact]
    public void ArabicText_IsSentAsUnicodePerCharacter()
    {
        var events = InjectionPlanner.Plan("لا", null, false, default);

        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal(InputEventType.UnicodeChar, e.Type));
        Assert.Equal('ل', events[0].Character);
        Assert.Equal('ا', events[1].Character);
    }

    [Fact]
    public void ControlKey_SendsDownAndUp()
    {
        var events = InjectionPlanner.Plan(null, VirtualKeys.Back, false, default);

        Assert.Equal(2, events.Count);
        Assert.Equal(InputEventType.VirtualKeyDown, events[0].Type);
        Assert.Equal(InputEventType.VirtualKeyUp, events[1].Type);
        Assert.Equal(VirtualKeys.Back, events[0].VirtualKey);
    }

    [Fact]
    public void CtrlPlusLetter_IsSentAsShortcut()
    {
        var mods = new StickySnapshot(Shift: false, Ctrl: true, Alt: false, Win: false);

        var events = InjectionPlanner.Plan("s", null, false, mods);

        Assert.Equal(4, events.Count);
        Assert.Equal(VirtualKeys.Ctrl, events[0].VirtualKey);
        Assert.Equal(InputEventType.VirtualKeyDown, events[0].Type);
        Assert.Equal(0x53, events[1].VirtualKey); // S
        Assert.Equal(0x53, events[2].VirtualKey);
        Assert.Equal(InputEventType.VirtualKeyUp, events[3].Type);
        Assert.Equal(VirtualKeys.Ctrl, events[3].VirtualKey);
    }

    [Fact]
    public void CtrlPlusSymbol_FallsBackToUnicode()
    {
        var mods = new StickySnapshot(Shift: false, Ctrl: true, Alt: false, Win: false);

        var events = InjectionPlanner.Plan("!", null, false, mods);

        var single = Assert.Single(events);
        Assert.Equal(InputEventType.UnicodeChar, single.Type);
        Assert.Equal('!', single.Character);
    }

    [Fact]
    public void ExtendedArrow_MarksExtendedFlag()
    {
        var events = InjectionPlanner.Plan(null, VirtualKeys.Left, true, default);

        Assert.All(events, e => Assert.True(e.Extended));
    }

    [Theory]
    [InlineData('a', 0x41)]
    [InlineData('Z', 0x5A)]
    [InlineData('0', 0x30)]
    [InlineData(';', 0xBA)]
    public void AsciiVirtualKeyMapping_IsCorrect(char ch, int expected)
    {
        Assert.True(InjectionPlanner.TryGetAsciiVirtualKey(ch, out var vk));
        Assert.Equal((ushort)expected, vk);
    }

    [Fact]
    public void ArabicLetter_HasNoAsciiVirtualKey()
    {
        Assert.False(InjectionPlanner.TryGetAsciiVirtualKey('ش', out _));
    }
}
