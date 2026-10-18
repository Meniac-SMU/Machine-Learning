$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$evidence=Join-Path $repoRoot 'Logs/Manager5v5/display-v9-2400000-vs-1600000-20261016-r001'
$build=Join-Path $repoRoot 'Builds/Manager5v5/keeper-accel12-display-v9-20261016-r001'
if(Test-Path -LiteralPath $build){throw 'Preserve existing build'}
$exe=Join-Path $build 'Soccer5v5.exe'
$log=Join-Path $evidence 'build.log'
$unity=Join-Path $repoRoot 'Logs/u21/Editor/Unity.exe'
& (Join-Path $PSScriptRoot 'Save-FiveSource.ps1') -OutputPath (Join-Path $evidence 'source-build.zip')
$p=Start-Process -FilePath $unity -ArgumentList @('-batchmode','-nographics','-projectPath',"`"$repoRoot`"",'-logFile',"`"$log`"",'-executeMethod','MachineLearning.Soccer.Manager5v5.Editor.FiveSpectatorBuilder.BuildBatch','-fivePlayerPath',"`"$exe`"") -WindowStyle Hidden -PassThru
Write-Output ('UNITY_PID='+$p.Id)
$p.WaitForExit()
$receipt=@{exitCode=$p.ExitCode;executable=$exe;endedUtc=[DateTime]::UtcNow.ToString('o');trainingStarted=$false}
$receipt|ConvertTo-Json|Set-Content (Join-Path $evidence 'build-result.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $evidence 'ProjectSettings-before.asset') -Destination (Join-Path $repoRoot 'ProjectSettings/ProjectSettings.asset')
if($p.ExitCode -ne 0){throw 'Build failed; evidence preserved'}
& (Join-Path $PSScriptRoot 'Build-Lifecycle.ps1') -Action Register -BuildDirectory $build -Stage '5v5-v9-display' -Purpose 'GK 가속도1.2배 환경의 240만 대160만 1배속300초 관전' -Evidence $log -BuiltAtUtc $receipt.endedUtc
