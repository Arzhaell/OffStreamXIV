using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Hooking;

namespace OffStreamXIV;

/// <summary>Une fenêtre vue depuis le chargement du plugin.</summary>
internal sealed class KnownWindow(string key, string plugin, bool listed, string label)
{
    public string Key { get; } = key;

    /// <summary>Nom interne du plugin qui l'a ouverte (<see cref="PluginOwner"/>).</summary>
    public string Plugin { get; set; } = plugin;

    /// <summary>Fenêtre « principale », proposée dans la liste (pas un menu ni une infobulle).</summary>
    public bool Listed { get; } = listed;

    public string Label { get; set; } = label;

    public bool Open { get; set; }

    /// <summary>Dessinée dans sa propre fenêtre Windows, donc hors de l'image du jeu.</summary>
    public bool Separate { get; set; }

    /// <summary>Retirée des captures par Windows.</summary>
    public bool Excluded { get; set; }
}

/// <summary>
/// Cache des fenêtres à OBS. Une fenêtre cachée sort de l'image du jeu : ImGui la dessine dans sa propre fenêtre Windows
/// transparente, posée au même endroit (comme une fenêtre déplacée sur un autre écran), et Windows retire cette
/// fenêtre de toutes les captures. On ne touche pas au rendu du jeu.
/// </summary>
internal sealed unsafe partial class WindowHider : IDisposable
{
    private readonly Configuration configuration;

    // Interception de l'ouverture des fenêtres (igBegin de cimgui, par où passent tous les plugins) : elle sert à
    // retrouver le plugin de chaque fenêtre, et à sortir une fenêtre cachée de l'image du jeu dès sa première image.
    private readonly Hook<BeginDelegate>? beginHook;

    // Fenêtres vues, par identifiant ImGui.
    private readonly Dictionary<uint, KnownWindow> known = [];

    // Fenêtres à qui on a demandé leur propre fenêtre Windows, par identifiant ImGui : on retire la demande quand
    // elles redeviennent visibles, sans toucher à celles qu'un plugin a demandées lui-même.
    private readonly HashSet<uint> forced = [];

    // Fenêtres Windows retirées des captures.
    private readonly HashSet<nint> excluded = [];

    private bool beginFailed;

    // Fenêtres des systèmes de fenêtres branchés directement sur le dessin des plugins (voir WindowSystemOwners).
    private Dictionary<string, string> windowSystems = [];
    private long windowSystemsTime;

    public WindowHider(Configuration configuration)
    {
        this.configuration = configuration;
        try
        {
            var cimgui = GetModuleHandleW("cimgui.dll");
            var begin = NativeLibrary.GetExport(cimgui, "igBegin");
            beginHook = Plugin.GameInterop.HookFromAddress<BeginDelegate>(begin, BeginDetour);
            beginHook.Enable();
        }
        catch (Exception e)
        {
            // Sans l'interception, les fenêtres se cachent quand même, mais sans leur plugin et une image plus tard.
            Plugin.Log.Error(e, "Could not watch ImGui windows opening; plugin names won't be available.");
        }
    }

    private delegate byte BeginDelegate(byte* name, byte* open, ImGuiWindowFlags flags);

    /// <summary>Fenêtres vues depuis le chargement du plugin.</summary>
    public IEnumerable<KnownWindow> Known => known.Values;

    /// <summary>
    /// Faux quand Dalamud n'autorise pas les fenêtres séparées : réglage « fenêtres multi-écrans » désactivé, jeu en
    /// plein écran exclusif, ou un seul écran.
    /// </summary>
    public bool ViewportsEnabled { get; private set; }

    /// <summary>Dernier code d'erreur de Windows en retirant une fenêtre des captures (0 si aucune).</summary>
    public int LastError { get; private set; }

    /// <summary>Vrai si la fenêtre ImGui de ce nom est dessinée à part et déjà retirée des captures.</summary>
    public bool IsExcluded(string name) => known.TryGetValue(ImGuiP.ImHashStr(name), out var window) && window.Excluded;

    /// <summary>À appeler à chaque image, pendant le dessin de l'interface.</summary>
    /// <param name="refreshLabels">Relire les titres des fenêtres (seulement quand la liste est affichée).</param>
    public void Update(bool refreshLabels)
    {
        ViewportsEnabled = (ImGui.GetIO().ConfigFlags & ImGuiConfigFlags.ViewportsEnable) != 0;
        var context = ImGui.GetCurrentContext();
        if (context.Handle == null)
            return;

        var mainViewportId = ImGui.GetMainViewport().ID;
        var all = context.Windows;
        for (var i = 0; i < all.Size; i++)
        {
            var window = all[i];
            if (window.Handle == null || window.IsFallbackWindow)
                continue;

            var flags = window.Flags;
            var hidden = IsHidden(window);
            if (WindowRules.CanOwnViewport(flags))
                RequestOwnViewport(window, hidden);

            var open = window.Active || window.WasActive;
            var info = known.GetValueOrDefault(window.ID);
            if (info == null && open && WindowRules.IsListed(flags))
            {
                // Fenêtre ouverte sans passer par l'interception (elle a échoué, ou la fenêtre précède le plugin).
                var name = Name(window);
                info = known[window.ID] = new KnownWindow(WindowKey.From(name), PluginOwner.Unknown, true, WindowKey.Label(name));
            }

            var hwnd = open ? SeparateHandle(window, mainViewportId) : 0;
            var isExcluded = false;
            if (hwnd != 0)
            {
                if (hidden)
                    isExcluded = Exclude(hwnd);
                else if (excluded.Remove(hwnd))
                    CaptureExclusion.Restore(hwnd);
            }

            if (info == null)
                continue;
            info.Open = open;
            info.Separate = hwnd != 0;
            info.Excluded = isExcluded;
            if (open && refreshLabels && info.Listed)
                info.Label = WindowKey.Label(Name(window));
        }

        // Oublie les fenêtres Windows qu'ImGui a détruites (fenêtre fermée ou revenue dans l'image du jeu).
        if (ImGui.GetFrameCount() % 120 == 0)
            excluded.RemoveWhere(hwnd => !CaptureExclusion.IsAlive(hwnd));
    }

    /// <summary>Remet tout comme avant : fenêtres de nouveau dans l'image du jeu et visibles dans les captures.</summary>
    public void Dispose()
    {
        beginHook?.Dispose();

        var context = ImGui.GetCurrentContext();
        if (context.Handle != null && forced.Count > 0)
        {
            // On ne réécrit que les fenêtres encore présentes dans ImGui.
            var all = context.Windows;
            for (var i = 0; i < all.Size; i++)
            {
                var window = all[i];
                if (forced.Contains(window.ID))
                    window.WindowClass.ViewportFlagsOverrideSet &= ~ImGuiViewportFlags.NoAutoMerge;
            }
        }

        forced.Clear();
        foreach (var hwnd in excluded)
            CaptureExclusion.Restore(hwnd);
        excluded.Clear();
    }

    private byte BeginDetour(byte* name, byte* open, ImGuiWindowFlags flags)
    {
        // Une erreur ne doit jamais remonter dans ImGui (le jeu planterait) : on la note une fois et on laisse passer.
        try
        {
            BeforeBegin(name, flags);
        }
        catch (Exception e)
        {
            if (!beginFailed)
            {
                beginFailed = true;
                Plugin.Log.Error(e, "Error while a window was opening.");
            }
        }

        return beginHook!.Original(name, open, flags);
    }

    private void BeforeBegin(byte* name, ImGuiWindowFlags flags)
    {
        if (name == null)
            return;

        // Même identifiant que celui qu'ImGui va donner à la fenêtre. Une fenêtre vue avant l'interception
        // (plugin inconnu) retrouve ici son plugin.
        var id = ImGuiPNative.ImHashStr(name, 0, 0);
        if (!known.TryGetValue(id, out var info) || info.Plugin == PluginOwner.Unknown)
        {
            var text = Marshal.PtrToStringUTF8((nint)name) ?? string.Empty;
            var key = WindowKey.From(text);
            info = known[id] = new KnownWindow(key, PluginOwner.FromStack(), WindowRules.IsListed(flags), WindowKey.Label(text));
        }

        // Rien du plugin dans la pile : la fenêtre est peut-être dans un système de fenêtres branché tel quel.
        if (info.Plugin == PluginOwner.Dalamud && WindowSystemOwner(info.Key) is { } owner)
        {
            info.Plugin = owner;
            if (configuration.Windows.TryGetValue(info.Key, out var setting))
                setting.Plugin = owner;
            Plugin.Log.Information($"Window {info.Key} belongs to {owner} (window system).");
        }

        if (!WindowRules.CanOwnViewport(flags))
            return;

        var hidden = Hiding.IsHidden(configuration, info.Key, info.Plugin);
        var window = ImGuiP.FindWindowByID(id);
        if (window.Handle != null)
            RequestOwnViewport(window, hidden);
        if (!hidden)
            return;

        // Un réglage que le plugin fixe juste avant d'ouvrir sa fenêtre remplace le nôtre : on y ajoute la demande.
        // Pour une fenêtre pas encore créée, on la fait naître directement dans sa propre fenêtre Windows.
        ref var next = ref ImGui.GetCurrentContext().NextWindowData;
        if ((next.Flags & ImGuiNextWindowDataFlags.HasWindowClass) != 0)
        {
            if ((next.WindowClass.ViewportFlagsOverrideSet & ImGuiViewportFlags.NoAutoMerge) == 0)
            {
                next.WindowClass.ViewportFlagsOverrideSet |= ImGuiViewportFlags.NoAutoMerge;
                forced.Add(id);
            }
        }
        else if (window.Handle == null)
        {
            // Mêmes valeurs de départ que le constructeur d'ImGuiWindowClass.
            var windowClass = new ImGuiWindowClass
            {
                ParentViewportId = uint.MaxValue,
                DockingAllowUnclassed = 1,
                ViewportFlagsOverrideSet = ImGuiViewportFlags.NoAutoMerge,
            };
            ImGui.SetNextWindowClass(ref windowClass);
            forced.Add(id);
        }
    }

    /// <summary>
    /// Plugin dont un système de fenêtres contient cette fenêtre ; null pour les fenêtres de Dalamud lui-même. La liste est
    /// relue au plus toutes les deux secondes, quand une fenêtre n'y est pas.
    /// </summary>
    private string? WindowSystemOwner(string key)
    {
        if (windowSystems.TryGetValue(key, out var plugin))
            return plugin;
        if (Environment.TickCount64 - windowSystemsTime < 2000)
            return null;
        windowSystemsTime = Environment.TickCount64;
        windowSystems = WindowSystemOwners.Read();
        return windowSystems.GetValueOrDefault(key);
    }

    private bool IsHidden(ImGuiWindowPtr window)
    {
        var owner = WindowRules.Owner(window, static w => w.Flags, ParentOf);
        return known.TryGetValue(owner.ID, out var info) && Hiding.IsHidden(configuration, info.Key, info.Plugin);
    }

    /// <summary>Fenêtre où un menu ou une infobulle a été ouvert ; null au sommet.</summary>
    private static ImGuiWindowPtr? ParentOf(ImGuiWindowPtr window)
    {
        var parent = window.ParentWindowInBeginStack;
        return parent.Handle == null || parent.IsFallbackWindow ? null : parent;
    }

    /// <summary>
    /// Demande à ImGui de dessiner la fenêtre dans sa propre fenêtre Windows, même posée sur le jeu. ImGui garde ce
    /// réglage d'une image à l'autre (il ne le remplace que si le plugin en fixe un lui-même avec SetNextWindowClass).
    /// </summary>
    private void RequestOwnViewport(ImGuiWindowPtr window, bool hidden)
    {
        ref var overrideSet = ref window.WindowClass.ViewportFlagsOverrideSet;
        if (hidden)
        {
            if ((overrideSet & ImGuiViewportFlags.NoAutoMerge) == 0)
            {
                overrideSet |= ImGuiViewportFlags.NoAutoMerge;
                forced.Add(window.ID);
            }
        }
        else if (forced.Remove(window.ID))
        {
            overrideSet &= ~ImGuiViewportFlags.NoAutoMerge;
        }
    }

    /// <summary>Fenêtre Windows propre à cette fenêtre ImGui ; 0 si elle est dessinée dans l'image du jeu.</summary>
    private static nint SeparateHandle(ImGuiWindowPtr window, uint mainViewportId)
    {
        if (!window.ViewportOwned)
            return 0;
        var viewport = window.Viewport;
        if (viewport.Handle == null)
            return 0;
        ref var platform = ref viewport.ImGuiViewport;
        return platform.ID == mainViewportId ? 0 : (nint)platform.PlatformHandle;
    }

    private bool Exclude(nint hwnd)
    {
        if (CaptureExclusion.Exclude(hwnd, out var error))
        {
            excluded.Add(hwnd);
            return true;
        }

        if (error != LastError)
        {
            LastError = error;
            Plugin.Log.Warning($"Windows refused to hide a window from captures (error {error}).");
        }

        return false;
    }

    private static string Name(ImGuiWindowPtr window) => Marshal.PtrToStringUTF8((nint)window.Name) ?? string.Empty;

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandleW(string moduleName);
}
