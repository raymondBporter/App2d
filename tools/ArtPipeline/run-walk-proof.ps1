param(
    [string]$Python = "python",
    [string]$GeneratedFrames = "",
    [string]$MotionReview = ""
)

$ErrorActionPreference = "Stop"
$pipelineRoot = $PSScriptRoot
$repositoryRoot = Split-Path (Split-Path $pipelineRoot -Parent) -Parent
$profile = Join-Path $pipelineRoot "walk-profile.json"
$output = Join-Path $repositoryRoot "Assets\Work\art-pipeline\WalkB"
$generated = Join-Path $output "GeneratedFrames"
$normalized = Join-Path $output "Normalized"

if ([string]::IsNullOrWhiteSpace($GeneratedFrames)) {
    $GeneratedFrames = $generated
}
if ([string]::IsNullOrWhiteSpace($MotionReview)) {
    $MotionReview = Join-Path $output "motion-review.json"
}

& $Python (Join-Path $pipelineRoot "render_walk_guides.py") `
    --profile $profile `
    --output $output

if (-not (Test-Path -LiteralPath $GeneratedFrames -PathType Container)) {
    throw "Generated frame directory not found: $GeneratedFrames. Generate painted-01.png through painted-06.png individually."
}

& $Python (Join-Path $pipelineRoot "normalize_generated_frames.py") `
    --input $GeneratedFrames `
    --profile $profile `
    --output $normalized

& $Python (Join-Path $pipelineRoot "validate_walk_frames.py") `
    --frames $normalized `
    --profile $profile `
    --motion-review $MotionReview `
    --report (Join-Path $output "validation-report.json")

Write-Host "Walk proof passed validation: $normalized"
Write-Host "Review it there, then explicitly promote the chosen frames into Assets\Content or Assets\Library."
