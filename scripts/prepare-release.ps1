[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$semVerPattern = '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$'
if ($Version -notmatch $semVerPattern) {
    throw "Version '$Version' is not a supported semantic version."
}

$versionCore = ($Version -split '[+-]', 2)[0]
foreach ($part in @($versionCore -split '\.')) {
    if ([int64]$part -gt 65535) {
        throw "Each numeric version part must be between 0 and 65535."
    }
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repoRoot "VolturaAiWatcher\VolturaAiWatcher.csproj"
$content = [System.IO.File]::ReadAllText($projectPath)
$pattern = '(?<prefix><Version>)[^<\r\n]+(?<suffix></Version>)'
$matches = [System.Text.RegularExpressions.Regex]::Matches($content, $pattern)
if ($matches.Count -ne 1) {
    throw "Expected exactly one <Version> element in $projectPath."
}

$updated = [System.Text.RegularExpressions.Regex]::Replace(
    $content,
    $pattern,
    "`${prefix}$Version`${suffix}")
[System.IO.File]::WriteAllText($projectPath, $updated, [System.Text.UTF8Encoding]::new($false))
Write-Host "Prepared Voltura AI Watcher version $Version."
