[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^MNG_MS3V2-\d{8}-r\d{3}$')][string]$RunId,
    [ValidateSet(32)][int]$NumEnvs = 32,
    [switch]$Resume,
    [switch]$ValidateOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$build = Join-Path $root 'Builds/MNG_V2/MS3V2'
$executable = Join-Path $build 'MNG_MS3V2.exe'
$info = Get-Content -LiteralPath (Join-Path $build 'build-info.json') -Raw | ConvertFrom-Json
function Sha([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
if ($info.schema -ne 'MNG-OBS-v2-244' -or $info.observations -ne 244 -or $info.behavior -ne 'MNG_ManagerV2') { throw 'V2 build contract mismatch.' }
if ((Sha $executable) -ne $info.executableSha256 -or
    (Sha (Join-Path $build 'MNG_MS3V2_Data/level0')) -ne $info.levelSha256 -or
    (Sha (Join-Path $build 'MNG_MS3V2_Data/Managed/MNG.Runtime.dll')) -ne $info.runtimeSha256) { throw 'V2 build SHA mismatch.' }
$run = Join-Path $root "results/$RunId"
$evidence = Join-Path $root "Logs/MNG-Rebuild/$RunId"
if ($Resume) {
    if (!(Test-Path -LiteralPath (Join-Path $run 'MNG_ManagerV2/mng-v2-pool.pt'))) { throw 'Missing pool resume sidecar.' }
    $previous = Get-Content -LiteralPath (Join-Path $evidence 'manifest.json') -Raw | ConvertFrom-Json
    if ($previous.runtimeSha256 -ne $info.runtimeSha256 -or
        $previous.adapterSha256 -ne (Sha (Join-Path $root 'Tools/mng_v2_learn.py'))) { throw 'Source/build changed; v2 resume denied.' }
} elseif (Test-Path -LiteralPath $run) { throw 'Run exists; never overwrite a run.' }
$config = Join-Path $evidence $(if ($Resume) { 'resume.yaml' } else { 'smoke.yaml' })
$text = Get-Content -LiteralPath (Join-Path $root 'Tools/MNG_V2_Smoke.yaml') -Raw
if ($Resume) { $text = $text.Replace('max_steps: 16384','max_steps: 32768') }
if ($text -match 'init_path') { throw 'Smoke must initialize fresh or resume its own optimizer.' }
if ($ValidateOnly) { Write-Output 'V2 smoke build/schema/selection validation PASS'; return }
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
if (Test-Path -LiteralPath $config) { throw 'Preserve existing smoke config/log; inspect the prior attempt first.' }
Set-Content -LiteralPath $config -Value $text -Encoding ascii
$manifest = [ordered]@{schema=$info.schema;behavior=$info.behavior;observations=244;status='smoke-only-ineligible';workers=32;
    runtimeSha256=$info.runtimeSha256;executableSha256=$info.executableSha256;configSha256=(Sha $config);
    adapterSha256=(Sha (Join-Path $root 'Tools/mng_v2_learn.py'));resume=[bool]$Resume;runId=$RunId}
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence $(if ($Resume) {'resume-manifest.json'} else {'manifest.json'})) -Encoding utf8
$arguments = @((Join-Path $root 'Tools/mng_v2_learn.py'),$config,'--run-id',$RunId,'--results-dir',(Join-Path $root 'results'),
    '--env',$executable,'--num-envs','32','--base-port','6400','--seed','20260922','--torch-device','cuda','--timeout-wait','120',
    '--max-lifetime-restarts','0','--no-graphics','--debug')
if ($Resume) { $arguments += '--resume' }
$arguments += @('--env-args','-mngEvidenceDir',(Join-Path $evidence $(if($Resume){'resume-workers'}else{'workers'})),
    '-mngRunId',$RunId,'-mngBuildSha',$info.runtimeSha256,'-mngPolicyAssistMode','none','-mngFullTrace','true')
Push-Location $root
try {
    & 'C:\Users\USER\miniconda3\envs\mlagents\python.exe' @arguments 2>&1 |
        Tee-Object -FilePath (Join-Path $evidence $(if($Resume){'resume-trainer.log'}else{'trainer.log'}))
    if ($LASTEXITCODE -ne 0) { throw "V2 smoke trainer exit $LASTEXITCODE" }
} finally { Pop-Location }
