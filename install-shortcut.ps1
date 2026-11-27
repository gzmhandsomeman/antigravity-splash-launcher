param(
    [switch]$Fast,
    [switch]$Fullscreen,
    [switch]$Windowed,
    [switch]$Sound,
    [switch]$ReplaceDefault,
    [switch]$Taskbar,
    [switch]$StartMenu,
    [switch]$All,
    [switch]$RestoreDefault,
    [string]$TargetExe = ""
)

$ErrorActionPreference = "Stop"

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$LauncherExe = Join-Path $ProjectDir "AntigravityLauncher.exe"
$AntigravityExe = if (-not [string]::IsNullOrWhiteSpace($TargetExe)) {
    $TargetExe
} else {
    Join-Path $env:LOCALAPPDATA "Programs\antigravity\Antigravity.exe"
}

$DesktopDir = [Environment]::GetFolderPath("Desktop")
$ZeroGShortcut = Join-Path $DesktopDir "Antigravity (Zero-G).lnk"
$DefaultShortcut = Join-Path $DesktopDir "Antigravity.lnk"
$TaskbarShortcut = Join-Path $env:APPDATA "Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\Antigravity.lnk"
$StartMenuShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Antigravity.lnk"

$BackupDesktop = Join-Path $ProjectDir "Antigravity.lnk.bak"
$BackupTaskbar = Join-Path $ProjectDir "TaskBar-Antigravity.lnk.bak"
$BackupStartMenu = Join-Path $ProjectDir "StartMenu-Antigravity.lnk.bak"

if ($RestoreDefault) {
    $restored = 0
    if (Test-Path $BackupDesktop) {
        Copy-Item -Path $BackupDesktop -Destination $DefaultShortcut -Force
        Write-Host "Restored Desktop shortcut: $DefaultShortcut"
        $restored++
    }
    if (Test-Path $BackupTaskbar) {
        Copy-Item -Path $BackupTaskbar -Destination $TaskbarShortcut -Force
        Write-Host "Restored Taskbar shortcut: $TaskbarShortcut"
        $restored++
    }
    if (Test-Path $BackupStartMenu) {
        Copy-Item -Path $BackupStartMenu -Destination $StartMenuShortcut -Force
        Write-Host "Restored Start Menu shortcut: $StartMenuShortcut"
        $restored++
    }
    if ($restored -eq 0) {
        throw "No backup shortcuts found in $ProjectDir"
    }
    exit 0
}

if (-not (Test-Path $LauncherExe)) {
    throw "AntigravityLauncher.exe not found. Run .\build.ps1 first."
}

$LaunchFlags = @()
if ($Fast) { $LaunchFlags += "--fast" }
if ($Windowed) { $LaunchFlags += "--windowed" }
elseif ($Fullscreen) { $LaunchFlags += "--fullscreen" }
if ($Sound) { $LaunchFlags += "--sound" }
if (-not [string]::IsNullOrWhiteSpace($TargetExe)) { $LaunchFlags += "--target `"$TargetExe`"" }
$ArgsString = $LaunchFlags -join " "

$WshShell = New-Object -ComObject WScript.Shell

function Write-Shortcut([string]$Path, [string]$Arguments) {
    $Lnk = $WshShell.CreateShortcut($Path)
    $Lnk.TargetPath = $LauncherExe
    $Lnk.Arguments = $Arguments
    $Lnk.WorkingDirectory = $ProjectDir
    if (Test-Path $AntigravityExe) {
        $Lnk.IconLocation = "$AntigravityExe,0"
    }
    $Lnk.Description = "Launch Antigravity with 15s Fullscreen Zero-G Cinematic Splash"
    $Lnk.Save()
}

function Backup-And-Update([string]$ShortcutPath, [string]$BackupPath, [string]$Label) {
    if (-not (Test-Path $ShortcutPath)) {
        Write-Host "Skipped $Label (shortcut not found at $ShortcutPath)"
        return
    }
    if (-not (Test-Path $BackupPath)) {
        Copy-Item -Path $ShortcutPath -Destination $BackupPath -Force
        Write-Host "Backed up $Label shortcut to: $BackupPath"
    }
    Write-Shortcut -Path $ShortcutPath -Arguments $ArgsString
    Write-Host "Updated $Label shortcut: $ShortcutPath"
}

if ($All) {
    $ReplaceDefault = $true
    $Taskbar = $true
    $StartMenu = $true
}

if ($ReplaceDefault) {
    Backup-And-Update -ShortcutPath $DefaultShortcut -BackupPath $BackupDesktop -Label "Desktop"
}
if ($Taskbar) {
    Backup-And-Update -ShortcutPath $TaskbarShortcut -BackupPath $BackupTaskbar -Label "Taskbar"
}
if ($StartMenu) {
    Backup-And-Update -ShortcutPath $StartMenuShortcut -BackupPath $BackupStartMenu -Label "Start Menu"
}

if (-not ($ReplaceDefault -or $Taskbar -or $StartMenu)) {
    Write-Shortcut -Path $ZeroGShortcut -Arguments $ArgsString
    Write-Host "Created desktop shortcut: $ZeroGShortcut"
}
