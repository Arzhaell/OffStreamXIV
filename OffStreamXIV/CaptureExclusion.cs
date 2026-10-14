using System.Runtime.InteropServices;

namespace OffStreamXIV;

/// <summary>
/// Demande à Windows de retirer une fenêtre de toutes les captures (OBS, captures d'écran, partage d'écran) : elle reste
/// affichée à l'écran. Windows 10 2004 ou plus récent.
/// </summary>
internal static partial class CaptureExclusion
{
    private const uint WdaNone = 0x00;
    private const uint WdaExcludeFromCapture = 0x11;

    /// <summary>Retire la fenêtre des captures ; faux si Windows refuse (le code d'erreur est dans <paramref name="error"/>).</summary>
    public static bool Exclude(nint hwnd, out int error)
    {
        error = 0;
        if (GetWindowDisplayAffinity(hwnd, out var current) && current == WdaExcludeFromCapture)
            return true;
        if (SetWindowDisplayAffinity(hwnd, WdaExcludeFromCapture))
            return true;
        error = Marshal.GetLastPInvokeError();
        return false;
    }

    /// <summary>Rend la fenêtre de nouveau visible dans les captures.</summary>
    public static void Restore(nint hwnd)
    {
        if (IsWindow(hwnd))
            SetWindowDisplayAffinity(hwnd, WdaNone);
    }

    public static bool IsAlive(nint hwnd) => IsWindow(hwnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowDisplayAffinity(nint hWnd, uint dwAffinity);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowDisplayAffinity(nint hWnd, out uint pdwAffinity);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindow(nint hWnd);
}
