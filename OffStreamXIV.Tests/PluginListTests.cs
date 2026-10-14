namespace OffStreamXIV.Tests;

public class PluginListTests
{
    private static readonly InstalledPlugin[] Installed =
    [
        new("SimpleHUD", "Simple HUD", true, HasMainUi: true, HasConfigUi: true),
        new("RefBoardXIV", "RefBoardXIV", true, HasMainUi: true, HasConfigUi: true),
        new("CharacterPanelRefined", "Character Panel Refined", true, HasConfigUi: true),
        new("SimpleTweaksPlugin", "Simple Tweaks", true, HasConfigUi: true),
        new("Unloaded", "Unloaded plugin", false),
    ];

    private static readonly Dictionary<string, SortedSet<string>> NoGameWindows = [];

    private static KnownWindow Window(string key, string plugin, bool open = true, bool listed = true) =>
        new(key, plugin, listed, key) { Open = open };

    private static List<PluginGroup> Build(
        KnownWindow[] windows, Configuration? configuration = null, string filter = "", Dictionary<string, SortedSet<string>>? gameWindows = null) =>
        PluginList.Build(Installed, windows, configuration ?? new Configuration { HiddenPlugins = [] }, gameWindows ?? NoGameWindows, filter);

    private static Configuration WithRoles(string plugin, string[] main, string[] settings)
    {
        var configuration = new Configuration { HiddenPlugins = [] };
        var roles = configuration.Roles[plugin] = new PluginRoles();
        foreach (var key in main)
            roles.Main[key] = key;
        foreach (var key in settings)
            roles.Settings[key] = key;
        return configuration;
    }

    [Fact]
    public void MainWindowAndSettingsAreSeparatedFromOtherWindows()
    {
        var group = Build(
                [Window("Board###board", "RefBoardXIV"), Window("Settings###config", "RefBoardXIV"), Window("Timer", "RefBoardXIV")],
                WithRoles("RefBoardXIV", ["Board###board"], ["Settings###config"]))
            .Single(g => g.InternalName == "RefBoardXIV");

        Assert.Equal(["Board###board"], group.Main.Select(w => w.Key));
        Assert.Equal(["Settings###config"], group.Settings.Select(w => w.Key));
        Assert.Equal(["Timer"], group.Windows.Select(w => w.Key));
        Assert.False(group.SameMainAndSettings);
    }

    [Fact]
    public void SameWindowForMainAndSettingsIsOneLine()
    {
        var group = Build([Window("SimpleHUD", "SimpleHUD")], WithRoles("SimpleHUD", ["SimpleHUD"], ["SimpleHUD"]))
            .Single(g => g.InternalName == "SimpleHUD");

        Assert.True(group.SameMainAndSettings);
        Assert.Single(group.AllWindows);
    }

    [Fact]
    public void RolesNotFoundYetStillShowWhenDalamudKnowsThem()
    {
        var groups = Build([]);
        var cpr = groups.Single(g => g.InternalName == "CharacterPanelRefined");
        Assert.False(cpr.ShowsMain);
        Assert.True(cpr.ShowsSettings);
        Assert.Empty(cpr.Settings);
        Assert.False(cpr.IsEmpty);
    }

    [Fact]
    public void FoundWindowsStayListedWhenClosed()
    {
        var group = Build([], WithRoles("RefBoardXIV", ["Board###board"], ["Settings###config"]))
            .Single(g => g.InternalName == "RefBoardXIV");
        Assert.False(Assert.Single(group.Main).Open);
        Assert.False(Assert.Single(group.Settings).Open);
    }

    [Fact]
    public void HiddenGameWindowWithoutPluginStaysListed()
    {
        var configuration = new Configuration { HiddenPlugins = [], HiddenGameWindows = ["CharacterStatus"] };
        var unknown = Build([], configuration).Single(g => g.InternalName == PluginOwner.Unknown);
        Assert.Equal("CharacterStatus", Assert.Single(unknown.GameWindows).Addon);
    }

    [Fact]
    public void GameWindowsAreListedUnderEveryPluginThatChangesThem()
    {
        var gameWindows = new Dictionary<string, SortedSet<string>>
        {
            ["CharacterPanelRefined"] = ["CharacterStatus", "ItemDetail"],
            ["SimpleTweaksPlugin"] = ["CharacterStatus"],
        };
        var groups = Build([], gameWindows: gameWindows);

        var cpr = groups.Single(g => g.InternalName == "CharacterPanelRefined");
        Assert.Equal(["CharacterStatus", "ItemDetail"], cpr.GameWindows.Select(g => g.Addon).Order());
        Assert.Equal(["Simple Tweaks"], cpr.GameWindows.Single(g => g.Addon == "CharacterStatus").Others);
        Assert.Empty(cpr.GameWindows.Single(g => g.Addon == "ItemDetail").Others);
        Assert.Equal(["Character Panel Refined"], groups.Single(g => g.InternalName == "SimpleTweaksPlugin").GameWindows.Single().Others);
    }

    [Fact]
    public void DalamudComesFirstThenPluginsByName()
    {
        var groups = Build([]);
        Assert.Equal(["Dalamud", "Character Panel Refined", "RefBoardXIV", "Simple HUD", "Simple Tweaks"], groups.Select(g => g.Name));
    }

    [Fact]
    public void WindowsAreGroupedUnderTheirPlugin()
    {
        var groups = Build([Window("Settings##hud", "SimpleHUD"), Window("Plugin Installer", "Dalamud")]);
        Assert.Equal(["Plugin Installer"], groups.Single(g => g.InternalName == "Dalamud").Windows.Select(w => w.Key));
        Assert.Equal(["Settings##hud"], groups.Single(g => g.InternalName == "SimpleHUD").Windows.Select(w => w.Key));
    }

    [Fact]
    public void MenusAndTheTestWindowAreNotListed()
    {
        var groups = Build([Window("##Menu", "SimpleHUD", listed: false), Window(TestWindow.Key, Plugin.InternalName)]);
        Assert.All(groups, g => Assert.Empty(g.Windows));
    }

    [Fact]
    public void OpenWindowsComeFirst()
    {
        var groups = Build([Window("A closed", "SimpleHUD", open: false), Window("B open", "SimpleHUD")]);
        Assert.Equal(["B open", "A closed"], groups.Single(g => g.InternalName == "SimpleHUD").Windows.Select(w => w.Key));
    }

    [Fact]
    public void ClosedWindowsWithTheirOwnSettingStayListed()
    {
        var configuration = new Configuration { HiddenPlugins = [] };
        Hiding.SetWindow(configuration, "Loot###loot", "Unloaded", "Loot", true);

        var group = Build([], configuration).Single(g => g.InternalName == "Unloaded");

        Assert.False(group.Loaded);
        var row = Assert.Single(group.Windows);
        Assert.Equal("Loot", row.Label);
        Assert.False(row.Open);
    }

    [Fact]
    public void HiddenPluginsStayListedEvenUnloaded()
    {
        var configuration = new Configuration { HiddenPlugins = ["Unloaded"] };
        Assert.Contains(Build([], configuration), g => g.InternalName == "Unloaded" && !g.Loaded);
        Assert.DoesNotContain(Build([]), g => g.InternalName == "Unloaded");
    }

    [Fact]
    public void FilterKeepsAMatchingPluginWithAllItsWindows()
    {
        var groups = Build([Window("Settings", "SimpleHUD"), Window("Board", "RefBoardXIV")], filter: "hud");
        var group = Assert.Single(groups);
        Assert.Equal(["Settings"], group.Windows.Select(w => w.Key));
    }

    [Fact]
    public void FilterKeepsOnlyMatchingWindowsOfOtherPlugins()
    {
        var groups = Build([Window("Settings", "SimpleHUD"), Window("HUD layout", "SimpleHUD"), Window("Board", "RefBoardXIV")], filter: "layout");
        var group = Assert.Single(groups);
        Assert.Equal(["HUD layout"], group.Windows.Select(w => w.Key));
    }
}
