param([Parameter(Mandatory=$true)][string]$RathenaRoot)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($RathenaRoot)
$version=(& git -C $root rev-parse HEAD).Trim()
if($LASTEXITCODE -ne 0){throw 'Checkout rAthena inválido.'}
$verified=[DateTimeOffset]::UtcNow.ToString('o')
$settings=[IO.File]::ReadAllText((Join-Path $root 'conf/battle/monster.conf'))
foreach($rule in @('mob_spawn_delay: 100','boss_spawn_delay: 100','plant_spawn_delay: 100','mob_spawn_variance: 1','mob_respawn_time: 1000')){
    if($settings -notmatch ('(?m)^'+[regex]::Escape($rule)+'\r?$')){throw "Configuração global mudou; revisar o cálculo: $rule"}
}
$database=[IO.File]::ReadAllText((Join-Path $root 'db/re/mob_db.yml'))
$mvps=@{}
foreach($block in [regex]::Split($database,'(?m)^  - Id: ')[1..([regex]::Matches($database,'(?m)^  - Id: ').Count)]){
    $body=$block
    if($body -notmatch '(?m)^    MvpExp: [1-9][0-9]*\r?$'){continue}
    $name=[regex]::Match($body,'(?m)^    Name: (.+)$').Groups[1].Value.Trim().Trim('"')
    $mvps[[int]($body -split '\r?\n',2)[0]]=$name
}
$records=[Collections.Generic.List[object]]::new()
foreach($config in @('npc/re/scripts_monsters.conf','npc/scripts_monsters.conf')){
    foreach($line in Get-Content -LiteralPath (Join-Path $root $config)){
        if($line -notmatch '^npc:\s*(npc/(?:re/)?mobs/[^\s]+)'){continue}
        $relative=$Matches[1];$path=Join-Path $root $relative
        if(!(Test-Path -LiteralPath $path)){throw "Arquivo ativo não disponível: $relative"}
        $lineNumber=0
        foreach($spawn in Get-Content -LiteralPath $path){
            $lineNumber++;$spawn=$spawn.Trim()
            if($spawn -notmatch '^[a-z0-9_]+(?:,[^\t]+)?\t+(?:boss_monster|monster)\t+'){continue}
            $fields=$spawn -split '\t+';if($fields.Count -lt 4){continue}
            $values=($fields[3] -split '//')[0].Trim() -split ','
            if($values.Count -lt 3 -or $values[0] -notmatch '^\d+$'){continue}
            $id=[int]$values[0];if(!$mvps.ContainsKey($id)){continue}
            if($values[1] -ne '1' -or $values[2] -notmatch '^\d+$'){continue}
            if($values.Count -gt 4 -and $values[4].Trim() -notin @('','0','1')){continue}
            $base=[long]$values[2];$rawVariance=if($values.Count -gt 3 -and $values[3] -match '^\d+$'){[long]$values[3]}else{0}
            if($base -lt 1000 -or $base+$rawVariance -gt 604800000){continue}
            $variance=if($fields[1] -eq 'boss_monster'){$rawVariance}else{0}
            $map=($fields[0] -split ',')[0]
            $records.Add([pscustomobject][ordered]@{MonsterId=$id;MonsterName=$mvps[$id];MapCode=$map;ScheduleType='Interval';SpawnIdentifier="${relative}:$lineNumber";RespawnBaseSeconds=$base/1000;RespawnVarianceSeconds=$variance/1000;RespawnMinSeconds=$base/1000;RespawnMaxSeconds=($base+$variance)/1000;Source='rAthena Renewal';SourceVersion=$version;SourceUrl="https://github.com/rathena/rathena/blob/$version/${relative}#L$lineNumber";IsFarmlandConfirmed=$false;LastVerifiedAt=$verified;RawVarianceMilliseconds=$rawVariance;SpawnDefinition=$spawn})
        }
    }
}
$known=@($records|ForEach-Object {$_.MonsterId}|Sort-Object -Unique)
$excluded=@($mvps.Keys|Where-Object{$_ -notin $known}|Sort-Object|ForEach-Object{[ordered]@{MonsterId=$_;MonsterName=$mvps[$_];Reason='Sem spawn permanente convencional habilitado nas listas Renewal examinadas; pode ser instância, evento, invocação ou variante.'}})
$catalog=[ordered]@{Version=1;SourceVersion=$version;LastVerifiedAt=$verified;Rules='Renewal, permanent enabled spawns only; boss_spawn_delay=100; mob_spawn_delay=100; mob_spawn_variance=1. delay1 + rnd()%delay2 in milliseconds. Upper bound is conservative/exclusive before rounding. Farmland settings are unknown.';Records=@($records|Sort-Object MonsterId,MapCode,SpawnIdentifier);WithoutConventionalSpawn=$excluded}
$target=Join-Path $PSScriptRoot '../src/RagnarokCompanion/Data/mvp-respawns.json'
[IO.File]::WriteAllText([IO.Path]::GetFullPath($target),($catalog|ConvertTo-Json -Depth 9),[Text.UTF8Encoding]::new($false))
Write-Output "Verified: $($known.Count) MVP IDs; $($records.Count) spawn definitions; $($excluded.Count) IDs without conventional spawn. Source $version"
