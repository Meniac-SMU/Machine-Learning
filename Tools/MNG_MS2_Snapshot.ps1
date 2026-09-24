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
    $OutputDirectory = Join-Path $projectRoot 'Logs\MNG-MS\MS2-source-v1'
}
if (Test-Path -LiteralPath $OutputDirectory) {
    throw "MS2 snapshot directory already exists; preserve it and choose another path: $OutputDirectory"
}

$snapshotRoot = Join-Path $OutputDirectory 'source-snapshot'
New-Item -ItemType Directory -Path $snapshotRoot -Force | Out-Null
$roots = @('Assets\_Soccer\Manager','Packages','ProjectSettings','Tools','docs\soccer')
$files = foreach ($relativeRoot in $roots) {
    $absoluteRoot = Join-Path $projectRoot $relativeRoot
    if (-not (Test-Path -LiteralPath $absoluteRoot)) { continue }
    Get-ChildItem -LiteralPath $absoluteRoot -File -Recurse | Where-Object {
        $_.FullName -notmatch '[\\/]Models[\\/]' -and
        $_.FullName -notmatch '[\\/]EvaluationModels[\\/]' -and
        $_.Extension -ne '.onnx' -and $_.Extension -ne '.pt'
    }
}
$files += Get-Item -LiteralPath (Join-Path $projectRoot 'AGENTS.md')
$files = $files | Sort-Object FullName -Unique
$manifestFiles = @()
foreach ($file in $files) {
    $relative = $file.FullName.Substring($projectRoot.Length + 1)
    $destination = Join-Path $snapshotRoot $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination
    $manifestFiles += [ordered]@{
        path = $relative.Replace('\', '/')
        bytes = $file.Length
        sha256 = Get-Sha256Lower $file.FullName
    }
}

$head = (& git -C $projectRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Could not read Git HEAD for the MS2 snapshot.' }
& git -C $projectRoot status --short | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $OutputDirectory 'git-status.txt')
if ($LASTEXITCODE -ne 0) { throw 'Could not capture MS2 git status.' }
& git -C $projectRoot diff --binary -- . | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $OutputDirectory 'tracked-working-tree.patch')
if ($LASTEXITCODE -ne 0) { throw 'Could not capture the MS2 tracked patch.' }
& git -C $projectRoot ls-files --others --exclude-standard | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $OutputDirectory 'untracked-files.txt')
if ($LASTEXITCODE -ne 0) { throw 'Could not capture the MS2 untracked list.' }

$manifest = [ordered]@{
    schema = 'MNG-MS2-source-v1'
    capturedUtc = [DateTime]::UtcNow.ToString('O')
    gitHead = $head
    snapshotRoot = 'source-snapshot'
    fileCount = $manifestFiles.Count
    initialization = [ordered]@{
        path = 'results/MNG_MS1-20260920-r007/MNG_Manager/MNG_Manager-7443.pt'
        sha256 = Get-Sha256Lower (Join-Path $projectRoot 'results\MNG_MS1-20260920-r007\MNG_Manager\MNG_Manager-7443.pt')
    }
    files = $manifestFiles
}
$manifest | ConvertTo-Json -Depth 7 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $OutputDirectory 'source-sha256.json')
Write-Host "MNG MS2 SOURCE SNAPSHOT PASS files=$($manifestFiles.Count) output=$OutputDirectory"
