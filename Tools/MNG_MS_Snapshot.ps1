[CmdletBinding()]
param(
    [string]$OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256Lower([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot 'Logs\MNG-MS\MS0'
}
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "MS0 snapshot directory already exists; preserve it and choose another path: $OutputDirectory"
}

$snapshotRoot = Join-Path $OutputDirectory 'source-snapshot'
New-Item -ItemType Directory -Path $snapshotRoot -Force | Out-Null

$roots = @(
    'Assets\_Soccer\Manager',
    'Packages',
    'ProjectSettings',
    'Tools',
    'docs\soccer'
)
$files = foreach ($relativeRoot in $roots) {
    $absoluteRoot = Join-Path $projectRoot $relativeRoot
    if (-not (Test-Path -LiteralPath $absoluteRoot)) { continue }
    Get-ChildItem -LiteralPath $absoluteRoot -File -Recurse | Where-Object {
        $_.FullName -notmatch '[\\/]Models[\\/]' -and
        $_.FullName -notmatch '[\\/]results[\\/]' -and
        $_.Extension -ne '.onnx' -and
        $_.Extension -ne '.pt'
    }
}
$files += Get-Item -LiteralPath (Join-Path $projectRoot 'AGENTS.md')
$files = $files | Sort-Object FullName -Unique

$manifestFiles = @()
foreach ($file in $files) {
    $relative = $file.FullName.Substring($projectRoot.Length + 1)
    $destination = Join-Path $snapshotRoot $relative
    $destinationParent = Split-Path -Parent $destination
    New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
    $manifestFiles += [ordered]@{
        path = $relative.Replace('\', '/')
        bytes = $file.Length
        sha256 = Get-Sha256Lower $file.FullName
    }
}

$head = (& git -C $projectRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not read the Git HEAD for the MS0 snapshot.' }
& git -C $projectRoot status --short | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $OutputDirectory 'git-status.txt')
if ($LASTEXITCODE -ne 0) { throw 'Could not capture git status for the MS0 snapshot.' }
& git -C $projectRoot diff --binary -- . | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $OutputDirectory 'tracked-working-tree.patch')
if ($LASTEXITCODE -ne 0) { throw 'Could not capture the tracked MS0 working-tree patch.' }
& git -C $projectRoot ls-files --others --exclude-standard | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $OutputDirectory 'untracked-files.txt')
if ($LASTEXITCODE -ne 0) { throw 'Could not capture the untracked MS0 file list.' }

$pythonPath = 'C:\Users\USER\miniconda3\envs\mlagents\python.exe'
$pythonVersion = if (Test-Path -LiteralPath $pythonPath) {
    (& $pythonPath --version 2>&1 | Out-String).Trim()
}
else { 'not-found' }
$environment = [ordered]@{
    capturedUtc = [DateTime]::UtcNow.ToString('O')
    gitHead = $head
    unityVersion = (Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt') | Select-Object -First 1)
    python = $pythonVersion
    computer = $env:COMPUTERNAME
}
$environment | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $OutputDirectory 'environment.json')

$manifest = [ordered]@{
    schema = 'MNG-MS0-source-v1'
    capturedUtc = [DateTime]::UtcNow.ToString('O')
    gitHead = $head
    snapshotRoot = 'source-snapshot'
    fileCount = $manifestFiles.Count
    files = $manifestFiles
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $OutputDirectory 'source-sha256.json')

Write-Host "MNG MS0 SOURCE SNAPSHOT PASS files=$($manifestFiles.Count) output=$OutputDirectory"

