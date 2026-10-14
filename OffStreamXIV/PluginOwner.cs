using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Bindings.ImGui;

namespace OffStreamXIV;

/// <summary>Une étape de la pile d'appels, vue par <see cref="PluginOwner.Find"/>.</summary>
/// <param name="Plugin">Nom interne du plugin auquel appartient le code ; null pour Dalamud et .NET.</param>
/// <param name="Relay">Code qui ne fait que transmettre l'ouverture de la fenêtre : OffStreamXIV (l'interception) ou ImGui.</param>
internal readonly record struct CallFrame(string? Plugin, bool Relay);

/// <summary>
/// Retrouve le plugin qui ouvre une fenêtre. Dalamud charge chaque plugin dans son propre espace (AssemblyLoadContext)
/// qui porte le nom interne du plugin : on remonte les appels jusqu'au premier code chargé dans un tel espace.
/// </summary>
internal static class PluginOwner
{
    /// <summary>Fenêtres de Dalamud lui-même.</summary>
    public const string Dalamud = "Dalamud";

    /// <summary>Fenêtres dont le plugin n'a pas pu être retrouvé.</summary>
    public const string Unknown = "?";

    /// <summary>Chargeur de plugins de Dalamud (vérifié par DalamudInternalsTests sur la version installée).</summary>
    internal const string LoadContextType = "ManagedLoadContext";

    private static readonly Assembly Self = typeof(PluginOwner).Assembly;
    private static readonly Assembly Bindings = typeof(ImGui).Assembly;

    public static string FromStack() =>
        Find(new StackTrace(false).GetFrames().Select(frame => Describe(frame.GetMethod()?.DeclaringType?.Assembly)));

    /// <summary>
    /// Saute l'interception et ImGui en haut de la pile, puis prend le premier plugin rencontré. OffStreamXIV compte comme
    /// un plugin plus bas dans la pile : c'est alors lui qui ouvre ses propres fenêtres.
    /// </summary>
    public static string Find(IEnumerable<CallFrame> frames)
    {
        var top = true;
        foreach (var frame in frames)
        {
            if (top && frame.Relay)
                continue;
            top = false;
            if (frame.Plugin != null)
                return frame.Plugin;
        }

        return Dalamud;
    }

    /// <summary>Nom interne du plugin auquel appartient ce code ; null pour Dalamud et .NET.</summary>
    public static string? OfAssembly(Assembly? assembly)
    {
        if (assembly == null)
            return null;

        // Le chargeur de plugins de Dalamud s'appelle ManagedLoadContext ; Dalamud et .NET sont dans l'espace par défaut.
        var context = AssemblyLoadContext.GetLoadContext(assembly);
        return context != null && context.GetType().Name == LoadContextType ? context.Name : null;
    }

    private static CallFrame Describe(Assembly? assembly) =>
        assembly == null ? new CallFrame(null, false) : new CallFrame(OfAssembly(assembly), assembly == Self || assembly == Bindings);
}
