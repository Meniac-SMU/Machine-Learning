[CmdletBinding()]
param(
    [ValidateSet('Build','EditMode','PlayMode')][string]$Stage='Build',
    [string]$Filter='MachineLearning.Soccer.Manager5v5.Tests',
    [string]$RunName,
    [switch]$IncludeHoldout,
    [string]$RuleSettings,
    [string]$EvidenceDirectory='Logs/Manager5v5/initial-20261012'
)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$evidencePath=[IO.Path]::GetFullPath((Join-Path $repoRoot $EvidenceDirectory))
if(!$evidencePath.StartsWith($repoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence must stay inside this project.'}
$unityPath=Join-Path $repoRoot 'Logs/u21/Editor/Unity.exe'
if(!(Test-Path -LiteralPath $unityPath)){throw 'Expected Unity6000.3.21f1 is missing.'}
$existing=@(Get-Process -ErrorAction SilentlyContinue | Where-Object {$_.ProcessName -eq 'Unity' -and $_.Path -eq $unityPath})
if($existing.Count){throw 'This project Editor is already running. Wait for it to finish.'}
New-Item -ItemType Directory -Path $evidencePath -Force | Out-Null
if(!$RunName){$RunName=$Stage.ToLowerInvariant()+'-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')}
if($RunName -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Invalid run name.'}
$logPath=Join-Path $evidencePath ($RunName+'.log')
$resultPath=Join-Path $evidencePath ($RunName+'.xml')
if(Test-Path -LiteralPath $logPath){throw 'Run name already exists; preserve the previous evidence.'}
$sourceArchive=Join-Path $evidencePath ($RunName+'-source.zip')
& (Join-Path $PSScriptRoot 'Save-FiveSource.ps1') -OutputPath $sourceArchive
$reportArchive=Join-Path $evidencePath ('reports-'+$RunName)
New-Item -ItemType Directory -Path $reportArchive -Force | Out-Null
$arguments=@('-batchmode','-nographics','-projectPath',"`"$repoRoot`"",'-logFile',"`"$logPath`"",'-fiveEvidenceDirectory',"`"$reportArchive`"")
if($RuleSettings){
    $ruleSettingsPath=[IO.Path]::GetFullPath((Join-Path $repoRoot $RuleSettings))
    if(!$ruleSettingsPath.StartsWith($repoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $ruleSettingsPath)){throw 'Rule settings must be an existing project file.'}
    $frozenSettings=Join-Path $reportArchive 'rule-settings-used.json'
    Copy-Item -LiteralPath $ruleSettingsPath -Destination $frozenSettings
    $arguments+=@('--five-rule-settings',"`"$frozenSettings`"")
}
if($Stage -eq 'Build'){$arguments+=@('-executeMethod','MachineLearning.Soccer.Manager5v5.Editor.FiveBuilder.BuildBatch')}
else{
    $arguments+=@('-runTests','-testPlatform',$Stage,'-testFilter',$Filter,'-testResults',"`"$resultPath`"")
    if(!$IncludeHoldout){$arguments+=@('-testCategory','!LongRunning')}
}
$started=[DateTime]::UtcNow
$process=Start-Process -FilePath $unityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output ('UNITY_PID='+$process.Id)
$process.WaitForExit()
$reportRoots=@($evidencePath,(Join-Path $repoRoot 'Logs/Manager5v5/initial-20261012')) | Select-Object -Unique
$reports=@(foreach($reportRoot in $reportRoots){Get-ChildItem -LiteralPath $reportRoot -File | Where-Object {$_.Name -match '^g[2-5]-(clear-passes|command-behaviour|rule-matches|matchups|rule-scenarios).*\.json$' -and $_.LastWriteTimeUtc -ge $started}})
if($reports.Count){New-Item -ItemType Directory -Path $reportArchive -Force | Out-Null;foreach($report in $reports){Copy-Item -LiteralPath $report.FullName -Destination (Join-Path $reportArchive $report.Name)}}
$receipt=[ordered]@{stage=$Stage;filter=$Filter;startedUtc=$started.ToString('o');endedUtc=[DateTime]::UtcNow.ToString('o');exitCode=$process.ExitCode;log=$logPath;results=$resultPath;sourceArchive=$sourceArchive;trainingStarted=$false}
if($Stage -ne 'Build' -and (Test-Path -LiteralPath $resultPath)){
    [xml]$testResults=Get-Content -LiteralPath $resultPath -Raw
    $receipt.testResult=$testResults.'test-run'.result
    $receipt.passed=[int]$testResults.'test-run'.passed
    $receipt.failed=[int]$testResults.'test-run'.failed
    $receipt.skipped=[int]$testResults.'test-run'.skipped
}
$receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidencePath ($RunName+'.json')) -Encoding UTF8
$receipt | ConvertTo-Json -Depth 5
if($process.ExitCode -ne 0){exit $process.ExitCode}
if($Stage -ne 'Build' -and (!(Test-Path -LiteralPath $resultPath) -or $receipt.testResult -ne 'Passed')){exit 1}
