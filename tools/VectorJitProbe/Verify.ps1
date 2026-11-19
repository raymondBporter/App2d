param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\..\artifacts\vector-jit'),
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'VectorJitProbe.csproj'
if (-not $NoBuild) {
    & dotnet build $project --configuration Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Could not build the release JIT probe.' }
}

$output = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$assembly = Join-Path $PSScriptRoot 'bin\Release\net10.0-windows10.0.19041.0\VectorJitProbe.dll'
$disassembly = Join-Path $output 'optimized.asm'
Set-Content -LiteralPath $disassembly -Value ''

# Inspect fully optimized JIT code rather than startup tier-0 or precompiled code.
# Restore the caller's environment even when the probe or verification fails.
$jitEnvironment = @{
    DOTNET_TieredCompilation = '0'
    DOTNET_ReadyToRun = '0'
    DOTNET_JitDisasm = 'VectorJitProbe.Probes:*'
    DOTNET_JitDisasmDiffable = '1'
    DOTNET_JitStdOutFile = $disassembly
}
$previousEnvironment = @{}
try {
    foreach ($name in $jitEnvironment.Keys) {
        $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $jitEnvironment[$name], 'Process')
    }
    $runOutput = @(& dotnet exec $assembly)
    if ($LASTEXITCODE -ne 0) { throw 'A vector probe returned an unexpected result.' }
    $runOutput | Set-Content -LiteralPath (Join-Path $output 'run.txt')
    $runOutput | Write-Output
}
finally {
    foreach ($name in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
}

$nativeCode = Get-Content -LiteralPath $disassembly -Raw
if ($nativeCode -notmatch '(?m)^; Emitting .*X64') {
    throw "This verifier expects x64 JIT disassembly. Inspect $disassembly for this architecture."
}

# Every probe must inline completely: no call, no boxing, no runtime dispatch.
$expected = @(
    'F2Length', 'F2Normalized', 'F2Distance', 'F2Clamp', 'F2Cross', 'F2Reject',
    'F2GenericLength', 'F2GenericLerp', 'F3Cross', 'F3Normalized', 'F4Lerp', 'F4Dot',
    'D2Length', 'D2Divide', 'D2Cross', 'D2GenericLength', 'D3Cross', 'D3Distance', 'D4Dot'
)
$verified = @{}
foreach ($block in [regex]::Split($nativeCode, '(?m)^; Assembly listing for method ')) {
    $header = [regex]::Match($block, '^VectorJitProbe\.Probes:(\w+)')
    if (-not $header.Success) { continue }
    $name = $header.Groups[1].Value
    if ($expected -notcontains $name) { continue }
    if ($block -notmatch '\(FullOpts\)') { throw "$name was not fully optimized." }
    if ($block -match 'CORINFO_HELP_(?:BOX|NEW|ALLOC|VIRTUAL|RUNTIMEHANDLE)') {
        throw "$name contains an allocation, boxing, or runtime-dispatch helper. Inspect $disassembly."
    }

    $calls = @(foreach ($line in $block -split '\r?\n') {
        $branch = [regex]::Match($line, '^\s*(call|tail\.jmp|jmp)\s+(\S.*)$')
        if (-not $branch.Success) { continue }
        if ($branch.Groups[1].Value -eq 'jmp' -and $branch.Groups[2].Value -match '^(?:SHORT\s+|NEAR\s+)?G_M') { continue }
        $line.Trim()
    })
    if ($calls.Count -ne 0) {
        throw "$name retained a call instead of inlining (is AggressiveInlining missing on what it calls?): $($calls -join '; ')"
    }
    $size = [regex]::Match($block, '; Total bytes of code (\d+)')
    if (-not $size.Success) { throw "Missing native-code size for $name." }
    $verified[$name] = [pscustomobject]@{ Operation = $name; CodeBytes = [int]$size.Groups[1].Value }
}
foreach ($name in $expected) {
    if (-not $verified.ContainsKey($name)) { throw "No native code was captured for $name." }
}

[pscustomobject]@{
    Runtime = $runOutput -join [Environment]::NewLine
    Architecture = 'X64'
    Probes = $expected.Count
    Operations = @($expected | ForEach-Object { $verified[$_] })
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'verification.json')

Write-Output "Verified $($expected.Count) optimized vector probes: none contains a call, boxing, or runtime dispatch."
Write-Output "Disassembly and verification details: $output"
