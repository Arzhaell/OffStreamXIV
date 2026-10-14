using System.Linq;

namespace OffStreamXIV;

/// <summary>
/// Ce qui est caché à OBS : chaque fenêtre suit son plugin, sauf si elle a son propre réglage. Changer un plugin efface
/// les réglages propres de ses fenêtres (elles le suivent toutes).
/// </summary>
internal static class Hiding
{
    /// <summary>Début de la clé des fenêtres d'OffStreamXIV toujours cachées (fenêtre d'essai, textes redessinés) : jamais listées.</summary>
    public const string AlwaysHiddenPrefix = "###OffStreamXIVAlways.";

    public static bool IsAlwaysHidden(string key) => key.StartsWith(AlwaysHiddenPrefix, System.StringComparison.Ordinal);

    public static bool IsHidden(Configuration configuration, string key, string plugin) =>
        IsAlwaysHidden(key)
        || (configuration.Windows.TryGetValue(key, out var window) ? window.Hidden : configuration.HiddenPlugins.Contains(plugin));

    public static bool IsPluginHidden(Configuration configuration, string plugin) => configuration.HiddenPlugins.Contains(plugin);

    public static void SetPlugin(Configuration configuration, string plugin, bool hidden)
    {
        if (hidden)
            configuration.HiddenPlugins.Add(plugin);
        else
            configuration.HiddenPlugins.Remove(plugin);

        foreach (var key in configuration.Windows.Where(w => w.Value.Plugin == plugin).Select(w => w.Key).ToList())
            configuration.Windows.Remove(key);
    }

    /// <summary>Une fenêtre réglée comme son plugin n'a pas de réglage propre : elle suivra les changements du plugin.</summary>
    public static void SetWindow(Configuration configuration, string key, string plugin, string label, bool hidden)
    {
        if (hidden == IsPluginHidden(configuration, plugin))
            configuration.Windows.Remove(key);
        else
            configuration.Windows[key] = new WindowSetting { Hidden = hidden, Plugin = plugin, Label = label };
    }
}
