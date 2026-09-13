param(
    [string]$Blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe',
    [switch]$BuildRuntime
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Blender)) { throw "Blender was not found at $Blender. Pass -Blender with its executable path." }
$attackRecipe = Join-Path $PSScriptRoot '..\..\Assets\Sources\characters\player-sword\downward-attack\render.json'
& $Blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python (Join-Path $PSScriptRoot 'render_blender_character.py') -- --config $attackRecipe
if ($LASTEXITCODE -ne 0) { throw 'Blender export failed. See the Blender output above.' }
if ($BuildRuntime) {
    & python (Join-Path $PSScriptRoot 'build_runtime_assets.py')
    if ($LASTEXITCODE -ne 0) { throw 'Runtime asset import failed.' }
}
