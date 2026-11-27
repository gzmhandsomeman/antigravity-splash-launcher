param(
    [string]$AsarPath = "",
    [string]$IconPath = ""
)

$ErrorActionPreference = "Stop"

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$Csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$WpfDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"
$FxDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"

if (-not (Test-Path $Csc)) {
    throw "csc.exe not found at $Csc"
}

$IconPng = Join-Path $ProjectDir "antigravity-icon.png"
if ([string]::IsNullOrWhiteSpace($AsarPath)) {
    $AsarPath = Join-Path $env:LOCALAPPDATA "Programs\antigravity\resources\app.asar"
}

function Extract-AntigravityIconFromAsar([string]$AsarFile, [string]$OutPng) {
    $fs = [System.IO.File]::OpenRead($AsarFile)
    $br = New-Object System.IO.BinaryReader($fs)
    try {
        $null = $br.ReadUInt32()
        $headerSize = $br.ReadUInt32()
        $null = $br.ReadUInt32()
        $jsonLen = $br.ReadUInt32()
        $jsonBytes = $br.ReadBytes($jsonLen)
        $dataOffset = [int64]8 + [int64]$headerSize
        $jsonStr = [System.Text.Encoding]::UTF8.GetString($jsonBytes)
        $m = [regex]::Match($jsonStr, '"icon\.png"\s*:\s*\{\s*"size"\s*:\s*(\d+)\s*,\s*"offset"\s*:\s*"(\d+)"')
        if (-not $m.Success) {
            throw "icon.png entry not found in app.asar header"
        }
        $size = [int]$m.Groups[1].Value
        $offset = [int64]$m.Groups[2].Value
        $fs.Position = $dataOffset + $offset
        $pngBytes = $br.ReadBytes($size)
        if ($pngBytes.Length -lt 8 -or $pngBytes[0] -ne 0x89 -or $pngBytes[1] -ne 0x50 -or $pngBytes[2] -ne 0x4E -or $pngBytes[3] -ne 0x47) {
            throw "Extracted icon.png does not have a valid PNG signature"
        }
        [System.IO.File]::WriteAllBytes($OutPng, $pngBytes)
    }
    finally {
        $br.Close()
        $fs.Close()
    }
}

$EmbedIcon = $null
if (-not [string]::IsNullOrWhiteSpace($IconPath) -and (Test-Path $IconPath)) {
    $EmbedIcon = $IconPath
}
else {
    $needExtract = -not (Test-Path $IconPng)
    if ((-not $needExtract) -and (Test-Path $AsarPath)) {
        if ((Get-Item $AsarPath).LastWriteTimeUtc -gt (Get-Item $IconPng).LastWriteTimeUtc) {
            $needExtract = $true
        }
    }

    if ($needExtract -and (Test-Path $AsarPath)) {
        Write-Host "Extracting original 512x512 Antigravity icon from app.asar..."
        Extract-AntigravityIconFromAsar -AsarFile $AsarPath -OutPng $IconPng
    }

    if (Test-Path $IconPng) {
        $EmbedIcon = $IconPng
    }
    else {
        Write-Host "Note: Antigravity app.asar not found; compiling with built-in procedural fallback prism icon."
    }
}

$SourceFiles = @(
    (Join-Path $ProjectDir "AntigravitySplash.cs"),
    (Join-Path $ProjectDir "IconAssetLoader.cs"),
    (Join-Path $ProjectDir "CinematicRenderer.cs"),
    (Join-Path $ProjectDir "AudioSynthesizer.cs")
)
$ManifestFile = Join-Path $ProjectDir "app.manifest"
$OutputExe = Join-Path $ProjectDir "AntigravityLauncher.exe"

$ArgsList = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/platform:x64",
    "/win32manifest:`"$ManifestFile`"",
    "/out:`"$OutputExe`"",
    "/reference:`"$WpfDir\PresentationFramework.dll`"",
    "/reference:`"$WpfDir\PresentationCore.dll`"",
    "/reference:`"$WpfDir\WindowsBase.dll`"",
    "/reference:`"$FxDir\System.Xaml.dll`"",
    "/reference:`"$FxDir\System.Drawing.dll`"",
    "/reference:`"$FxDir\System.dll`"",
    "/reference:`"$FxDir\System.Core.dll`""
)

if ($EmbedIcon) {
    $ArgsList += "/resource:`"$EmbedIcon`",antigravity-icon.png"
}

$ArgsList += ($SourceFiles | ForEach-Object { "`"$_`"" })

Write-Host "Building AntigravityLauncher.exe..."
& $Csc @ArgsList
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

Write-Host "Build succeeded: $OutputExe"
