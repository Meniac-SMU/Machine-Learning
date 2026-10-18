[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputFile=if([IO.Path]::IsPathRooted($OutputPath)){[IO.Path]::GetFullPath($OutputPath)}else{[IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))}
if(!$outputFile.StartsWith($repoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Snapshot must stay in the project.'}
if(Test-Path -LiteralPath $outputFile){throw 'Preserve existing snapshots.'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$files=@(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'Assets/_Soccer/Manager5v5') -File -Recurse)
$files+=@(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'docs/soccer/rebuild-5v5') -File -Recurse)
$files+=@(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'Tools') -File | Where-Object {$_.BaseName -like '*Five*' -and $_.Extension -in @('.ps1','.py')})
$files+=@(foreach($relative in @('Assets/_Soccer/Manager5v5.meta','AGENTS.md','docs/README.md','docs/soccer/current-status.md','docs/soccer/rewards.md','Tools/README.md')){
    $path=Join-Path $repoRoot $relative
    if(Test-Path -LiteralPath $path){Get-Item -LiteralPath $path}
})
$zip=[IO.Compression.ZipFile]::Open($outputFile,[IO.Compression.ZipArchiveMode]::Create)
try{
    $manifest=@()
    foreach($file in $files){
        $relative=[IO.Path]::GetRelativePath($repoRoot,$file.FullName).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal)|Out-Null
        $manifest+=@{path=$relative;sha256=(Get-FileHash -LiteralPath $file.FullName).Hash}
    }
    $entry=$zip.CreateEntry('source-manifest.json');$writer=[IO.StreamWriter]::new($entry.Open())
    try{$writer.Write((ConvertTo-Json -InputObject $manifest -Depth 4))}finally{$writer.Dispose()}
}finally{$zip.Dispose()}
Write-Output ('SOURCE_SNAPSHOT='+$outputFile)
