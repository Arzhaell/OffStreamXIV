using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace OffStreamXIV;

/// <summary>État d'une fenêtre du jeu, pour la fenêtre principale.</summary>
/// <param name="Open">Fenêtre du jeu affichée.</param>
/// <param name="Watched">Ouverte depuis le chargement d'OffStreamXIV : on sait ce que les plugins y ont ajouté.</param>
/// <param name="AddedTexts">Textes ajoutés par des plugins (cachés à OBS si la case est cochée).</param>
/// <param name="AddedOthers">Autres éléments ajoutés (images…), pas encore cachés.</param>
/// <param name="Above">Fenêtres du jeu par-dessus, sous lesquelles les textes ne sont pas redessinés.</param>
internal readonly record struct GameWindowState(bool Open, bool Watched, int AddedTexts, int AddedOthers, string Above);

/// <summary>
/// Cache à OBS les textes que des plugins ajoutent dans une fenêtre du jeu (par exemple Character Panel Refined dans
/// l'onglet Personnage). Ces textes sont dessinés par le jeu, dans l'image qu'OBS filme : on les rend transparents dans
/// le jeu, et on les redessine à l'identique dans une fenêtre à part, retirée des captures.
/// Ce qui a été ajouté : tout ce qui n'existait pas encore quand le jeu a créé la fenêtre (avant les plugins).
/// </summary>
internal sealed unsafe class GameWindowAdditions : IDisposable
{
    // Marge autour de la fenêtre du jeu : un contour de texte peut déborder un peu.
    private const float Margin = 8f;

    private readonly Configuration configuration;
    private readonly WindowHider hider;

    // Nœuds de chaque fenêtre du jeu juste avant que les plugins la modifient (adresses comparées, jamais relues).
    private readonly Dictionary<string, HashSet<nint>> original = [];

    // Couleurs d'origine des textes rendus transparents, par fenêtre du jeu puis par nœud.
    private readonly Dictionary<string, Dictionary<nint, (ByteColor Text, ByteColor Edge)>> hiddenColors = [];

    // Polices du jeu chargées par Dalamud, par famille et taille (en demi-pixels).
    private readonly Dictionary<(GameFontFamily, int), IFontHandle> fonts = [];

    private readonly List<nint> nodes = [];
    private readonly List<(nint Node, ScreenRect Rect, float Opacity)> drawn = [];
    private readonly List<ScreenRect> above = [];
    private readonly List<string> aboveNames = [];

    public GameWindowAdditions(Configuration configuration, WindowHider hider)
    {
        this.configuration = configuration;
        this.hider = hider;

        // Toutes les fenêtres du jeu sont notées à leur création : on peut ainsi en cocher une plus tard sans la rouvrir.
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreSetup, OnPreSetup);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, OnPreFinalize);
        RefreshDisplayListeners();
    }

    public static string OverlayKey(string addon) => Hiding.AlwaysHiddenPrefix + "Overlay." + addon;

    public bool IsHidden(string addon) => configuration.HiddenGameWindows.Contains(addon);

    /// <param name="withAbove">Chercher aussi les fenêtres du jeu par-dessus (pour l'infobulle).</param>
    public GameWindowState State(string addon, bool withAbove = false)
    {
        var unit = Find(addon);
        if (unit == null || !unit->IsVisible)
            return new GameWindowState(false, original.ContainsKey(addon), 0, 0, string.Empty);
        if (!original.TryGetValue(addon, out var before))
            return new GameWindowState(true, false, 0, 0, string.Empty);

        int texts = 0, others = 0;
        Collect(unit);
        foreach (var address in nodes)
        {
            if (before.Contains(address))
                continue;
            if (((AtkResNode*)address)->Type == NodeType.Text)
                texts++;
            else
                others++;
        }

        if (!withAbove)
            return new GameWindowState(true, true, texts, others, string.Empty);
        FindAbove(unit);
        return new GameWindowState(true, true, texts, others, string.Join(", ", aboveNames));
    }

    /// <summary>Coche ou décoche une fenêtre du jeu ; décochée, ses textes redeviennent visibles dans le jeu tout de suite.</summary>
    public void SetHidden(string addon, bool hidden)
    {
        if (hidden ? !configuration.HiddenGameWindows.Add(addon) : !configuration.HiddenGameWindows.Remove(addon))
            return;
        if (!hidden)
            Restore(addon);
        RefreshDisplayListeners();
    }

    /// <summary>Redessine, pour toi seul, les textes cachés de chaque fenêtre du jeu cochée. À appeler pendant le dessin d'ImGui.</summary>
    public void Draw()
    {
        foreach (var addon in configuration.HiddenGameWindows)
            DrawOverlay(addon);
    }

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(OnPreSetup, OnPreFinalize, OnBeforeDisplay);
        foreach (var addon in new List<string>(hiddenColors.Keys))
            Restore(addon);
        foreach (var font in fonts.Values)
            font.Dispose();
        fonts.Clear();
    }

    private static AtkUnitBase* Find(string addon) => (AtkUnitBase*)Plugin.GameGui.GetAddonByName(addon).Address;

    /// <summary>
    /// Le jeu calcule l'affichage pendant Update : on y rend les textes transparents, et encore avant Draw au cas où un
    /// plugin les aurait réécrits entre les deux. Seulement pour les fenêtres du jeu cochées.
    /// </summary>
    private void RefreshDisplayListeners()
    {
        Plugin.AddonLifecycle.UnregisterListener(OnBeforeDisplay);
        if (configuration.HiddenGameWindows.Count == 0)
            return;
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, configuration.HiddenGameWindows, OnBeforeDisplay);
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, configuration.HiddenGameWindows, OnBeforeDisplay);
    }

    private void OnPreSetup(AddonEvent type, AddonArgs args)
    {
        // Le jeu a créé les nœuds de la fenêtre ; les plugins ne l'ont pas encore modifiée (ils le font après).
        Collect((AtkUnitBase*)args.Addon.Address);
        original[args.AddonName] = [.. nodes];
        hiddenColors.Remove(args.AddonName);
        if (IsHidden(args.AddonName))
            Plugin.Log.Information($"{args.AddonName}: {nodes.Count} game nodes before plugins.");
    }

    private void OnPreFinalize(AddonEvent type, AddonArgs args)
    {
        original.Remove(args.AddonName);
        hiddenColors.Remove(args.AddonName);
    }

    private void OnBeforeDisplay(AddonEvent type, AddonArgs args)
    {
        var addon = args.AddonName;
        if (!configuration.HiddenGameWindows.Contains(addon) || !original.TryGetValue(addon, out var before))
            return;

        if (!hiddenColors.TryGetValue(addon, out var colors))
            colors = hiddenColors[addon] = [];
        var count = colors.Count;
        Collect((AtkUnitBase*)args.Addon.Address);
        foreach (var address in nodes)
        {
            var node = (AtkResNode*)address;
            if (before.Contains(address) || node->Type != NodeType.Text)
                continue;

            // Un plugin qui réécrit la couleur la rend de nouveau visible : on garde la nouvelle et on recache.
            var text = (AtkTextNode*)node;
            if (text->TextColor.A == 0 && text->EdgeColor.A == 0)
                continue;
            colors[address] = (text->TextColor, text->EdgeColor);
            text->TextColor.A = 0;
            text->EdgeColor.A = 0;
        }

        if (colors.Count != count)
            Plugin.Log.Information($"{addon}: {colors.Count} texts added by plugins hidden from OBS.");
    }

    /// <summary>Remet les couleurs d'origine des textes encore présents dans la fenêtre du jeu.</summary>
    private void Restore(string addon)
    {
        if (!hiddenColors.Remove(addon, out var colors))
            return;
        var unit = Find(addon);
        if (unit == null)
            return;

        Collect(unit);
        foreach (var address in nodes)
        {
            if (!colors.TryGetValue(address, out var color))
                continue;
            var text = (AtkTextNode*)address;
            text->TextColor = color.Text;
            text->EdgeColor = color.Edge;
        }
    }

    private void DrawOverlay(string addon)
    {
        var unit = Find(addon);
        if (unit == null || !unit->IsVisible || unit->RootNode == null || Plugin.GameGui.GameUiHidden)
            return;
        if (!original.TryGetValue(addon, out var before) || !hiddenColors.TryGetValue(addon, out var colors) || colors.Count == 0)
            return;

        // Textes à redessiner : ajoutés, visibles dans le jeu et pas recouverts par une autre fenêtre du jeu.
        FindAbove(unit);
        Collect(unit);
        drawn.Clear();
        var minimum = new Vector2(float.MaxValue);
        var maximum = new Vector2(float.MinValue);
        foreach (var address in nodes)
        {
            var node = (AtkResNode*)address;
            if (before.Contains(address) || !colors.ContainsKey(address) || !IsShown(node, out var opacity)
                || ((AtkTextNode*)node)->NodeText.Length == 0)
                continue;
            var rect = RectOf(node);
            if (NativeText.IsCovered(rect, above))
                continue;
            drawn.Add((address, rect, opacity));
            minimum = Vector2.Min(minimum, new Vector2(rect.X, rect.Y));
            maximum = Vector2.Max(maximum, new Vector2(rect.X + rect.Width, rect.Y + rect.Height));
        }

        if (drawn.Count == 0)
            return;

        // Une fenêtre ImGui transparente et traversée par la souris, posée sur ces textes. Sa clé la rend toujours cachée :
        // elle a sa propre fenêtre Windows, retirée des captures.
        var origin = ImGuiHelpers.MainViewport.Pos;
        ImGui.SetNextWindowPos(origin + minimum - new Vector2(Margin));
        ImGui.SetNextWindowSize(maximum - minimum + new Vector2(2 * Margin));
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs
                                       | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing
                                       | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoNav;
        var key = OverlayKey(addon);
        ImGui.Begin(key, flags);

        // Rien n'est dessiné tant que Windows n'a pas retiré la fenêtre des captures (sinon OBS pourrait la voir à sa création).
        if (hider.IsExcluded(key))
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.PushClipRectFullScreen();
            foreach (var (address, rect, opacity) in drawn)
            {
                var color = colors[address];
                DrawText((AtkTextNode*)address, rect, opacity, color.Text, color.Edge, origin, drawList);
            }

            drawList.PopClipRect();
        }

        ImGui.End();
    }

    private void DrawText(
        AtkTextNode* text, ScreenRect rect, float opacity, ByteColor color, ByteColor edgeColor, Vector2 origin, ImDrawListPtr drawList)
    {
        var node = &text->AtkResNode;

        // Taille réelle : taille de police du jeu (en points) multipliée par l'échelle de la fenêtre.
        var scale = node->Transform.M22 > 0 ? node->Transform.M22 : 1f;
        var font = Font(Family(text->FontType), text->FontSize * scale * 4f / 3f);
        if (font == null)
            return;

        using (font.Push())
        {
            var drawParams = new SeStringDrawParams
            {
                TargetDrawList = drawList,
                ScreenOffset = origin + new Vector2(rect.X, rect.Y),
                Font = ImGui.GetFont(),
                FontSize = ImGui.GetFontSize(),
                Color = NativeText.ToImGuiColor(color.R, color.G, color.B, color.A),
                EdgeColor = NativeText.ToImGuiColor(edgeColor.R, edgeColor.G, edgeColor.B, edgeColor.A),
                Edge = (text->TextFlags & TextFlags.Edge) != 0,
                Bold = (text->TextFlags & TextFlags.Bold) != 0,
                Italic = (text->TextFlags & TextFlags.Italic) != 0,
                Opacity = opacity,
                WrapWidth = (text->TextFlags & TextFlags.WordWrap) != 0 ? rect.Width : float.MaxValue,
            };

            // Dessiné en haut à gauche, puis déplacé selon l'alignement une fois sa taille connue.
            var firstVertex = drawList.VtxBuffer.Size;
            var size = ImGuiHelpers.SeStringWrapped(text->NodeText.AsSpan(), drawParams).Size;
            var offset = NativeText.AlignOffset((int)text->AlignmentType, new Vector2(rect.Width, rect.Height), size);
            var vertices = drawList.VtxBuffer;
            for (var i = firstVertex; i < vertices.Size; i++)
                vertices.Ref<ImDrawVert>(i).Pos += offset;
        }
    }

    /// <summary>Visible si le nœud et tous ses parents le sont ; opacité cumulée des parents (fondus d'ouverture).</summary>
    private static bool IsShown(AtkResNode* node, out float opacity)
    {
        opacity = 1f;
        for (var depth = 0; node != null && depth < 64; depth++, node = node->ParentNode)
        {
            if ((node->NodeFlags & NodeFlags.Visible) == 0)
                return false;
            opacity *= node->Color.A / 255f;
        }

        return opacity > 0f;
    }

    private static ScreenRect RectOf(AtkResNode* node)
    {
        var scaleX = node->Transform.M11 > 0 ? node->Transform.M11 : 1f;
        var scaleY = node->Transform.M22 > 0 ? node->Transform.M22 : 1f;
        return new ScreenRect(node->ScreenX, node->ScreenY, node->Width * scaleX, node->Height * scaleY);
    }

    /// <summary>
    /// Fenêtres du jeu affichées par-dessus celle-ci (couche plus haute, ou même couche mais plus récente), sauf sa fenêtre
    /// parente et ses onglets.
    /// </summary>
    private void FindAbove(AtkUnitBase* unit)
    {
        above.Clear();
        aboveNames.Clear();
        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null)
            return;

        var screen = ImGuiHelpers.MainViewport.Size;
        var found = false;
        foreach (ref var layer in manager->AtkUnitManager.DepthLayers)
        {
            for (var i = 0; i < layer.Count; i++)
            {
                var other = layer.Entries[i].Value;
                if (other == unit)
                {
                    found = true;
                    continue;
                }

                if (!found || other == null || !other->IsVisible || other->RootNode == null || IsFamily(unit, other))
                    continue;
                var rect = RectOf(other->RootNode);
                if (NativeText.IsFullScreen(rect, screen) || rect.Width <= 0 || rect.Height <= 0)
                    continue;
                above.Add(rect);
                aboveNames.Add(other->NameString);
            }
        }
    }

    private static bool IsFamily(AtkUnitBase* unit, AtkUnitBase* other) =>
        other->Id == unit->ParentId || other->Id == unit->HostId || other->ParentId == unit->Id || other->HostId == unit->Id
        || (unit->HostId != 0 && other->HostId == unit->HostId);

    /// <summary>Tous les nœuds de la fenêtre du jeu, composants compris (dans <see cref="nodes"/>).</summary>
    private void Collect(AtkUnitBase* unit)
    {
        nodes.Clear();
        if (unit != null && unit->RootNode != null)
            CollectSiblings(unit->RootNode, 0);
    }

    private void CollectSiblings(AtkResNode* first, int depth)
    {
        // Garde-fous : une boucle dans les nœuds ne doit pas bloquer le jeu.
        if (depth > 64)
            return;
        for (var node = first; node != null && nodes.Count < 8192; node = node->PrevSiblingNode)
        {
            nodes.Add((nint)node);
            if (node->ChildNode != null)
                CollectSiblings(node->ChildNode, depth + 1);

            // Un nœud composant a un type ≥ 1000 ; ses propres nœuds sont dans le composant.
            if ((ushort)node->Type >= 1000)
            {
                var component = ((AtkComponentNode*)node)->Component;
                if (component != null && component->UldManager.RootNode != null)
                    CollectSiblings(component->UldManager.RootNode, depth + 1);
            }
        }
    }

    private static GameFontFamily Family(FontType type) => type switch
    {
        FontType.MiedingerMed => GameFontFamily.MiedingerMid,
        FontType.Miedinger => GameFontFamily.Meidinger,
        FontType.TrumpGothic => GameFontFamily.TrumpGothic,
        FontType.Jupiter or FontType.JupiterLarge => GameFontFamily.Jupiter,
        _ => GameFontFamily.Axis,
    };

    /// <summary>Police du jeu à cette taille ; null le temps que Dalamud la charge.</summary>
    private IFontHandle? Font(GameFontFamily family, float sizePx)
    {
        var key = (family, (int)MathF.Round(sizePx * 2));
        if (!fonts.TryGetValue(key, out var handle))
            handle = fonts[key] = Plugin.PluginInterface.UiBuilder.FontAtlas.NewGameFontHandle(new GameFontStyle(family, key.Item2 / 2f));
        return handle.Available ? handle : null;
    }
}
