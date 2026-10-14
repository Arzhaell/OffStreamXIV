using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OffStreamXIV;

/// <summary>
/// Plugin des fenêtres que la pile d'appels ne permet pas de retrouver. Le modèle de plugin de Dalamud branche son
/// système de fenêtres directement sur le dessin (<c>UiBuilder.Draw += WindowSystem.Draw</c>) : quand la fenêtre
/// s'ouvre, seul du code de Dalamud est dans la pile. Même chose quand le plugin passe par une petite méthode à lui
/// (<c>void OnDraw() => windowSystem.Draw()</c>, ex. QuestTracker) : .NET en fait un appel terminal et la retire de la
/// pile. On lit alors, dans Dalamud (en lecture seule), ce que chaque plugin a branché sur son dessin, et les fenêtres
/// de ces systèmes de fenêtres.
/// </summary>
internal static class WindowSystemOwners
{
    // Ce qu'on lit dans Dalamud : ExposedPlugin → LocalPlugin → DalamudInterface → LocalUiBuilder → événement Draw, puis
    // Windows → WindowName. DalamudInternalsTests vérifie que la version de Dalamud installée les a toujours.
    internal const string LocalPluginType = "LocalPlugin";
    internal const string DalamudInterfaceProperty = "DalamudInterface";
    internal const string UiBuilderProperty = "LocalUiBuilder";
    internal const string DrawField = "Draw";
    internal const string WindowsProperty = "Windows";
    internal const string WindowNameProperty = "WindowName";

    internal const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static bool failed;

    /// <summary>Clé de fenêtre (voir <see cref="WindowKey.From"/>) → nom interne du plugin.</summary>
    public static Dictionary<string, string> Read()
    {
        var result = new Dictionary<string, string>();
        try
        {
            // OffStreamXIV lui-même dessine via son événement Draw : si aucun n'est trouvé, c'est que Dalamud a changé.
            var drawFound = false;
            foreach (var exposed in Plugin.PluginInterface.InstalledPlugins.Where(p => p.IsLoaded))
            {
                var local = exposed.GetType().GetFields(Flags).FirstOrDefault(f => f.FieldType.Name == LocalPluginType)?.GetValue(exposed);
                var pluginInterface = Value(local, DalamudInterfaceProperty);
                var uiBuilder = Value(pluginInterface, UiBuilderProperty);
                if (uiBuilder?.GetType().GetField(DrawField, Flags)?.GetValue(uiBuilder) is not Delegate draw)
                    continue;
                drawFound = true;
                foreach (var name in WindowNames(draw))
                    result.TryAdd(WindowKey.From(name), exposed.InternalName);
            }

            if (!drawFound)
                Warn(null, "The window systems of plugins weren't found in Dalamud: their windows may be listed under Dalamud.");
        }
        catch (Exception e)
        {
            Warn(e, "Could not read the window systems of plugins.");
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

    /// <summary>
    /// Noms des fenêtres des systèmes de fenêtres branchés sur ce dessin (objet avec Windows → WindowName), ou gardés
    /// dans un champ de l'objet dont la méthode est branchée.
    /// </summary>
    public static IEnumerable<string> WindowNames(Delegate draw)
    {
        foreach (var call in draw.GetInvocationList())
        {
            foreach (var windows in WindowSystems(call.Target))
            {
                foreach (var window in windows.Cast<object?>().ToList())
                {
                    if (Value(window, WindowNameProperty) is string name)
                        yield return name;
                }
            }
        }
    }

    private static IEnumerable<IEnumerable> WindowSystems(object? target)
    {
        if (target == null)
            yield break;
        if (Value(target, WindowsProperty) is IEnumerable windows)
        {
            yield return windows;
            yield break;
        }

        // Seuls les champs dont le type a des fenêtres sont lus : rien d'autre du plugin n'est touché.
        foreach (var field in target.GetType().GetFields(Flags))
        {
            if (field.FieldType.GetProperty(WindowsProperty, Flags) != null && Value(field.GetValue(target), WindowsProperty) is IEnumerable held)
                yield return held;
        }
    }

    private static object? Value(object? owner, string property) => owner?.GetType().GetProperty(property, Flags)?.GetValue(owner);
}
