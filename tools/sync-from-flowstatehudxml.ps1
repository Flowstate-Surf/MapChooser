$ErrorActionPreference = 'Stop'
$game = 'I:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game\csgo_addons\flowstatehudxml\panorama'
$dsts = @(
  'I:\cs2-local-server\game\csgo\panorama',
  'I:\cs2-local-server\game\csgo\addons\swiftlys2\plugins\MapChanger\resources'
)
$rels = @(
  'layout\custom_game\mapchooser\menu.vxml_c',
  'styles\custom_game\mapchooser\menu.vcss_c',
  'layout\custom_game\mapchooser\menu.vxml',
  'styles\custom_game\mapchooser\menu.css'
)
foreach ($d in $dsts) {
  foreach ($rel in $rels) {
    $s = Join-Path $game $rel
    $t = Join-Path $d $rel
    New-Item -ItemType Directory -Force -Path (Split-Path $t -Parent) | Out-Null
    Copy-Item $s $t -Force
  }
  Write-Output "staged -> $d"
}
# Re-verify parity on the compiled pair
foreach ($rel in $rels[0..1]) {
  $sh = (Get-FileHash (Join-Path $game $rel)).Hash
  foreach ($d in $dsts) {
    $match = $sh -eq (Get-FileHash (Join-Path $d $rel)).Hash
    Write-Output ("match={0}  {1}  <- {2}" -f $match, $rel, $d)
  }
}
