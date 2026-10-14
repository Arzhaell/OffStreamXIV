namespace OffStreamXIV;

/// <summary>
/// Nom d'une fenêtre ImGui. ImGui identifie une fenêtre par le texte à partir du dernier « ### » s'il y en a un
/// (le titre devant peut changer, par exemple « Réglages 12%###config »), sinon par son nom entier.
/// </summary>
internal static class WindowKey
{
    /// <summary>Clé stable d'une fenêtre : ce qui détermine son identifiant dans ImGui.</summary>
    public static string From(string name)
    {
        var id = name.LastIndexOf("###", System.StringComparison.Ordinal);
        return id >= 0 ? name[id..] : name;
    }

    /// <summary>Titre affiché : ce qui précède le premier « ## » ; la clé si le titre est vide.</summary>
    public static string Label(string name)
    {
        var hidden = name.IndexOf("##", System.StringComparison.Ordinal);
        var label = hidden >= 0 ? name[..hidden] : name;
        return string.IsNullOrWhiteSpace(label) ? From(name) : label;
    }
}
