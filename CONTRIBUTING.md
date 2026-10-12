# Contributing

Thanks for helping out. This file covers the things that are not obvious from
looking at the code.

## Build and run locally

```bash
dotnet build -c Release
dotnet run
```

You'll need the .NET 8 SDK and Windows 10 1809 or newer. The app is x64-only.

## Code style

The codebase is C# 12 with tabs for indentation. Nullable reference types are
**off** (`<Nullable>disable</Nullable>` in the csproj) — don't turn them on as a
drive-by change, the annotations aren't complete and you'll drown in warnings.

Match the surrounding code rather than your own preferences. Naming is
conventional .NET (`_camelCase` fields, `PascalCase` members).

## The MVVM Toolkit source generators are disabled

`Fluorite.csproj` removes the `CommunityToolkit.Mvvm` analyzer after restore. The
property setters that the generators would produce are already written out by
hand in the models, so running the generators makes every one of them a duplicate
definition error.

If you want to convert a hand-written observable property back to an
`[ObservableProperty]` field:

1. Delete the existing property (getter, setter, backing field).
2. Delete the matching `__KnownINotifyProperty*Args` references — they will not
   be regenerated while the analyzers are off.
3. Keep the analyzer disabled.

The attributes themselves are left in place across the models as documentation of
what each field is for.

## Assets

`src/Fluorite/assets/` contains only files we own (logo, shaders). **Do not commit
wallpapers, fonts, or icon packs.** They are excluded by `.gitignore` on purpose:

- **Fonts** must be licensed for redistribution (SIL OFL, Apache-2.0, or an
  explicit commercial grant). Apple system fonts are not redistributable.
- **Wallpapers** must come with the photographer's permission, and Unsplash-style
  licences require attribution that we'd have to carry.
- **Icon packs** made of other companies' logos are trademarked artwork. We
  generate icons from the executables users have installed instead.

If you want to ship a bundled asset, open a PR that includes the licence text and
the source, and add an attribution entry to the README.

## Making a release

1. Bump `<Version>` in `Fluorite.csproj`.
2. Tag: `git tag vX.Y.Z`.
3. Build and smoke-test a self-contained publish:
   `dotnet publish -c Release -r win-x64 --self-contained true -o out`
4. Launch it and confirm the grid, dock, and settings all open.

There is no automated test suite. Manual smoke-testing of the main window,
desktop dock, search, and settings is expected before tagging.

## What needs more care than usual

- `Launchpad.Views/LaunchpadWindow.xaml.cs` is large and owns a lot of Win32
  behaviour (click-through, monitor geometry, animation state). Change it
  carefully and check multi-monitor setups.
- `Launchpad.Infrastructure/` easing curves are tuned by feel. If you change one,
  check the page-flick gesture still feels right.
- Settings are persisted as plain JSON under
  `%LOCALAPPDATA%\Launchpad`. Changing an `AppSettings` property name silently
  drops users' saved values — keep the names stable or write a migration.