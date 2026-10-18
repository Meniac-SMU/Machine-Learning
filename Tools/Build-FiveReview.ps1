[CmdletBinding()]
param([string]$RunName,[string]$Stage='5v5-initial')
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if(!$RunName){$RunName='initial-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')}
if($RunName -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Invalid run name.'}
$buildDir=Join-Path $repoRoot ('Builds/Manager5v5/'+$RunName)
if(Test-Path -LiteralPath $buildDir){throw 'Preserve previous builds; choose a fresh name.'}
$evidence=Join-Path $repoRoot ('Logs/Manager5v5/initial-20261012/'+$RunName)
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$unityPath=Join-Path $repoRoot 'Logs/u21/Editor/Unity.exe'
if(@(Get-Process -ErrorAction SilentlyContinue | Where-Object {$_.ProcessName -eq 'Unity' -and $_.Path -eq $unityPath}).Count){throw 'Editor is still running.'}
$exePath=Join-Path $buildDir 'Soccer5v5.exe'
$logPath=Join-Path $evidence 'build.log'
& (Join-Path $PSScriptRoot 'Save-FiveSource.ps1') -OutputPath (Join-Path $evidence 'source.zip')
$process=Start-Process -FilePath $unityPath -ArgumentList @('-batchmode','-nographics','-projectPath',"`"$repoRoot`"",'-logFile',"`"$logPath`"",'-fiveEvidenceDirectory',"`"$evidence`"",'-executeMethod','MachineLearning.Soccer.Manager5v5.Editor.FiveBuilder.BuildPlayerBatch','-fivePlayerPath',"`"$exePath`"") -WindowStyle Hidden -PassThru
Write-Output ('UNITY_PID='+$process.Id);$process.WaitForExit()
$receipt=[ordered]@{exitCode=$process.ExitCode;endedUtc=[DateTime]::UtcNow.ToString('o');executable=$exePath;log=$logPath;trainingStarted=$false}
$receipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'build-result.json') -Encoding UTF8
if($process.ExitCode -ne 0){throw '5v5 Player build failed; inspect build.log.'}
& (Join-Path $PSScriptRoot 'Build-Lifecycle.ps1') -Action Register -BuildDirectory $buildDir -Stage $Stage -Purpose '규칙형 감독 초기 경기와 화면 검토; 학습 없음' -Evidence $logPath -BuiltAtUtc $receipt.endedUtc
& (Join-Path $PSScriptRoot 'Build-Lifecycle.ps1') -Action Review -Json | Set-Content -LiteralPath (Join-Path $evidence 'build-review.json') -Encoding UTF8
$receipt | ConvertTo-Json
