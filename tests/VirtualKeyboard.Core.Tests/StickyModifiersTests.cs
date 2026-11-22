using VirtualKeyboard.Core.Layouts;
using VirtualKeyboard.Core.Sticky;

namespace VirtualKeyboard.Core.Tests;

public class StickyModifiersTests
{
    [Fact]
    public void Tap_CyclesOffArmedLocked()
    {
        var sticky = new StickyModifiers();

        sticky.Tap(ModifierKind.Shift);
        Assert.Equal(ModifierState.Armed, sticky.GetState(ModifierKind.Shift));

        sticky.Tap(ModifierKind.Shift);
        Assert.Equal(ModifierState.Locked, sticky.GetState(ModifierKind.Shift));

        sticky.Tap(ModifierKind.Shift);
        Assert.Equal(ModifierState.Off, sticky.GetState(ModifierKind.Shift));
    }

    [Fact]
    public void Consume_ClearsArmedButKeepsLocked()
    {
        var sticky = new StickyModifiers();
        sticky.Tap(ModifierKind.Ctrl);
        sticky.Tap(ModifierKind.Shift);
        sticky.Tap(ModifierKind.Shift);

        Assert.True(sticky.Consume());

        Assert.Equal(ModifierState.Off, sticky.GetState(ModifierKind.Ctrl));
        Assert.Equal(ModifierState.Locked, sticky.GetState(ModifierKind.Shift));
        Assert.False(sticky.Consume());
    }

    [Fact]
    public void Snapshot_ReportsActiveAndLocked()
    {
        var sticky = new StickyModifiers();
        sticky.Tap(ModifierKind.Ctrl);
        sticky.Tap(ModifierKind.Alt);
        sticky.Tap(ModifierKind.Alt);

        var snapshot = sticky.Snapshot();

        Assert.True(snapshot.Ctrl);
        Assert.True(snapshot.Alt);
        Assert.False(snapshot.Shift);
        Assert.True(sticky.IsLocked(ModifierKind.Alt));
        Assert.True(snapshot.HasCtrlLike);
    }

    [Fact]
    public void CapsToggle_LocksAndUnlocksShift()
    {
        var sticky = new StickyModifiers();
        sticky.SetLocked(ModifierKind.Shift, true);
        Assert.True(sticky.IsLocked(ModifierKind.Shift));

        sticky.SetLocked(ModifierKind.Shift, false);
        Assert.False(sticky.AnyActive);
    }
}
