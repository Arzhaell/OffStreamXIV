# OffStreamXIV

*[Version française](README.fr.md)*

A Dalamud plugin for Final Fantasy XIV: choose what your stream sees. Hide plugin windows from OBS and other capture
software, while you keep seeing them on your screen.

## Installation
1. In game, type `/xlsettings` and open the **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste this URL, click **+**, tick the checkbox and save:
   ```
   https://raw.githubusercontent.com/Arzhaell/OffStreamXIV/main/repo.json
   ```
3. Type `/xlplugins`, search for **OffStreamXIV** and install it.

## Requirements
- Dalamud's **"Enable multi-monitor windows"** setting turned on (`/xlsettings`, **Look & Feel** tab).
- The game in **windowed** or **borderless window** mode, and **more than one monitor**: Dalamud turns separate
  windows off otherwise.
- Windows 10 version 2004 or later.

## Usage
`/offstream` opens the list of your plugins (and Dalamud). For each plugin:

- check the **plugin** to hide everything it shows, including the windows you haven't opened yet;
- **Main window** and **Settings**: one checkbox each (a single line when the plugin uses the same window for both).
  The first time, click **Find**: OffStreamXIV opens the window through Dalamud and remembers which one it is;
- its **other windows**, one by one;
- its **additions in game windows**: the texts a plugin adds inside a window of the game itself (for example Character
  Panel Refined in the Character window).

The **Separate** and **Out of captures** columns show whether a hidden window really is out of the game image. OffStreamXIV's
own windows are hidden at first. `/offstream test` opens a test window, always hidden, to check your OBS sources.

## How it works
Plugin windows are normally drawn inside the game image, so OBS captures all of them. A window you hide is moved out
of the game image: it is drawn in its own transparent window, at the same place on top of the game (like a plugin
window moved to another monitor), and Windows removes that window from every capture. On your screen, nothing changes.
It works with game capture, window capture and display capture, in OBS as in other capture software. The game's
rendering is not touched.

**Additions in game windows** are drawn by the game itself, inside the image OBS captures. OffStreamXIV makes them
transparent in the game and redraws them for you, with the game's font and colors, in a separate window removed from
captures. Limits:

- OBS sees empty lines instead, and the layout changes made by the plugin stay visible;
- only texts are hidden: not images, not texts changed inside the game's own lines (like item tooltips), not the
  plugin's game tooltips;
- when several plugins change the same game window, their additions are hidden together;
- a game window that was open before OffStreamXIV loaded must be closed and reopened.

How OffStreamXIV knows:

- which plugin opens a window: it watches ImGui's window opening (`igBegin`) and looks at which plugin made the call,
  or which plugin's window system holds that window;
- which plugins change a game window: the listeners they registered with Dalamud (`IAddonLifecycle`);
- what a plugin added in a game window: everything that wasn't there when the game created the window, before plugins
  change it.

## Disclaimer
Like any third-party tool, using Dalamud and its plugins is against the FFXIV Terms of Service. Use it at your own
risk.

OffStreamXIV is not affiliated with or endorsed by the OBS Project. OBS is only named here to say which software it hides
windows from.

## Building and testing
- `dotnet test OffStreamXIV.Tests -c Release`: tests of the logic (hiding rules, plugin of each window, list, main window
  and settings, game window additions, text layout).
- `dotnet build OffStreamXIV -c Release`: to try it, add `OffStreamXIV\bin\Release\OffStreamXIV.dll` to the dev plugin locations
  (`/xlsettings`, **Experimental** tab); Dalamud reloads it by itself, even with the game running. It does not load the
  version installed from the repository at the same time: disable that one.

## Releasing a new version
Pushing an annotated `vX.Y.Z` tag is all it takes: the GitHub workflow runs the tests, builds the plugin, creates the
release (its notes are the tag message) and updates `repo.json`.

```bash
git tag -a v1.0.0 --cleanup=verbatim -F notes.txt
git push origin v1.0.0
```

## Credits
- [Dalamud](https://github.com/goatcorp/Dalamud) and [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs):
  plugin framework and game structures.
- [Dear ImGui](https://github.com/ocornut/imgui) (MIT): the user interface library used by Dalamud.
- [xUnit](https://xunit.net/) for the tests.

FINAL FANTASY XIV © SQUARE ENIX CO., LTD. This plugin is not affiliated with or endorsed by Square Enix.
