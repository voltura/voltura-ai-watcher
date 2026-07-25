Set-StrictMode -Version Latest

function Assert-StableVolturaVersion
{
    param([Parameter(Mandatory = $true)][string]$Version)

    if ($Version -notmatch '^(0|[1-9]\d*)\.(\d)\.(\d)$')
    {
        throw "Version '$Version' must be a stable version with single-digit minor and patch components."
    }
}

function Get-NextVolturaVersion
{
    param([Parameter(Mandatory = $true)][string]$Version)

    Assert-StableVolturaVersion -Version $Version
    $parts = $Version.Split('.')
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2]
    if ($patch -lt 9)
    {
        $patch++
    }
    else
    {
        $patch = 0
        if ($minor -lt 9)
        {
            $minor++
        }
        else
        {
            $minor = 0
            $major++
        }
    }

    return "$major.$minor.$patch"
}

function Get-VolturaReleaseNotesSection
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Version
    )

    Assert-StableVolturaVersion -Version $Version
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf))
    {
        throw "Release notes file was not found: $Path"
    }

    $text = [System.IO.File]::ReadAllText($Path)
    $escapedVersion = [System.Text.RegularExpressions.Regex]::Escape($Version)
    $headingPattern = "(?m)^##[ \t]+v$escapedVersion[ \t]*\r?$"
    $headings = [System.Text.RegularExpressions.Regex]::Matches($text, $headingPattern)
    if ($headings.Count -ne 1)
    {
        throw "Expected exactly one '## v$Version' heading in $Path; found $($headings.Count)."
    }

    $contentStart = $headings[0].Index + $headings[0].Length
    $nextHeadingPattern = [System.Text.RegularExpressions.Regex]::new(
        '(?m)^##[ \t]+v(?:0|[1-9]\d*)\.\d+\.\d+[ \t]*\r?$')
    $nextHeading = $nextHeadingPattern.Match($text, $contentStart)
    $contentEnd = if ($nextHeading.Success) { $nextHeading.Index } else { $text.Length }
    $content = $text.Substring($contentStart, $contentEnd - $contentStart).Trim()
    if ([string]::IsNullOrWhiteSpace($content))
    {
        throw "Release notes for v$Version are empty."
    }

    return $content
}

function Resolve-VolturaReleaseVersion
{
    param(
        [Parameter(Mandatory = $true)][string]$CurrentVersion,
        [string]$LatestReleasedVersion,
        [string]$ExplicitVersion,
        [Parameter(Mandatory = $true)][bool]$CurrentTagExists
    )

    Assert-StableVolturaVersion -Version $CurrentVersion
    if (-not [string]::IsNullOrWhiteSpace($LatestReleasedVersion))
    {
        Assert-StableVolturaVersion -Version $LatestReleasedVersion
    }
    if (-not [string]::IsNullOrWhiteSpace($ExplicitVersion))
    {
        Assert-StableVolturaVersion -Version $ExplicitVersion
        if (-not [string]::IsNullOrWhiteSpace($LatestReleasedVersion) -and
            [System.Version]$ExplicitVersion -le [System.Version]$LatestReleasedVersion)
        {
            throw "Explicit version '$ExplicitVersion' must be newer than '$LatestReleasedVersion'."
        }

        return $ExplicitVersion
    }

    if ([string]::IsNullOrWhiteSpace($LatestReleasedVersion))
    {
        if ($CurrentTagExists)
        {
            throw "Project version '$CurrentVersion' is already tagged, but no published release exists."
        }

        return $CurrentVersion
    }

    if ($CurrentTagExists)
    {
        return Get-NextVolturaVersion -Version $CurrentVersion
    }

    if ([System.Version]$CurrentVersion -gt [System.Version]$LatestReleasedVersion)
    {
        return $CurrentVersion
    }

    throw "Project version '$CurrentVersion' is not a releasable successor to '$LatestReleasedVersion'."
}

Export-ModuleMember -Function Assert-StableVolturaVersion, Get-NextVolturaVersion, Get-VolturaReleaseNotesSection, Resolve-VolturaReleaseVersion
