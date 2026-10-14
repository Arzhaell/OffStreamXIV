using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Newtonsoft.Json;

namespace OffStreamXIV;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public PluginLanguage Language { get; set; } = PluginLanguage.Auto;

    /// <summary>Plugins dont toutes les fenêtres sont cachées à OBS, par nom interne. Les fenêtres d'OffStreamXIV le sont au départ.</summary>
    // Replace : sinon la lecture du fichier ajouterait les valeurs de départ à celles enregistrées.
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HashSet<string> HiddenPlugins { get; set; } = [Plugin.InternalName];

    /// <summary>Fenêtres réglées autrement que leur plugin, par clé (voir <see cref="WindowKey.From"/>).</summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public Dictionary<string, WindowSetting> Windows { get; set; } = [];

    /// <summary>Fenêtres du jeu (nom de l'addon) dont les textes ajoutés par des plugins sont cachés à OBS.</summary>
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HashSet<string> HiddenGameWindows { get; set; } = [];

    /// <summary>Fenêtre principale et fenêtre de paramètres repérées de chaque plugin, par nom interne du plugin.</summary>
    public Dictionary<string, PluginRoles> Roles { get; set; } = [];

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}

/// <summary>Fenêtres qu'un plugin ouvre pour sa fenêtre principale et pour ses paramètres (clé → titre).</summary>
[Serializable]
public class PluginRoles
{
    public Dictionary<string, string> Main { get; set; } = [];

    public Dictionary<string, string> Settings { get; set; } = [];

    public Dictionary<string, string> Of(WindowRole role) => role == WindowRole.Main ? Main : Settings;
}

/// <summary>Une fenêtre cachée alors que son plugin est visible, ou l'inverse.</summary>
[Serializable]
public class WindowSetting
{
    public bool Hidden { get; set; }

    /// <summary>Plugin de la fenêtre, pour la ranger dans la liste même fermée.</summary>
    public string Plugin { get; set; } = string.Empty;

    /// <summary>Titre de la fenêtre la dernière fois qu'elle a été réglée.</summary>
    public string Label { get; set; } = string.Empty;
}
