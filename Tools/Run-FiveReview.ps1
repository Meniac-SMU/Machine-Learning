[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$BuildDirectory,[Parameter(Mandatory=$true)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildDir=[IO.Path]::GetFullPath((Join-Path $repoRoot $BuildDirectory))
$evidence=[IO.Path]::GetFullPath((Join-Path $repoRoot $EvidenceDirectory))
foreach($path in @($buildDir,$evidence)){if(!$path.StartsWith($repoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Paths must stay inside the project.'}}
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
$logPath=Join-Path $evidence 'player.log'
if(Test-Path -LiteralPath $logPath){throw 'Use a fresh evidence directory.'}
$process=Start-Process -FilePath (Join-Path $buildDir 'Soccer5v5.exe') -ArgumentList @('-force-d3d11','-screen-fullscreen','0','-screen-width','1600','-screen-height','1000','-logFile',"`"$logPath`"",'--five-review-root',"`"$evidence`"") -WindowStyle Hidden -PassThru
Write-Output ('PLAYER_PID='+$process.Id);$process.WaitForExit()
$receipt=[ordered]@{exitCode=$process.ExitCode;endedUtc=[DateTime]::UtcNow.ToString('o');evidence=$evidence;trainingStarted=$false}
$receipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'process-result.json') -Encoding UTF8
if(Test-Path -LiteralPath (Join-Path $evidence 'player-result.json')){
    & (Join-Path $PSScriptRoot 'Build-Lifecycle.ps1') -Action Use -BuildDirectory $buildDir -Purpose '5v5 규칙형300초 실제 렌더 경기 검토' -Evidence (Join-Path $evidence 'player-result.json')
}
$receipt | ConvertTo-Json
if($process.ExitCode -ne 0){exit $process.ExitCode}
