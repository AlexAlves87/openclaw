# Command Palette Development Script
# Usage: .\tools\cmdpal-dev.ps1 [build|deploy|remove|cycle]

param(
    [Parameter(Position=0)]
    [ValidateSet('build', 'deploy', 'remove', 'cycle', 'status')]
    [string]$Action = 'cycle'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectPath = "$RepoRoot\src\OpenClaw.CommandPalette"

# Detect architecture
$Arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x64' }
$OutputPath = "$ProjectPath\bin\$Arch\Debug\net10.0-windows10.0.26100.0\win-$Arch"

function Write-Step($msg) { Write-Host "`n>> $msg" -ForegroundColor Cyan }

function Get-InstalledPackage {
    Get-AppxPackage -Name "*OpenClaw*" 2>$null
}

function Remove-ExtensionPackage {
    $pkg = Get-InstalledPackage
    if ($pkg) {
        Write-Step "Removing $($pkg.PackageFullName)..."
        Remove-AppxPackage -Package $pkg.PackageFullName
        Write-Host "Removed!" -ForegroundColor Green
    } else {
        Write-Host "Not installed" -ForegroundColor Yellow
    }
}

function Build-Extension {
    Write-Step "Building Command Palette ($Arch)..."
    Push-Location $RepoRoot
    dotnet build src/OpenClaw.CommandPalette -c Debug -p:Platform=$Arch
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    Pop-Location
    Write-Host "Build succeeded!" -ForegroundColor Green
}

function Deploy-Extension {
    Write-Step "Deploying extension..."
    
    $manifest = "$OutputPath\AppxManifest.xml"
    if (-not (Test-Path $manifest)) {
        throw "AppxManifest.xml not found at $manifest - did you build first?"
    }
    
    # Register for development (loose files, no MSIX needed)
    Add-AppxPackage -Register $manifest -ForceApplicationShutdown
    Write-Host "Deployed! Open Command Palette, type 'Reload', then 'OpenClaw'" -ForegroundColor Green
}

function Show-Status {
    Write-Step "Extension Status (Platform: $Arch)"
    $pkg = Get-InstalledPackage
    if ($pkg) {
        Write-Host "Installed: $($pkg.PackageFullName)" -ForegroundColor Green
        Write-Host "Version:   $($pkg.Version)"
        Write-Host "Location:  $($pkg.InstallLocation)"
    } else {
        Write-Host "Not installed" -ForegroundColor Yellow
    }
}

# Main
switch ($Action) {
    'build'  { Build-Extension }
    'deploy' { Deploy-Extension }
    'remove' { Remove-ExtensionPackage }
    'status' { Show-Status }
    'cycle'  {
        Remove-ExtensionPackage
        Build-Extension
        Deploy-Extension
        Write-Host "`n=== In Command Palette: type 'Reload' then 'OpenClaw' ===" -ForegroundColor Magenta
    }
}
