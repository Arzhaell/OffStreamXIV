namespace OffStreamXIV.Tests;

public class RoleDetectionTests
{
    [Fact]
    public void TheWindowThatOpensIsTheOne()
    {
        var (keys, closed) = RoleDetection.Detect(new HashSet<string> { "Main" }, new HashSet<string> { "Main", "Settings" });
        Assert.Equal(["Settings"], keys);
        Assert.False(closed);
    }

    [Fact]
    public void AnOpenWindowThatTheRequestClosesIsTheOne()
    {
        // Le plugin bascule sa fenêtre : déjà ouverte, la demande la ferme.
        var (keys, closed) = RoleDetection.Detect(new HashSet<string> { "Main", "Settings" }, new HashSet<string> { "Main" });
        Assert.Equal(["Settings"], keys);
        Assert.True(closed);
    }

    [Fact]
    public void NothingChangedMeansNothingFound()
    {
        var (keys, closed) = RoleDetection.Detect(new HashSet<string> { "Main" }, new HashSet<string> { "Main" });
        Assert.Empty(keys);
        Assert.False(closed);
    }

    [Fact]
    public void OpeningWinsOverClosing()
    {
        var (keys, _) = RoleDetection.Detect(new HashSet<string> { "Main" }, new HashSet<string> { "Settings" });
        Assert.Equal(["Settings"], keys);
    }
}
