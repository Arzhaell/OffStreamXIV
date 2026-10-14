namespace OffStreamXIV.Tests;

public class WindowSystemOwnersTests
{
    // Imitations d'un système de fenêtres de Dalamud et de ses fenêtres : seuls les noms des propriétés comptent.
    private sealed class FakeWindow(string name)
    {
        public string WindowName { get; } = name;
    }

    private sealed class FakeWindowSystem
    {
        public IReadOnlyList<FakeWindow> Windows { get; } = [new("Quest Tracker##QuestTrackerMainWindow"), new("Settings###config")];

        public void Draw()
        {
        }
    }

    private sealed class PluginCode
    {
        public void DrawUi()
        {
        }
    }

    // Comme le WindowService de QuestTracker : son système de fenêtres est un paramètre du constructeur principal.
    private sealed class WindowService(FakeWindowSystem windowSystem)
    {
        public void OnDraw() => windowSystem.Draw();
    }

    [Fact]
    public void WindowSystemPluggedDirectlyGivesItsWindows()
    {
        Action draw = new FakeWindowSystem().Draw;
        Assert.Equal(["Quest Tracker##QuestTrackerMainWindow", "Settings###config"], WindowSystemOwners.WindowNames(draw));
    }

    [Fact]
    public void WindowSystemHeldByPluginCodeGivesItsWindows()
    {
        // .NET retire OnDraw de la pile (appel terminal) : le plugin n'y apparaît pas, il faut lire son champ.
        Action draw = new WindowService(new FakeWindowSystem()).OnDraw;
        Assert.Equal(["Quest Tracker##QuestTrackerMainWindow", "Settings###config"], WindowSystemOwners.WindowNames(draw));
    }

    [Fact]
    public void PluginCodeWithoutWindowSystemGivesNothing()
    {
        Action draw = new PluginCode().DrawUi;
        draw += static () => { };
        Assert.Empty(WindowSystemOwners.WindowNames(draw));
    }
}
