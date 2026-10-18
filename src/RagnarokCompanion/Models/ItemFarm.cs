using System;
using System.Collections.Generic;
using System.Globalization;
namespace RagnarokCompanion.Models;
public sealed class FarmCatalog {
 public int Version {get;set;} public string SourceVersion {get;set;}="";
 public List<FarmItem> Items {get;set;}=new(); public List<FarmMonster> Monsters {get;set;}=new(); public List<FarmDrop> Drops {get;set;}=new();
}
public sealed class FarmItem {public int Id {get;set;} public string Name {get;set;}="";public string Alias {get;set;}=""; public string EnglishName {get;set;}=""; public string UnidentifiedName {get;set;}=""; public string Label=>$"{Name} · #{Id}";}
public sealed class FarmMonster {public int Id {get;set;} public string Name {get;set;}="";public bool IsSpecial {get;set;} public List<FarmSpawn> Spawns {get;set;}=new();}
public sealed class FarmSpawn {public string Map {get;set;}="";public string Name {get;set;}="";public int? Count {get;set;}public string Evidence {get;set;}="";}
public sealed class FarmDrop {public int ItemId {get;set;}public int MonsterId {get;set;}public double Chance {get;set;} }
public sealed class FarmResult {
 public FarmMonster Monster {get;init;}=new();public FarmSpawn? BestMap {get;init;}public double Chance {get;init;}
 public bool Farmable=>!Monster.IsSpecial;
 public bool HasMap=>BestMap!=null;
 public string MonsterName=>Monster.Name;public string ChanceLabel=>Chance.ToString("0.##",CultureInfo.GetCultureInfo("pt-BR"))+"%";
 public string MapLabel=>BestMap==null?"Mapa não identificado":BestMap.Name==BestMap.Map?BestMap.Map:BestMap.Name+" · "+BestMap.Map;
 public string Population=>BestMap?.Count is int count?count==1?"1 monstro":$"{count} monstros":"Quantidade desconhecida";
 public double? Score=>BestMap?.Count is int count?Chance*count:null;
 public string ScoreLabel=>Score is double score?$"Indicador: {score:0.##} · não representa itens/hora":"Indicador indisponível";
 public string MapSource=>BestMap==null?"Regularidade não confirmada":BestMap.Evidence.StartsWith("Divine Pride",StringComparison.Ordinal)?BestMap.Evidence.Split('(')[0]:BestMap.Evidence.StartsWith("rAthena",StringComparison.Ordinal)?"Mapa/população: referência Renewal, não confirmado no Farmland":"Mapa/população: Farmland";
 public string SourceLabel=>"Drops Farmland · dados públicos autorizados";
 public string Evidence=>BestMap?.Evidence??"Sem arquivos de spawn Farmland: regularidade e população não confirmadas.";
 public string Heading=>Farmable?"Monstro comum · "+(HasMap?"mapa de referência disponível":"spawn não confirmado"):"Variante especial — excluída por padrão";
}
public sealed class ItemFarmPreferences { public int SelectedId {get;set;}public double Minimum {get;set;}=10;public bool FarmableOnly {get;set;}=true;public string Sort {get;set;}="Maior chance";}
