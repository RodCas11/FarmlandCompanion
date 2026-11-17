[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Lua)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
New-Item -ItemType Directory -Force (Join-Path $root 'artifacts') | Out-Null
$sdk=Join-Path $root '.tools/dotnet/dotnet.exe'
& $sdk build (Join-Path $root 'src/RagnarokCompanion/RagnarokCompanion.csproj') -c Release --nologo
if($LASTEXITCODE -ne 0){throw 'Compilação do importador falhou.'}
& $sdk (Join-Path $root 'src/RagnarokCompanion/bin/Release/net8.0-windows/FarmlandCompanion.dll') --import-farmland-builds "--lua=$([IO.Path]::GetFullPath($Lua))" "--database=$root/src/RagnarokCompanion/Data/farmland-items.sqlite" "--output=$root/src/RagnarokCompanion/Data/build-farmland-items.json" "--report=$root/artifacts/farmland-custom-import.txt"
if($LASTEXITCODE -ne 0){throw 'Falha na importação dos dados do Farmland.'}
Get-Content -Encoding UTF8 (Join-Path $root 'artifacts/farmland-custom-import.txt')
