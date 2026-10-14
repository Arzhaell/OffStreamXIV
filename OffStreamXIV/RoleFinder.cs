using System;
using System.Collections.Generic;
using System.Linq;

namespace OffStreamXIV;

/// <summary>Ce qu'une fenêtre est pour son plugin.</summary>
public enum WindowRole
{
    /// <summary>Ouverte par le bouton « fenêtre principale » de Dalamud.</summary>
    Main,

    /// <summary>Ouverte par le bouton « paramètres » de Dalamud.</summary>
    Settings,
}

/// <summary>
/// Repère quelle fenêtre un plugin ouvre pour sa fenêtre principale ou ses paramètres : on lui demande de l'ouvrir via
/// Dalamud (comme les boutons de l'installateur de plugins) et on regarde quelle fenêtre apparaît.
/// </summary>
internal sealed class RoleFinder(Configuration configuration, WindowHider hider)
{
    // Une seconde environ pour que la fenêtre apparaisse.
    private const int MaxFrames = 90;

    private readonly Dictionary<(string Plugin, WindowRole Role), string> failures = [];
    private Search? pending;

    public bool IsSearching(string plugin, WindowRole role) => pending is { } p && p.Plugin == plugin && p.Role == role;

    /// <summary>Message du dernier repérage raté, ou null.</summary>
    public string? Failure(string plugin, WindowRole role) => failures.GetValueOrDefault((plugin, role));

    /// <summary>Oublie ce qui a été repéré pour ce plugin (pour le repérer à nouveau).</summary>
    public void Forget(string plugin)
    {
        if (configuration.Roles.Remove(plugin))
            configuration.Save();
    }

    public void Start(string plugin, WindowRole role)
    {
        failures.Remove((plugin, role));
        pending = new Search(plugin, role, OpenWindows(plugin));
        if (!Open(plugin, role))
        {
            failures[(plugin, role)] = Loc.T("This plugin couldn't open this window.", "Ce plugin n'a pas pu ouvrir cette fenêtre.");
            pending = null;
        }
    }

    /// <summary>À appeler à chaque image, après la mise à jour des fenêtres.</summary>
    public void Update()
    {
        if (pending is not { } search)
            return;

        var (keys, closed) = RoleDetection.Detect(search.Before, OpenWindows(search.Plugin));
        if (keys.Count > 0)
        {
            if (!configuration.Roles.TryGetValue(search.Plugin, out var roles))
                roles = configuration.Roles[search.Plugin] = new PluginRoles();
            var windows = roles.Of(search.Role);
            windows.Clear();
            foreach (var key in keys)
                windows[key] = hider.Known.FirstOrDefault(w => w.Key == key)?.Label ?? key;
            configuration.Save();
            pending = null;

            // La fenêtre était déjà ouverte et la demande l'a fermée : on la rouvre.
            if (closed)
                Open(search.Plugin, search.Role);
            return;
        }

        if (++search.Frames >= MaxFrames)
        {
            failures[(search.Plugin, search.Role)] = Loc.T(
                "Nothing changed: close this plugin's windows, then try again.",
                "Rien n'a changé : ferme les fenêtres de ce plugin, puis réessaie.");
            pending = null;
        }
    }

    private static bool Open(string plugin, WindowRole role)
    {
        try
        {
            var exposed = Plugin.PluginInterface.InstalledPlugins.FirstOrDefault(p => p.InternalName == plugin && p.IsLoaded);
            if (exposed == null || (role == WindowRole.Main ? !exposed.HasMainUi : !exposed.HasConfigUi))
                return false;
            if (role == WindowRole.Main)
                exposed.OpenMainUi();
            else
                exposed.OpenConfigUi();
            return true;
        }
        catch (Exception e)
        {
            Plugin.Log.Warning(e, $"Could not open a window of {plugin}.");
            return false;
        }
    }

    private HashSet<string> OpenWindows(string plugin) =>
        hider.Known.Where(w => w.Plugin == plugin && w.Listed && w.Open && !Hiding.IsAlwaysHidden(w.Key)).Select(w => w.Key).ToHashSet();

    private sealed class Search(string plugin, WindowRole role, HashSet<string> before)
    {
        public string Plugin { get; } = plugin;

        public WindowRole Role { get; } = role;

        public HashSet<string> Before { get; } = before;

        public int Frames { get; set; }
    }
}

/// <summary>Déduction du repérage, réunie ici pour être testée sans le jeu.</summary>
internal static class RoleDetection
{
    /// <summary>
    /// Fenêtres apparues depuis la demande ; à défaut, fenêtres disparues (le plugin a refermé une fenêtre déjà ouverte :
    /// c'est elle). <c>Closed</c> dit lequel des deux.
    /// </summary>
    public static (IReadOnlyCollection<string> Keys, bool Closed) Detect(IReadOnlySet<string> before, IReadOnlySet<string> now)
    {
        var opened = now.Where(k => !before.Contains(k)).ToList();
        if (opened.Count > 0)
            return (opened, false);
        var closed = before.Where(k => !now.Contains(k)).ToList();
        return (closed, closed.Count > 0);
    }
}
