$ErrorActionPreference = 'Stop'
$cs2   = 'I:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive'
$addon = 'flowstatehudxml'
$rc    = Join-Path $cs2 'game\bin\win64\resourcecompiler.exe'
$contentRoot = Join-Path $cs2 "content\csgo_addons\$addon\panorama"
$gameRoot    = Join-Path $cs2 "game\csgo_addons\$addon\panorama"
$repo = 'i:\Repos\MapChanger\MapChooser\resources'

$relLayout = 'layout\custom_game\mapchooser\menu.vxml'
$relStyle  = 'styles\custom_game\mapchooser\menu.css'
$relLayoutC = 'layout\custom_game\mapchooser\menu.vxml_c'
$relStyleC  = 'styles\custom_game\mapchooser\menu.vcss_c'

Write-Output "resourcecompiler: $(Test-Path $rc)"
Write-Output "content root:     $contentRoot (exists=$(Test-Path $contentRoot))"
Write-Output "game root:        $gameRoot (exists=$(Test-Path $gameRoot))"

# 1. Sync the repo's current sources into the addon's content tree.
foreach ($pair in @(@($relLayout, $relLayout), @($relStyle, $relStyle))) {
    $src = Join-Path $repo $pair[0]
    $dst = Join-Path $contentRoot $pair[1]
    New-Item -ItemType Directory -Force -Path (Split-Path $dst -Parent) | Out-Null
    Copy-Item $src $dst -Force
    Write-Output "synced source -> $($pair[1])"
}

# 2. Compile the stylesheet first, then the layout that includes it.
foreach ($pair in @(@($relStyle, $relStyleC), @($relLayout, $relLayoutC))) {
    $rel = $pair[0]
    $out = & $rc -f -i (Join-Path $contentRoot $rel) 2>&1
    $compiled = Join-Path $gameRoot $pair[1]
    if ((Test-Path $compiled) -and ($out -match 'OK: 1 compiled|OK: 0 compiled, 0 failed, 1 skipped')) { Write-Output "compiled OK: $rel" }
    else { Write-Output "COMPILE FAILED: $rel"; $out | ForEach-Object { Write-Output "  $_" }; exit 1 }
}

# 3. Report the resulting compiled files in this addon.
foreach ($relC in @($relLayoutC, $relStyleC)) {
    $p = Join-Path $gameRoot $relC
    if (Test-Path $p) { $fi = Get-Item $p; Write-Output ("OUTPUT  {0}  [{1}b]  {2}" -f $fi.LastWriteTime, $fi.Length, $relC) }
    else { Write-Output "MISSING OUTPUT: $relC" }
}
