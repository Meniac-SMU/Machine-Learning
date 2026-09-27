[CmdletBinding()]
param([string]$UnityPath='C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe')
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$stamp=[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$output=Join-Path $projectRoot "Builds/MNG_Exhibition/UI1-$stamp"
$evidence=Join-Path $projectRoot "Logs/Exhibition/build-$stamp"
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$log=Join-Path $evidence 'build.log'
$arguments=@('-batchmode','-nographics','-projectPath',"`"$projectRoot`"",'-executeMethod','MachineLearning.Soccer.Manager.Exhibition.Editor.ExhibitionBuilder.BuildBatch','-exhibitionBuildPath',"`"$output`"",'-logFile',"`"$log`"")
$source=@(Get-ChildItem (Join-Path $projectRoot 'Assets/_Soccer/Manager/Exhibition') -File -Recurse | Get-FileHash -Algorithm SHA256 | Select-Object Path,Hash)
$source | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $evidence 'source-before.json') -Encoding utf8
$process=Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if($process.ExitCode -ne 0){throw "Exhibition build failed ($($process.ExitCode)). See $log"}
if(!(Test-Path (Join-Path $output 'MANAGER.exe'))){throw 'Player missing after build.'}
Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/_Soccer/Manager/Exhibition/Fonts/LICENSE.txt') -Destination (Join-Path $output 'Pretendard-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/soccer/exhibition/player-guide.md') -Destination (Join-Path $output 'README.md')
& (Join-Path $PSScriptRoot 'Build-Lifecycle.ps1') -Action Register -BuildDirectory $output -Stage 'Exhibition UI 1 final' -Purpose 'Graduation exhibition interactive player' -Evidence $log -BuiltAtUtc ([DateTime]::UtcNow.ToString('o'))
& (Join-Path $PSScriptRoot 'Build-Lifecycle.ps1') -Action Review -Json | Set-Content (Join-Path $evidence 'build-retention-review.json') -Encoding utf8
Write-Output "EXHIBITION_BUILD=$output"
Write-Output "EXHIBITION_EVIDENCE=$evidence"
