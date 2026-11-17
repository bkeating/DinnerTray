# Security

DinnerTray is experimental desktop software. Security fixes target the current source and latest release; older versions are not separately maintained.

## Reporting a vulnerability

Use GitHub's **Security → Report a vulnerability** option if private reporting is available. If it is not, open an issue requesting a private contact channel without including exploit instructions, credentials, or sensitive data. Do not post browser profiles, cookies, DevTools endpoints, or diagnostic video frames publicly.

## Security boundaries

- DinnerTray launches a dedicated Chrome/Edge profile with a local DevTools endpoint. Other software running as you may be able to control that browser; keep unrelated sensitive browsing in your normal profile.
- It does not attach to your default browser profile, bypass encrypted-video protections, or disable origin restrictions.
- Playback recovery is scoped to the followed YouTube tab and rate-limited. It does not resolve authentication, age restrictions, or unavailable content.
- Release builds are currently unsigned. Download only from the project's repository/releases or build from source. Published checksums identify bytes; they are not a code-signing certificate.
- CI runs pull-request code with read-only repository permissions and no release credentials. Creating a draft release requires a version tag on a push event.
