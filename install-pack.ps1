param([string]$GameDirectory)
$ErrorActionPreference = 'Stop'
if (-not $GameDirectory) {
 $steam = (Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
 $libraries = @()
 if ($steam) { $libraries += $steam; $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
  if (Test-Path -LiteralPath $vdf) { foreach ($m in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"(.+?)"')) { $libraries += $m.Groups[1].Value.Replace('\\', '\') } } }
 foreach ($library in $libraries) { $candidate = Join-Path $library 'steamapps\common\Crusader Kings III'; if (Test-Path -LiteralPath (Join-Path $candidate 'binaries\ck3.exe')) { $GameDirectory = $candidate; break } }
 if (-not $GameDirectory) { throw 'CK3 not found. Pass -GameDirectory "path to Crusader Kings III".' }
}
$ckGameRoot = (Resolve-Path -LiteralPath $GameDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $ckGameRoot 'binaries\ck3.exe'))) { throw 'CK3 folder not found / Папка CK3 не найдена.' }
$ckFiles = @(
 @{Source='zz_ck3t_personal_trainer.txt'; Relative='game\common\modifiers\zz_ck3t_personal_trainer.txt'},
 @{Source='ck3t_l_russian.yml'; Relative='game\localization\russian\ck3t\ck3t_l_russian.yml'},
 @{Source='zz_ck3t_v2_modifiers.txt'; Relative='game\common\modifiers\zz_ck3t_v2_modifiers.txt'},
 @{Source='zz_ck3t_v2_traits.txt'; Relative='game\common\traits\zz_ck3t_v2_traits.txt'},
 @{Source='ck3t_v2_l_russian.yml'; Relative='game\localization\russian\ck3t\ck3t_v2_l_russian.yml'},
 @{Source='zz_ck3t_v3_traits.txt'; Relative='game\common\traits\zz_ck3t_v3_traits.txt'},
 @{Source='ck3t_v3_l_russian.yml'; Relative='game\localization\russian\ck3t\ck3t_v3_l_russian.yml'},
 @{Source='ck3t_l_english.yml'; Relative='game\localization\english\ck3t\ck3t_l_english.yml'},
 @{Source='ck3t_v2_l_english.yml'; Relative='game\localization\english\ck3t\ck3t_v2_l_english.yml'},
 @{Source='ck3t_v3_l_english.yml'; Relative='game\localization\english\ck3t\ck3t_v3_l_english.yml'}
)
foreach ($ckFile in $ckFiles) {
 $ckSource = Join-Path $PSScriptRoot "pack\$($ckFile.Source)"
 $ckDestination = [IO.Path]::GetFullPath((Join-Path $ckGameRoot $ckFile.Relative))
 if (-not $ckDestination.StartsWith($ckGameRoot.TrimEnd('\')+'\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid destination path / Некорректный путь назначения.' }
 if ((Test-Path -LiteralPath $ckDestination) -and ((Get-FileHash -LiteralPath $ckDestination).Hash -ne (Get-FileHash -LiteralPath $ckSource).Hash)) { throw "File differs from this pack, overwrite cancelled / Файл отличается от нашего пакета, перезапись отменена: $ckDestination" }
}
foreach ($ckFile in $ckFiles) {
 $ckDestination = Join-Path $ckGameRoot $ckFile.Relative
 New-Item -ItemType Directory -Path (Split-Path $ckDestination) -Force | Out-Null
 Copy-Item -LiteralPath (Join-Path $PSScriptRoot "pack\$($ckFile.Source)") -Destination $ckDestination -Force
}
Write-Host 'Pack installed. If CK3 was running, save the campaign and restart the game manually.'
Write-Host 'Пакет установлен. Если CK3 была запущена, сохрани кампанию и вручную перезапусти игру.'
