[CmdletBinding()]
param([string]$SourceDirectory='',[string]$OutputDatabase='',[string]$Rules='',[string]$Spawns='',[string]$Lua='',[switch]$DataOnly,[string]$Descriptions='')
$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(!$SourceDirectory){$SourceDirectory=$project}
if(!$OutputDatabase){$OutputDatabase=Join-Path $project 'src/RagnarokCompanion/Data/farmland-items.sqlite'}
if(!$Rules){$Rules=Join-Path $project 'src/RagnarokCompanion/Data/item-monster-rules.json'}
$sdk=Join-Path $project '.tools/dotnet/dotnet.exe';if(!(Test-Path $sdk)){$sdk='dotnet'}
& $sdk build (Join-Path $project 'src/RagnarokCompanion/RagnarokCompanion.csproj') -c Release --nologo
if($LASTEXITCODE -ne 0){throw 'Compilação do importador falhou; banco existente preservado.'}
$database=[IO.Path]::GetFullPath($OutputDatabase)
$report=Join-Path $project 'artifacts/item-import.txt';New-Item -ItemType Directory -Force (Split-Path $report) | Out-Null
$dll=Join-Path $project 'src/RagnarokCompanion/bin/Release/net8.0-windows/FarmlandCompanion.dll'
if($Descriptions){& $sdk $dll --import-item-descriptions "--descriptions=$([IO.Path]::GetFullPath($Descriptions))" "--database=$database" "--import-report=$report"}
elseif($DataOnly){if(!$Lua){$Lua=Join-Path $SourceDirectory 'bRO.lua'};& $sdk $dll --update-item-data "--lua=$([IO.Path]::GetFullPath($Lua))" "--database=$database" "--import-report=$report"}
elseif($Spawns){& $sdk $dll --import-item-spawns "--spawns=$([IO.Path]::GetFullPath($Spawns))" "--database=$database" "--import-report=$report"}
else{& $sdk $dll --import-items "--source=$([IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\','/'))" "--database=$database" "--rules=$([IO.Path]::GetFullPath($Rules))" "--import-report=$report"}
if(Test-Path $report){Get-Content -Encoding UTF8 $report}
if($LASTEXITCODE -ne 0){throw 'Importação falhou; arquivos originais e último banco válido preservados.'}
Write-Output "Banco público: $database"
