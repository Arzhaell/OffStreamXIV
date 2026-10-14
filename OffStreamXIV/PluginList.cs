using System;
using System.Collections.Generic;
using System.Linq;

namespace OffStreamXIV;

/// <summary>Un plugin installé, tel que Dalamud le décrit.</summary>
internal readonly record struct InstalledPlugin(string InternalName, string Name, bool Loaded, bool HasMainUi = false, bool HasConfigUi = false);

/// <summary>Une ligne de fenêtre dans la liste.</summary>
internal readonly record struct WindowRow(string Key, string Label, bool Open, bool Separate, bool Excluded);

/// <summary>Une fenêtre du jeu que le plugin modifie ; <c>Others</c> : les autres plugins qui la modifient aussi.</summary>
internal readonly record struct GameWindowRow(string Addon, IReadOnlyList<string> Others);

/// <summary>Un plugin et ce qu'il affiche, dans la liste.</summary>
internal sealed class PluginGroup(string internalName)
{
    public string InternalName { get; } = internalName;

    public string Name { get; set; } = internalName;

    public bool Loaded { get; set; }

    public bool HasMainUi { get; set; }

    public bool HasConfigUi { get; set; }

    /// <summary>Fenêtres repérées comme fenêtre principale (vide tant qu'elle n'est pas repérée).</summary>
    public List<WindowRow> Main { get; } = [];

    /// <summary>Fenêtres repérées comme paramètres.</summary>
    public List<WindowRow> Settings { get; } = [];

    /// <summary>Ses autres fenêtres.</summary>
    public List<WindowRow> Windows { get; } = [];

    public List<GameWindowRow> GameWindows { get; } = [];

    /// <summary>Le plugin ouvre la même fenêtre pour sa fenêtre principale et ses paramètres : une seule ligne.</summary>
    public bool SameMainAndSettings =>
        Main.Count > 0 && Main.Select(w => w.Key).ToHashSet().SetEquals(Settings.Select(w => w.Key));

    public bool ShowsMain => HasMainUi || Main.Count > 0;

    public bool ShowsSettings => HasConfigUi || Settings.Count > 0;

    public IEnumerable<WindowRow> AllWindows => Main.Concat(Settings).Concat(Windows).DistinctBy(w => w.Key);

    public bool IsEmpty => !ShowsMain && !ShowsSettings && Windows.Count == 0 && GameWindows.Count == 0;
}

/// <summary>
/// Liste de la fenêtre principale : Dalamud puis les plugins chargés, chacun avec sa fenêtre principale, ses paramètres,
/// ses autres fenêtres (vues depuis le lancement, ou réglées à part même fermées) et les fenêtres du jeu qu'il modifie.
/// Un plugin déchargé reste visible s'il a un réglage.
/// </summary>
internal static class PluginList
{
    public static List<PluginGroup> Build(
        IEnumerable<InstalledPlugin> installed,
        IEnumerable<KnownWindow> windows,
        Configuration configuration,
        IReadOnlyDictionary<string, SortedSet<string>> gameWindows,
        string filter)
    {
        var groups = new Dictionary<string, PluginGroup>();
        PluginGroup Group(string internalName)
        {
            if (!groups.TryGetValue(internalName, out var group))
                group = groups[internalName] = new PluginGroup(internalName);
            return group;
        }

        Group(PluginOwner.Dalamud).Loaded = true;
        foreach (var plugin in installed.Where(p => p.Loaded))
        {
            var group = Group(plugin.InternalName);
            group.Name = plugin.Name;
            group.Loaded = true;
            group.HasMainUi = plugin.HasMainUi;
            group.HasConfigUi = plugin.HasConfigUi;
        }

        // Toutes les fenêtres connues, par plugin : vues depuis le lancement, puis réglées à part ou repérées mais fermées.
        var rows = new Dictionary<string, (string Plugin, WindowRow Row)>();
        foreach (var window in windows.Where(w => w.Listed && !Hiding.IsAlwaysHidden(w.Key)))
            rows.TryAdd(window.Key, (window.Plugin, new WindowRow(window.Key, window.Label, window.Open, window.Separate, window.Excluded)));
        foreach (var (key, setting) in configuration.Windows)
            rows.TryAdd(key, (setting.Plugin, new WindowRow(key, setting.Label, false, false, false)));
        foreach (var (plugin, roles) in configuration.Roles)
        {
            foreach (var (key, label) in roles.Main.Concat(roles.Settings))
                rows.TryAdd(key, (plugin, new WindowRow(key, label, false, false, false)));
        }

        foreach (var (key, (plugin, row)) in rows)
        {
            var group = Group(plugin);
            var roles = configuration.Roles.GetValueOrDefault(plugin);
            var isMain = roles?.Main.ContainsKey(key) == true;
            var isSettings = roles?.Settings.ContainsKey(key) == true;
            if (isMain)
                group.Main.Add(row);
            if (isSettings)
                group.Settings.Add(row);
            if (!isMain && !isSettings)
                group.Windows.Add(row);
        }

        foreach (var (plugin, addons) in gameWindows)
        {
            foreach (var addon in addons)
            {
                var others = gameWindows.Where(g => g.Key != plugin && g.Value.Contains(addon)).Select(g => g.Key).Order().ToList();
                Group(plugin).GameWindows.Add(new GameWindowRow(addon, others));
            }
        }

        // Fenêtre du jeu cochée qu'aucun plugin chargé ne modifie plus : listée quand même, pour pouvoir la décocher.
        foreach (var addon in configuration.HiddenGameWindows.Where(a => !gameWindows.Values.Any(set => set.Contains(a))))
            Group(PluginOwner.Unknown).GameWindows.Add(new GameWindowRow(addon, []));

        foreach (var plugin in configuration.HiddenPlugins)
            Group(plugin);

        // Noms affichés des autres plugins d'une même fenêtre du jeu.
        foreach (var group in groups.Values)
        {
            for (var i = 0; i < group.GameWindows.Count; i++)
            {
                var row = group.GameWindows[i];
                group.GameWindows[i] = row with { Others = row.Others.Select(o => groups.TryGetValue(o, out var g) ? g.Name : o).ToList() };
            }
        }

        var comparer = StringComparer.CurrentCultureIgnoreCase;
        var result = new List<PluginGroup>();
        foreach (var group in groups.Values.OrderBy(g => g.InternalName != PluginOwner.Dalamud).ThenBy(g => g.Name, comparer))
        {
            // Filtre : un plugin trouvé par son nom garde tout, sinon seulement ce qui correspond.
            if (filter.Length > 0 && !Matches(group.Name, filter) && !Matches(group.InternalName, filter))
            {
                var mainMatches = group.Main.Any(w => Matches(w.Label, filter));
                var settingsMatches = group.Settings.Any(w => Matches(w.Label, filter));
                if (!mainMatches)
                {
                    group.Main.Clear();
                    group.HasMainUi = false;
                }

                if (!settingsMatches)
                {
                    group.Settings.Clear();
                    group.HasConfigUi = false;
                }

                group.Windows.RemoveAll(w => !Matches(w.Label, filter));
                group.GameWindows.RemoveAll(w => !Matches(w.Addon, filter) && !Matches(GameWindowNames.Label(w.Addon), filter));
                if (group.IsEmpty)
                    continue;
            }

            group.Windows.Sort((a, b) => a.Open != b.Open ? b.Open.CompareTo(a.Open) : comparer.Compare(a.Label, b.Label));
            group.GameWindows.Sort((a, b) => comparer.Compare(GameWindowNames.Label(a.Addon), GameWindowNames.Label(b.Addon)));
            result.Add(group);
        }

        return result;
    }

    private static bool Matches(string text, string filter) => text.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
}
