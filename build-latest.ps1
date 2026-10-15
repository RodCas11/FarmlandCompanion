[CmdletBinding()]
param([string]$SdkPath = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'build/PublishTools.psm1') -Force -DisableNameChecking
if (!$SdkPath) { $SdkPath = Join-Path $PSScriptRoot '.tools/dotnet/dotnet.exe'; if (!(Test-Path -LiteralPath $SdkPath)) { $SdkPath = 'dotnet' } }
$dist = Join-Path $PSScriptRoot 'dist'
$work = Join-Path $PSScriptRoot '.tools/latest-staging'
$transaction = Join-Path $work ([Guid]::NewGuid().ToString('N'))
$package = Join-Path $transaction 'package'
$latest = Join-Path $dist 'latest'
$previous = Join-Path $transaction 'previous-valid'
$legacyBackup = Join-Path $transaction 'legacy'
$moved = [Collections.Generic.List[object]]::new()
$cleanup = [Collections.Generic.List[object]]::new()
$removed = [Collections.Generic.List[string]]::new()
$preserved = [Collections.Generic.List[string]]::new()
$committed = $false
$oldMoved = $false
$newInstalled = $false
$publishLock = $null
Push-Location $PSScriptRoot
try {
    $null = Assert-BuildPath $dist $PSScriptRoot
    $null = Assert-BuildPath $work $PSScriptRoot
    $null = Assert-BuildPath $transaction $work
    New-Item -ItemType Directory -Force $work | Out-Null
    try { $publishLock = [IO.File]::Open((Join-Path $work '.publish.lock'), 'OpenOrCreate', 'ReadWrite', 'None') }
    catch { throw 'Outra publicação está em execução. Aguarde sua conclusão.' }
    & $sdkPath build src/RagnarokCompanion/RagnarokCompanion.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Compilação falhou. A última distribuição válida permanece intacta.' }
    & $sdkPath test tests/RagnarokCompanion.Tests/RagnarokCompanion.Tests.csproj -c Release --logger 'trx;LogFileName=tests.trx' --results-directory artifacts
    if ($LASTEXITCODE -ne 0) { throw 'Testes falharam. A última distribuição válida permanece intacta.' }
    & $sdkPath publish src/RagnarokCompanion/RagnarokCompanion.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $package
    if ($LASTEXITCODE -ne 0) { throw 'Publicação temporária falhou. A distribuição atual permanece intacta.' }
    New-Item -ItemType Directory -Force "$package/docs","$package/licenses","$package/Data" | Out-Null
    Copy-Item -LiteralPath README.md -Destination "$package/README.md"
    Copy-Item -LiteralPath docs/RAGNAPLACE_API.md -Destination "$package/docs/RAGNAPLACE_API.md"
    Copy-Item -LiteralPath docs/SPOTLIGHT.md -Destination "$package/docs/SPOTLIGHT.md"
    Copy-Item -LiteralPath licenses/rathena-GPL-3.0.txt,licenses/THIRD-PARTY-NOTICES.md -Destination "$package/licenses"
    Copy-Item -LiteralPath src/RagnarokCompanion/Data/mvp-seed.json -Destination "$package/Data/mvp-seed.json"
    New-BuildManifest $package
    $null = Test-BuildPackage $package
    $reportDir = Join-Path $PSScriptRoot 'artifacts/latest-smoke'
    New-Item -ItemType Directory -Force $reportDir | Out-Null
    $report = Join-Path $reportDir 'smoke-test.txt'
    if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
    $smoke = Start-Process -FilePath "$package/FarmlandCompanion.exe" -ArgumentList @('--smoke-test', ('--smoke-output="{0}"' -f $reportDir)) -PassThru -WindowStyle Hidden
    if (!$smoke.WaitForExit(30000)) { Stop-Process -Id $smoke.Id; throw 'O smoke test da nova build não concluiu; distribuição anterior preservada.' }
    if ($smoke.ExitCode -ne 0 -or !(Test-Path -LiteralPath $report) -or (Get-Content -LiteralPath $report -Raw) -notmatch '^PASS:') { throw "Falha no smoke test. Consulte $report. A distribuição anterior foi preservada." }
    $null = Test-BuildPackage $package

    # Recognize only the exact legacy artifacts produced by this project, by SHA256.
    $legacy = Get-Content -LiteralPath build/legacy-artifacts.json -Raw -Encoding UTF8 | ConvertFrom-Json
    $legacyCandidates = [Collections.Generic.List[object]]::new()
    foreach ($record in $legacy.Files) {
        $path = Assert-BuildPath (Join-Path $dist $record.Path) $dist
        if (!(Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $record.Sha256) { $legacyCandidates.Add($record) }
        else { $preserved.Add("$($record.Path): conteúdo alterado ou desconhecido") }
    }
    # Preserve whole legacy subdirectories if they contain any unknown content.
    foreach ($folder in @('Data','docs','licenses')) {
        $dir = Join-Path $dist $folder
        if (!(Test-Path -LiteralPath $dir)) { continue }
        try {
            $unknown = @(Get-SafeBuildFiles $dir | Where-Object { $rel = Get-BuildRelative $dist $_.FullName; @($legacyCandidates | Where-Object { $_.Path -eq $rel }).Count -ne 1 })
            if ($unknown.Count) { throw 'contém arquivos desconhecidos ou alterados' }
        } catch {
            $preserved.Add("${folder}: $($_.Exception.Message)")
            foreach ($candidate in @($legacyCandidates.ToArray())) { if ($candidate.Path.StartsWith($folder + '/')) { $null = $legacyCandidates.Remove($candidate) } }
        }
    }
    foreach ($item in @(Get-ChildItem -LiteralPath $dist -Force -ErrorAction SilentlyContinue)) {
        if ($item.Name -notin @('latest','Data','docs','licenses') -and @($legacy.Files | Where-Object { $_.Path -eq $item.Name }).Count -eq 0) { $preserved.Add("$($item.Name): arquivo/pasta sem identificação de build") }
    }
    if (Test-Path -LiteralPath $latest) {
        $null = Assert-BuildPath $latest $dist
        $null = Test-BuildPackage $latest
        foreach ($file in @(Get-SafeBuildFiles $latest)) { Assert-BuildFileUnlocked $file.FullName }
    }
    foreach ($record in $legacyCandidates) { Assert-BuildFileUnlocked (Join-Path $dist $record.Path) }
    New-Item -ItemType Directory -Force $dist | Out-Null
    if (Test-Path -LiteralPath $latest) { Write-Verbose "Moving previous: $latest -> $previous"; [IO.Directory]::Move($latest, $previous); $oldMoved = $true }
    foreach ($record in $legacyCandidates) {
        $source = Join-Path $dist $record.Path; $backup = Join-Path $legacyBackup $record.Path
        New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($backup)) | Out-Null
        Move-Item -LiteralPath $source -Destination $backup
        $moved.Add(@{Source=$source;Backup=$backup;Hash=$record.Sha256}); $removed.Add($record.Path)
    }
    Write-Verbose "Installing: $package -> $latest"
    Move-StagedBuildPackage $package $latest $work $dist
    $newInstalled = $true
    $null = Test-BuildPackage $latest
    $committed = $true
    foreach ($folder in @('Data','docs','licenses')) { if ((Join-Path $dist $folder) -notin $preserved) { Remove-EmptyBuildDirectories (Join-Path $dist $folder) $dist } }
    # Only exact files whose hashes were checked are removed, never a recursive wildcard.
    if (Test-Path -LiteralPath $previous) {
        try { $null = Test-BuildPackage $previous; foreach ($file in @(Get-SafeBuildFiles $previous)) { $cleanup.Add(@{Path=$file.FullName;Hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}) } }
        catch { Write-Warning "Temporário anterior preservado: $previous" }
    }
    foreach ($entry in $moved) { $cleanup.Add(@{Path=$entry.Backup;Hash=$entry.Hash}) }
    Write-Host "Publicação concluída: $latest/FarmlandCompanion.exe"
    foreach ($name in $removed) { Write-Host "Removido da distribuição: $name" }
    foreach ($name in $preserved) { Write-Warning "Preservado: $name" }
    @{ Executable='dist/latest/FarmlandCompanion.exe'; Removed=$removed.ToArray(); Preserved=$preserved.ToArray(); Sha256=(Get-FileHash -LiteralPath "$latest/FarmlandCompanion.exe").Hash } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath artifacts/publication.json -Encoding UTF8
} catch {
    if (!$committed) {
        if ($newInstalled) { [IO.Directory]::Move($latest, $package) }
        foreach ($entry in @($moved.ToArray()) | Sort-Object { $_.Source.Length } -Descending) {
            New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($entry.Source)) | Out-Null
            Write-Verbose "Restoring legacy: $($entry.Backup)"; Move-Item -LiteralPath $entry.Backup -Destination $entry.Source
        }
        if ($oldMoved -and !(Test-Path -LiteralPath $latest)) { [IO.Directory]::Move($previous, $latest) }
    }
    throw
} finally {
    # Temp package created by this invocation is safe only if it has a valid manifest.
    if (Test-Path -LiteralPath $package) {
        try { $null = Test-BuildPackage $package; foreach ($file in @(Get-SafeBuildFiles $package)) { $cleanup.Add(@{Path=$file.FullName;Hash=(Get-FileHash -LiteralPath $file.FullName).Hash}) } }
        catch { Write-Warning "Temporário preservado para inspeção: $package" }
    }
    foreach ($entry in $cleanup) {
        try { $null = Assert-BuildPath $entry.Path $transaction; if ((Test-Path -LiteralPath $entry.Path) -and (Get-FileHash -LiteralPath $entry.Path).Hash -eq $entry.Hash) { Remove-Item -LiteralPath $entry.Path -Force } }
        catch { Write-Warning "Temporário preservado: $($entry.Path)" }
    }
    if (Test-Path -LiteralPath $transaction) { Remove-EmptyBuildDirectories $transaction $work }
    if ($publishLock) { $publishLock.Dispose() }
    Pop-Location
}
