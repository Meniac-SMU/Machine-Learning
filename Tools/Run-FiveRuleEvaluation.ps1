[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BuildDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][string]$Candidate,
    [ValidateSet('legacy','strong')][string]$Variant='strong',
    [string]$Settings,
    [ValidateRange(1,32)][int]$Pairs=8,
    [int]$Seed=31000,
    [int]$Worker=180
)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildPath=[IO.Path]::GetFullPath((Join-Path $repoRoot $BuildDirectory))
$outputPath=[IO.Path]::GetFullPath((Join-Path $repoRoot $OutputDirectory))
foreach($path in @($buildPath,$outputPath)){
    if(!$path.StartsWith($repoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Keep evaluation inside this project.'}
}
if(Test-Path -LiteralPath $outputPath){throw 'Use a fresh output directory; failed evidence is preserved.'}
$pythonPath='C:\Users\USER\miniconda3\envs\mlagents\python.exe'
$exePath=Join-Path $buildPath 'Soccer5v5Learning.exe'
$optionalArguments=@()
if($Settings){$optionalArguments+=@('--settings',$Settings)}
& $pythonPath (Join-Path $PSScriptRoot 'five_rule_evaluate.py') --build $exePath --output $outputPath --candidate $Candidate --variant $Variant --pairs $Pairs --seed $Seed --worker $Worker --workers 4 @optionalArguments
if($LASTEXITCODE -ne 0){throw 'Rule evaluation failed. Preserve this output and use a new directory after repair.'}
& (Join-Path $PSScriptRoot 'Build-Lifecycle.ps1') -Action Use -BuildDirectory $buildPath -Purpose ('Rule opponent evaluation: '+$Variant) -Evidence (Join-Path $outputPath 'summary.json')
Get-Content -LiteralPath (Join-Path $outputPath 'summary.json')
