# publish.ps1 — build self-contained Windows x64 binaries + a source bundle.
#
# Layout this script expects:
#   <repo-root>\
#     publish.ps1                  ← this script
#     source\                      ← all C# projects + .sln live here
#     UnrealBinaryBuilder.exe      ← produced (lands in repo root)
#     ubb.exe                      ← produced (lands in repo root)
#     UnrealBinaryBuilder-source.zip  ← optional source bundle
#
# Usage:
#   .\publish.ps1                  # default Release build
#   .\publish.ps1 -Configuration Debug
#   .\publish.ps1 -SkipSourceZip
#
[CmdletBinding()]
param(
    [string] $Configuration = "Release",
    [string] $Runtime = "win-x64",
    [switch] $SkipSourceZip,
    [switch] $SkipClean
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root 'source'
Set-Location $root

if (-not (Test-Path $src)) {
    throw "source/ folder not found at $src"
}

if (-not $SkipClean) {
    Write-Host "==> Cleaning bin/obj under source/..." -ForegroundColor Cyan
    Get-ChildItem -Path $src -Recurse -Force -Include 'bin', 'obj' -Directory |
        ForEach-Object { Remove-Item -Recurse -Force $_.FullName }
}

$projects = @(
    @{ Name = 'UnrealBinaryBuilder'; Path = (Join-Path $src 'UnrealBinaryBuilder/UnrealBinaryBuilder.csproj') },
    @{ Name = 'ubb';                 Path = (Join-Path $src 'UnrealBinaryBuilder.Cli/UnrealBinaryBuilder.Cli.csproj') }
)

# Version per commit: 4.<git rev-list --count HEAD>. Falls back to the
# Directory.Build.props default (4.0.0) if git isn't available or the script
# is run outside a repo.
$buildVersion = $null
try {
    $count = (& git -C $root rev-list --count HEAD 2>$null)
    if ($LASTEXITCODE -eq 0 -and $count) {
        $buildVersion = "4.$($count.Trim())"
        Write-Host "==> Publish version: $buildVersion" -ForegroundColor Cyan
    }
}
catch { }
$global:LASTEXITCODE = 0

$staging = Join-Path $env:TEMP "ubb_publish_$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $staging | Out-Null

try {
    foreach ($p in $projects) {
        Write-Host "==> Publishing $($p.Name)..." -ForegroundColor Cyan
        $stage = Join-Path $staging $p.Name
        $publishArgs = @(
            'publish', $p.Path,
            '-c', $Configuration,
            '-r', $Runtime,
            '--self-contained', 'true',
            '-p:PublishSingleFile=true',
            '-p:IncludeNativeLibrariesForSelfExtract=true',
            '-p:DebugType=None',
            '-p:DebugSymbols=false',
            '-o', $stage
        )
        if ($buildVersion) { $publishArgs += "-p:Version=$buildVersion" }
        & dotnet @publishArgs
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed for $($p.Name)"
        }

        Get-ChildItem -Path $stage -File -Filter '*.exe' | ForEach-Object {
            $dest = Join-Path $root $_.Name
            if (Test-Path $dest) {
                try { Remove-Item -Force $dest } catch {
                    Write-Warning "Could not overwrite $dest (in use). Leaving the previous binary in place."
                    return
                }
            }
            Copy-Item $_.FullName -Destination $dest -Force
            Write-Host "    → $dest"
        }
    }
}
finally {
    Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
}

if (-not $SkipSourceZip) {
    Write-Host "==> Bundling source zip..." -ForegroundColor Cyan
    $sourceZip = Join-Path $root 'UnrealBinaryBuilder-source.zip'
    $tmp = Join-Path $env:TEMP "ubb_src_$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $tmp | Out-Null

    # Copy source/ into temp, scrub bin/obj. Use robocopy because Copy-Item -Recurse
    # has known quirks copying directory contents vs the directory itself.
    $tmpSrc = Join-Path $tmp 'source'
    New-Item -ItemType Directory -Path $tmpSrc | Out-Null
    & robocopy $src $tmpSrc /E /XD bin obj .vs | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed (exit $LASTEXITCODE)" }
    $global:LASTEXITCODE = 0  # robocopy uses 0–7 for success

    # Top-level files worth shipping with the source.
    foreach ($f in 'README.md', 'CHANGELOG.md', 'LICENSE.md', 'publish.ps1') {
        $p = Join-Path $root $f
        if (Test-Path $p) { Copy-Item $p $tmp -Force }
    }
    foreach ($d in 'docs', 'examples') {
        $p = Join-Path $root $d
        if (Test-Path $p) { Copy-Item -Recurse -Force $p (Join-Path $tmp $d) }
    }

    if (Test-Path $sourceZip) { Remove-Item -Force $sourceZip }
    Compress-Archive -Path (Join-Path $tmp '*') -DestinationPath $sourceZip
    Remove-Item -Recurse -Force $tmp
    Write-Host "    → $sourceZip"
}

Write-Host ""
Write-Host "==> Done." -ForegroundColor Green
Get-ChildItem -Path $root -File | Where-Object { $_.Extension -in '.exe', '.zip' } |
    Format-Table Name, @{N='Size MB';E={[int]($_.Length/1MB)}}, LastWriteTime
