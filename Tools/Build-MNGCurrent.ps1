[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_-]*$')][string]$Stage,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Purpose,
    [string]$UnityEditor='C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe',
    [switch]$PlanOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildRelative='Builds/MNG_V2/'+$Stage+'-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$buildRoot=Join-Path $root $buildRelative
$logRelative='Logs/BuildLifecycle/'+[IO.Path]::GetFileName($buildRoot)+'.log'
$logPath=Join-Path $root $logRelative
$method='MachineLearning.Soccer.Manager.Editor.MNG_V2Builder.BuildStabilityPreparationBatch'
if($PlanOnly){[pscustomobject]@{stage=$Stage;purpose=$Purpose;output=$buildRelative;log=$logRelative;method=$method;players=@('MS3V2','EvaluationV2');executesBuild=$false}|ConvertTo-Json;return}
if(!(Test-Path -LiteralPath $UnityEditor -PathType Leaf)){throw 'Unity Editor executable not found'}
$editors=@(Get-Process -Name Unity -ErrorAction SilentlyContinue|Where-Object {$_.Path -and $_.Path -like '*\Editor\Unity.exe'})
if($editors.Count){throw 'Close the project Editor before batch building'}
if(Test-Path -LiteralPath $buildRoot){throw 'Preserve existing build directory'}
New-Item -ItemType Directory -Path (Split-Path $logPath -Parent) -Force|Out-Null
$head=(& git -C $root rev-parse HEAD).Trim()
$dirty=@(& git -C $root status --porcelain=v1 --untracked-files=all)
$request=[ordered]@{startedAtUtc=[DateTime]::UtcNow.ToString('o');stage=$Stage;purpose=$Purpose;sourceCommit=$head;workingTreeChanges=$dirty;method=$method;output=$buildRelative;status='started'}
$requestPath=[IO.Path]::ChangeExtension($logPath,'.json')
$request|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $requestPath -Encoding utf8
$arguments=@('-batchmode','-nographics','-projectPath',('"'+$root+'"'),'-executeMethod',$method,'-mngBuildRoot',('"'+$buildRoot+'"'),'-logFile',('"'+$logPath+'"'))
$process=Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){$request.status='failed';$request|ConvertTo-Json -Depth 6|Set-Content $requestPath -Encoding utf8;throw "Build failed; retain diagnostic output: $logRelative"}
$completed=[DateTime]::UtcNow.ToString('o')
foreach($name in @('MS3V2','EvaluationV2')){
    $directory=Join-Path $buildRoot $name
    if(!(Test-Path (Join-Path $directory 'build-info.json'))){throw "Build metadata missing: $name"}
    & (Join-Path $PSScriptRoot 'Build-Lifecycle.ps1') -Action Register -BuildDirectory $directory -Stage $Stage -Purpose ($Purpose+' / '+$name) -Evidence ([IO.Path]::ChangeExtension($logRelative,'.json')) -BuiltAtUtc $completed -SourceCommit $head
}
$request.status='completed';$request.completedAtUtc=$completed
$request|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $requestPath -Encoding utf8
& (Join-Path $PSScriptRoot 'Build-Lifecycle.ps1') -Action Review
Write-Output 'Builds registered. Review/validate before replacing any retained build or preparation manifest.'
