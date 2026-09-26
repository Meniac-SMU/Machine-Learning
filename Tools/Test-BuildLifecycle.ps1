[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$fixture=Join-Path $root ('Builds/_LifecycleTest-'+[Guid]::NewGuid().ToString('N'))
$tool=Join-Path $PSScriptRoot 'Build-Lifecycle.ps1'
$passed=0
function Assert($condition,[string]$message){if(!$condition){throw $message};$script:passed++}
function MustReject([scriptblock]$operation,[string]$message){$rejected=$false;try{& $operation|Out-Null}catch{$rejected=$true};Assert $rejected $message}
try{
    New-Item -ItemType Directory -Path $fixture|Out-Null
    [IO.File]::WriteAllText((Join-Path $fixture 'Fixture.exe'),'non-executable test fixture')
    MustReject { & $tool -Action Use -BuildDirectory $fixture -Purpose test -Evidence test } 'Unregistered use must fail'
    MustReject { & $tool -Action Register -BuildDirectory $root -Stage test -Purpose test -Evidence test } 'Outside Builds must fail'
    MustReject { & $tool -Action Register -BuildDirectory 'Builds/../Assets' -Stage test -Purpose test -Evidence test } 'Traversal must fail'
    & $tool -Action Register -BuildDirectory $fixture -Stage test -Purpose test -Evidence 'synthetic fixture' -BuiltAtUtc '2025-01-01T00:00:00Z'|Out-Null
    $recordPath=Join-Path $fixture 'build-lifecycle.json'
    Assert (Test-Path $recordPath) 'Registration missing'
    $before=(Get-FileHash $recordPath).Hash
    MustReject { & $tool -Action Register -BuildDirectory $fixture -Stage replacement -Purpose test -Evidence test } 'Registration overwrite must fail'
    & $tool -Action Use -BuildDirectory $fixture -Purpose 'test use' -Evidence 'synthetic fixture'
    Assert ((Get-FileHash $recordPath).Hash -eq $before) 'Use must preserve registration'
    $rows=(& $tool -Action Review -Json|Out-String)|ConvertFrom-Json
    $row=$rows|Where-Object path -like 'Builds/_LifecycleTest-*'
    Assert ($row.decision -eq 'review-unpinned' -and $null -ne $row.lastUsedAtUtc) 'Review must report use and unpinned state'
    [IO.File]::WriteAllText((Join-Path $fixture 'build-usage.jsonl'),'{"usedAtUtc":"2025-01-01T00:00:00Z","purpose":"synthetic old use","evidence":"test"}'+[Environment]::NewLine)
    $rows=(& $tool -Action Review -UnusedDays 1 -Json|Out-String)|ConvertFrom-Json
    $row=$rows|Where-Object path -like 'Builds/_LifecycleTest-*'
    Assert ($row.decision -eq 'review-long-unused') 'Stale use must be flagged for review'
    $expectedProtected=@((Get-Content (Join-Path $root 'docs/project/build-retention.json') -Raw|ConvertFrom-Json).retained).Count
    Assert (@($rows|Where-Object decision -eq 'protected').Count -eq $expectedProtected) 'Retained builds must remain protected'
    Assert (Test-Path (Join-Path $fixture 'Fixture.exe')) 'Review must not delete'
    $beforePaths=@(Get-ChildItem (Join-Path $root 'Builds/MNG_V2') -Directory|ForEach-Object FullName)
    $plan=(& (Join-Path $PSScriptRoot 'Build-MNGCurrent.ps1') -Stage test -Purpose test -PlanOnly|Out-String)|ConvertFrom-Json
    Assert (!$plan.executesBuild -and !(Test-Path (Join-Path $root $plan.output))) 'PlanOnly must not create a build'
    Assert (@(Compare-Object $beforePaths @(Get-ChildItem (Join-Path $root 'Builds/MNG_V2') -Directory|ForEach-Object FullName)).Count -eq 0) 'PlanOnly changed builds'
    Write-Output "Build lifecycle tests passed: $passed"
}finally{
    $safe=[IO.Path]::GetFullPath($fixture)
    $allowed=[IO.Path]::GetFullPath((Join-Path $root 'Builds'))+'\'
    if(!$safe.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($safe) -notlike '_LifecycleTest-*'){throw 'Unsafe fixture cleanup path'}
    if(Test-Path -LiteralPath $safe){Remove-Item -LiteralPath $safe -Recurse -Force}
}
