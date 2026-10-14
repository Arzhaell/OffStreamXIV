using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace OffStreamXIV;

public sealed class Plugin : IDalamudPlugin
{
    /// <summary>Nom interne du plugin : celui de sa DLL, que Dalamud donne aussi à son espace de chargement.</summary>
    public const string InternalName = "OffStreamXIV";

    private const string CommandName = "/offstream";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private readonly WindowSystem windowSystem = new("OffStreamXIV");
    private readonly MainWindow mainWindow;
    private readonly TestWindow testWindow;

    public Plugin()
    {
        Configuration = LoadConfiguration();
        Loc.Update(Configuration.Language);
        Hider = new WindowHider(Configuration);
        Additions = new GameWindowAdditions(Configuration, Hider);
        Roles = new RoleFinder(Configuration, Hider);

        mainWindow = new MainWindow(this);
        testWindow = new TestWindow();
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(testWindow);

        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += mainWindow.Toggle;
        PluginInterface.LanguageChanged += OnDalamudLanguageChanged;

        RegisterCommand();
    }

    public Configuration Configuration { get; }

    internal WindowHider Hider { get; }

    internal GameWindowAdditions Additions { get; }

    internal RoleFinder Roles { get; }

    /// <summary>Tout le plugin : ses fenêtres (y compris celles pas encore ouvertes) et ses ajouts dans les fenêtres du jeu.</summary>
    internal void SetPluginHidden(string plugin, bool hidden, IEnumerable<string> gameWindows)
    {
        Hiding.SetPlugin(Configuration, plugin, hidden);
        foreach (var addon in gameWindows)
            Additions.SetHidden(addon, hidden);
        Configuration.Save();
    }

    internal void SetWindowsHidden(string plugin, IEnumerable<WindowRow> windows, bool hidden)
    {
        foreach (var window in windows)
            Hiding.SetWindow(Configuration, window.Key, plugin, window.Label, hidden);
        Configuration.Save();
    }

    internal void SetGameWindowHidden(string addon, bool hidden)
    {
        Additions.SetHidden(addon, hidden);
        Configuration.Save();
    }

    public void SetLanguage(PluginLanguage language)
    {
        Configuration.Language = language;
        Configuration.Save();
        RefreshLanguage();
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
        PluginInterface.LanguageChanged -= OnDalamudLanguageChanged;
        PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= mainWindow.Toggle;
        windowSystem.RemoveAllWindows();
        Additions.Dispose();
        Hider.Dispose();
    }

    // Un fichier de configuration illisible ne doit pas empêcher le plugin de se charger : on repart des réglages par défaut.
    private static Configuration LoadConfiguration()
    {
        try
        {
            return PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        }
        catch (System.Exception e)
        {
            Log.Error(e, "Could not read the configuration, falling back to default settings.");
            return new Configuration();
        }
    }

    private void OnDraw()
    {
        windowSystem.Draw();
        Additions.Draw();
        // Après nos fenêtres : la liste montre alors leur état de cette image.
        Hider.Update(mainWindow.IsOpen);
        Roles.Update();
    }

    private void OnCommand(string command, string arguments)
    {
        if (arguments.Trim().Equals("test", System.StringComparison.OrdinalIgnoreCase))
            testWindow.Toggle();
        else
            mainWindow.Toggle();
    }

    private static string CommandHelp => Loc.T(
        "/offstream: choose which plugins and windows OBS can see. /offstream test: a test window, always hidden from OBS.",
        "/offstream : choisir les plugins et fenêtres qu'OBS peut voir. /offstream test : une fenêtre d'essai, toujours cachée à OBS.");

    private void OnDalamudLanguageChanged(string languageCode)
    {
        if (Configuration.Language == PluginLanguage.Auto)
            RefreshLanguage();
    }

    private void RefreshLanguage()
    {
        Loc.Update(Configuration.Language);
        CommandManager.RemoveHandler(CommandName);
        RegisterCommand();
    }

    private void RegisterCommand()
    {
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) { HelpMessage = CommandHelp });
    }
}
