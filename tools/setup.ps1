#Requires -Version 5.1
<#
.SYNOPSIS
Optional asset-authoring setup: creates a Python virtual environment, installs
the pipeline dependencies, and regenerates Runtime plus baked Static outputs.
Normal game builds do not need this script. Commit changed Static outputs.
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
