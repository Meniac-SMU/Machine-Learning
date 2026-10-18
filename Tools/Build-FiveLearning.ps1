[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$RunName, [string]$EvidenceDirectory='Logs/Manager5v5/p8-r1m2-20261013', [string]$Stage='5v5-P8', [string]$Purpose='P8 관측676/행동8/R1+M2 보상·기억 계약과 Python 통신 검증; 학습 없음')
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if($RunName -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Invalid run name.'}
$buildDir=Join-Path $repoRoot "Builds/Manager5v5/$RunName"
$evidence=[IO.Path]::GetFullPath((Join-Path $repoRoot "$EvidenceDirectory/build-$RunName"))
if(!$evidence.StartsWith($repoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence must stay in the project.'}
if((Test-Path $buildDir) -or (Test-Path $evidence)){throw 'Use a fresh build and evidence directory.'}
$unityPath=Join-Path $repoRoot 'Logs/u21/Editor/Unity.exe'
if(@(Get-Process -ErrorAction SilentlyContinue | Where-Object {$_.ProcessName -eq 'Unity' -and $_.Path -eq $unityPath}).Count){throw 'Editor is still running.'}
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$exePath=Join-Path $buildDir 'Soccer5v5Learning.exe'
$logPath=Join-Path $evidence 'build.log'
& (Join-Path $PSScriptRoot 'Save-FiveSource.ps1') -OutputPath (Join-Path $evidence 'source-before.zip')
$process=Start-Process -FilePath $unityPath -ArgumentList @('-batchmode','-nographics','-projectPath',"`"$repoRoot`"",'-logFile',"`"$logPath`"",'-fiveEvidenceDirectory',"`"$evidence`"",'-executeMethod','MachineLearning.Soccer.Manager5v5.Editor.FiveLearningBuilder.BuildBatch','-fivePlayerPath',"`"$exePath`"") -WindowStyle Hidden -PassThru
Write-Output ('UNITY_PID='+$process.Id)
$process.WaitForExit()
$receipt=[ordered]@{exitCode=$process.ExitCode;endedUtc=[DateTime]::UtcNow.ToString('o');executable=$exePath;log=$logPath;trainingStarted=$false}
$receipt | ConvertTo-Json | Set-Content (Join-Path $evidence 'build-result.json') -Encoding utf8
if($process.ExitCode -ne 0){throw 'P8 build failed. Preserve logs and use a new run.'}
& (Join-Path $PSScriptRoot 'Save-FiveSource.ps1') -OutputPath (Join-Path $evidence 'source-built.zip')
& (Join-Path $PSScriptRoot 'Build-Lifecycle.ps1') -Action Register -BuildDirectory $buildDir -Stage $Stage -Purpose $Purpose -Evidence $logPath -BuiltAtUtc $receipt.endedUtc
& (Join-Path $PSScriptRoot 'Build-Lifecycle.ps1') -Action Review -Json | Set-Content (Join-Path $evidence 'build-review.json') -Encoding utf8
$receipt | ConvertTo-Json
