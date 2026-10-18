$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$rootEvidence=Join-Path $repoRoot 'Logs/Manager5v5/all-command-limits-v10-20261016'
$evidence=Join-Path $rootEvidence 'display-build-r002'
$build=Join-Path $repoRoot 'Builds/Manager5v5/all-command-limits-display-v10-20261016-r002'
if((Test-Path -LiteralPath $build) -or (Test-Path -LiteralPath $evidence)){throw 'Preserve existing build and evidence'}
$unity=Join-Path $repoRoot 'Logs/u21/Editor/Unity.exe'
if(@(Get-Process -ErrorAction SilentlyContinue | Where-Object {$_.ProcessName -eq 'Unity' -and $_.Path -eq $unity}).Count){throw 'Editor is still running'}
New-Item -ItemType Directory -Path $evidence | Out-Null
$exe=Join-Path $build 'Soccer5v5.exe'
$log=Join-Path $evidence 'build.log'
& (Join-Path $PSScriptRoot 'Save-FiveSource.ps1') -OutputPath (Join-Path $evidence 'source-build.zip')
try {
    $p=Start-Process -FilePath $unity -ArgumentList @('-batchmode','-nographics','-projectPath',"`"$repoRoot`"",'-logFile',"`"$log`"",'-executeMethod','MachineLearning.Soccer.Manager5v5.Editor.FiveSpectatorBuilder.BuildBatch','-fivePlayerPath',"`"$exe`"") -WindowStyle Hidden -PassThru
    Write-Output ('UNITY_PID='+$p.Id)
    $p.WaitForExit()
    $receipt=@{exitCode=$p.ExitCode;executable=$exe;endedUtc=[DateTime]::UtcNow.ToString('o');trainingStarted=$false;viewerLaunched=$false}
    $receipt|ConvertTo-Json|Set-Content (Join-Path $evidence 'build-result.json') -Encoding utf8
} finally {
    Copy-Item -LiteralPath (Join-Path $rootEvidence 'ProjectSettings-before.asset') -Destination (Join-Path $repoRoot 'ProjectSettings/ProjectSettings.asset')
}
if($p.ExitCode -ne 0){throw 'Build failed; evidence preserved'}
& (Join-Path $PSScriptRoot 'Build-Lifecycle.ps1') -Action Register -BuildDirectory $build -Stage '5v5-v10-display' -Purpose 'T7 전체 공 회수 최대 2연속, 나머지 명령 최대 3연속 관전 빌드' -Evidence $log -BuiltAtUtc $receipt.endedUtc
