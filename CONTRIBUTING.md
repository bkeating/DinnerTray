# Contributing to DinnerTray

Keep it small, playful, and understandable. A single executable and a short tray menu are deliberate product choices.

## Before changing code

- Search existing issues. For a significant feature or a new creature, describe the interaction you want before expanding the UI.
- Include your Windows version, display scaling, monitor layout, app version, and browser version in bug reports.
- For video issues, distinguish a stalled browser player from a frozen tray image. Do not attach cookies, browser profiles, authentication tokens, or private video frames.

## Local workflow

1. Fork and clone the repository. Install the .NET 10 SDK on Windows and Node.js 22+.
2. Make a focused change in a branch.
3. Run `./test.ps1` and `./build.ps1 -Portable`.
4. For desktop behavior, also test the actual app: dragging, throwing, minimized windows, multiple monitors, display scaling, and quitting. Describe what you tested and what you could not.
5. Open a pull request explaining the problem, resulting behavior, and verification. Include a screenshot or short recording when it helps review a visible change.

## Design notes

- Keep video capture, playback recovery, desktop physics, and character rendering independent.
- New characters should implement `IDesktopCreature`; do not copy the desktop movement engine.
- Do not add DRM bypasses, browser security overrides, telemetry, or account requirements.
- Dispose native handles, images, menus, and timers. Desktop helpers should not steal focus or intercept clicks on transparent areas.
- Test meaningful behavior with the existing lightweight test runners. No test framework or extra runtime dependency is needed for the current project.
- Use original artwork or clearly licensed assets and preserve required attribution.

Contributions are provided under the repository's MIT license.
