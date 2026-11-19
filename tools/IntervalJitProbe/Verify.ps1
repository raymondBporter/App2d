param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\..\artifacts\interval-jit'),
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'IntervalJitProbe.csproj'
if (-not $NoBuild) {
    & dotnet build $project --configuration Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'Could not build the release JIT probe.' }
}

$output = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$assembly = Join-Path $PSScriptRoot 'bin\Release\net10.0-windows10.0.19041.0\IntervalJitProbe.dll'
$disassembly = Join-Path $output 'optimized.asm'
Set-Content -LiteralPath $disassembly -Value ''

# Inspect fully optimized JIT code rather than startup tier-0 or precompiled code.
# Restore the caller's environment even when the probe or verification fails.
$jitEnvironment = @{
    DOTNET_TieredCompilation = '0'
    DOTNET_ReadyToRun = '0'
    DOTNET_JitDisasm = 'IntervalJitProbe.Probes:*'
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
    if ($LASTEXITCODE -ne 0) { throw 'An interval probe returned an unexpected result.' }
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

$expected = @(
    'FloatLength', 'DoubleLength', 'IntLength', 'DecimalLength',
    'FloatContains', 'DoubleContains', 'IntContains', 'FloatContainsFromEndpoints',
    'FloatClamp', 'DoubleClamp', 'IntClamp', 'FloatOverlapsFromLimit', 'DoubleOverlaps',
    'FloatContainsInterval', 'FloatIntersect', 'DoubleIntersect', 'FloatHull', 'DoubleHull',
    'FloatDistance', 'DoubleDistance', 'FloatMaxDistance', 'FloatSignedDistance',
    'FloatBounds'
)
$callFree = @(
    'FloatLength', 'DoubleLength', 'IntLength',
    'FloatContains', 'DoubleContains', 'IntContains',
    'DoubleOverlaps', 'FloatContainsInterval',
    'FloatDistance', 'DoubleDistance', 'FloatMaxDistance', 'FloatSignedDistance',
    'FloatBounds'
)

# The remaining operations may call decimal arithmetic, interval construction/validation,
# or Clamp's invalid-endpoint throw path. These are resolved ordinary calls, not interface dispatch.
$allowedCalls = @(
    '^\s*call\s+\[System\.Decimal\+DecCalc:(?:VarDecCmpSub|DecAddSub)\(',
    '^\s*call\s+\[System\.Math:ThrowMinMaxException\[(?:float|double|int)\]\(',
    '^\s*call\s+\[App2d\.Core\.Mathematics\.Interval`1\[(?:float|double)\]:\.ctor\(',
    '^\s*call\s+\[App2d\.Core\.Mathematics\.Interval`1\[(?:float|double)\]:Validate\('
)
$verified = @{}
foreach ($block in [regex]::Split($nativeCode, '(?m)^; Assembly listing for method ')) {
    $header = [regex]::Match($block, '^IntervalJitProbe\.Probes:(\w+)\(')
    if (-not $header.Success) { continue }
    $name = $header.Groups[1].Value
    if ($expected -notcontains $name) { throw "Unexpected probe: $name" }
    if ($block -notmatch '\(FullOpts\)') { throw "$name was not fully optimized." }
    if ($block -match 'CORINFO_HELP_(?:BOX|NEW|ALLOC|VIRTUAL|RUNTIMEHANDLE)') {
        throw "$name contains an allocation, boxing, or runtime-dispatch helper. Inspect $disassembly."
    }

    $calls = @(foreach ($line in $block -split '\r?\n') {
        $branch = [regex]::Match($line, '^\s*(call|tail\.jmp|jmp)\s+(\S.*)$')
        if (-not $branch.Success) { continue }
        $opcode = $branch.Groups[1].Value
        $target = $branch.Groups[2].Value
        if ($opcode -eq 'jmp' -and $target -match '^(?:SHORT\s+|NEAR\s+)?G_M') { continue }
        $line
    })
    if ($callFree -contains $name -and $calls.Count -ne 0) {
        throw "$name retained a call instead of inlining: $($calls -join '; ')"
    }
    foreach ($call in $calls) {
        $permitted = $false
        foreach ($pattern in $allowedCalls) {
            if ($call -match $pattern) { $permitted = $true; break }
        }
        if (-not $permitted) { throw "$name contains an unexpected call or dispatch: $call" }
    }
    $size = [regex]::Match($block, '; Total bytes of code (\d+)')
    if (-not $size.Success) { throw "Missing native-code size for $name." }
    $verified[$name] = [pscustomobject]@{
        Operation = $name
        CodeBytes = [int]$size.Groups[1].Value
        Calls = $calls
    }
}
foreach ($name in $expected) {
    if (-not $verified.ContainsKey($name)) { throw "No native code was captured for $name." }
}

[pscustomobject]@{
    Runtime = $runOutput -join [Environment]::NewLine
    Architecture = 'X64'
    Probes = $expected.Count
    CallFreeProbes = $callFree.Count
    BoxingOrRuntimeDispatchHelpers = 0
    Operations = @($expected | ForEach-Object { $verified[$_] })
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'verification.json')

Write-Output "Verified $($expected.Count) optimized probes: $($callFree.Count) contain no calls; all avoid boxing and runtime query dispatch."
Write-Output "Disassembly and verification details: $output"
