using System.Collections.Generic;

namespace OffStreamXIV;

/// <summary>
/// Description lisible des fenêtres du jeu les plus courantes. Le jeu ne les nomme qu'en interne (« CharacterStatus ») :
/// les autres gardent ce nom interne, qui reste affiché au survol.
/// </summary>
internal static class GameWindowNames
{
    private static readonly Dictionary<string, (string English, string French)> Names = new()
    {
        ["Character"] = ("Character", "Personnage"),
        ["CharacterStatus"] = ("Character — attributes", "Personnage — attributs"),
        ["CharacterProfile"] = ("Character — profile", "Personnage — profil"),
        ["ItemDetail"] = ("Item tooltip", "Infobulle d'objet"),
        ["ActionDetail"] = ("Action tooltip", "Infobulle d'action"),
        ["Inventory"] = ("Inventory", "Inventaire"),
        ["InventoryExpansion"] = ("Inventory", "Inventaire"),
        ["InventoryLarge"] = ("Inventory", "Inventaire"),
        ["ArmouryBoard"] = ("Armoury chest", "Arsenal"),
        ["ChatLog"] = ("Chat", "Discussion"),
        ["_PartyList"] = ("Party list", "Liste de l'équipe"),
        ["_TargetInfo"] = ("Target", "Cible"),
        ["_NaviMap"] = ("Minimap", "Mini-carte"),
    };

    public static string Label(string addon) =>
        Names.TryGetValue(addon, out var name) ? Loc.T(name.English, name.French) : addon;
}
