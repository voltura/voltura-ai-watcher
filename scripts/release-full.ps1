[CmdletBinding()]
param(
    [string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repoRoot "VolturaAiWatcher\VolturaAiWatcher.csproj"
$notesPath = Join-Path $repoRoot "docs\release-notes.md"
$runtime = "win-x64"
$appProcessNames = @("Voltura AI Watcher", "VolturaAiWatcher")
$appWasRunning = $false
$releaseSucceeded = $false
$finalExecutable = Join-Path $repoRoot "VolturaAiWatcher\bin\Release\net10.0-windows\Voltura AI Watcher.exe"
$originalLocation = Get-Location
$totalReleaseSteps = 6
$currentReleaseStep = 0
$currentReleaseStepTitle = ""
$releaseStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$stepStopwatch = [System.Diagnostics.Stopwatch]::new()

Import-Module (Join-Path $PSScriptRoot "ReleaseTools.psm1") -Force

function Format-ReleaseDuration
{
    param([Parameter(Mandatory = $true)][System.TimeSpan]$Duration)

    $totalSeconds = [Math]::Max(0, [Math]::Round($Duration.TotalSeconds))
    $hours = [Math]::Floor($totalSeconds / 3600)
    $minutes = [Math]::Floor(($totalSeconds % 3600) / 60)
    $seconds = $totalSeconds % 60
    if ($hours -gt 0)
    {
        return "{0}h {1:00}m {2:00}s" -f $hours, $minutes, $seconds
    }
    if ($minutes -gt 0)
    {
        return "{0}m {1:00}s" -f $minutes, $seconds
    }

    return "{0}s" -f $seconds
}

function Start-ReleaseStep
{
    param(
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][string]$Detail
    )

    $script:currentReleaseStep++
    $script:currentReleaseStepTitle = $Title
    $script:stepStopwatch.Restart()
    Write-Host ""
    Write-Host ("=" * 72) -ForegroundColor Cyan
    Write-Host "Performing step $script:currentReleaseStep out of $script:totalReleaseSteps`: $Title" -ForegroundColor Yellow
    Write-Host "  $Detail"
    Write-Host "  Total elapsed: $(Format-ReleaseDuration $script:releaseStopwatch.Elapsed)" -ForegroundColor DarkGray
    Write-Host ("=" * 72) -ForegroundColor Cyan
}

function Complete-ReleaseStep
{
    $script:stepStopwatch.Stop()
    Write-Host "Step $script:currentReleaseStep completed in $(Format-ReleaseDuration $script:stepStopwatch.Elapsed)." -ForegroundColor Green
}

function Write-ReleaseSuccess
{
    param([Parameter(Mandatory = $true)][string]$Summary)

    Write-Host ""
    Write-Host ("=" * 72) -ForegroundColor Green
    Write-Host "SUCCESS" -ForegroundColor Green
    Write-Host ("=" * 72) -ForegroundColor Green
    Write-Host $Summary
    Write-Host "Total release time: $(Format-ReleaseDuration $script:releaseStopwatch.Elapsed)"
}

function Write-ReleaseIssue
{
    param([Parameter(Mandatory = $true)][System.Management.Automation.ErrorRecord]$ErrorRecord)

    Write-Host ""
    Write-Host ("=" * 72) -ForegroundColor Red
    Write-Host "RED = ISSUE" -ForegroundColor Red
    Write-Host ("=" * 72) -ForegroundColor Red
    if (-not [string]::IsNullOrWhiteSpace($script:currentReleaseStepTitle))
    {
        Write-Host "Stopped during step $script:currentReleaseStep of $script:totalReleaseSteps`: $script:currentReleaseStepTitle"
    }
    Write-Host $ErrorRecord.Exception.Message -ForegroundColor Red
    Write-Host "Total elapsed before issue: $(Format-ReleaseDuration $script:releaseStopwatch.Elapsed)"
}

function Invoke-Checked
{
    param(
        [Parameter(Mandatory = $true)][string]$Command,
        [Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "Command failed with exit code ${LASTEXITCODE}: $Command $($Arguments -join ' ')"
    }
}

function Get-ProjectVersion
{
    $project = [xml][System.IO.File]::ReadAllText($projectPath)
    return [string]$project.Project.PropertyGroup.Version
}

function Get-ReleaseIfPresent
{
    param([Parameter(Mandatory = $true)][string]$Tag)

    $previousErrorPreference = $ErrorActionPreference
    try
    {
        $ErrorActionPreference = "SilentlyContinue"
        $json = & gh release view $Tag --repo $script:repository --json tagName,isDraft,isPrerelease,targetCommitish,url 2>$null
        $releaseViewExitCode = $LASTEXITCODE
    }
    finally
    {
        $ErrorActionPreference = $previousErrorPreference
    }
    if ($releaseViewExitCode -ne 0)
    {
        return $null
    }

    return $json | ConvertFrom-Json
}

function Find-MakeNsis
{
    $command = Get-Command makensis -ErrorAction SilentlyContinue
    if ($null -ne $command)
    {
        return $command.Source
    }

    return @(
        "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
        "$env:ProgramFiles\NSIS\makensis.exe"
    ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}

function Assert-ExpectedTrackedChanges
{
    $allowed = @(
        "VolturaAiWatcher/VolturaAiWatcher.csproj",
        "VolturaAiWatcher/Assets/voltura-ai-watcher.ico",
        "docs/assets/voltura-ai-watcher.png",
        "docs/assets/voltura-ai-watcher-social-preview.png",
        "installer/assets/installer-header.bmp",
        "installer/assets/installer-welcome.bmp"
    )
    $statusLines = @(git status --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0)
    {
        throw "Could not inspect generated release changes."
    }

    $unexpected = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $statusLines)
    {
        if ($line.Length -lt 4)
        {
            continue
        }

        $path = $line.Substring(3).Replace('\', '/')
        if ($allowed -notcontains $path)
        {
            $unexpected.Add($path)
        }
    }

    if ($unexpected.Count -gt 0)
    {
        throw "Release generation changed unexpected paths: $($unexpected -join ', ')"
    }

    return $allowed
}

function Assert-Installer
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ExpectedVersion,
        [Parameter(Mandatory = $true)][long]$MinimumBytes
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf))
    {
        throw "Installer was not created: $Path"
    }

    $file = Get-Item -LiteralPath $Path
    if ($file.Length -lt $MinimumBytes)
    {
        throw "Installer '$($file.Name)' is unexpectedly small: $($file.Length) bytes."
    }
    if (-not $file.VersionInfo.ProductVersion.StartsWith($ExpectedVersion, [System.StringComparison]::Ordinal))
    {
        throw "Installer '$($file.Name)' reports version '$($file.VersionInfo.ProductVersion)', expected '$ExpectedVersion'."
    }
}

function Invoke-ReleasePackaging
{
    param([Parameter(Mandatory = $true)][string]$ReleaseVersion)

    for ($attempt = 1; $attempt -le 3; $attempt++)
    {
        try
        {
            & (Join-Path $PSScriptRoot "package-win.ps1") -Version $ReleaseVersion -Runtime $runtime
            return
        }
        catch
        {
            if ($attempt -eq 3)
            {
                throw
            }

            Write-Warning "Packaging attempt $attempt failed; shutting down build servers before retrying. $($_.Exception.Message)"
            & dotnet build-server shutdown | Out-Host
            Start-Sleep -Seconds 2
        }
    }
}

try
{
    Set-Location $repoRoot
    Start-ReleaseStep `
        -Title "Validating the release environment" `
        -Detail "Checking tools, Git state, GitHub state, the target version, and release notes."
    if ([System.Environment]::OSVersion.Platform -ne [System.PlatformID]::Win32NT)
    {
        throw "Local releases are supported only on Windows."
    }

    & (Join-Path $PSScriptRoot "test-release-tools.ps1")
    & (Join-Path $PSScriptRoot "test-installer-prerequisites.ps1")

    foreach ($requiredCommand in @("git", "dotnet", "gh"))
    {
        if ($null -eq (Get-Command $requiredCommand -ErrorAction SilentlyContinue))
        {
            throw "Required command was not found: $requiredCommand"
        }
    }
    $makeNsis = Find-MakeNsis
    if ([string]::IsNullOrWhiteSpace($makeNsis))
    {
        throw "NSIS makensis.exe was not found."
    }
    if (-not (dotnet --list-sdks | Where-Object { $_ -match '^10\.0\.' } | Select-Object -First 1))
    {
        throw ".NET 10 SDK was not found."
    }

    $initialStatus = @(git status --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0 -or $initialStatus.Count -gt 0)
    {
        throw "The repository must be clean before starting a local release."
    }
    $branch = (git branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0 -or $branch -ne "main")
    {
        throw "Local releases must run from the main branch."
    }
    foreach ($gitState in @("MERGE_HEAD", "rebase-merge", "rebase-apply"))
    {
        $statePath = (git rev-parse --git-path $gitState).Trim()
        if (Test-Path -LiteralPath $statePath)
        {
            throw "A merge or rebase is in progress: $statePath"
        }
    }
    if (-not (git remote get-url origin 2>$null))
    {
        throw "The origin remote is not configured."
    }

    Invoke-Checked gh auth status --hostname github.com
    $script:repository = (gh repo view --json nameWithOwner --jq .nameWithOwner).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($script:repository))
    {
        throw "Could not resolve the GitHub repository."
    }

    $workflowState = (gh api "repos/$script:repository/actions/workflows/release.yml" --jq .state).Trim()
    if ($LASTEXITCODE -ne 0)
    {
        throw "Could not inspect the GitHub release workflow state."
    }
    if ($workflowState -ne "disabled_manually")
    {
        throw "The GitHub release workflow must be disabled before running a local release; current state is '$workflowState'."
    }

    Invoke-Checked git fetch origin main --tags
    Invoke-Checked git merge-base --is-ancestor origin/main HEAD

    $previousErrorPreference = $ErrorActionPreference
    try
    {
        $ErrorActionPreference = "SilentlyContinue"
        $latestTag = (gh api "repos/$script:repository/releases/latest" --jq .tag_name 2>$null).Trim()
        $latestReleaseExitCode = $LASTEXITCODE
    }
    finally
    {
        $ErrorActionPreference = $previousErrorPreference
    }
    if ($latestReleaseExitCode -eq 0)
    {
        if ($latestTag -notmatch '^v(.+)$')
        {
            throw "Latest release tag '$latestTag' is not a supported version tag."
        }
        $latestVersion = $latestTag.Substring(1)
    }
    else
    {
        $latestTag = $null
        $latestVersion = $null
        Write-Host "No published release exists; preparing the first release."
    }
    $currentVersion = Get-ProjectVersion
    $currentTag = "v$currentVersion"
    $currentRelease = Get-ReleaseIfPresent -Tag $currentTag
    $currentTagRemote = git ls-remote --tags origin "refs/tags/$currentTag"
    if ($LASTEXITCODE -ne 0)
    {
        throw "Could not inspect remote tag '$currentTag'."
    }
    $currentTagExists = -not [string]::IsNullOrWhiteSpace(($currentTagRemote -join ""))

    if ($null -ne $currentRelease -and $currentRelease.isDraft -and [string]::IsNullOrWhiteSpace($Version))
    {
        $targetVersion = $currentVersion
    }
    else
    {
        $targetVersion = Resolve-VolturaReleaseVersion `
            -CurrentVersion $currentVersion `
            -LatestReleasedVersion $latestVersion `
            -ExplicitVersion $Version `
            -CurrentTagExists $currentTagExists
    }
    $targetTag = "v$targetVersion"
    $releaseNotes = Get-VolturaReleaseNotesSection -Path $notesPath -Version $targetVersion

    $targetRelease = Get-ReleaseIfPresent -Tag $targetTag
    if ($null -ne $targetRelease -and -not $targetRelease.isDraft)
    {
        throw "Release '$targetTag' is already public."
    }
    Write-Host "  Repository: $script:repository"
    Write-Host "  Current project version: $currentVersion"
    Write-Host "  Latest published release: $(if ($null -eq $latestTag) { 'none' } else { $latestTag })"
    Write-Host "  Target release: $targetTag"
    Complete-ReleaseStep

    Start-ReleaseStep `
        -Title "Preparing release sources" `
        -Detail "Stopping the running watcher, setting the version, and regenerating all branding."
    $runningProcesses = @(Get-Process -Name $appProcessNames -ErrorAction SilentlyContinue)
    $appWasRunning = $runningProcesses.Count -gt 0
    if ($runningProcesses.Count -gt 0)
    {
        $runningProcesses | Stop-Process -Force
        Start-Sleep -Milliseconds 600
    }

    if ($currentVersion -ne $targetVersion)
    {
        & (Join-Path $PSScriptRoot "prepare-release.ps1") $targetVersion
    }
    if ((Get-ProjectVersion) -ne $targetVersion)
    {
        throw "Project version was not prepared as '$targetVersion'."
    }

    & (Join-Path $PSScriptRoot "generate-branding.ps1")
    Complete-ReleaseStep

    Start-ReleaseStep `
        -Title "Testing and creating installation packages" `
        -Detail "Running the release test suite and building both warning-free Windows installers."
    Invoke-Checked dotnet test ".\VolturaAiWatcher.Tests\VolturaAiWatcher.Tests.csproj" --configuration Release
    & dotnet build-server shutdown | Out-Host
    Start-Sleep -Milliseconds 800
    Invoke-ReleasePackaging -ReleaseVersion $targetVersion
    Complete-ReleaseStep

    Start-ReleaseStep `
        -Title "Committing and verifying final artifacts" `
        -Detail "Committing and pushing generated release changes, then rebuilding from the exact release commit."
    $allowedChanges = Assert-ExpectedTrackedChanges
    $gitAddArguments = @("add", "--") + @($allowedChanges)
    Invoke-Checked -Command git -Arguments $gitAddArguments
    git diff --cached --quiet
    $hasReleaseChanges = $LASTEXITCODE -ne 0
    if ($hasReleaseChanges)
    {
        Invoke-Checked git commit -m "Release Voltura AI Watcher $targetVersion"
        Invoke-Checked git push origin main
    }

    $releaseCommit = (git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0)
    {
        throw "Could not resolve the release commit."
    }

    Invoke-ReleasePackaging -ReleaseVersion $targetVersion
    Invoke-Checked dotnet build $projectPath --configuration Release
    $postBuildStatus = @(git status --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0 -or $postBuildStatus.Count -gt 0)
    {
        throw "The repository is not clean after the final release build: $($postBuildStatus -join '; ')"
    }

    $publishRoot = Join-Path $repoRoot "artifacts\publish"
    $smallInstaller = Join-Path $publishRoot "VolturaAiWatcher-Setup-$targetVersion-$runtime.exe"
    $fullInstaller = Join-Path $publishRoot "VolturaAiWatcher-Setup-$targetVersion-$runtime-full.exe"
    Assert-Installer -Path $smallInstaller -ExpectedVersion $targetVersion -MinimumBytes 500000
    Assert-Installer -Path $fullInstaller -ExpectedVersion $targetVersion -MinimumBytes 10000000
    foreach ($publishDirectory in @(
        (Join-Path $publishRoot "VolturaAiWatcher-$runtime-framework-dependent"),
        (Join-Path $publishRoot "VolturaAiWatcher-$runtime-full")))
    {
        $publishedExecutable = Join-Path $publishDirectory "Voltura AI Watcher.exe"
        if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf))
        {
            throw "Published application has the wrong executable identity: $publishedExecutable"
        }
    }

    $smallHash = (Get-FileHash -LiteralPath $smallInstaller -Algorithm SHA256).Hash.ToLowerInvariant()
    $fullHash = (Get-FileHash -LiteralPath $fullInstaller -Algorithm SHA256).Hash.ToLowerInvariant()
    $bodyPath = Join-Path $publishRoot "release-notes-$targetTag.md"
    $body = @"
## What's new

$releaseNotes

## Downloads

- **VolturaAiWatcher-Setup-$targetVersion-$runtime.exe**: compact installer; downloads the .NET 10 Desktop Runtime if required.
- **VolturaAiWatcher-Setup-$targetVersion-$runtime-full.exe**: offline installer with the required runtime bundled.

## Installation note

Windows may display an unsigned-publisher or Microsoft Defender SmartScreen warning because these freeware binaries are not code-signed. Download installers only from this official release page.

Voltura AI Watcher is free software from Voltura AB. Optional support is available through [Ko-fi](https://ko-fi.com/voltura) or [PayPal](https://www.paypal.me/voltura).

$(if ([string]::IsNullOrWhiteSpace($latestTag)) { "**Source:** https://github.com/$script:repository/tree/$targetTag" } else { "**Full changelog:** https://github.com/$script:repository/compare/$latestTag...$targetTag" })
"@
    [System.IO.File]::WriteAllText($bodyPath, $body.Trim() + [System.Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    Write-Host "  Release commit: $releaseCommit"
    Write-Host "  Compact installer: $([System.IO.Path]::GetFileName($smallInstaller)) ($([Math]::Round((Get-Item -LiteralPath $smallInstaller).Length / 1MB, 1)) MB)"
    Write-Host "  Offline installer: $([System.IO.Path]::GetFileName($fullInstaller)) ($([Math]::Round((Get-Item -LiteralPath $fullInstaller).Length / 1MB, 1)) MB)"
    Complete-ReleaseStep

    Start-ReleaseStep `
        -Title "Creating and auditing the GitHub release" `
        -Detail "Uploading the exact installer set and verifying the draft metadata, sizes, and digests."
    $targetRelease = Get-ReleaseIfPresent -Tag $targetTag
    if ($null -eq $targetRelease)
    {
        $createArguments = @(
            "release", "create", $targetTag,
            "--repo", $script:repository,
            "--target", $releaseCommit,
            "--title", "Voltura AI Watcher $targetTag",
            "--draft",
            "--notes-file", $bodyPath)
        if (-not [string]::IsNullOrWhiteSpace($latestTag))
        {
            $createArguments += "--fail-on-no-commits"
        }
        $createArguments += @($smallInstaller, $fullInstaller)
        Invoke-Checked -Command gh -Arguments $createArguments
    }
    else
    {
        if (-not $targetRelease.isDraft -or $targetRelease.targetCommitish -ne $releaseCommit)
        {
            throw "Existing release '$targetTag' is not a matching resumable draft."
        }
        Invoke-Checked gh release edit $targetTag --repo $script:repository --title "Voltura AI Watcher $targetTag" --notes-file $bodyPath
        Invoke-Checked gh release upload $targetTag --repo $script:repository --clobber $smallInstaller $fullInstaller
    }

    $draft = gh release view $targetTag --repo $script:repository --json isDraft,targetCommitish,assets,url | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $draft.isDraft -or $draft.targetCommitish -ne $releaseCommit)
    {
        throw "Draft release audit failed for '$targetTag'."
    }
    $expectedAssetNames = @(
        [System.IO.Path]::GetFileName($smallInstaller),
        [System.IO.Path]::GetFileName($fullInstaller))
    $actualAssetNames = @($draft.assets | ForEach-Object { $_.name } | Sort-Object)
    $expectedAssetKey = ($expectedAssetNames | Sort-Object) -join '|'
    $actualAssetKey = $actualAssetNames -join '|'
    if ($expectedAssetKey -ne $actualAssetKey)
    {
        throw "Draft release assets do not match the expected installer set."
    }
    foreach ($asset in $draft.assets)
    {
        if ($asset.size -le 0 -or [string]::IsNullOrWhiteSpace($asset.digest))
        {
            throw "Release asset '$($asset.name)' has invalid size or digest metadata."
        }
    }
    Complete-ReleaseStep

    Start-ReleaseStep `
        -Title "Publishing GitHub Latest" `
        -Detail "Publishing the audited draft, marking it Latest, and fetching the final release tag."
    Invoke-Checked gh release edit $targetTag --repo $script:repository --draft=false --latest
    $latestPublishedTag = (gh api "repos/$script:repository/releases/latest" --jq .tag_name).Trim()
    if ($LASTEXITCODE -ne 0 -or $latestPublishedTag -ne $targetTag)
    {
        throw "GitHub did not mark '$targetTag' as the latest release."
    }
    Invoke-Checked git fetch origin "refs/tags/$targetTag`:refs/tags/$targetTag"
    Complete-ReleaseStep

    $releaseSucceeded = $true
    Write-Host "$([System.IO.Path]::GetFileName($smallInstaller)) SHA-256 $smallHash"
    Write-Host "$([System.IO.Path]::GetFileName($fullInstaller)) SHA-256 $fullHash"
    Write-ReleaseSuccess "Published as GitHub Latest: https://github.com/$script:repository/releases/tag/$targetTag"
}
catch
{
    Write-ReleaseIssue $_
    throw
}
finally
{
    $releaseStopwatch.Stop()
    Set-Location $originalLocation
    if (($releaseSucceeded -or $appWasRunning) -and (Test-Path -LiteralPath $finalExecutable -PathType Leaf))
    {
        if (@(Get-Process -Name $appProcessNames -ErrorAction SilentlyContinue).Count -eq 0)
        {
            Start-Process -FilePath $finalExecutable
        }
    }
}
