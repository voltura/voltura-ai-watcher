[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repoRoot "VolturaAiWatcher\VolturaAiWatcher.csproj"
$projectXml = [xml][System.IO.File]::ReadAllText($projectPath)
$projectVersion = [string]$projectXml.Project.PropertyGroup.Version
if ($projectVersion -ne $Version) {
    throw "Git tag version '$Version' does not match project version '$projectVersion'."
}

Write-Host "Validated release version $Version."
