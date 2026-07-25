[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repoRoot "VolturaAiWatcher\VolturaAiWatcher.csproj"

& dotnet run --project $projectPath --configuration Debug
if ($LASTEXITCODE -ne 0)
{
    throw "Debug run failed with exit code $LASTEXITCODE."
}
