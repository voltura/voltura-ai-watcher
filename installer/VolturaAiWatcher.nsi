!ifndef APP_VERSION
  !error "APP_VERSION must be provided"
!endif
!ifndef APP_VERSION_QUAD
  !error "APP_VERSION_QUAD must be provided"
!endif
!ifndef APP_ESTIMATED_SIZE_KB
  !error "APP_ESTIMATED_SIZE_KB must be provided"
!endif
!ifndef PUBLISH_DIR
  !error "PUBLISH_DIR must be provided"
!endif
!ifndef OUTPUT_FILE
  !error "OUTPUT_FILE must be provided"
!endif

!define APP_NAME "Voltura AI Watcher"
!define EXE_NAME "Voltura AI Watcher.exe"
!define PUBLISHER "Voltura AB"
!define DEVELOPER "Joakim Skoglund"
!define PRODUCT_URL "https://github.com/voltura/voltura-ai-watcher"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Voltura AI Watcher"
!define RUN_KEY "Software\Microsoft\Windows\CurrentVersion\Run"
!define RUN_VALUE "VolturaAiWatcher"

!ifdef FRAMEWORK_DEPENDENT
!define INSTALLER_FILE_SUFFIX ""
!define INSTALLER_KIND "Framework-dependent"
!define INSTALLER_WELCOME "Setup installs the compact application build.$\r$\n$\r$\nIf the .NET 10 Windows Desktop runtime is missing, setup downloads the signed Microsoft installer. Internet access and administrator approval may then be required."
!else
!define INSTALLER_FILE_SUFFIX "-full"
!define INSTALLER_KIND "Full"
!define INSTALLER_WELCOME "Setup installs the complete self-contained application for the current Windows user.$\r$\n$\r$\nNo separate .NET runtime download is required."
!endif

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "WinMessages.nsh"

Unicode true
Name "${APP_NAME}"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\${APP_NAME}"
RequestExecutionLevel user
XPStyle on
ManifestDPIAware true
ManifestSupportedOS all
SetCompressor lzma

VIProductVersion "${APP_VERSION_QUAD}"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "CompanyName" "${PUBLISHER}"
VIAddVersionKey "FileDescription" "${APP_NAME} Installer"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "OriginalFilename" "VolturaAiWatcher-Setup-${APP_VERSION}-win-x64${INSTALLER_FILE_SUFFIX}.exe"
VIAddVersionKey "InternalName" "VolturaAiWatcherSetup"
VIAddVersionKey "LegalCopyright" "Copyright (c) ${PUBLISHER}"
VIAddVersionKey "Comments" "Developer: ${DEVELOPER}; Website: ${PRODUCT_URL}"

!define MUI_ABORTWARNING
!define MUI_ICON "${__FILEDIR__}\..\VolturaAiWatcher\Assets\voltura-ai-watcher.ico"
!define MUI_UNICON "${__FILEDIR__}\..\VolturaAiWatcher\Assets\voltura-ai-watcher.ico"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_RIGHT
!define MUI_HEADERIMAGE_BITMAP "${__FILEDIR__}\assets\installer-header.bmp"
!define MUI_HEADERIMAGE_UNBITMAP "${__FILEDIR__}\assets\installer-header.bmp"
!define MUI_WELCOMEFINISHPAGE_BITMAP "${__FILEDIR__}\assets\installer-welcome.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "${__FILEDIR__}\assets\installer-welcome.bmp"
!define MUI_WELCOMEPAGE_TITLE "Install ${APP_NAME} ${APP_VERSION}"
!define MUI_WELCOMEPAGE_TEXT "Install the minimal Codex activity panel.$\r$\n$\r$\n${INSTALLER_WELCOME}"
!define MUI_FINISHPAGE_TITLE "${APP_NAME} is ready"
!define MUI_FINISHPAGE_TEXT "The watcher is installed with its cyberpunk tray controls and Start Menu shortcut."
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXE_NAME}"
!define MUI_FINISHPAGE_RUN_TEXT "Start ${APP_NAME}"
!define MUI_CUSTOMFUNCTION_GUIINIT RestoreInstallerWindow

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function RestoreInstallerWindow
  ShowWindow $HWNDPARENT ${SW_RESTORE}
  BringToFront
FunctionEnd

Section "Install"
  Call PromptCloseRunningApp
  ReadRegStr $R0 HKCU "${RUN_KEY}" "${RUN_VALUE}"

  !ifdef FRAMEWORK_DEPENDENT
  nsExec::ExecToStack '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -Command "$$ErrorActionPreference=$\'Stop$\'; $$runtime=$\'10.0$\'; $$dotnet=Join-Path $$env:ProgramFiles $\'dotnet\dotnet.exe$\'; function Test-Runtime { if (-not (Test-Path -LiteralPath $$dotnet -PathType Leaf)) { return $$false }; $$runtimes=& $$dotnet --list-runtimes; if ($$LASTEXITCODE -ne 0) { return $$false }; return $$runtimes -match ($\'^Microsoft\.WindowsDesktop\.App $\'+[regex]::Escape($$runtime)+$\'\.$\') }; if (-not (Test-Runtime)) { $$installer=Join-Path $$env:TEMP ($\'VolturaAiWatcher-WindowsDesktop-$\'+$$runtime+$\'-win-x64.exe$\'); try { Invoke-WebRequest -Uri ($\'https://aka.ms/dotnet/$\'+$$runtime+$\'/windowsdesktop-runtime-win-x64.exe$\') -OutFile $$installer; $$signature=Get-AuthenticodeSignature -FilePath $$installer; if ($$signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) { throw $\'The downloaded .NET Windows Desktop runtime did not have a valid Authenticode signature.$\' }; $$process=Start-Process -FilePath $$installer -ArgumentList @($\'/install$\',$\'/quiet$\',$\'/norestart$\') -Verb RunAs -Wait -PassThru; if ($$process.ExitCode -notin 0,3010) { throw ($\'.NET runtime installer failed with exit code $\'+$$process.ExitCode) } } finally { Remove-Item -LiteralPath $$installer -Force -ErrorAction SilentlyContinue } }; if (-not (Test-Runtime)) { throw ($\'.NET $\'+$$runtime+$\' Windows Desktop runtime was not available after installation.$\') }"'
  Pop $0
  Pop $1
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "The required .NET 10 Windows Desktop runtime could not be installed.$\r$\n$\r$\nDetails:$\r$\n$1"
    Abort "The required .NET runtime was not installed."
  ${EndIf}
  !endif

  RMDir /r "$INSTDIR"
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*.*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${EXE_NAME}" "" "$INSTDIR\${EXE_NAME}" 0
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\Uninstall.exe"

  ${If} $R0 != ""
    WriteRegStr HKCU "${RUN_KEY}" "${RUN_VALUE}" "$\"$INSTDIR\${EXE_NAME}$\""
  ${EndIf}

  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "${PUBLISHER}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "URLInfoAbout" "${PRODUCT_URL}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "HelpLink" "${PRODUCT_URL}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Comments" "${INSTALLER_KIND} installer"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\${EXE_NAME}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" "$\"$INSTDIR\Uninstall.exe$\""
  WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" "$\"$INSTDIR\Uninstall.exe$\" /S"
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "EstimatedSize" ${APP_ESTIMATED_SIZE_KB}
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  Call un.PromptCloseRunningApp
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  DeleteRegValue HKCU "${RUN_KEY}" "${RUN_VALUE}"
  RMDir /r "$INSTDIR"
SectionEnd

Function PromptCloseRunningApp
  nsExec::ExecToStack '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -Command "if (Get-Process -Name $\'Voltura AI Watcher$\',$\'VolturaAiWatcher$\' -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }"'
  Pop $0
  Pop $1
  ${If} $0 != 0
    Return
  ${EndIf}
  MessageBox MB_ICONEXCLAMATION|MB_OKCANCEL "${APP_NAME} is running. Setup needs to close it before continuing." IDOK install_close IDCANCEL install_cancel
install_close:
  nsExec::ExecToLog '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -Command "Stop-Process -Name $\'Voltura AI Watcher$\',$\'VolturaAiWatcher$\' -Force -ErrorAction SilentlyContinue"'
  Sleep 800
  Return
install_cancel:
  Abort "Setup was canceled because ${APP_NAME} is still running."
FunctionEnd

Function un.PromptCloseRunningApp
  nsExec::ExecToStack '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -Command "if (Get-Process -Name $\'Voltura AI Watcher$\',$\'VolturaAiWatcher$\' -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }"'
  Pop $0
  Pop $1
  ${If} $0 != 0
    Return
  ${EndIf}
  MessageBox MB_ICONEXCLAMATION|MB_OKCANCEL "${APP_NAME} is running. Uninstall needs to close it before continuing." IDOK uninstall_close IDCANCEL uninstall_cancel
uninstall_close:
  nsExec::ExecToLog '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -Command "Stop-Process -Name $\'Voltura AI Watcher$\',$\'VolturaAiWatcher$\' -Force -ErrorAction SilentlyContinue"'
  Sleep 800
  Return
uninstall_cancel:
  Abort "Uninstall was canceled because ${APP_NAME} is still running."
FunctionEnd
