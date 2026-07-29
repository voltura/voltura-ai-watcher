[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$installerPath = Join-Path $repoRoot "installer\VolturaAiWatcher.nsi"
$packageScriptPath = Join-Path $PSScriptRoot "package-win.ps1"
$installer = [System.IO.File]::ReadAllText($installerPath)
$packageScript = [System.IO.File]::ReadAllText($packageScriptPath)

function Assert-True
{
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition)
    {
        throw $Message
    }
}

function Assert-Contains
{
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$Message
    )

    Assert-True -Condition ($Text.IndexOf($Value, [System.StringComparison]::Ordinal) -ge 0) -Message $Message
}

function Assert-NotContains
{
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$Message
    )

    Assert-True -Condition ($Text.IndexOf($Value, [System.StringComparison]::Ordinal) -lt 0) -Message $Message
}

function Get-FunctionBody
{
    param([Parameter(Mandatory = $true)][string]$Name)

    $match = [System.Text.RegularExpressions.Regex]::Match(
        $installer,
        "Function $([System.Text.RegularExpressions.Regex]::Escape($Name))(?<body>[\s\S]*?)FunctionEnd")
    return $match.Groups["body"].Value
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

function Get-PreprocessedInstaller
{
    param([switch]$FrameworkDependent)

    $makeNsis = Find-MakeNsis
    Assert-True -Condition (-not [string]::IsNullOrWhiteSpace($makeNsis)) -Message "NSIS is required for installer preprocessor tests."
    $arguments = @(
        "/SAFEPPO",
        "/DAPP_VERSION=0.1.6",
        "/DAPP_VERSION_QUAD=0.1.6.0",
        "/DAPP_ESTIMATED_SIZE_KB=1",
        "/DPUBLISH_DIR=C:\preprocessor-fixture",
        "/DOUTPUT_FILE=C:\preprocessor-fixture\VolturaAiWatcher.exe"
    )
    if ($FrameworkDependent)
    {
        $arguments += "/DFRAMEWORK_DEPENDENT"
    }
    $arguments += $installerPath
    $output = & $makeNsis @arguments 2>&1
    if ($LASTEXITCODE -ne 0)
    {
        throw "NSIS preprocessing failed with exit code ${LASTEXITCODE}: $($output -join [System.Environment]::NewLine)"
    }

    return $output -join [System.Environment]::NewLine
}

$runtimeValidation = $packageScript.IndexOf('if ($Runtime -cne "win-x64")', [System.StringComparison]::Ordinal)
Assert-True -Condition ($runtimeValidation -ge 0) -Message "Package script must reject unsupported runtimes."
foreach ($operation in @(
    '$projectRoot = [System.IO.Path]::GetFullPath',
    '[System.IO.Directory]::CreateDirectory($publishRoot)',
    'Remove-Item -LiteralPath $resolvedDirectory',
    '& dotnet publish',
    '& $makensisPath @arguments'))
{
    $operationIndex = $packageScript.IndexOf($operation, [System.StringComparison]::Ordinal)
    Assert-True -Condition ($operationIndex -gt $runtimeValidation) -Message "Runtime validation must precede '$operation'."
}
Assert-Contains -Text $packageScript -Value '"/WX"' -Message "NSIS warnings must fail both installer builds."

$full = Get-PreprocessedInstaller
$compact = Get-PreprocessedInstaller -FrameworkDependent
$prerequisiteNames = @("TestRequiredRuntime", "InstallRequiredRuntime", "CleanupRequiredRuntime")
foreach ($name in $prerequisiteNames)
{
    Assert-NotContains -Text $full -Value "Function $name" -Message "Full installer must not define $name."
    Assert-NotContains -Text $full -Value "Call $name" -Message "Full installer must not call $name."
    Assert-Contains -Text $compact -Value "Function $name" -Message "Compact installer must define $name."
}
foreach ($name in @("TestRequiredRuntime", "InstallRequiredRuntime"))
{
    Assert-Contains -Text $compact -Value "Call $name" -Message "Compact installer must call $name."
}

foreach ($requiredText in @(
    "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe",
    '$PLUGINSDIR\VolturaAiWatcher-WindowsDesktop.exe',
    'Invoke-WebRequest -UseBasicParsing -TimeoutSec 300',
    '$$ProgressPreference=$\''SilentlyContinue$\''',
    'Status-ne [System.Management.Automation.SignatureStatus]::Valid',
    '$null-eq $$s.SignerCertificate',
    'O=Microsoft Corporation',
    '/install /quiet /norestart',
    'ShellExecuteEx',
    'WaitForSingleObject',
    'GetExitCodeProcess',
    'SetRebootFlag true',
    'SetErrorLevel 3010'))
{
    Assert-Contains -Text $installer -Value $requiredText -Message "Installer prerequisite contract is missing '$requiredText'."
}
Assert-NotContains -Text $installer -Value "Start-Process -FilePath" -Message "Runtime installation must use direct NSIS elevation."
Assert-NotContains -Text $installer -Value " -File " -Message "Runtime bootstrap must remain an inline PowerShell command."

$commands = [System.Text.RegularExpressions.Regex]::Matches(
    $installer,
    'StrCpy \$2 ''(?<command>[^\r\n]+)''')
Assert-True -Condition ($commands.Count -eq 3) -Message "Expected exactly three staged prerequisite commands."
foreach ($match in $commands)
{
    $expanded = $match.Groups["command"].Value
    foreach ($variable in @('$WINDIR', '$PROGRAMFILES64', '$PLUGINSDIR'))
    {
        $expanded = $expanded.Replace($variable, "C:\$('x' * 236)")
    }
    Assert-True `
        -Condition ($expanded.Length -le 1023) `
        -Message "Worst-case prerequisite command length $($expanded.Length) exceeds the active NSIS capacity."
}
Assert-Contains -Text $installer -Value 'IntCmp $3 ${NSIS_MAX_STRLEN}' -Message "Prerequisite commands must be checked against NSIS_MAX_STRLEN."

$installSection = [System.Text.RegularExpressions.Regex]::Match(
    $installer,
    'Section "Install"(?<body>[\s\S]*?)SectionEnd').Groups["body"].Value
$prerequisiteIndex = $installSection.LastIndexOf("Call InstallRequiredRuntime", [System.StringComparison]::Ordinal)
Assert-True -Condition ($prerequisiteIndex -ge 0) -Message "Install section must acquire the missing runtime."
foreach ($mutation in @('Call PromptCloseRunningApp', 'RMDir /r "$INSTDIR"', 'CreateShortcut', 'WriteRegStr'))
{
    Assert-True `
        -Condition ($prerequisiteIndex -lt $installSection.IndexOf($mutation, [System.StringComparison]::Ordinal)) `
        -Message "Prerequisite installation must finish before '$mutation'."
}

$installBody = Get-FunctionBody -Name "InstallRequiredRuntime"
Assert-True `
    -Condition (([System.Text.RegularExpressions.Regex]::Matches($installBody, "Call CleanupRequiredRuntime")).Count -ge 3) `
    -Message "Every staged runtime path must clean up the downloaded installer."
foreach ($state in @('$0 == 0', '$0 == 3010', '$0 == 51001', '$0 == 51002', 'provisionally complete pending restart'))
{
    Assert-Contains -Text $installBody -Value $state -Message "Runtime state handling is missing '$state'."
}

Assert-Contains -Text $installer -Value "!define MUI_FINISHPAGE_REBOOTLATER_DEFAULT" -Message "Restart-required completion must default to restarting later."
Assert-Contains -Text $compact -Value "IfRebootFlag" -Message "Compact finish page must react to the reboot flag."
Assert-Contains -Text $compact -Value "MUI_TEXT_FINISH_REBOOTLATER" -Message "Compact finish page must expose restart choices."

Write-Host "Installer prerequisite tests passed."
