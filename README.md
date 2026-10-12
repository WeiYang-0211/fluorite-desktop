# Fluorite

**The application dock Windows actually leaves out.**

Fluorite is a Windows 11-style launcher for people who want a searchable, always-at-hand grid of their apps — with a live dock, real wallpaper-aware glass, and multi-monitor placement that just works. No account, no activation, no tracking.

*Runs on Windows 10 1809+ · .NET 8 · x64 · fully open source under [Apache-2.0](LICENSE).*

## Your apps, one flick away

Press <kbd>Alt</kbd>+<kbd>Space</kbd> (configurable) and a frosted grid rises over your wallpaper. Pin what you use, drop in folders, hide the rest. It reads the *real* Start Menu and Desktop entries — so icons, names, and arguments match what Windows already knows, and a fresh install shows up correctly without you configuring anything.

- **Multi-page grid** with flick, wheel, and hot-corner gestures. Tunable columns, rows, and edge margins.

- **Live running indicators.** A dot lights up under apps that are already open; clicking raises the existing window instead of starting a second copy.

- **Desktop-resident dock.** Keep a slim always-on-top strip after the launchpad closes, with magnification on hover.

- **Pinyin search.** Type a few initials and it matches apps whose names start with those letters, so a Chinese app like WeChat is reachable as `wx` — or fuzzy-match full names, straight from the hotkey.

- **Per-monitor placement.** Show it on the mouse's display, on a chosen one, or fullscreen on all of them.

## Built to disappear into your desktop

The whole point is the surface underneath. Fluorite blurs and tints whatever wallpaper is actually on your desktop — it doesn't swap in a stock background — so the dock feels like part of Windows rather than a window sitting on top of it.

- **Live wallpaper blur** across `Screen`, `Wallpaper`, and any custom image you point it at.

- **Aurora & reflection backdrops** — a shader-driven flow field with rippling reflections, not a static gradient.

- **Frosted glass tiles**, icon shapes, label styling, and several clock faces (minimal, flip, glass, paper, digital) with live seconds.

- **Theme bundles** that set wallpaper, icon treatment, clock, and dock together in one click.

## Everything else it does

| | |
| --- | --- |
| **Workspaces** | Save and restore full layouts; keep several named desktops and switch between them. |
| **Steam** | Discovers installed games and puts them in the grid with their own art. |
| **Desktop drop** | Drag a folder or shortcut onto the grid to pin it. |
| **Tray** | Minimize to tray, launch at login, jump straight to settings. |
| **Global hotkey** | Any key combination, re-registered live when you change it. |
| **Fullscreen-aware** | Steps aside for games and full-screen video automatically. |

## How it works

```
flowchart TD
  Hotkey["Global hotkey<br/>or hot corner"] --> WM["Window message<br/>native hook"]
  WM --> VM["MainViewModel<br/>paged app state"]
  VM --> Scan["AppScannerService<br/>Start Menu, Desktop,<br/>registry, Steam"]
  VM --> Icons["IconService<br/>extract from .exe,<br/>shell, cache"]
  Icons --> Win["LaunchpadWindow<br/>frosted glass, grid,<br/>page gestures"]
  VM --> Dock["DesktopDockWindow<br/>always-on-top strip"]
  VM --> Store["StorageService<br/>atomic JSON writes<br/>+%LOCALAPPDATA%"]
  Hotkey --> Find["SearchService<br/>pinyin initials<br/>+ fuzzy match"]
  Find --> VM
```

Fluorite is a native WPF app. Apps are discovered the same way Explorer finds them (Start Menu, Desktop shortcuts, `App Paths`), icons are pulled from the installed executables and shell, and everything is cached to `%LOCALAPPDATA%\Launchpad`. Windows itself supplies the blur and acrylic; Fluorite drives DWM, `IShellLink`, and UI Automation directly.

## Quick start

You need **Windows 10 1809 or newer** and the **.NET 8 SDK**.

```bash
git clone https://github.com/WeiYang-0211/fluorite.git
cd fluorite
dotnet build -c Release
dotnet run
```

Self-contained publish — no .NET runtime needed on the target machine:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -o out
```

Everything is stored under `%LOCALAPPDATA%\Launchpad`. Delete that folder to reset the app completely.

## Project layout

```
src/Fluorite/
  Launchpad/                 App bootstrap, startup, tray, single-instance IPC
  Launchpad.Models/          AppItem, AppSettings, folders, theme enums
  Launchpad.ViewModels/      MainViewModel, SettingsViewModel
  Launchpad.Views/           LaunchpadWindow, desktop dock, dialogs, rendered chrome
  Launchpad.Services/        Scanning, launching, storage, search, widgets, Steam
  Launchpad.Services.Update/ Release metadata + package verification
  Launchpad.Infrastructure/  Easing curves, frame metrics, gestures, logging
  Launchpad.Interop/         DWM, shell links, shell icons, native methods
  resources/                 Shared style and theme resource dictionaries
```

Only one third-party dependency: **[CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)** (MIT), plus the .NET 8 Windows Desktop framework.

## Bundled assets

The repository ships only what we own — the logo and the aurora shader. Two things are deliberately **not** here:

- **Wallpapers.** Use the system wallpaper (blurred, live) or point at any image on disk. Nothing is missing in practice.

- **Icon packs.** Icons are extracted from the executables *you* have installed, so your grid shows icons for apps nobody else has. This also sidesteps redistributing other companies' logos.

Font fallback uses fonts already on Windows. If you add a bundled asset, make sure you hold redistribution rights — see [CONTRIBUTING.md](CONTRIBUTING.md).

## Build it with us

This is a small project and most of the interesting edges are still rough. You don't have to write code to help.

- **Use it and tell us what happened.** A dock that felt great or a theme that glitched tells us more than any benchmark. A screenshot is enough.

- **Report rough edges.** Multi-monitor placement, icon misreads, gesture feel — all worth an issue, however small.

- **Test a setup.** Different DPI scaling, mixed monitors, a non-English Windows, or a machine with a huge library — your configuration isn't the default one.

- **Write code.** Rendering polish, gesture tuning, and platform interop all have room to grow.

- **Help more people read it.** The UI ships English and Chinese today; more translations and clearer docs are welcome.

New contributors start with [CONTRIBUTING.md](CONTRIBUTING.md).

## License

Fluorite is open source under the [Apache License 2.0](LICENSE). Commercial use, modification, and redistribution are all permitted under its terms.

If you fork it, please keep the Fluorite name off your own distribution — that's ordinary trademark hygiene and keeps attribution unambiguous.