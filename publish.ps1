#Requires -Version 5.1
<#
    Builds the portable, self-contained single-file keyboard.

    Output: publish\VirtualKeyboard.exe  (no .NET runtime required on the target PC)
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root 'publish'
}

$project = Join-Path $root 'src\VirtualKeyboard.App\VirtualKeyboard.App.csproj'
if (-not (Test-Path $project)) {
    throw "Project not found: $project"
}

Write-Host 'Publishing Virtual Keyboard (win-x64, self-contained, single file)...' -ForegroundColor Cyan

& dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    --output $OutputDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$exe = Join-Path $OutputDirectory 'VirtualKeyboard.exe'
if (-not (Test-Path $exe)) {
    throw "Publish finished but $exe was not produced."
}

$sizeMb = [math]::Round((Get-Item $exe).Length / 1MB, 1)

# Notify Windows Explorer to refresh the cached .exe icon immediately.
try {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class ShellIconRefresh {
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, string dwItem1, IntPtr dwItem2);
}
"@ -ErrorAction SilentlyContinue
    [ShellIconRefresh]::SHChangeNotify(0x00002000, 0x0005, $exe, [IntPtr]::Zero)
    [ShellIconRefresh]::SHChangeNotify(0x08000000, 0x0000, $null, [IntPtr]::Zero)
    $ie4 = Join-Path $env:SystemRoot 'System32\ie4uinit.exe'
    if (Test-Path $ie4) { Start-Process -FilePath $ie4 -ArgumentList '-show' -WindowStyle Hidden -ErrorAction SilentlyContinue }
}
catch {
    # Icon cache refresh is best-effort.
}

Write-Host ''
Write-Host "Done: $exe ($sizeMb MB)" -ForegroundColor Green
Write-Host 'Copy that single .exe anywhere and run it.'
Write-Host 'settings.json, learned-words.json and wordlists\ are created next to it.'
