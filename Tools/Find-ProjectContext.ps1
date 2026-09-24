[CmdletBinding()]
param(
    [ValidateSet('CurrentDocs','Manager','Core','Tools','Archive','Legacy')]
    [string]$Scope = 'CurrentDocs',
    [string]$Pattern,
    [switch]$Literal
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$paths = switch ($Scope) {
    'CurrentDocs' { @('AGENTS.md','README.md','docs/README.md','docs/project','docs/soccer','Tools/README.md','Assets/_Soccer/Manager/MNG_README.md') }
    'Manager' { @('Assets/_Soccer/Manager/Runtime','Assets/_Soccer/Manager/Editor','Assets/_Soccer/Manager/Tests') }
    'Core' { @('Assets/_Soccer/Core','Assets/_Soccer/Editor','Assets/_Soccer/Tests','Assets/_Soccer/Teams') }
    'Tools' { @('Tools') }
    'Archive' { @('docs/archive') }
    'Legacy' { @('Assets/_Legacy') }
}
$rgArgs = @('--glob','!**/__pycache__/**','--glob','!*.meta','--glob','!*.onnx','--glob','!*.pt')
if ($Scope -eq 'CurrentDocs') { $rgArgs += @('--glob','*.md') }
if ($PSBoundParameters.ContainsKey('Pattern')) {
    $rgArgs += @('-n')
    if ($Literal) { $rgArgs += '-F' }
    $rgArgs += @('-e',$Pattern)
} else { $rgArgs += '--files' }
$rgArgs += '--'
$rgArgs += $paths
Push-Location -LiteralPath $projectRoot
try {
    & rg @rgArgs
    if ($LASTEXITCODE -gt 1) { throw "rg failed with exit code $LASTEXITCODE" }
    # No matches is a successful read-only query, not a test failure.
} finally { Pop-Location }
