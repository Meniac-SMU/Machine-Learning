[CmdletBinding()]
param(
    [ValidateSet('Register','Use','Review')][string]$Action='Review',
    [string]$BuildDirectory,
    [string]$Stage,
    [string]$Purpose,
    [string]$Evidence,
    [string]$BuiltAtUtc,
    [string]$SourceCommit,
    [ValidateRange(1,36500)][int]$UnusedDays,
    [switch]$Json
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildRoot=Join-Path $projectRoot 'Builds'
$policy=Get-Content (Join-Path $projectRoot 'docs/project/build-retention.json') -Raw|ConvertFrom-Json
function Relative([string]$path){[IO.Path]::GetRelativePath($projectRoot,$path).Replace('\','/')}
function ResolveBuild([string]$path){
    if([string]::IsNullOrWhiteSpace($path)){throw 'BuildDirectory is required'}
    $full=[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted($path)){$path}else{Join-Path $projectRoot $path}))
    if(!$full.StartsWith($buildRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'BuildDirectory must be inside project Builds'}
    $item=Get-Item -LiteralPath $full
    if(!$item.PSIsContainer){throw 'BuildDirectory must be a directory'}
    $ancestor=$item
    while($ancestor.FullName -ne $projectRoot){
        if($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked build paths are not supported'}
        $ancestor=$ancestor.Parent
    }
    $players=@(Get-ChildItem -LiteralPath $full -File -Filter '*.exe'|Where-Object Name -notlike 'UnityCrashHandler*')
    if($players.Count -ne 1){throw 'Select one Player directory containing exactly one game executable'}
    return $full
}
function WriteJson($value,[string]$path){[IO.File]::WriteAllText($path,($value|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))}
function Field($obj,[string]$name){if($null -ne $obj -and $obj.PSObject.Properties.Name -contains $name){return $obj.$name};return $null}
if($Action -ne 'Review'){
    $directory=ResolveBuild $BuildDirectory
    if([string]::IsNullOrWhiteSpace($Purpose) -or [string]::IsNullOrWhiteSpace($Evidence)){throw 'Purpose and Evidence are required'}
    $recordPath=Join-Path $directory 'build-lifecycle.json'
    if($Action -eq 'Use'){
        if(!(Test-Path -LiteralPath $recordPath)){throw 'Register the build before recording use'}
        # Record only actual use, never inspection, hashing, or a failed launch.
        $event=[ordered]@{usedAtUtc=[DateTime]::UtcNow.ToString('o');purpose=$Purpose;evidence=$Evidence}
        [IO.File]::AppendAllText((Join-Path $directory 'build-usage.jsonl'),($event|ConvertTo-Json -Compress)+[Environment]::NewLine,[Text.UTF8Encoding]::new($false))
        return
    }
    if([string]::IsNullOrWhiteSpace($Stage)){throw 'Stage is required'}
    if(Test-Path -LiteralPath $recordPath){throw 'Build registration is immutable; use a new directory for a new build'}
    $date=$null
    if($BuiltAtUtc){$date=[DateTimeOffset]::Parse($BuiltAtUtc).ToUniversalTime().ToString('o')}
    $exe=Get-ChildItem -LiteralPath $directory -File -Filter '*.exe'|Where-Object Name -notlike 'UnityCrashHandler*'
    $infoPath=Join-Path $directory 'build-info.json'
    $info=if(Test-Path $infoPath){Get-Content $infoPath -Raw|ConvertFrom-Json}else{$null}
    $keyHashes=@($exe|ForEach-Object{[ordered]@{path=(Relative $_.FullName);sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
    foreach($file in @(Get-ChildItem -LiteralPath $directory -File -Recurse|Where-Object Name -in 'level0','MNG.Runtime.dll','Soccer.Runtime.dll')){
        $keyHashes += [ordered]@{path=(Relative $file.FullName);sha256=(Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
    }
    $record=[ordered]@{schema='project-build-lifecycle-v1';registeredAtUtc=[DateTime]::UtcNow.ToString('o');builtAtUtc=$date;
        timestampBasis=$(if($date){'explicit build completion/provenance'}else{'unknown; registration time is not build time'});
        stage=$Stage;purpose=$Purpose;evidence=$Evidence;buildDirectory=(Relative $directory);sourceCommit=$SourceCommit;
        environmentRevision=(Field $info 'environmentRevision');scene=(Field $info 'scene');keyHashes=$keyHashes}
    WriteJson $record $recordPath
    Write-Output "Registered: $(Relative $directory)"
    return
}
$rows=@(foreach($exe in @(Get-ChildItem -LiteralPath $buildRoot -File -Recurse -Filter '*.exe'|Where-Object Name -notlike 'UnityCrashHandler*')){
    $directory=ResolveBuild $exe.Directory.FullName
    $relative=Relative $directory
    $recordPath=Join-Path $directory 'build-lifecycle.json'
    $record=if(Test-Path $recordPath){Get-Content $recordPath -Raw|ConvertFrom-Json}else{$null}
    $retention=@($policy.retained|Where-Object path -eq $relative)
    $lastUsed=$null
    $usage=Join-Path $directory 'build-usage.jsonl'
    if(Test-Path $usage){$last=Get-Content $usage|Where-Object {$_}|Select-Object -Last 1;if($last){$lastUsed=($last|ConvertFrom-Json).usedAtUtc}}
    $builtAt=Field $record 'builtAtUtc'
    if($builtAt){$builtAt=([DateTimeOffset]$builtAt).ToUniversalTime().ToString('o')}
    if($lastUsed){$lastUsed=([DateTimeOffset]$lastUsed).ToUniversalTime().ToString('o')}
    if($builtAt){$builtAt=([DateTimeOffset]$builtAt).ToUniversalTime().ToString('o')}
    if($lastUsed){$lastUsed=([DateTimeOffset]$lastUsed).ToUniversalTime().ToString('o')}
    $age=$null
    if($lastUsed){$age=[math]::Floor(([DateTimeOffset]::UtcNow-[DateTimeOffset]::Parse($lastUsed)).TotalDays)}
    $buildAge=if($builtAt){[math]::Floor(([DateTimeOffset]::UtcNow-[DateTimeOffset]::Parse($builtAt)).TotalDays)}else{$null}
    $decision=if($retention.Count){'protected'}elseif(!$record){'review-unregistered'}else{'review-unpinned'}
    if(!$retention.Count -and $UnusedDays -and $null -ne $age -and $age -ge $UnusedDays){$decision='review-long-unused'}
    $bytes=[long]((Get-ChildItem -LiteralPath $directory -File -Recurse|Measure-Object Length -Sum).Sum)
    [pscustomobject]@{path=$relative;stage=(Field $record 'stage');MiB=[math]::Round($bytes/1MB,2);builtAtUtc=$builtAt;buildAgeDays=$buildAge;lastUsedAtUtc=$lastUsed;unusedDays=$age;decision=$decision;reason=$(if($retention.Count){$retention[0].reason}else{'Check current manifests, replacement validation, processes, and evidence before deletion'})}
})
if($Json){$rows|ConvertTo-Json -Depth 8}else{$rows|Format-Table path,MiB,stage,buildAgeDays,unusedDays,decision -AutoSize}
