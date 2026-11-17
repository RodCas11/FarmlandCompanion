[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$SourceDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sdk=Join-Path $root '.tools/dotnet/dotnet.exe'
New-Item -ItemType Directory -Force (Join-Path $root 'artifacts') | Out-Null
& $sdk build (Join-Path $root 'src/RagnarokCompanion/RagnarokCompanion.csproj') -c Release --nologo
if($LASTEXITCODE -ne 0){throw 'Compilação do importador falhou.'}
& $sdk (Join-Path $root 'src/RagnarokCompanion/bin/Release/net8.0-windows/FarmlandCompanion.dll') --complete-item-catalog "--source=$([IO.Path]::GetFullPath($SourceDirectory))" "--database=$root/src/RagnarokCompanion/Data/farmland-items.sqlite" "--equipment=$root/src/RagnarokCompanion/Data/build-equipment.json.gz" "--report=$root/artifacts/complete-item-import.txt"
if($LASTEXITCODE -ne 0){throw 'Importação incompleta. Consulte o relatório local.'}
Get-Content -Encoding UTF8 (Join-Path $root 'artifacts/complete-item-import.txt')
