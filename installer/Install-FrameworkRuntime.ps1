[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$runtimeMajorMinor = "10.0"
$dotnetHost = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"

function Test-RequiredRuntime {
    if (-not (Test-Path -LiteralPath $dotnetHost -PathType Leaf)) {
        return $false
    }

    $runtimes = & $dotnetHost --list-runtimes
    if ($LASTEXITCODE -ne 0) {
        return $false
    }

    $desktopPattern = "^Microsoft\.WindowsDesktop\.App $([regex]::Escape($runtimeMajorMinor))\."
    return $runtimes -match $desktopPattern
}

if (-not (Test-RequiredRuntime)) {
    $installerPath = Join-Path $env:TEMP "VolturaDownloadWatcher-WindowsDesktop-$runtimeMajorMinor-win-x64.exe"
    try {
        Invoke-WebRequest `
            -Uri "https://aka.ms/dotnet/$runtimeMajorMinor/windowsdesktop-runtime-win-x64.exe" `
            -OutFile $installerPath

        $signature = Get-AuthenticodeSignature -FilePath $installerPath
        if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
            throw "The downloaded .NET Windows Desktop runtime did not have a valid Authenticode signature."
        }

        $process = Start-Process `
            -FilePath $installerPath `
            -ArgumentList @("/install", "/quiet", "/norestart") `
            -Verb RunAs `
            -Wait `
            -PassThru
        if ($process.ExitCode -notin 0, 3010) {
            throw "The .NET Windows Desktop runtime installer failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        Remove-Item -LiteralPath $installerPath -Force -ErrorAction SilentlyContinue
    }
}

if (-not (Test-RequiredRuntime)) {
    throw ".NET $runtimeMajorMinor Windows Desktop runtime was not available after installation."
}
