# AGENTS.md

## Project Notes

- Target framework: `net10.0-windows`.
- UI stack: WPF with a WinForms `NotifyIcon`.
- The app is a read-only local Codex activity watcher. It reads Codex JSONL session data from `CODEX_HOME`, or `%USERPROFILE%\.codex` when that variable is not configured.
- The app starts in the notification area, uses a compact borderless always-on-top window, and does not modify Codex chats.
- Keep the cyberpunk green visual language and avoid stock WinForms styling in the WPF surface or tray menus.
- The Windows-facing assembly name is `Voltura AI Watcher`, producing `Voltura AI Watcher.exe`. Keep installer, publish, shortcut, startup-registration, mutex, and process-name handling aligned with that identity.
- WPF and WinForms types overlap. Fully qualify framework types when ambiguity is possible.

## Monitoring And UI Behavior

- Treat Codex session files as append-only external data. Never write to, truncate, rename, or delete them.
- Keep file monitoring resilient to creation, changes, renames, deletion, truncation, and temporary read-sharing failures.
- Preserve per-thread ordering and deduplication when parsing incremental JSONL updates.
- Human-visible assistant messages, approval requests, user-input requests, work updates, and completion states must remain distinguishable.
- Optional sound is for actionable transitions only: input, approval, or connector waits. Do not play it for ordinary assistant messages, work updates, completion, interruption, or failure.
- Cleared-through state is local app state only and must not alter Codex data.
- Normal close is close-to-tray. Explicit tray Exit is the supported shutdown path.
- The app is single-instance per Windows session. A duplicate process must signal the existing instance and exit.
- Keep the window off the taskbar; the notification-area icon is its persistent shell presence.
- Tray menus and nested submenus must use the custom renderer and remain legible above the topmost window.
- Start with Windows must preserve the exact installed executable path.
- UI and monitoring exceptions should be logged to `%APPDATA%\VolturaAiWatcher\startup.log` without exposing private Codex content unnecessarily.

## Branding And Packaging

- `assets/branding/voltura-ai-watcher-master.png` is the canonical high-resolution branding source.
- Run `scripts/generate-branding.ps1` to regenerate the application icon, README images, and NSIS artwork.
- The ICO must contain 16, 24, 32, 48, and 256 pixel frames.
- NSIS artwork must remain opaque 24-bit BMP at exactly `150x57` and `164x314`.
- `scripts/package-win.ps1` creates both the compact framework-dependent installer and the offline self-contained installer under `artifacts/publish`.
- The compact installer installs the signed .NET 10 Windows Desktop runtime from Microsoft only when it is missing.
- Release binaries are intentionally unsigned; release notes must mention the possible Windows unknown-publisher or SmartScreen warning.

## Versioning And Releases

- The product version is sourced from the single `<Version>` element in `VolturaAiWatcher/VolturaAiWatcher.csproj`.
- Stable versions use the odometer format supported by `scripts/ReleaseTools.psm1`: single-digit minor and patch components, for example `0.1.9` to `0.2.0`.
- `docs/release-notes.md` is mandatory for user-visible changes. Keep newest versions first under one `## v<version>` heading with brief, non-technical bullets users can observe.
- Do not put tests, refactors, build plumbing, workflow changes, or internal implementation details in end-user release notes.
- The local release must fail before changing the version if the target release-note section is missing, duplicated, or empty.
- `scripts/release-local.ps1` is the authoritative one-command release path. It validates, builds, tests, packages, commits any release-generated changes, pushes, audits a draft release, and publishes it as Latest.
- Releases must not rely on GitHub Actions. `.github/workflows/release.yml` is preserved only for possible future use, has no push trigger, and must remain manually disabled for local releases.

## Verification

- Before building, stop any running `Voltura AI Watcher.exe` process if it can lock output files.
- Run `scripts/build.ps1` after code changes.
- Run `scripts/test-release-tools.ps1` after changing version or release-note handling.
- Run `scripts/generate-branding.ps1` and visually inspect generated PNG/BMP assets after branding changes.
- Run `scripts/package-win.ps1` after packaging changes and verify both installers have the expected version metadata.
- After verification, launch the freshly built executable, confirm it remains alive for several seconds, and inspect `%APPDATA%\VolturaAiWatcher\startup.log` after any apparent startup failure.
