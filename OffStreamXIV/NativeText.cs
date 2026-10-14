using System;
using System.Collections.Generic;
using System.Numerics;

namespace OffStreamXIV;

/// <summary>Rectangle à l'écran, en pixels du jeu.</summary>
internal readonly record struct ScreenRect(float X, float Y, float Width, float Height)
{
    public bool Overlaps(ScreenRect other) =>
        X < other.X + other.Width && other.X < X + Width && Y < other.Y + other.Height && other.Y < Y + Height;
}

/// <summary>Calculs pour redessiner un texte du jeu à l'identique, réunis ici pour être testés sans le jeu.</summary>
internal static class NativeText
{
    /// <summary>
    /// Décalage du texte dans sa zone, selon l'alignement du jeu (AlignmentType : 0 à 8, de gauche à droite puis de haut
    /// en bas : haut-gauche, haut, haut-droite, gauche, centre, droite, bas-gauche, bas, bas-droite).
    /// </summary>
    public static Vector2 AlignOffset(int alignment, Vector2 box, Vector2 text)
    {
        alignment = Math.Clamp(alignment, 0, 8);
        var column = alignment % 3;
        var row = alignment / 3;
        return new Vector2((box.X - text.X) * column / 2f, (box.Y - text.Y) * row / 2f);
    }

    /// <summary>Couleur du jeu (rouge, vert, bleu, opacité) vers la couleur d'ImGui.</summary>
    public static uint ToImGuiColor(byte r, byte g, byte b, byte a) => (uint)(a << 24 | b << 16 | g << 8 | r);

    /// <summary>Un texte recouvert par une autre fenêtre du jeu ne doit pas être redessiné par-dessus elle.</summary>
    public static bool IsCovered(ScreenRect text, IReadOnlyList<ScreenRect> above)
    {
        foreach (var rect in above)
        {
            if (rect.Overlaps(text))
                return true;
        }

        return false;
    }

    /// <summary>Fenêtre qui couvre presque tout l'écran (souvent invisible) : on ne la compte pas comme un recouvrement.</summary>
    public static bool IsFullScreen(ScreenRect rect, Vector2 screen) => rect.Width >= screen.X * 0.9f && rect.Height >= screen.Y * 0.9f;
}
