using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OffStreamXIV;

/// <summary>
/// Fenêtres du jeu que chaque plugin modifie, d'après les écouteurs qu'il a déclarés auprès de Dalamud
/// (IAddonLifecycle). Dalamud ne le publie pas : on lit sa liste interne, en lecture seule. Si Dalamud change, la liste
/// reste vide et rien d'autre ne casse.
/// </summary>
internal static class AddonListeners
{
    // Ce qu'on lit dans Dalamud. DalamudInternalsTests vérifie que la version de Dalamud installée les a toujours.
    internal const string ServiceField = "addonLifecycleService";
    internal const string ListenersProperty = "EventListeners";
    internal const string DelegateProperty = "FunctionDelegate";
    internal const string ClearingProperty = "IsRequestedToClear";

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static bool failed;
    private static bool logged;

    /// <summary>Nom interne du plugin → noms des fenêtres du jeu (addons) qu'il surveille.</summary>
    public static Dictionary<string, SortedSet<string>> ByPlugin()
    {
        var result = new Dictionary<string, SortedSet<string>>();
        try
        {
            var scoped = Plugin.AddonLifecycle;
            var service = scoped.GetType().GetField(ServiceField, Flags)?.GetValue(scoped);
            if (service?.GetType().GetProperty(ListenersProperty, Flags)?.GetValue(service) is IDictionary byEvent)
                result = Parse(byEvent, OwnerOf);
            else
                Warn(null, "Dalamud's game window listeners weren't found: game windows won't be listed under their plugins.");
        }
        catch (Exception e)
        {
            Warn(e, "Could not read which plugins change game windows.");
        }

        if (!logged && result.Count > 0)
        {
            logged = true;
            Plugin.Log.Information("Game windows changed by plugins: " + string.Join("; ", result.Select(p => $"{p.Key} → {string.Join(", ", p.Value)}")));
        }

        return result;
    }

    /// <summary>
    /// Lit la liste de Dalamud : type d'événement → nom de la fenêtre du jeu → écouteurs (FunctionDelegate,
    /// IsRequestedToClear). <paramref name="ownerOf"/> donne le plugin d'une méthode appelée, ou null.
    /// </summary>
    public static Dictionary<string, SortedSet<string>> Parse(IDictionary byEvent, Func<Delegate, string?> ownerOf)
    {
        var result = new Dictionary<string, SortedSet<string>>();

        // Copies avant lecture : un plugin qui se charge peut ajouter des écouteurs en même temps.
        foreach (var byAddon in Entries(byEvent).Select(e => e.Value).OfType<IDictionary>())
        {
            foreach (var (key, value) in Entries(byAddon))
            {
                // Nom vide : écouteur de toutes les fenêtres, qu'on ne peut pas rattacher à l'une d'elles.
                if (key is not string addon || addon.Length == 0 || value is not IEnumerable listeners)
                    continue;
                foreach (var listener in listeners.Cast<object>().ToList())
                {
                    var type = listener.GetType();
                    if (type.GetProperty(ClearingProperty, Flags)?.GetValue(listener) is true)
                        continue;
                    if (type.GetProperty(DelegateProperty, Flags)?.GetValue(listener) is not Delegate handler)
                        continue;
                    foreach (var call in handler.GetInvocationList())
                    {
                        if (ownerOf(call) is not { } plugin || plugin == Plugin.InternalName)
                            continue;
                        if (!result.TryGetValue(plugin, out var addons))
                            addons = result[plugin] = [];
                        addons.Add(addon);
                    }
                }
            }
        }

        return result;
    }

    private static void Warn(Exception? e, string message)
    {
        if (failed)
            return;
        failed = true;
        Plugin.Log.Warning(e, message);
    }

    /// <summary>Paires clé → valeur d'un dictionnaire vu sans son type (son énumérateur de dictionnaire, pas celui des paires).</summary>
    private static List<(object Key, object? Value)> Entries(IDictionary dictionary)
    {
        var entries = new List<(object, object?)>();
        foreach (DictionaryEntry entry in dictionary)
            entries.Add((entry.Key, entry.Value));
        return entries;
    }

    private static string? OwnerOf(Delegate call) =>
        PluginOwner.OfAssembly(call.Method.DeclaringType?.Assembly) ?? PluginOwner.OfAssembly(call.Target?.GetType().Assembly);
}
