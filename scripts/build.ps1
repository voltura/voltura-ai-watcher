[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$appProject = Join-Path $projectRoot "VolturaAiWatcher\VolturaAiWatcher.csproj"
$testProject = Join-Path $projectRoot "VolturaAiWatcher.Tests\VolturaAiWatcher.Tests.csproj"

& dotnet build $appProject -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Application build failed with exit code $LASTEXITCODE."
}

& dotnet test $testProject -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Tests failed with exit code $LASTEXITCODE."
}
