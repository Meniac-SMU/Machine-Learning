[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Checkpoint,[Parameter(Mandatory=$true)][ValidateSet('initial','middle','final')][string]$Label)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$python='C:\Users\USER\miniconda3\envs\mlagents\python.exe'
$initial='results/Manager5v5/MNG5_V2-20261013-r001/MNG5_Manager_v2/MNG5_Manager_v2-0.onnx'
$build='Builds/Manager5v5/p9-20261013-r1/Soccer5v5Learning.exe'
$env:OPENBLAS_NUM_THREADS='1'; $env:OMP_NUM_THREADS='1'; $env:MKL_NUM_THREADS='1'; $env:CUDA_VISIBLE_DEVICES='-1'
Push-Location -LiteralPath $repo
try {
    foreach($opponent in @(@{name='rule';source='rule'},@{name='recover';source='recover'},@{name='initial';source=$initial})) {
        $output="Logs/Manager5v5/p9-20261013/evaluation-$Label-$($opponent.name)"
        & $python Tools/five_evaluate.py --candidate $Checkpoint --opponent $opponent.source --build $build --output $output --pairs 12 --seed 29000 --workers 4 --worker 80
        if($LASTEXITCODE -ne 0){throw "P9 evaluation failed: $output"}
    }
    & $python Tools/Audit-FiveCheckpoint.py --checkpoint ([IO.Path]::ChangeExtension($Checkpoint,'.pt')) --observations "Logs/Manager5v5/p9-20261013/evaluation-$Label-rule" --output "Logs/Manager5v5/p9-20261013/parity-$Label.json"
    if($LASTEXITCODE -ne 0){throw 'Checkpoint probability parity failed.'}
} finally {Pop-Location}
