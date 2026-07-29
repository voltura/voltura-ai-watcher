# Voltura AI Watcher

<p align="center">
  <img src="docs/assets/voltura-ai-watcher.png" width="220" alt="Voltura AI Watcher neon logo">
</p>

Voltura AI Watcher is a compact Windows notification-area app that shows the
latest human-visible activity from local Codex chats. It makes working,
completed, approval, and input-needed states easy to spot without changing any
Codex data.

`CODEX_HOME` is used when configured; otherwise the app watches
`%USERPROFILE%\.codex`.

## Features

- Live, read-only monitoring of local Codex chat activity.
- Filters for the messages that need attention.
- Direct navigation back to the related Codex chat.
- Optional sound, minimized startup, and Start with Windows.
- Compact cyberpunk panel with notification-area controls.

## Download

Download the compact online installer or the full offline installer from the
[latest GitHub release](https://github.com/voltura/voltura-ai-watcher/releases/latest).

The installers are not code-signed, so Windows may show an unknown-publisher or
Microsoft Defender SmartScreen warning.

## Build and run

```powershell
.\scripts\build.ps1
.\scripts\run-debug.ps1
```

## Create installers

Install .NET 10 and NSIS 3.12 or later, then run:

```powershell
.\scripts\package-win.ps1
```

The command regenerates branding and creates framework-dependent and offline
self-contained installers under `artifacts\publish`.

## Local release

End-user changes are documented in `docs\release-notes.md`. Releases are built
and published locally without relying on GitHub Actions:

```powershell
.\scripts\release-full.ps1
```

See [the local release guide](docs/release.md) for prerequisites and recovery
behavior.
