using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using Dalamud.Plugin;

namespace OffStreamXIV.Tests;

/// <summary>
/// OffStreamXIV lit l'interne de Dalamud par réflexion ; une erreur n'y laisserait qu'un avertissement dans le journal.
/// Ces tests vérifient, sur la version de Dalamud installée, que tout ce qu'il lit existe encore.
/// </summary>
public class DalamudInternalsTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Assembly Dalamud = typeof(IDalamudPluginInterface).Assembly;

    static DalamudInternalsTests()
    {
        // Les bibliothèques que Dalamud utilise sont à côté de lui, pas dans le dossier des tests.
        var folder = Path.GetDirectoryName(Dalamud.Location)!;
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var path = Path.Combine(folder, name.Name + ".dll");
            return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };
    }

    private static Type DalamudType(string name) =>
        Dalamud.GetType(name) ?? throw new Xunit.Sdk.XunitException($"{name} n'existe plus dans Dalamud.");

    private static Type MemberType(Type owner, string name)
    {
        var member = (MemberInfo?)owner.GetProperty(name, Flags) ?? owner.GetField(name, Flags);
        return member switch
        {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => throw new Xunit.Sdk.XunitException($"{owner.Name}.{name} n'existe plus dans Dalamud."),
        };
    }

    [Fact]
    public void PluginWindowSystemsCanBeRead()
    {
        var exposed = DalamudType("Dalamud.Plugin.ExposedPlugin");
        var local = exposed.GetFields(Flags).Single(f => f.FieldType.Name == WindowSystemOwners.LocalPluginType).FieldType;
        var pluginInterface = MemberType(local, WindowSystemOwners.DalamudInterfaceProperty);
        var uiBuilder = MemberType(pluginInterface, WindowSystemOwners.UiBuilderProperty);
        Assert.True(typeof(Delegate).IsAssignableFrom(MemberType(uiBuilder, WindowSystemOwners.DrawField)));

        var windowSystem = DalamudType("Dalamud.Interface.Windowing.WindowSystem");
        Assert.True(typeof(IEnumerable).IsAssignableFrom(MemberType(windowSystem, WindowSystemOwners.WindowsProperty)));
        Assert.Equal(typeof(string), MemberType(DalamudType("Dalamud.Interface.Windowing.Window"), WindowSystemOwners.WindowNameProperty));
    }

    [Fact]
    public void GameWindowListenersCanBeRead()
    {
        var scoped = DalamudType("Dalamud.Game.Addon.Lifecycle.AddonLifecyclePluginScoped");
        var service = MemberType(scoped, AddonListeners.ServiceField);
        Assert.True(typeof(IDictionary).IsAssignableFrom(MemberType(service, AddonListeners.ListenersProperty)));

        var listener = DalamudType("Dalamud.Game.Addon.Lifecycle.AddonLifecycleEventListener");
        Assert.True(typeof(Delegate).IsAssignableFrom(MemberType(listener, AddonListeners.DelegateProperty)));
        Assert.Equal(typeof(bool), MemberType(listener, AddonListeners.ClearingProperty));
    }

    [Fact]
    public void PluginLoaderIsStillNamedTheSame() =>
        Assert.Equal(PluginOwner.LoadContextType, DalamudType("Dalamud.Plugin.Internal.Loader.ManagedLoadContext").Name);
}
