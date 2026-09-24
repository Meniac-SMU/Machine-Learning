function Assert-MNGV2Model {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$ModelPath, [Parameter(Mandatory)][string]$ManifestPath)
    $root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
    $registry = Get-Content -Raw -LiteralPath (Join-Path $root 'docs/soccer/training/ms-v2-model-registry.json') | ConvertFrom-Json
    $sha = (Get-FileHash -LiteralPath $ModelPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if (@($registry.entries | Where-Object { $_.sha256 -eq $sha -and $_.status -eq 'retired-architecture-v1' }).Count) { throw 'retired-architecture-v1: model selection denied.' }
    $manifest = Get-Content -Raw -LiteralPath $ManifestPath | ConvertFrom-Json
    if ($manifest.schema -ne 'MNG-OBS-v2-244' -or $manifest.behavior -ne 'MNG_ManagerV2' -or
        $manifest.observations -ne 244 -or $manifest.sha256 -ne $sha) { throw 'V2 model schema or SHA mismatch.' }
    if ($manifest.status -notin @('reference','candidate','champion')) { throw 'V2 model is not an eligible reference/candidate/champion.' }
    return $manifest
}
