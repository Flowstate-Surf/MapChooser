$ErrorActionPreference = 'Stop'
function Get-Tokens($path) {
    if (-not (Test-Path $path)) { return @('<MISSING>') }
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $text = [System.Text.Encoding]::ASCII.GetString($bytes)
    return [regex]::Matches($text, '[a-zA-Z0-9_:/\.\- ]{5,}') | ForEach-Object { $_.Value }
}
foreach ($p in @(
    'I:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game\csgo\panorama\layout\custom_game\mapchooser\menu.vxml_c',
    'I:\cs2-local-server\game\csgo\panorama\layout\custom_game\mapchooser\menu.vxml_c',
    'I:\cs2-local-server\game\csgo\addons\swiftlys2\plugins\MapChanger\resources\layout\custom_game\mapchooser\menu.vxml_c')) {
    $toks = Get-Tokens $p
    $hasOpt0 = $toks -contains 'option_0'
    $hasRoot = [bool]($toks | Select-String 'mapchooser-root')
    $hint = ($toks | Select-String 'CLICK AN OPTION|TAB  CLOSE|W / S  SELECT' | Select-Object -First 1)
    if (Test-Path $p) { $lw = (Get-Item $p).LastWriteTime } else { $lw = 'N/A' }
    Write-Output ("{0}`n   lastwrite={1}  option_0={2}  rootId={3}  hint={4}" -f $p, $lw, $hasOpt0, $hasRoot, $hint)
}
