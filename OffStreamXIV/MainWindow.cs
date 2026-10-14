using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace OffStreamXIV;

/// <summary>
/// Fenêtre principale : l'état des fenêtres séparées, puis les plugins avec leur fenêtre principale, leurs paramètres,
/// leurs autres fenêtres et leurs ajouts dans les fenêtres du jeu, à cacher ou non.
/// </summary>
internal sealed class MainWindow : Window
{
    public const string Key = "###OffStreamXIVMain";

    private readonly Plugin plugin;
    private string filter = string.Empty;

    // Plugins installés et fenêtres du jeu qu'ils modifient, relus toutes les deux secondes.
    private List<InstalledPlugin> installed = [];
    private Dictionary<string, SortedSet<string>> gameWindows = [];
    private long refreshTime;

    public MainWindow(Plugin plugin)
        : base("OffStreamXIV" + Key)
    {
        this.plugin = plugin;
        Size = new Vector2(640, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(400, 260), MaximumSize = new Vector2(float.MaxValue) };
    }

    private Configuration Config => plugin.Configuration;

    public override void Draw()
    {
        DrawStatus();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##Filter", Loc.T("Search a plugin or a window…", "Chercher un plugin ou une fenêtre…"), ref filter, 100);

        // La liste prend la place restante, au-dessus du choix de la langue.
        var footer = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;
        DrawList(-footer);
        ImGui.Separator();
        DrawLanguage();
    }

    private void DrawStatus()
    {
        var hider = plugin.Hider;
        if (hider.ViewportsEnabled)
        {
            ImGui.TextColored(ImGuiColors.HealerGreen, Loc.T(
                "Separate windows are on: checked windows are hidden from OBS.",
                "Fenêtres séparées actives : les fenêtres cochées sont cachées à OBS."));
        }
        else
        {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudOrange))
            {
                ImGui.TextWrapped(Loc.T(
                    "Dalamud doesn't allow separate windows right now, so nothing can be hidden. Turn on \"Enable multi-monitor windows\" in Dalamud's settings (Look & Feel). It also needs the game in windowed or borderless window mode, and more than one monitor.",
                    "Dalamud n'autorise pas les fenêtres séparées en ce moment : rien ne peut être caché. Active « Enable multi-monitor windows » (fenêtres multi-écrans) dans les réglages de Dalamud (Look & Feel). Il faut aussi le jeu en mode fenêtré ou fenêtré sans bordure, et plus d'un écran."));
            }

            if (ImGui.Button(Loc.T("Open Dalamud's settings", "Ouvrir les réglages de Dalamud")))
                Plugin.PluginInterface.OpenDalamudSettingsTo(SettingsOpenKind.LookAndFeel, "multi-monitor");
        }

        if (hider.LastError != 0)
        {
            ImGui.TextColored(ImGuiColors.DalamudRed, Loc.T(
                $"Windows refused to hide a window from captures (error {hider.LastError}).",
                $"Windows a refusé de retirer une fenêtre des captures (erreur {hider.LastError})."));
        }
    }

    private void DrawList(float height)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY;
        if (!ImGui.BeginTable("##Plugins", 3, flags, new Vector2(0, height)))
            return;

        ImGui.TableSetupColumn(Loc.T("Hidden from OBS", "Caché à OBS"), ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn(Loc.T("Separate", "Séparée"), ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn(Loc.T("Out of captures", "Hors captures"), ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableHeadersRow();

        Refresh();
        var groups = PluginList.Build(installed, plugin.Hider.Known, Config, gameWindows, filter.Trim());
        foreach (var group in groups)
            DrawPlugin(group);

        ImGui.EndTable();
    }

    private void DrawPlugin(PluginGroup group)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        using var id = ImRaii.PushId(group.InternalName);

        // Case « à moitié cochée » quand une partie du plugin est réglée autrement que lui.
        var hidden = Hiding.IsPluginHidden(Config, group.InternalName);
        var mixed = group.AllWindows.Any(w => Hiding.IsHidden(Config, w.Key, group.InternalName) != hidden)
                    || group.GameWindows.Any(g => plugin.Additions.IsHidden(g.Addon) != hidden);
        if (Checkbox("##Hide", ref hidden, mixed))
            plugin.SetPluginHidden(group.InternalName, hidden, group.GameWindows.Select(g => g.Addon));
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Loc.T(
                "Hide everything this plugin shows: its windows (even the ones not opened yet) and what it adds in game windows.",
                "Cacher tout ce que ce plugin affiche : ses fenêtres (même celles pas encore ouvertes) et ses ajouts dans les fenêtres du jeu."));
        }

        ImGui.SameLine();
        var name = group.InternalName == PluginOwner.Unknown ? Loc.T("Unknown plugin", "Plugin inconnu") : group.Name;
        var openCount = group.AllWindows.Count(w => w.Open);
        var label = openCount > 0 ? $"{name} ({openCount})" : name;
        bool expanded;
        using (ImRaii.PushColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled), !group.Loaded))
        {
            var treeFlags = ImGuiTreeNodeFlags.SpanFullWidth | (group.IsEmpty ? ImGuiTreeNodeFlags.Leaf : ImGuiTreeNodeFlags.None);
            expanded = ImGui.TreeNodeEx($"{label}###Tree", treeFlags);
        }

        if (!group.Loaded && ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Plugin not loaded.", "Plugin non chargé."));
        if (!expanded)
            return;

        if (group.SameMainAndSettings)
        {
            DrawRole(group, WindowRole.Main, group.Main, Loc.T("Main window and settings (same window)", "Fenêtre principale et paramètres (même fenêtre)"));
        }
        else
        {
            if (group.ShowsMain)
                DrawRole(group, WindowRole.Main, group.Main, Loc.T("Main window", "Fenêtre principale"));
            if (group.ShowsSettings)
                DrawRole(group, WindowRole.Settings, group.Settings, Loc.T("Settings", "Paramètres"));
        }

        foreach (var window in group.Windows)
            DrawWindow(group.InternalName, window);
        foreach (var gameWindow in group.GameWindows)
            DrawGameWindow(gameWindow);
        ImGui.TreePop();
    }

    /// <summary>Fenêtre principale ou paramètres : une case pour toutes ses fenêtres, et de quoi la repérer.</summary>
    private void DrawRole(PluginGroup group, WindowRole role, List<WindowRow> windows, string title)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        using var id = ImRaii.PushId(role.ToString());
        var pluginName = group.InternalName;

        var hiddenCount = windows.Count(w => Hiding.IsHidden(Config, w.Key, pluginName));
        var hidden = windows.Count > 0 ? hiddenCount == windows.Count : Hiding.IsPluginHidden(Config, pluginName);
        using (ImRaii.Disabled(windows.Count == 0))
        {
            if (Checkbox("##Hide", ref hidden, hiddenCount > 0 && hiddenCount < windows.Count))
                plugin.SetWindowsHidden(pluginName, windows, hidden);
        }

        if (windows.Count == 0 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(Loc.T("Find this window first.", "Repère d'abord cette fenêtre."));

        ImGui.SameLine();
        var open = windows.Any(w => w.Open);
        var text = windows.Count > 0 ? $"{title} — {string.Join(", ", windows.Select(w => $"« {w.Label} »"))}" : title;
        if (open)
            ImGui.TextUnformatted(text);
        else
            ImGui.TextDisabled(text);
        if (windows.Count > 0 && ImGui.IsItemHovered())
            ImGui.SetTooltip(string.Join("\n", windows.Select(w => w.Key)));

        DrawFindButton(group, role, windows.Count > 0);

        if (!open)
            return;
        var openWindows = windows.Where(w => w.Open).ToList();
        ImGui.TableNextColumn();
        DrawCheck(openWindows.All(w => w.Separate), hidden);
        ImGui.TableNextColumn();
        DrawCheck(openWindows.All(w => w.Excluded), hidden);
    }

    /// <summary>« Repérer » tant que la fenêtre n'est pas connue ; ensuite un petit bouton pour tout repérer à nouveau.</summary>
    private void DrawFindButton(PluginGroup group, WindowRole role, bool known)
    {
        var roles = plugin.Roles;
        var pluginName = group.InternalName;
        var canOpen = group.Loaded && (role == WindowRole.Main ? group.HasMainUi : group.HasConfigUi);
        ImGui.SameLine();
        if (roles.IsSearching(pluginName, role))
        {
            ImGui.TextDisabled(Loc.T("finding…", "repérage…"));
        }
        else if (!known && canOpen)
        {
            if (ImGui.SmallButton(Loc.T("Find", "Repérer")))
                roles.Start(pluginName, role);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Loc.T(
                    "Opens this window through Dalamud and remembers which one it is.",
                    "Ouvre cette fenêtre via Dalamud et retient laquelle c'est."));
            }
        }
        else if (known)
        {
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.SmallButton(FontAwesomeIcon.Redo.ToIconString()))
                    roles.Forget(pluginName);
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Loc.T(
                    "Forget which windows are the main window and the settings of this plugin, to find them again.",
                    "Oublier quelles fenêtres sont la fenêtre principale et les paramètres de ce plugin, pour les repérer à nouveau."));
            }
        }

        if (roles.Failure(pluginName, role) is { } failure)
            ImGui.TextColored(ImGuiColors.DalamudOrange, failure);
    }

    private void DrawWindow(string pluginName, WindowRow window)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        using var id = ImRaii.PushId(window.Key);

        var hidden = Hiding.IsHidden(Config, window.Key, pluginName);
        if (ImGui.Checkbox("##Hide", ref hidden))
            plugin.SetWindowsHidden(pluginName, [window], hidden);

        ImGui.SameLine();
        if (window.Open)
            ImGui.TextUnformatted(window.Label);
        else
            ImGui.TextDisabled($"{window.Label} ({Loc.T("closed", "fermée")})");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(window.Key);

        if (!window.Open)
            return;

        ImGui.TableNextColumn();
        DrawCheck(window.Separate, hidden);
        if (hidden && !window.Separate && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Loc.T(
                "This window is still drawn in the game image: OBS can see it.\nEither separate windows are off, or the plugin keeps this window inside the game image.",
                "Cette fenêtre est encore dessinée dans l'image du jeu : OBS la voit.\nSoit les fenêtres séparées sont désactivées, soit le plugin garde cette fenêtre dans l'image du jeu."));
        }

        ImGui.TableNextColumn();
        DrawCheck(window.Excluded, hidden);
    }

    /// <summary>Ajouts dans une fenêtre du jeu : une case, et dans la dernière colonne ce qui a été trouvé.</summary>
    private void DrawGameWindow(GameWindowRow row)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        using var id = ImRaii.PushId("Game." + row.Addon);

        var additions = plugin.Additions;
        var hidden = additions.IsHidden(row.Addon);
        if (ImGui.Checkbox("##Hide", ref hidden))
            plugin.SetGameWindowHidden(row.Addon, hidden);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Loc.T(
                "Hides from OBS the texts that plugins add in this game window. You keep seeing them: OffStreamXIV redraws them for you.\nOBS sees empty lines instead. Images and texts changed inside the game's own lines aren't hidden.",
                "Cache à OBS les textes que des plugins ajoutent dans cette fenêtre du jeu. Tu les vois toujours : OffStreamXIV les redessine pour toi.\nOBS voit des lignes vides à la place. Les images et les textes modifiés dans les lignes du jeu ne sont pas cachés."));
        }

        ImGui.SameLine();
        var name = GameWindowNames.Label(row.Addon);
        ImGui.TextUnformatted(Loc.T($"Additions in the game window « {name} »", $"Ajouts dans la fenêtre du jeu « {name} »"));
        if (ImGui.IsItemHovered())
        {
            var state = additions.State(row.Addon, withAbove: true);
            var above = state.Above.Length > 0 ? "\n" + Loc.T($"Covered by: {state.Above}", $"Recouverte par : {state.Above}") : string.Empty;
            ImGui.SetTooltip(row.Addon + above);
        }

        if (row.Others.Count > 0)
        {
            ImGui.SameLine();
            var others = string.Join(", ", row.Others);
            ImGui.TextDisabled(Loc.T($"(also: {others})", $"(aussi : {others})"));
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Loc.T(
                    "These plugins change the same game window: their additions are hidden together.",
                    "Ces plugins modifient la même fenêtre du jeu : leurs ajouts sont cachés ensemble."));
            }
        }

        ImGui.TableNextColumn();
        ImGui.TableNextColumn();
        DrawGameWindowState(additions.State(row.Addon), hidden);
    }

    private static void DrawGameWindowState(GameWindowState state, bool hidden)
    {
        if (!state.Open)
        {
            ImGui.TextDisabled(Loc.T("closed", "fermée"));
            return;
        }

        if (!state.Watched)
        {
            ImGui.TextColored(hidden ? ImGuiColors.DalamudOrange : ImGuiColors.DalamudGrey, Loc.T("reopen it", "à rouvrir"));
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Loc.T(
                    "This window was open before OffStreamXIV loaded: close and reopen it.",
                    "Cette fenêtre était ouverte avant le chargement d'OffStreamXIV : ferme-la puis rouvre-la."));
            }

            return;
        }

        var text = Loc.T($"{state.AddedTexts} texts", $"{state.AddedTexts} textes");
        if (hidden && state.AddedTexts > 0)
            ImGui.TextColored(ImGuiColors.HealerGreen, text);
        else
            ImGui.TextDisabled(text);
        if (state.AddedOthers > 0 && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Loc.T(
                $"Plugins also added {state.AddedOthers} other elements here (images…), which aren't hidden.",
                $"Les plugins ont aussi ajouté {state.AddedOthers} autres éléments ici (images…), qui ne sont pas cachés."));
        }
    }

    /// <summary>Case à cocher qui peut s'afficher « à moitié cochée » (même rendu que dans ImGui).</summary>
    private static bool Checkbox(string label, ref bool value, bool mixed)
    {
        if (!mixed)
            return ImGui.Checkbox(label, ref value);

        var context = ImGui.GetCurrentContext();
        var backup = context.CurrentItemFlags;
        context.CurrentItemFlags |= ImGuiItemFlags.MixedValue;
        var pressed = ImGui.Checkbox(label, ref value);
        context.CurrentItemFlags = backup;
        return pressed;
    }

    /// <summary>« oui » / « non » ; un « non » sur une fenêtre à cacher est un problème, en orange.</summary>
    private static void DrawCheck(bool value, bool expected)
    {
        if (value)
            ImGui.TextColored(ImGuiColors.HealerGreen, Loc.T("yes", "oui"));
        else if (expected)
            ImGui.TextColored(ImGuiColors.DalamudOrange, Loc.T("no", "non"));
        else
            ImGui.TextDisabled(Loc.T("no", "non"));
    }

    /// <summary>
    /// Plugins installés et fenêtres du jeu qu'ils modifient, relus toutes les deux secondes (Dalamud reconstruit ces
    /// listes à chaque demande).
    /// </summary>
    private void Refresh()
    {
        if (Environment.TickCount64 - refreshTime <= 2000)
            return;
        refreshTime = Environment.TickCount64;
        installed = Plugin.PluginInterface.InstalledPlugins
            .Select(p => new InstalledPlugin(p.InternalName, p.Name, p.IsLoaded, p.IsLoaded && p.HasMainUi, p.IsLoaded && p.HasConfigUi))
            .ToList();
        gameWindows = AddonListeners.ByPlugin();
    }

    private void DrawLanguage()
    {
        var current = Config.Language;
        ImGui.SetNextItemWidth(200 * ImGuiHelpers.GlobalScale);
        if (!ImGui.BeginCombo(Loc.T("Language", "Langue"), LanguageName(current)))
            return;

        foreach (var language in Enum.GetValues<PluginLanguage>())
        {
            if (ImGui.Selectable(LanguageName(language), language == current))
                plugin.SetLanguage(language);
        }

        ImGui.EndCombo();
    }

    private static string LanguageName(PluginLanguage language) => language switch
    {
        PluginLanguage.English => "English",
        PluginLanguage.French => "Français",
        _ => Loc.T("Automatic (Dalamud)", "Automatique (Dalamud)"),
    };
}
