Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:LegacyPackageFiles = @('RagnarokCompanion.exe','README.md','Data/mvp-seed.json','docs/RAGNAPLACE_API.md','licenses/rathena-GPL-3.0.txt','licenses/THIRD-PARTY-NOTICES.md')
$script:PackageFiles = @('FarmlandCompanion.exe','README.md','Data/mvp-seed.json','docs/RAGNAPLACE_API.md','docs/SPOTLIGHT.md','licenses/rathena-GPL-3.0.txt','licenses/THIRD-PARTY-NOTICES.md')

function Assert-BuildPath([string]$Path, [string]$Scope) {
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\','/')
    $base = [IO.Path]::GetFullPath($Scope).TrimEnd('\','/')
    if (!$full.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Caminho fora da área de build: $full" }
    $cursor = $full
    while ($cursor -and $cursor.Length -ge $base.Length) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Link/junção não permitido na distribuição: $cursor" }
        }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
    return $full
}
function Get-SafeBuildFiles([string]$Root) {
    $pending = [Collections.Generic.Stack[string]]::new(); $pending.Push($Root)
    while ($pending.Count) {
        $dir = $pending.Pop()
        if ((Get-Item -LiteralPath $dir -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Link/junção preservado: $dir" }
        foreach ($item in Get-ChildItem -LiteralPath $dir -Force) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Link/junção preservado: $($item.FullName)" }
            if ($item.PSIsContainer) { $pending.Push($item.FullName) } else { $item }
        }
    }
}
function Get-BuildRelative([string]$Root, [string]$Path) { $Path.Substring($Root.TrimEnd('\','/').Length + 1).Replace('\','/') }
function New-BuildManifest([string]$Root) {
    $records = foreach ($name in $script:PackageFiles) {
        $path = Join-Path $Root $name
        if (!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) { throw "Artefato obrigatório ausente: $name" }
        @{ Path=$name; Sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
    }
    $manifest = @{ Application='FarmlandCompanion'; Format=2; Files=@($records) } | ConvertTo-Json -Depth 6
    [IO.File]::WriteAllText((Join-Path $Root '.build-manifest.json'), $manifest)
}
function Test-BuildPackage([string]$Root) {
    $verifiedFiles = @(Get-SafeBuildFiles $Root)
    $manifestPath = Join-Path $Root '.build-manifest.json'
    if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw "Distribuição sem manifesto: $Root. Preservada para revisão." }
    $m = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($m.Application -eq 'RagnarokCompanion' -and $m.Format -eq 1) { $expected = $script:LegacyPackageFiles; $exeName = 'RagnarokCompanion.exe' }
    elseif ($m.Application -eq 'FarmlandCompanion' -and $m.Format -eq 2) { $expected = $script:PackageFiles; $exeName = 'FarmlandCompanion.exe' }
    else { throw 'Manifesto inválido; distribuição preservada.' }
    if (@($m.Files).Count -ne $expected.Count) { throw 'Manifesto incompleto; distribuição preservada.' }
    foreach ($name in $expected) {
        $record = @($m.Files | Where-Object { $_.Path -eq $name })
        $path = Assert-BuildPath (Join-Path $Root $name) $Root
        if ($record.Count -ne 1 -or !(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $record[0].Sha256) { throw "Arquivo alterado/ausente: $path. Distribuição preservada." }
    }
    foreach ($file in $verifiedFiles) {
        $relative = Get-BuildRelative $Root $file.FullName
        if ($relative -ne '.build-manifest.json' -and $relative -notin $expected) { throw "Arquivo desconhecido preservado: $($file.FullName)" }
    }
    $exe = [IO.File]::OpenRead((Join-Path $Root $exeName))
    try {
        $reader = [IO.BinaryReader]::new($exe)
        if ($reader.ReadUInt16() -ne 0x5A4D) { throw 'Executável inválido (MZ).' }
        $exe.Position = 0x3C; $offset = $reader.ReadInt32(); $exe.Position = $offset
        if ($reader.ReadUInt32() -ne 0x4550 -or $reader.ReadUInt16() -ne 0x8664) { throw 'O executável não é Windows x64.' }
    } finally { $exe.Dispose() }
    return $true
}
function Assert-BuildFileUnlocked([string]$Path) {
    try { $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None); $stream.Dispose() }
    catch { throw "Publicação interrompida: arquivo em uso ou sem acesso: $Path. Use Sair na bandeja e tente novamente. A distribuição existente foi preservada." }
}
function Move-StagedBuildPackage([string]$Source, [string]$Destination, [string]$StagingRoot, [string]$DistRoot) {
    $null = Assert-BuildPath $Source $StagingRoot
    $null = Assert-BuildPath $Destination $DistRoot
    $null = Test-BuildPackage $Source
    if (Test-Path -LiteralPath $Destination) { throw 'Destino da publicação já existe; preservado.' }
    # The smoke process has exited. Windows may still briefly retain its image/directory.
    # Only the new, verified staging package is retried, never the old distribution.
    for ($attempt = 0; $attempt -lt 21; $attempt++) {
        try { [IO.Directory]::Move($Source, $Destination); return }
        catch {
            $cause = $_.Exception.GetBaseException()
            if ($attempt -eq 20 -or ($cause -isnot [IO.IOException] -and $cause -isnot [UnauthorizedAccessException])) { throw }
            Start-Sleep -Milliseconds 500
        }
    }
}
function Remove-EmptyBuildDirectories([string]$Root, [string]$Scope) {
    $null = Assert-BuildPath $Root $Scope
    if (!(Test-Path -LiteralPath $Root)) { return }
    $null = @(Get-SafeBuildFiles $Root)
    foreach ($dir in @(Get-ChildItem -LiteralPath $Root -Directory -Recurse -Force | Sort-Object { $_.FullName.Length } -Descending)) {
        if (@(Get-ChildItem -LiteralPath $dir.FullName -Force).Count -eq 0) { Remove-Item -LiteralPath $dir.FullName -Force }
    }
    if (@(Get-ChildItem -LiteralPath $Root -Force).Count -eq 0) { Remove-Item -LiteralPath $Root -Force }
}
Export-ModuleMember -Function *
