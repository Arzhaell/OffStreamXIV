namespace OffStreamXIV.Tests;

public class HidingTests
{
    private static Configuration Empty() => new() { HiddenPlugins = [] };

    [Fact]
    public void OffStreamXIVWindowsAreHiddenAtFirst()
    {
        var configuration = new Configuration();
        Assert.True(Hiding.IsHidden(configuration, MainWindow.Key, Plugin.InternalName));
        Assert.False(Hiding.IsHidden(configuration, "Settings", "SimpleHUD"));
    }

    [Fact]
    public void TestWindowIsAlwaysHidden() => Assert.True(Hiding.IsHidden(Empty(), TestWindow.Key, Plugin.InternalName));

    [Fact]
    public void RedrawnGameTextsAreAlwaysHidden()
    {
        var key = GameWindowAdditions.OverlayKey("CharacterStatus");
        Assert.True(Hiding.IsHidden(Empty(), key, Plugin.InternalName));
        Assert.Empty(PluginList.Build([], [new KnownWindow(key, Plugin.InternalName, true, key)], Empty(), new Dictionary<string, SortedSet<string>>(), "")
            .SelectMany(g => g.AllWindows));
    }

    [Fact]
    public void WindowsFollowTheirPlugin()
    {
        var configuration = Empty();
        Hiding.SetPlugin(configuration, "SimpleHUD", true);
        Assert.True(Hiding.IsHidden(configuration, "Settings", "SimpleHUD"));
        Assert.True(Hiding.IsHidden(configuration, "Window never opened", "SimpleHUD"));
        Assert.False(Hiding.IsHidden(configuration, "Settings", "RefBoardXIV"));
    }

    [Fact]
    public void AWindowCanDifferFromItsPlugin()
    {
        var configuration = Empty();
        Hiding.SetPlugin(configuration, "SimpleHUD", true);
        Hiding.SetWindow(configuration, "Settings", "SimpleHUD", "Settings", false);
        Assert.False(Hiding.IsHidden(configuration, "Settings", "SimpleHUD"));
        Assert.True(Hiding.IsHidden(configuration, "HUD", "SimpleHUD"));
    }

    [Fact]
    public void AWindowSetLikeItsPluginKeepsFollowingIt()
    {
        var configuration = Empty();
        Hiding.SetWindow(configuration, "Settings", "SimpleHUD", "Settings", true);
        Hiding.SetWindow(configuration, "Settings", "SimpleHUD", "Settings", false);
        Assert.Empty(configuration.Windows);

        Hiding.SetPlugin(configuration, "SimpleHUD", true);
        Assert.True(Hiding.IsHidden(configuration, "Settings", "SimpleHUD"));
    }

    [Fact]
    public void ChangingAPluginResetsOnlyItsOwnWindows()
    {
        var configuration = Empty();
        Hiding.SetWindow(configuration, "Settings", "SimpleHUD", "Settings", true);
        Hiding.SetWindow(configuration, "Board", "RefBoardXIV", "Board", true);

        Hiding.SetPlugin(configuration, "SimpleHUD", false);

        Assert.False(configuration.Windows.ContainsKey("Settings"));
        Assert.True(configuration.Windows.ContainsKey("Board"));
    }

    [Fact]
    public void SavedSettingsReplaceTheStartingValues()
    {
        // Dalamud relit la configuration avec Newtonsoft : les valeurs de départ ne doivent pas s'ajouter à celles enregistrées.
        var json = """{ "Version": 2, "HiddenPlugins": [ "SimpleHUD" ] }""";
        var configuration = Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>(json)!;
        Assert.Equal(["SimpleHUD"], configuration.HiddenPlugins);
    }
}
