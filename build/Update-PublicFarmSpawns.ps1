param([Parameter(Mandatory=$true)][string]$RathenaRoot)
$ErrorActionPreference='Stop'
$version='d4b8e7b8f16061cc2496d377ac8f777ce72a4f39'
$rows=[Collections.Generic.List[object]]::new();$names=@{}
foreach($config in @('npc/re/scripts_monsters.conf','npc/scripts_monsters.conf')) {
 foreach($line in Get-Content -LiteralPath (Join-Path $RathenaRoot $config)) {
  if($line -notmatch '^npc:\s*(npc/(?:re/)?mobs/[^\s]+)'){continue}
  $relative=$Matches[1];$path=Join-Path $RathenaRoot $relative;if(!(Test-Path $path)){throw "Missing public source $relative"}
  $number=0;foreach($spawn in [IO.File]::ReadAllLines($path)) {
   $number++;if($spawn -match '^//\s*([a-z0-9_]+)\s+-\s+(.+?)\s*$'){$names[$Matches[1]]=$Matches[2]}
   if($spawn -notmatch '^([a-z0-9_]+)(?:,[^\t]+)?\t+(?:boss_monster|monster)\t+'){continue}
   $map=$Matches[1];if($map -match '^(pvp|guild_vs|job_|quiz|jail)' -or $map -eq 'kh_kiehl01'){continue}
   $fields=$spawn -split '\t+';if($fields.Count -ne 4){continue};$values=(($fields[3] -split '//')[0].Trim() -split ',')
   if($values.Count -lt 2 -or $values.Count -gt 5 -or $values[0] -notmatch '^\d+$' -or $values[1] -notmatch '^\d+$'){continue}
   if($values.Count -gt 4 -and $values[4].Trim() -notin @('','0','1')){continue}
   if($values.Count -gt 2 -and $values[2] -notmatch '^\d+$'){continue};if($values.Count -gt 3 -and $values[3] -notmatch '^\d+$'){continue}
   $id=[int]$values[0];$count=[int]$values[1];if($count -le 0){continue}
   $rows.Add([pscustomobject]@{MonsterId=$id;MapCode=$map;Count=$count;SourceUrl="https://github.com/rathena/rathena/blob/$version/${relative}#L$number"})
  }
 }
}
$records=@($rows|Group-Object MonsterId,MapCode|ForEach-Object {$first=$_.Group[0];$name=if($names.ContainsKey($first.MapCode)){$names[$first.MapCode]}else{$first.MapCode};[ordered]@{MonsterId=$first.MonsterId;MapCode=$first.MapCode;MapName=$name;Count=($_.Group|Measure-Object Count -Sum).Sum;SourceUrl=$first.SourceUrl}})
$data=[ordered]@{SourceVersion=$version;Records=$records}
$target=Join-Path $PSScriptRoot '../src/RagnarokCompanion/Data/public-regular-spawns.json';[IO.File]::WriteAllText([IO.Path]::GetFullPath($target),($data|ConvertTo-Json -Depth 5 -Compress),[Text.UTF8Encoding]::new($false))
Write-Output "$($records.Count) public permanent spawn groups; no private server files used."
