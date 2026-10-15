$ErrorActionPreference = 'Stop'
$ckTrainerRoot = $PSScriptRoot
$ckDotnet = (Get-Command dotnet).Source
$ckSdk = (& $ckDotnet --list-sdks | Select-Object -Last 1)
$ckVersion = $ckSdk.Split(' ')[0]
$ckSdkRoot = ($ckSdk -split '\[')[1].TrimEnd(']')
$ckCompiler = Join-Path $ckSdkRoot "$ckVersion\Roslyn\bincore\csc.dll"
$ckFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$ckArguments = @($ckCompiler, '/nologo', '/noconfig', '/nostdlib+', '/target:winexe', '/platform:x64', '/nowarn:0649', "/out:$ckTrainerRoot\CK3Trainer.exe", "/win32icon:$ckTrainerRoot\assets\trainer.ico", "/win32manifest:$ckTrainerRoot\src\app.manifest")
foreach ($ckLibrary in @('mscorlib','System','System.Core','System.Web.Extensions','System.Drawing','System.Windows.Forms')) { $ckArguments += "/r:$ckFramework\$ckLibrary.dll" }
foreach ($ckSource in @('OccupationCode.cs','NativeCommandCode.cs','AtomicStorage.cs','NativeBridge.cs','MachineCode.cs','ProtectionCode.cs','NativeProtection.cs','WarScoreCode.cs','NativeWarScores.cs','SiegeProgressCode.cs','NativeSieges.cs','CultureFieldCode.cs','NativeCultureHeads.cs','TrainerCore.cs','CampaignEngine.cs','CampaignStories.cs','CampaignModeDashboard.cs','CampaignForm.cs','MultiChoiceDialog.cs','LandDialog.cs','TrainerApp.cs','Lang.cs','LangData.cs')) { $ckArguments += "$ckTrainerRoot\src\$ckSource" }
& $ckDotnet @ckArguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
