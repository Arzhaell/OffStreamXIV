using System;
using Dalamud.Bindings.ImGui;

namespace OffStreamXIV;

/// <summary>Règles d'ImGui sur les fenêtres, réunies ici pour être testées sans le jeu.</summary>
internal static class WindowRules
{
    private const ImGuiWindowFlags Attached =
        ImGuiWindowFlags.ChildWindow | ImGuiWindowFlags.ChildMenu | ImGuiWindowFlags.Tooltip | ImGuiWindowFlags.Popup;

    /// <summary>
    /// Menus, infobulles, listes déroulantes et sous-fenêtres appartiennent à la fenêtre qui les a ouverts :
    /// ils sont cachés ou visibles avec elle.
    /// </summary>
    public static bool FollowsParent(ImGuiWindowFlags flags) => (flags & Attached) != 0;

    /// <summary>Fenêtre qu'on propose de cacher : une fenêtre « principale » d'un plugin.</summary>
    public static bool IsListed(ImGuiWindowFlags flags) => !FollowsParent(flags);

    /// <summary>
    /// Fenêtres qu'ImGui accepte de mettre dans leur propre fenêtre Windows quand on le lui demande
    /// (même règle que GetWindowAlwaysWantOwnViewport dans imgui.cpp) : pas les menus ni les infobulles.
    /// </summary>
    public static bool CanOwnViewport(ImGuiWindowFlags flags) =>
        (flags & (ImGuiWindowFlags.ChildWindow | ImGuiWindowFlags.ChildMenu | ImGuiWindowFlags.Tooltip)) == 0
        && ((flags & ImGuiWindowFlags.Popup) == 0 || (flags & ImGuiWindowFlags.Modal) != 0);

    /// <summary>
    /// Fenêtre dont dépend la décision de cacher : on remonte les menus et infobulles jusqu'à la fenêtre qui les a
    /// ouverts. <paramref name="parentOf"/> renvoie null au sommet (aucun parent, ou la fenêtre implicite d'ImGui).
    /// </summary>
    public static T Owner<T>(T window, Func<T, ImGuiWindowFlags> flagsOf, Func<T, T?> parentOf)
        where T : struct
    {
        // Garde-fou contre une boucle : ImGui n'en fait pas, mais une erreur ici bloquerait le jeu.
        for (var depth = 0; depth < 32 && FollowsParent(flagsOf(window)); depth++)
        {
            if (parentOf(window) is not { } parent)
                break;
            window = parent;
        }

        return window;
    }
}
