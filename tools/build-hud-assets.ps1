# Rebuilds the MapChanger HudKit menu panorama assets from the repo sources and stages the
# compiled output everywhere it must resolve:
#   1. <cs2-client>\game\csgo\panorama\...              (your local game client renders this)
#   2. <server>\game\csgo\panorama\...                  (dedicated server content for clients)
#   3. <server>\...\plugins\MapChanger\resources\...    (plugin folder copy, next to MapChanger.dll)
#
# The C# template (MapChooserHudTemplate) targets LayoutName "mapchooser/menu", RootId
# "mapchooser-root", and clickable rows option_0..option_7. The layout it loads is the COMPILED
# resource panorama/layout/custom_game/mapchooser/menu.vxml_c, so the .vxml/.vcss sources here
# MUST be compiled with the CS2 resourcecompiler before they do anything — shipping raw sources
# silently renders nothing.
#
#   pwsh -File tools/build-hud-assets.ps1
#
# Point the paths at your own installs if they differ.

[CmdletBinding()]
param(
    [string] $Cs2Client = 'I:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive',
    [string] $Cs2Server = 'I:\cs2-local-server',
    [string] $RepoRoot  = "$PSScriptRoot\..",
    [switch] $SkipServer
)

$ErrorActionPreference = 'Stop'

$repoRoot   = (Resolve-Path $RepoRoot).Path
$srcLayout  = Join-Path $repoRoot 'resources\layout\custom_game\mapchooser\menu.vxml'
$srcStyle   = Join-Path $repoRoot 'resources\styles\custom_game\mapchooser\menu.css'
$rc         = Join-Path $Cs2Client 'game\bin\win64\resourcecompiler.exe'

# Compile staging addon (its game/ output is where the compiled _c files land).
$addon        = 'mapchooser_compile'
$contentRoot  = Join-Path $Cs2Client "content\csgo_addons\$addon\panorama"
$compiledRoot = Join-Path $Cs2Client "game\csgo_addons\$addon\panorama"

foreach ($f in @($srcLayout, $srcStyle, $rc)) {
    if (-not (Test-Path $f)) { throw "Required file not found: $f" }
}

$relLayout = 'layout\custom_game\mapchooser\menu.vxml'
$relStyle  = 'styles\custom_game\mapchooser\menu.css'
$relLayoutC = 'layout\custom_game\mapchooser\menu.vxml_c'
$relStyleC  = 'styles\custom_game\mapchooser\menu.vcss_c'

# ── 1. Sync sources into the compile addon's content tree ────────────────────
foreach ($pair in @(@($srcLayout, $relLayout), @($srcStyle, $relStyle))) {
    $dst = Join-Path $contentRoot $pair[1]
    New-Item -ItemType Directory -Force -Path (Split-Path $dst -Parent) | Out-Null
    Copy-Item $pair[0] $dst -Force
}
Write-Host "Synced sources into $contentRoot"

# ── 2. Compile ───────────────────────────────────────────────────────────────
foreach ($pair in @(@($relStyle, $relStyleC), @($relLayout, $relLayoutC))) {
    $rel = $pair[0]
    $out = & $rc -f -i (Join-Path $contentRoot $rel) 2>&1
    if ((Test-Path (Join-Path $compiledRoot $pair[1])) -and ($out -match 'OK: 1 compiled|OK: 0 compiled, 0 failed, 1 skipped')) { Write-Host "compiled OK: $rel" }
    else { Write-Host "COMPILE FAILED: $rel`n$out"; exit 1 }
}

foreach ($relC in @($relLayoutC, $relStyleC)) {
    if (-not (Test-Path (Join-Path $compiledRoot $relC))) { throw "Expected compiled output missing: $relC" }
}

# ── 3. Mirror compiled output to every mount point ───────────────────────────
$targets = [System.Collections.Generic.List[string]]::new()
$targets.Add((Join-Path $Cs2Client 'game\csgo\panorama'))
if (-not $SkipServer) {
    $targets.Add((Join-Path $Cs2Server 'game\csgo\panorama'))
    $targets.Add((Join-Path $Cs2Server 'game\csgo\addons\swiftlys2\plugins\MapChanger\resources'))
}

foreach ($t in $targets) {
    foreach ($pair in @(@($relLayoutC, $relLayoutC), @($relStyleC, $relStyleC))) {
        $dst = Join-Path $t $pair[1]
        New-Item -ItemType Directory -Force -Path (Split-Path $dst -Parent) | Out-Null
        Copy-Item (Join-Path $compiledRoot $pair[0]) $dst -Force
    }
    # Keep the raw sources alongside the compiled files in the plugin folder (matches flowtimer).
    if ($t -match 'plugins\\MapChanger') {
        Copy-Item $srcLayout (Join-Path $t 'layout\custom_game\mapchooser\menu.vxml') -Force
        Copy-Item $srcStyle  (Join-Path $t 'styles\custom_game\mapchooser\menu.css') -Force
    }
    Write-Host "staged -> $t"
}

Write-Host "`nDone. Restart the server (and reconnect your client) so the new resources load."
