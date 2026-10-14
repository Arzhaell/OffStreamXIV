namespace OffStreamXIV.Tests;

public class PluginOwnerTests
{
    // Étapes de pile typiques, de la plus récente (l'interception) à la plus ancienne.
    private static readonly CallFrame Interception = new("OffStreamXIV", true);
    private static readonly CallFrame ImGuiBindings = new(null, true);
    private static readonly CallFrame DalamudCode = new(null, false);

    private static CallFrame PluginCode(string name) => new(name, false);

    [Fact]
    public void WindowOpenedThroughDalamudWindowSystemBelongsToThePlugin() =>
        Assert.Equal("SimpleHUD", PluginOwner.Find([Interception, ImGuiBindings, DalamudCode, DalamudCode, PluginCode("SimpleHUD"), DalamudCode]));

    [Fact]
    public void OffStreamXIVOwnWindowsBelongToOffStreamXIV() =>
        Assert.Equal("OffStreamXIV", PluginOwner.Find([Interception, ImGuiBindings, DalamudCode, Interception with { Relay = false }]));

    [Fact]
    public void PluginCallingImGuiDirectly() =>
        Assert.Equal("Splatoon", PluginOwner.Find([Interception, PluginCode("Splatoon"), DalamudCode]));

    [Fact]
    public void TheClosestPluginWins() =>
        Assert.Equal("Inner", PluginOwner.Find([Interception, ImGuiBindings, PluginCode("Inner"), PluginCode("Outer")]));

    [Fact]
    public void WindowsWithoutPluginBelongToDalamud() =>
        Assert.Equal(PluginOwner.Dalamud, PluginOwner.Find([Interception, ImGuiBindings, DalamudCode, DalamudCode]));
}
