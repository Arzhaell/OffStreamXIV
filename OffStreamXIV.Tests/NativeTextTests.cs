using System.Numerics;

namespace OffStreamXIV.Tests;

public class NativeTextTests
{
    private static readonly Vector2 Box = new(100, 20);
    private static readonly Vector2 Text = new(40, 16);

    [Theory]
    [InlineData(0, 0, 0)] // haut-gauche
    [InlineData(2, 60, 0)] // haut-droite
    [InlineData(3, 0, 2)] // gauche, centré verticalement
    [InlineData(4, 30, 2)] // centre
    [InlineData(5, 60, 2)] // droite (les chiffres du panneau Personnage)
    [InlineData(8, 60, 4)] // bas-droite
    public void TextIsPlacedLikeTheGame(int alignment, float x, float y) =>
        Assert.Equal(new Vector2(x, y), NativeText.AlignOffset(alignment, Box, Text));

    [Fact]
    public void UnknownAlignmentStaysInsideTheBox() => Assert.Equal(new Vector2(60, 4), NativeText.AlignOffset(15, Box, Text));

    [Fact]
    public void GameColorBecomesImGuiColor() =>
        Assert.Equal(0x80C0B0A0u, NativeText.ToImGuiColor(0xA0, 0xB0, 0xC0, 0x80));

    [Fact]
    public void TextUnderAnotherGameWindowIsNotRedrawn()
    {
        var text = new ScreenRect(100, 100, 50, 20);
        Assert.True(NativeText.IsCovered(text, [new ScreenRect(140, 110, 200, 200)]));
        Assert.False(NativeText.IsCovered(text, [new ScreenRect(150, 100, 200, 200)])); // juste à côté
        Assert.False(NativeText.IsCovered(text, []));
    }

    [Fact]
    public void ScreenSizedWindowsDoNotCount()
    {
        var screen = new Vector2(2560, 1440);
        Assert.True(NativeText.IsFullScreen(new ScreenRect(0, 0, 2560, 1440), screen));
        Assert.False(NativeText.IsFullScreen(new ScreenRect(800, 300, 400, 600), screen));
    }
}
