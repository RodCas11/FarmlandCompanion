[CmdletBinding()]
param([string]$SdkPath = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Reuse local build, tests, WPF smoke and safe publication.
& (Join-Path $PSScriptRoot 'build-latest.ps1') -SdkPath $SdkPath
Import-Module (Join-Path $PSScriptRoot 'build/PublishTools.psm1') -Force -DisableNameChecking
$latest = Join-Path $PSScriptRoot 'dist/latest'
$null = Test-BuildPackage $latest
$output = Join-Path $PSScriptRoot 'artifacts/release'
New-Item -ItemType Directory -Force $output | Out-Null
$zip = Join-Path $output 'FarmlandCompanion-win-x64.zip'
$temporary = Join-Path $output ([Guid]::NewGuid().ToString('N') + '.zip')
$manifest = Get-Content -LiteralPath (Join-Path $latest '.build-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    $archive = [IO.Compression.ZipFile]::Open($temporary, [IO.Compression.ZipArchiveMode]::Create)
    try {
        # Only verified runtime files, documentation and required licenses/source data.
        # No build manifest, source project, PDB, test results, caches or credentials.
        foreach ($record in $manifest.Files) {
            $path = Assert-BuildPath (Join-Path $latest $record.Path) $latest
            $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $path, $record.Path, [IO.Compression.CompressionLevel]::Optimal)
        }
    } finally { $archive.Dispose() }
    Move-Item -LiteralPath $temporary -Destination $zip -Force
    $checksum = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($zip)
    [IO.File]::WriteAllText((Join-Path $output 'FarmlandCompanion-win-x64.sha256'), $checksum + "`n", [Text.UTF8Encoding]::new($false))
    Write-Host "Pacote do usuário: $zip"
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
