[CmdletBinding()]
param([string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (!$OutputDirectory) { $OutputDirectory = Join-Path $projectRoot ('Logs/MNG-Rebuild/R1-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Snapshot destination already exists.' }
$snapshotRoot = Join-Path $OutputDirectory 'source'
New-Item -ItemType Directory -Path $snapshotRoot -Force | Out-Null
$paths = @(& git -C $projectRoot ls-files --cached --others --exclude-standard) | Sort-Object -Unique
if ($LASTEXITCODE -ne 0) { throw 'Git inventory failed.' }
$entries = foreach ($relative in $paths) {
    $source = Join-Path $projectRoot $relative
    if (!(Test-Path -LiteralPath $source -PathType Leaf)) { continue }
    # Runs are immutable evidence in place; source (including dirty/untracked files) is copied.
    $copy = $relative -notmatch '^(results|Builds|Logs)/'
    if ($copy) {
        $destination = Join-Path $snapshotRoot $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination
    }
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($copy -and (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw "Snapshot mismatch: $relative" }
    [ordered]@{path=$relative; sha256=$hash; copied=$copy; bytes=(Get-Item -LiteralPath $source).Length}
}
$head = (& git -C $projectRoot rev-parse HEAD).Trim()
& git -C $projectRoot status --short | Set-Content -LiteralPath (Join-Path $OutputDirectory 'git-status.txt') -Encoding utf8
[ordered]@{schema='MNG-rebuild-source-v2'; capturedUtc=[DateTime]::UtcNow.ToString('O'); gitHead=$head; files=@($entries)} |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'source-sha256.json') -Encoding utf8
Write-Output "Verified snapshot: $OutputDirectory; files=$(@($entries).Count)"
