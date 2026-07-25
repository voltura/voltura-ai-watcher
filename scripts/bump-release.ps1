[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repoRoot "VolturaAiWatcher\VolturaAiWatcher.csproj"
$project = [xml][System.IO.File]::ReadAllText($projectPath)
$currentVersion = [string]$project.Project.PropertyGroup.Version
Import-Module (Join-Path $PSScriptRoot "ReleaseTools.psm1") -Force
$nextVersion = Get-NextVolturaVersion -Version $currentVersion
Write-Host "Bumping Voltura AI Watcher from $currentVersion to $nextVersion."
& (Join-Path $PSScriptRoot "prepare-release.ps1") $nextVersion
