param([string]$Quests=(Join-Path $PSScriptRoot '../quests.txt'),[string]$SourceDirectory=(Join-Path $PSScriptRoot '..'),[string]$Database=(Join-Path $PSScriptRoot '../src/RagnarokCompanion/Data/farmland-items.sqlite'))
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$sdk=Join-Path $root '.tools/dotnet/dotnet.exe';if(!(Test-Path $sdk)){$sdk='dotnet'}
& $sdk build "$root/src/RagnarokCompanion/RagnarokCompanion.csproj" -c Release --nologo
if($LASTEXITCODE -ne 0){throw 'Compilação falhou; banco preservado.'}
$report=Join-Path $root 'artifacts/quest-import.txt'
& $sdk "$root/src/RagnarokCompanion/bin/Release/net8.0-windows/FarmlandCompanion.dll" --import-quests "--quests=$([IO.Path]::GetFullPath($Quests))" "--source=$([IO.Path]::GetFullPath($SourceDirectory))" "--database=$([IO.Path]::GetFullPath($Database))" "--import-report=$report"
if(Test-Path $report){Get-Content $report -Encoding UTF8}
if($LASTEXITCODE -ne 0){throw 'Importação interrompida; banco anterior preservado.'}
