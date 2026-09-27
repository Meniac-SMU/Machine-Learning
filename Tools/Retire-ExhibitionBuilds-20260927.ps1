# One-time cleanup explicitly requested on 2026-09-27. Never touches training builds.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$KeepBuild,[Parameter(Mandatory)][string]$PlayerEvidence)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$allowed=Join-Path $root 'Builds\MNG_Exhibition'
$keep=(Resolve-Path -LiteralPath $KeepBuild).Path
if((Split-Path $keep -Parent) -ne $allowed -or (Split-Path $keep -Leaf) -notmatch '^UI1-\d{8}T\d{9}Z$'){throw 'Unexpected retained build path'}
$evidence=(Resolve-Path -LiteralPath $PlayerEvidence).Path
$result=Get-Content (Join-Path $evidence 'result.json') -Raw | ConvertFrom-Json
$exit=Get-Content (Join-Path $evidence 'process-exit.json') -Raw | ConvertFrom-Json
if(!$result.passed -or $result.errors.Count -ne 0 -or $exit.exitCode -ne 0 -or !(Test-Path (Join-Path $evidence 'quit-observed.json'))){throw 'Replacement Player verification missing'}
if(!(Test-Path (Join-Path $keep 'MANAGER.exe'))){throw 'Replacement missing'}
$policy=Get-Content (Join-Path $root 'docs/project/build-retention.json') -Raw | ConvertFrom-Json
if(@($policy.retained | Where-Object path -eq ([IO.Path]::GetRelativePath($root,$keep).Replace('\','/'))).Count -ne 1){throw 'Pin replacement first'}
$output=Join-Path (Split-Path $evidence -Parent) 'retired-builds'
if(Test-Path (Join-Path $output 'cleanup.json')){throw 'Cleanup evidence already exists; preserve it and use a new evidence directory'}
New-Item -ItemType Directory -Force $output | Out-Null
$targets=@(Get-ChildItem -LiteralPath $allowed -Directory | Where-Object FullName -ne $keep)
$records=@()
foreach($target in $targets){
    $full=(Resolve-Path -LiteralPath $target.FullName).Path
    if((Split-Path $full -Parent) -ne $allowed -or $target.Name -notmatch '^UI1-\d{8}T\d{9}Z$'){throw "Unsafe target: $full"}
    $ancestor=$target
    while($ancestor.FullName -ne $root){
        if($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Linked ancestor: $($ancestor.FullName)"}
        $ancestor=$ancestor.Parent
    }
    $children=@(Get-ChildItem -LiteralPath $full -Recurse -Force)
    if(@($children | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count){throw "Linked child in $full"}
    if(@($policy.retained | Where-Object path -eq ([IO.Path]::GetRelativePath($root,$full).Replace('\','/'))).Count){throw "Protected target: $full"}
    $files=@($children | Where-Object {!$_.PSIsContainer})
    $dest=Join-Path $output $target.Name
    New-Item -ItemType Directory -Force $dest | Out-Null
    @($files | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($full,$_.FullName);bytes=$_.Length} }) | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $dest 'files.json') -Encoding utf8
    $keys=@($files | Where-Object { $_.Extension -eq '.exe' -or $_.Name -in @('level0','MNG.Runtime.dll','MNG.Exhibition.dll','Soccer.Runtime.dll') })
    @($keys | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($full,$_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} }) | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $dest 'key-hashes.json') -Encoding utf8
    Get-ChildItem -LiteralPath $full -File | Where-Object Extension -in @('.json','.jsonl','.md','.txt') | Copy-Item -Destination $dest
    $records += [ordered]@{path=$full;fileCount=$files.Count;bytes=($files | Measure-Object Length -Sum).Sum;evidence=$dest;deleted=$false}
}
# Recheck consumers immediately before any mutation; preserve all process metadata in the report.
$processes=@(Get-CimInstance Win32_Process | Select-Object ProcessId,Name,ExecutablePath,CommandLine)
foreach($record in $records){
    if(@($processes | Where-Object { ($_.ExecutablePath -and $_.ExecutablePath.StartsWith($record.path+'\',[StringComparison]::OrdinalIgnoreCase)) -or ($_.CommandLine -and $_.CommandLine.Contains($record.path)) }).Count){throw "Build still in use: $($record.path)"}
}
$report=[ordered]@{authorization='User explicitly requested removal of prior UI exhibition builds and retention of this verified latest UI build';replacement=$keep;verification=$evidence;createdUtc=[DateTime]::UtcNow.ToString('o');builds=$records}
$report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $output 'cleanup.json') -Encoding utf8
foreach($record in $records){
    # Every absolute path above was resolved, allowlisted, checked for links and checked for consumers.
    Remove-Item -LiteralPath $record.path -Recurse -Force
    $record.deleted=!(Test-Path -LiteralPath $record.path)
    $report | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $output 'cleanup.json') -Encoding utf8
}
if(@(Get-ChildItem -LiteralPath $allowed -Directory).Count -ne 1){throw 'Unexpected build directories remain'}
Write-Output "Removed $($records.Count) previous UI builds; retained $keep"
