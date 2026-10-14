using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Plugin.Services;

namespace OffStreamXIV.Tests;

public class AddonListenersTests
{
    // Imitation d'un écouteur de Dalamud (AddonLifecycleEventListener) : seuls les noms des propriétés comptent.
    private sealed class FakeListener(IAddonLifecycle.AddonEventDelegate handler, bool clearing = false)
    {
        public IAddonLifecycle.AddonEventDelegate FunctionDelegate { get; } = handler;

        public bool IsRequestedToClear { get; } = clearing;
    }

    // Une méthode par « plugin » : le test rattache chaque écouteur au plugin d'après le nom de sa méthode.
    private static void CharacterPanelRefined(AddonEvent type, Dalamud.Game.Addon.Lifecycle.AddonArgTypes.AddonArgs args)
    {
    }

    private static void SimpleTweaks(AddonEvent type, Dalamud.Game.Addon.Lifecycle.AddonArgTypes.AddonArgs args)
    {
    }

    private static string? OwnerOf(Delegate call) => call.Method.Name;

    [Fact]
    public void ReadsDalamudListenersByPlugin()
    {
        // Même forme que la liste interne de Dalamud : événement → nom de la fenêtre du jeu → écouteurs.
        var listeners = new Dictionary<AddonEvent, Dictionary<string, HashSet<FakeListener>>>
        {
            [AddonEvent.PostSetup] = new()
            {
                ["CharacterStatus"] = [new FakeListener(CharacterPanelRefined), new FakeListener(SimpleTweaks)],
                [string.Empty] = [new FakeListener(SimpleTweaks)],
            },
            [AddonEvent.PreRequestedUpdate] = new()
            {
                ["ItemDetail"] = [new FakeListener(CharacterPanelRefined)],
                ["Inventory"] = [new FakeListener(SimpleTweaks, clearing: true)],
            },
        };

        var result = AddonListeners.Parse(listeners, OwnerOf);

        Assert.Equal(["CharacterStatus", "ItemDetail"], result[nameof(CharacterPanelRefined)]);
        // L'écouteur de toutes les fenêtres (nom vide) et celui en cours de retrait ne comptent pas.
        Assert.Equal(["CharacterStatus"], result[nameof(SimpleTweaks)]);
    }
}
