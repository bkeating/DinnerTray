# Development

## Source map

- `Program.cs`: single instance, tray menu, capture timer, and app lifetime.
- `BrowserBridge.cs`, `CdpClient.cs`: dedicated browser connection, tab selection, frames, and controls.
- `VideoFrame.js`: chooses a video and draws a 64 × 64 frame; reports unavailable pixels.
- `PlaybackHealth.js`, `PlaybackRecovery.cs`: YouTube health detection and conservative retry decisions.
- `SheepBehavior.cs`: perch decisions, landing reactions, and sprite rendering. Rebuild the embedded `assets/sheep.png` with `python scripts/draw-sheep.py` (Pillow).
- `DesktopSheep.cs`: transparent pet window, drag input, and pet manager.
- `DesktopSurfaces.cs`: native window enumeration and exposed platform segments.
- `DesktopPhysics.cs`, `ThrowGesture.cs`: gravity, platforms, and recent pointer velocity.
- `SlingGame.cs`, `SmashPhysics.cs`: game overlay, slingshot input, blocks, impulses, and debris.
- `AppArtwork.cs`, `AboutForm.cs`: app identity and About dialog.
- `assets/dinnertray.png`: official logo, embedded for the idle tray icon and About dialog and displayed in the README. `assets/dinnertray.ico` contains Windows icon sizes from 16 to 256 pixels for the executable.
- `tests/`: dependency-free C# checks and Node's built-in JavaScript test runner.

## Commands

```powershell
./test.ps1
./build.ps1 -Portable
./package.ps1
```

Scripts use `dotnet` from PATH when an SDK is installed. A local development SDK under `%LOCALAPPDATA%\DinnerTray\dotnet` is a fallback for this project's original development environment. It is not installed automatically or included in source control. Windows Forms rendering checks require Windows, even though the pure physics tests do not.

`build.ps1 -OutputDirectory PATH` selects a build destination. Portable builds include the runtime and are Windows x64. The small default build requires the .NET 10 Desktop Runtime. `package.ps1` creates `artifacts/DinnerTray-VERSION-windows-x64.zip` with only `DinnerTray.exe`, plus a SHA-256 checksum file.

## Debugging switches

- `--no-open`: attach without opening another browser tab.
- `--url URL`: open a specific page at startup.
- `--about`: show About at launch.
- `--diagnostics`: write playback counters and the latest 64 × 64 PNG to `%LOCALAPPDATA%\DinnerTray`.
- `--test-pages`: also permit local HTML test pages.
- `--export-icon`: export the app icon and exit.

Quit an existing instance before starting with different flags. `tests/check-playback.mjs` checks live diagnostics while a moving video is already playing; it is intentionally excluded from unattended CI. `tests/video.html` provides a local synthetic video. Neither synthetic tests nor frame counters prove every external video service works.

## Known limitations

- Ordinary top-level HTML video elements only; no cross-origin iframe or closed-shadow-root player traversal.
- DRM and origin restrictions may block pixel capture. Black encrypted frames are reported as unavailable, which can also briefly occur during an actual black scene.
- Desktop windows are approximated as rectangular frames. Custom-shaped windows, fullscreen applications, virtual desktops, mixed DPI, and unusual taskbars need more interactive testing.
- Block physics is simplified and axis-aligned. It does not simulate rotating rigid bodies.
- Game checks exercise production physics and form rendering without moving the real mouse. Interactive drag/capture behavior still requires a manual smoke test.
- Source builds and release binaries are unsigned; no automatic update mechanism is included.

## Release process

1. Update the version in `DinnerTray.csproj` and add a changelog entry. About reads the assembly version.
2. Run the automated suite and manually check tray playback, dragging, slingshot loading/release, reset, close, and quit.
3. Commit the release and push a matching tag, for example `v1.2.0`.
4. The Windows workflow checks the tag against the project version, runs tests, builds a portable executable, and packages it. A separate job creates a **draft** GitHub release with the ZIP and checksum, using only that job's write permission.
5. Review the draft, testing notes, and assets before publishing it. Do not describe untested desktop behavior as verified.

Regular pushes and pull requests produce downloadable build artifacts but do not create releases. External actions are pinned to commit SHAs; Dependabot proposes updates.
