using Dalamud.Bindings.ImGui;

namespace OffStreamXIV.Tests;

public class WindowRulesTests
{
    // Fenêtre ImGui simplifiée : son type et la fenêtre où elle a été ouverte.
    private readonly record struct Node(string Name, ImGuiWindowFlags Flags, int Parent);

    private static readonly Node[] Tree =
    [
        new("Plugin window", ImGuiWindowFlags.None, -1),
        new("Combo list", ImGuiWindowFlags.Popup | ImGuiWindowFlags.AlwaysAutoResize, 0),
        new("Tooltip in the combo", ImGuiWindowFlags.Tooltip, 1),
        new("Context menu", ImGuiWindowFlags.Popup, 0),
        new("Sub-menu", ImGuiWindowFlags.Popup | ImGuiWindowFlags.ChildMenu, 3),
        new("Modal", ImGuiWindowFlags.Popup | ImGuiWindowFlags.Modal, 0),
        new("Popup opened outside any window", ImGuiWindowFlags.Popup, -1),
        new("Window begun inside another one", ImGuiWindowFlags.None, 0),
    ];

    private static Node Owner(int index) =>
        WindowRules.Owner(Tree[index], n => n.Flags, n => n.Parent < 0 ? null : Tree[n.Parent]);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void MenusTooltipsAndModalsFollowTheWindowThatOpenedThem(int index) =>
        Assert.Equal("Plugin window", Owner(index).Name);

    [Fact]
    public void PopupWithoutParentDecidesForItself() => Assert.Equal("Popup opened outside any window", Owner(6).Name);

    [Fact]
    public void NormalWindowsAlwaysDecideForThemselves() => Assert.Equal("Window begun inside another one", Owner(7).Name);

    [Fact]
    public void OnlyMainWindowsAreListed()
    {
        Assert.True(WindowRules.IsListed(ImGuiWindowFlags.None));
        Assert.True(WindowRules.IsListed(ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoTitleBar));
        Assert.False(WindowRules.IsListed(ImGuiWindowFlags.Tooltip));
        Assert.False(WindowRules.IsListed(ImGuiWindowFlags.Popup));
        Assert.False(WindowRules.IsListed(ImGuiWindowFlags.ChildWindow));
    }

    [Fact]
    public void MainWindowsAndModalsCanGetTheirOwnWindowsWindow()
    {
        Assert.True(WindowRules.CanOwnViewport(ImGuiWindowFlags.None));
        Assert.True(WindowRules.CanOwnViewport(ImGuiWindowFlags.Popup | ImGuiWindowFlags.Modal));
        Assert.False(WindowRules.CanOwnViewport(ImGuiWindowFlags.Popup));
        Assert.False(WindowRules.CanOwnViewport(ImGuiWindowFlags.Tooltip));
        Assert.False(WindowRules.CanOwnViewport(ImGuiWindowFlags.Popup | ImGuiWindowFlags.ChildMenu));
        Assert.False(WindowRules.CanOwnViewport(ImGuiWindowFlags.ChildWindow));
    }

    [Fact]
    public void LoopsDoNotHang()
    {
        var looping = new Node("Loop", ImGuiWindowFlags.Popup, 0);
        var owner = WindowRules.Owner(looping, n => n.Flags, n => looping);
        Assert.Equal("Loop", owner.Name);
    }
}
