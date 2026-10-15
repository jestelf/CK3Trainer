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
$ckOwnedPaths = @()
foreach ($ckFile in $ckFiles) {
 $ckDestination = [IO.Path]::GetFullPath((Join-Path $ckGameRoot $ckFile.Relative))
 if (-not $ckDestination.StartsWith($ckGameRoot.TrimEnd('\')+'\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid removal path / Некорректный путь удаления.' }
 if (Test-Path -LiteralPath $ckDestination) {
  if ((Get-FileHash -LiteralPath $ckDestination).Hash -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot "pack\$($ckFile.Source)")).Hash) { throw "Modified file is not removed / Изменённый файл не удаляется: $ckDestination" }
  $ckOwnedPaths += $ckDestination
 }
}
foreach ($ckOwnedPath in $ckOwnedPaths) { Remove-Item -LiteralPath $ckOwnedPath }
Write-Host 'Only the pack files were removed. Remove bonuses with the trainer button and save the campaign first. Permanent changes in the save are not undone.'
Write-Host 'Удалены только файлы пакета. Перед удалением сними бонусы кнопкой трейнера и сохрани кампанию. Постоянные изменения в сохранении не отменяются.'
