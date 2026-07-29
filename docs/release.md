# Local Release

Releases are built on the maintainer's Windows machine. The preserved GitHub release workflow is disabled to avoid hosted-runner charges.

## Prerequisites

- Clean `main` worktree with no merge or rebase in progress
- Local branch not behind `origin/main`
- .NET 10 SDK
- NSIS with `makensis.exe`
- Git and an authenticated GitHub CLI with repository and workflow access
- GitHub release workflow in the `disabled_manually` state

## End-user notes

Maintain `docs/release-notes.md` newest first. Each stable version uses one heading such as `## v0.1.4`, followed by short, non-technical bullets about features and fixes users can observe. A release may contain as many bullets as its user-visible changes require, but each feature description must stay brief and high-level. Do not include tests, refactors, build plumbing, workflow changes, or internal implementation details.

The release command refuses to change the project version if the target section is missing, duplicated, or empty.

## One-command release

```powershell
.\scripts\release-full.ps1
```

With no parameters, the command advances the existing odometer version by one step, for example `0.1.9` to `0.2.0`. Before running it, keep the local Git worktree clean and add one non-empty `## v<next-version>` section to `docs/release-notes.md`.

An explicit stable version remains available as an exceptional override:

```powershell
.\scripts\release-full.ps1 -Version 0.2.0
```

The command validates its environment and release notes before changing the project version, regenerates all branding, runs release-tool and .NET tests, builds both installers, commits and pushes the version, rebuilds from the final commit, audits a draft release, and publishes it as Latest. A matching pending version or draft is resumed automatically after an interrupted attempt.

Installer outputs remain under `artifacts\publish`:

- `VolturaAiWatcher-Setup-<version>-win-x64.exe` is the compact framework-dependent installer.
- `VolturaAiWatcher-Setup-<version>-win-x64-full.exe` is the self-contained offline installer.

The command prints both SHA-256 hashes after publication. Release binaries are intentionally unsigned, so Windows may show an unknown-publisher or SmartScreen warning.

## Preserved GitHub workflow

The workflow file remains available for future use but has no push trigger and is disabled remotely. To deliberately restore hosted release builds:

```powershell
gh workflow enable release.yml
gh workflow run release.yml
```

Disable it again after use with `gh workflow disable release.yml`.
