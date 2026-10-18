param([string]$Payload = '')
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Import-Module (Join-Path $project 'build/PublishTools.psm1') -Force -DisableNameChecking
if (!$Payload) { $Payload = Join-Path $project 'dist/latest/FarmlandCompanion.exe' }
$scope = Join-Path $project '.tools/publish-tests'
$fixture = Join-Path $scope ([Guid]::NewGuid().ToString('N'))
$null = Assert-BuildPath $fixture $scope
New-Item -ItemType Directory -Force "$fixture/build","$fixture/docs","$fixture/licenses","$fixture/src/RagnarokCompanion/Data","$fixture/.tools","$fixture/dist","$fixture/artifacts" | Out-Null
foreach ($relative in @('build-latest.ps1','build/PublishTools.psm1','README.md','docs/RAGNAPLACE_API.md','docs/SPOTLIGHT.md','licenses/rathena-GPL-3.0.txt','licenses/THIRD-PARTY-NOTICES.md','src/RagnarokCompanion/Data/mvp-seed.json','src/RagnarokCompanion/Data/farmland-items.sqlite','src/RagnarokCompanion/Data/item-monster-rules.json')) { Copy-Item -LiteralPath (Join-Path $project $relative) -Destination (Join-Path $fixture $relative) }
Copy-Item -LiteralPath $Payload -Destination "$fixture/.tools/payload.exe"
Copy-Item -LiteralPath $Payload -Destination "$fixture/dist/RagnarokCompanion-v2.exe"
@{ Application='RagnarokCompanion'; Files=@(@{Path='RagnarokCompanion-v2.exe';Sha256=(Get-FileHash -LiteralPath $Payload).Hash}) } | ConvertTo-Json -Depth 5 | Set-Content "$fixture/build/legacy-artifacts.json" -Encoding UTF8
@'
$global:LASTEXITCODE = 0
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot ('fail-' + $args[0]))) { $global:LASTEXITCODE = 2; return }
if ($args[0] -eq 'publish') {
    $out = $args[[Array]::IndexOf($args, '-o') + 1]
    New-Item -ItemType Directory -Force $out | Out-Null
    if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'invalid-publish')) { Set-Content -LiteralPath (Join-Path $out 'FarmlandCompanion.exe') -Value 'invalid PE' }
    else { Copy-Item -LiteralPath (Join-Path $PSScriptRoot '.tools/payload.exe') -Destination (Join-Path $out 'FarmlandCompanion.exe') }
}
'@ | Set-Content -LiteralPath "$fixture/fake-dotnet.ps1" -Encoding UTF8
$results = [Collections.Generic.List[string]]::new()
function Check([bool]$Condition, [string]$Name) { if (!$Condition) { throw "FAIL: $Name (fixture: $fixture)" }; $results.Add("PASS: $Name") }
function Publish-Fixture {
    try { & "$fixture/build-latest.ps1" -SdkPath "$fixture/fake-dotnet.ps1" -Verbose *> "$fixture/artifacts/last-run.log"; return $true }
    catch { $_ | Format-List * -Force | Out-String | Add-Content "$fixture/artifacts/last-run.log"; return $false }
}
function Hash-Latest { (Get-FileHash -LiteralPath "$fixture/dist/latest/FarmlandCompanion.exe").Hash }

Set-Content -LiteralPath "$fixture/dist/personal-note.txt" -Value 'preserve this unknown file'
Check (Publish-Fixture) 'successful publication and isolated WPF smoke'
Check (!(Test-Path -LiteralPath "$fixture/dist/RagnarokCompanion-v2.exe")) 'exact recognized legacy build removed'
Check (Test-Path -LiteralPath "$fixture/dist/personal-note.txt") 'unknown root file preserved'
$validHash = Hash-Latest
Set-Content -LiteralPath "$fixture/fail-build" -Value 'test'
Check (!(Publish-Fixture) -and (Hash-Latest) -eq $validHash) 'compile failure keeps valid latest'
Remove-Item -LiteralPath "$fixture/fail-build"
Set-Content -LiteralPath "$fixture/fail-test" -Value 'test'
Check (!(Publish-Fixture) -and (Hash-Latest) -eq $validHash) 'test failure keeps valid latest'
Remove-Item -LiteralPath "$fixture/fail-test"
Set-Content -LiteralPath "$fixture/invalid-publish" -Value 'test'
Check (!(Publish-Fixture) -and (Hash-Latest) -eq $validHash) 'invalid executable keeps valid latest'
Remove-Item -LiteralPath "$fixture/invalid-publish"
Set-Content -LiteralPath "$fixture/dist/latest/personal.json" -Value '{"keep":"user data"}'
Check (!(Publish-Fixture) -and (Hash-Latest) -eq $validHash -and (Test-Path -LiteralPath "$fixture/dist/latest/personal.json")) 'unknown file inside latest blocks replacement and remains intact'
Remove-Item -LiteralPath "$fixture/dist/latest/personal.json"
$lock = [IO.File]::Open("$fixture/dist/latest/FarmlandCompanion.exe", 'Open', 'Read', 'Read')
try {
    Check (!(Publish-Fixture) -and (Hash-Latest) -eq $validHash) 'locked executable blocks replacement'
    Check ((Get-Content -LiteralPath "$fixture/artifacts/last-run.log" -Raw) -match 'Sair') 'locked executable reports how to close the running app'
} finally { $lock.Dispose() }
Check (Publish-Fixture) 'republication replaces the same latest'
Check (@(Get-ChildItem -LiteralPath "$fixture/dist" -Filter '*.exe' -Recurse).Count -eq 1) 'only one managed executable remains'
try { $null = Assert-BuildPath (Join-Path $fixture '../outside') $fixture; throw 'path escaped' } catch { Check ($_.Exception.Message -like '*fora da*') 'path traversal is rejected' }
$results | Set-Content -LiteralPath (Join-Path $project 'artifacts/publish-tests.txt') -Encoding UTF8
$results | Write-Output

# All files below belong to this unique fixture, created by this test. No user dirs.
$null = Assert-BuildPath $fixture $scope
foreach ($file in @(Get-SafeBuildFiles $fixture)) {
    $null = Assert-BuildPath $file.FullName $fixture
    for ($attempt = 0; $attempt -lt 21; $attempt++) {
        try { Remove-Item -LiteralPath $file.FullName -Force; break }
        catch {
            if ($attempt -eq 20) { throw }
            Start-Sleep -Milliseconds 500
        }
    }
}
Remove-EmptyBuildDirectories $fixture $scope
