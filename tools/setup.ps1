#Requires -Version 5.1
<#
.SYNOPSIS
Prepares a fresh clone to run App2d: creates a Python virtual environment, installs the
asset pipeline's dependencies, checks for non-redistributable source packs, and builds
Assets/Runtime. Safe to re-run at any time.
#>
[CmdletBinding()]
param(
    # Python launcher used to create the virtual environment on first run.
    [string]$Python = 'python'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$venv = Join-Path $repo '.venv'
$venvPython = Join-Path $venv 'Scripts\python.exe'
$maaot = Join-Path $repo 'Assets\Sources\third-party\maaot'

$missing = @('dark-cave.zip', 'mossy-cavern.zip') | Where-Object { -not (Test-Path (Join-Path $maaot $_)) }
if ($missing) {
    Write-Host ''
    Write-Host 'Missing Maaot cave packs. Their license forbids redistribution, so download them once:' -ForegroundColor Yellow
    Write-Host '  dark-cave.zip     https://maaot.itch.io/2d-browncave-assets'
    Write-Host '  mossy-cavern.zip  https://maaot.itch.io/mossy-cavern'
    Write-Host "Save them under $maaot and run this script again."
    Write-Host ''
    exit 1
}

if (-not (Test-Path $venvPython)) {
    Write-Host "==> Creating virtual environment at $venv"
    & $Python -m venv $venv
    if ($LASTEXITCODE -ne 0) { throw "Could not create a virtual environment with '$Python'. Install Python 3.12+ or pass -Python <path>." }
}

Write-Host '==> Installing pipeline dependencies'
& $venvPython -m pip install --quiet --disable-pip-version-check -r (Join-Path $repo 'tools\ArtPipeline\requirements.txt')
if ($LASTEXITCODE -ne 0) { throw 'pip install failed.' }

& $venvPython (Join-Path $repo 'tools\ArtPipeline\build_runtime_assets.py')
if ($LASTEXITCODE -ne 0) { throw 'Runtime asset build failed.' }
