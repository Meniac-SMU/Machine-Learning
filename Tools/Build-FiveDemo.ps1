param([string]$RunName=('saiccer-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')))
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$build=Join-Path $repo "Builds/Manager5v5/$RunName"
$evidence=Join-Path $repo "Logs/Manager5v5/$RunName-build"
if((Test-Path -LiteralPath $build) -or (Test-Path -LiteralPath $evidence)){throw 'Use fresh build/evidence paths'}
$unity=Join-Path $repo 'Logs/u21/Editor/Unity.exe'
New-Item -ItemType Directory -Path $evidence | Out-Null
& (Join-Path $PSScriptRoot 'Save-FiveSource.ps1') -OutputPath (Join-Path $evidence 'source-build.zip')
Copy-Item -LiteralPath (Join-Path $repo 'ProjectSettings/ProjectSettings.asset') -Destination (Join-Path $evidence 'ProjectSettings-before.asset')
Copy-Item -LiteralPath (Join-Path $repo 'ProjectSettings/EditorBuildSettings.asset') -Destination (Join-Path $evidence 'EditorBuildSettings-before.asset')
$log=Join-Path $evidence 'build.log'
try {
    $p=Start-Process -FilePath $unity -ArgumentList @('-batchmode','-nographics','-projectPath',"`"$repo`"",'-logFile',"`"$log`"",'-executeMethod','MachineLearning.Soccer.Manager5v5.Demo.Editor.FiveDemoBuilder.BuildBatch','-fivePlayerPath',"`"$build/SAICCER.exe`"") -WindowStyle Hidden -PassThru
    Write-Output "UNITY_PID=$($p.Id)"
    $p.WaitForExit()
    @{exitCode=$p.ExitCode;build=$build;endedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $evidence 'build-result.json') -Encoding utf8
} finally {
    Copy-Item -LiteralPath (Join-Path $evidence 'ProjectSettings-before.asset') -Destination (Join-Path $repo 'ProjectSettings/ProjectSettings.asset')
    Copy-Item -LiteralPath (Join-Path $evidence 'EditorBuildSettings-before.asset') -Destination (Join-Path $repo 'ProjectSettings/EditorBuildSettings.asset')
}
if($p.ExitCode -ne 0){throw "Build failed: $log"}
@('@echo off','"%~dp0SAICCER.exe" -force-d3d11 %*') | Set-Content (Join-Path $build 'Play-SAICCER.cmd') -Encoding ascii
@('SAICCER','Run Play-SAICCER.cmd. Keep the complete build folder together.','H: toggle human / AI. W/S or Up/Down: move. A/D or Left/Right: turn.','E or 1: pass. Space or 0: shoot. Both the number row and numpad are supported.','Mouse: look left/right up to 40 degrees. Esc: pause / resume.','AI spectator: Left/Right changes the camera.','RED CUBE: red field players + pink goalkeeper. NAVY CUBE: navy field players + sky goalkeeper.','Character arrows are reserved for future sets; each team currently uses its original set.') | Set-Content (Join-Path $build 'SAICCER-README.txt') -Encoding utf8
& (Join-Path $PSScriptRoot 'Build-Lifecycle.ps1') -Action Register -BuildDirectory $build -Stage '5v5 demo' -Purpose '통합 모델 선택과 인간 개입을 포함한 5대5 시연' -Evidence $log -BuiltAtUtc ([DateTime]::UtcNow.ToString('o'))
& (Join-Path $PSScriptRoot 'Build-Lifecycle.ps1') -Action Review -Json | Set-Content (Join-Path $evidence 'retention-review.json') -Encoding utf8
Write-Output "BUILD=$build"
