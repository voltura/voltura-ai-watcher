[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "ReleaseTools.psm1") -Force

function Assert-Equal
{
    param($Expected, $Actual, [string]$Message)
    if ($Expected -ne $Actual)
    {
        throw "$Message Expected '$Expected', received '$Actual'."
    }
}

function Assert-Throws
{
    param([scriptblock]$Action, [string]$Message)
    try
    {
        & $Action
    }
    catch
    {
        return
    }

    throw "$Message Expected an exception."
}

Assert-Equal "0.1.4" (Get-NextVolturaVersion "0.1.3") "Patch increment failed."
Assert-Equal "0.2.0" (Get-NextVolturaVersion "0.1.9") "Minor rollover failed."
Assert-Equal "1.0.0" (Get-NextVolturaVersion "0.9.9") "Major rollover failed."
Assert-Equal "0.1.4" (Resolve-VolturaReleaseVersion "0.1.3" "0.1.3" "" $true) "Released-version bump failed."
Assert-Equal "0.1.4" (Resolve-VolturaReleaseVersion "0.1.4" "0.1.3" "" $false) "Pending-version resume failed."
Assert-Equal "0.2.0" (Resolve-VolturaReleaseVersion "0.1.3" "0.1.3" "0.2.0" $true) "Explicit version failed."
Assert-Equal "0.1.0" (Resolve-VolturaReleaseVersion "0.1.0" "" "" $false) "Initial release failed."
Assert-Throws { Resolve-VolturaReleaseVersion "0.1.3" "0.1.3" "0.1.3" $true } "Old explicit version validation failed."
Assert-Throws { Resolve-VolturaReleaseVersion "0.1.0" "" "" $true } "Initial tagged-version validation failed."

$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("VolturaReleaseTools-" + [System.Guid]::NewGuid().ToString("N"))
[System.IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
try
{
    $notesPath = Join-Path $temporaryDirectory "release-notes.md"
    [System.IO.File]::WriteAllText($notesPath, "## v0.1.4`r`n`r`n- New scrolling.`r`n`r`n## v0.1.3`r`n`r`n- Previous release.`r`n")
    Assert-Equal "- New scrolling." (Get-VolturaReleaseNotesSection $notesPath "0.1.4") "Release-note boundary extraction failed."
    Assert-Equal "- Previous release." (Get-VolturaReleaseNotesSection $notesPath "0.1.3") "Final release-note extraction failed."

    [System.IO.File]::WriteAllText($notesPath, "## v0.1.4`r`n`r`n## v0.1.3`r`n- Previous release.`r`n")
    Assert-Throws { Get-VolturaReleaseNotesSection $notesPath "0.1.4" } "Empty release notes validation failed."

    [System.IO.File]::WriteAllText($notesPath, "## v0.1.4`r`n- One`r`n## v0.1.4`r`n- Two`r`n")
    Assert-Throws { Get-VolturaReleaseNotesSection $notesPath "0.1.4" } "Duplicate heading validation failed."
    Assert-Throws { Get-VolturaReleaseNotesSection $notesPath "0.1.3" } "Missing heading validation failed."
}
finally
{
    Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
}

Write-Host "Local release tool tests passed."
