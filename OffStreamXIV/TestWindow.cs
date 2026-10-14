using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace OffStreamXIV;

/// <summary>
/// Fenêtre d'essai, toujours cachée à OBS : de quoi vérifier sa source OBS, avec des menus, infobulles, listes, la saisie
/// au clavier et un fond transparent.
/// </summary>
internal sealed class TestWindow : Window
{
    public const string Key = Hiding.AlwaysHiddenPrefix + "Test";

    private static readonly string[] Fruits = ["Apple / Pomme", "Pear / Poire", "Cherry / Cerise", "Plum / Prune", "Fig / Figue"];

    private bool transparent;
    private int fruit;
    private string text = string.Empty;

    public TestWindow()
        : base("OffStreamXIV test" + Key)
    {
        Size = new Vector2(420, 330);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw() => Flags = transparent ? ImGuiWindowFlags.NoBackground : ImGuiWindowFlags.None;

    public override void Draw()
    {
        ImGui.TextWrapped(Loc.T(
            "This window is always hidden from OBS: you see it, OBS doesn't. Check it in your OBS sources, with the menus below too.",
            "Cette fenêtre est toujours cachée à OBS : tu la vois, OBS non. Vérifie-le dans tes sources OBS, avec les menus ci-dessous aussi."));
        ImGui.Checkbox(Loc.T("Transparent background", "Fond transparent"), ref transparent);
        ImGui.Separator();

        if (ImGui.BeginCombo(Loc.T("Drop-down list", "Liste déroulante"), Fruits[fruit]))
        {
            for (var i = 0; i < Fruits.Length; i++)
            {
                if (ImGui.Selectable(Fruits[i], i == fruit))
                    fruit = i;
            }

            ImGui.EndCombo();
        }

        ImGui.InputText(Loc.T("Typing", "Saisie"), ref text, 100);

        ImGui.Button(Loc.T("Hover me", "Survole-moi"));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("A tooltip: hidden from OBS too?", "Une infobulle : cachée à OBS aussi ?"));

        ImGui.SameLine();
        if (ImGui.Button(Loc.T("Open a modal window", "Ouvrir une fenêtre modale")))
            ImGui.OpenPopup("##Modal");

        ImGui.TextDisabled(Loc.T("Right-click here for a menu", "Clic droit ici pour un menu"));
        if (ImGui.BeginPopupContextItem("##Menu"))
        {
            ImGui.MenuItem(Loc.T("First choice", "Premier choix"));
            ImGui.MenuItem(Loc.T("Second choice", "Deuxième choix"));
            ImGui.EndPopup();
        }

        if (ImGui.BeginPopupModal("##Modal", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted(Loc.T("A modal window: hidden from OBS too?", "Une fenêtre modale : cachée à OBS aussi ?"));
            if (ImGui.Button("OK"))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }
}
