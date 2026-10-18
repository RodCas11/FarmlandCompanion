using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace RagnarokCompanion.Services;
public sealed record DescriptionPart(string Text,string? Color);
public sealed record DescriptionLine(IReadOnlyList<DescriptionPart> Parts,bool Divider=false,bool Heading=false) {
 public string Text=>string.Concat(Parts.Select(p=>p.Text));
}
public static class RagnarokDescriptionFormatter {
 static readonly Regex colors=new(@"\^([0-9a-fA-F]{6})",RegexOptions.Compiled);
 static readonly Regex equipment=new(@"^(Tipo|Type|Equipa em|Posição|Peso|Weight|Defesa|DEF|DEFM|Nível necessário|Nível requerido|Required Level|Classes?|Jobs?|Ataque|Attack|ATQ|ATQM|Elemento|Slots?)\s*:",RegexOptions.IgnoreCase|RegexOptions.Compiled);
 static readonly Regex effects=new(@"^(FOR|STR|AGI|VIT|INT|DES|DEX|SOR|LUK|ATQ|ATQM|DEF|DEFM|HP|SP|Resistência|Resistance|Dano|Damage|Precisão|Esquiva|Velocidade|Aumenta|Reduz)\b.*(?:[+−-]\s*\d|\d\s*%)",RegexOptions.IgnoreCase|RegexOptions.Compiled);
 public static IReadOnlyList<DescriptionLine> Format(string? raw){
  string text=(raw??"").Replace("\\r\\n","\n").Replace("\\n","\n").Replace("\\r","\n").Replace("\r\n","\n").Replace('\r','\n').Replace("\\t"," ").Replace('\t',' ');
  text=Regex.Replace(text,@"\x1B\[[0-?]*[ -/]*[@-~]","");
  text=new string(text.Where(c=>!char.IsControl(c)||c=='\n').ToArray());
  var lines=new List<DescriptionLine>();string? color=null;bool effectsHeading=false,equipmentHeading=false;
  foreach(var source in text.Split('\n')){
   var parts=new List<DescriptionPart>();int offset=0;foreach(Match match in colors.Matches(source)){if(match.Index>offset)parts.Add(new(source[offset..match.Index],color));color=match.Groups[1].Value.Equals("000000",StringComparison.OrdinalIgnoreCase)?null:"#"+match.Groups[1].Value.ToUpperInvariant();offset=match.Index+match.Length;}
   if(offset<source.Length)parts.Add(new(source[offset..],color));string plain=string.Concat(parts.Select(p=>p.Text)).Trim();
   if(Regex.IsMatch(plain,@"^[-–—_][\s\-–—_]{2,}$")){if(lines.Count>0&&!lines[^1].Divider)lines.Add(new(Array.Empty<DescriptionPart>(),Divider:true));continue;}
   if(plain.Length==0){if(lines.Count>0&&lines[^1].Text.Length>0&&!lines[^1].Divider)lines.Add(new(Array.Empty<DescriptionPart>()));continue;}
   bool explicitEffects=Regex.IsMatch(plain,@"^(Efeitos|Effects|Bônus|Bonuses)\s*:?$",RegexOptions.IgnoreCase);bool explicitEquipment=Regex.IsMatch(plain,@"^(Equipamento|Equipment|Requisitos|Requirements)\s*:?$",RegexOptions.IgnoreCase);
   if(equipment.IsMatch(plain)&&!equipmentHeading){lines.Add(new(new[]{new DescriptionPart("Equipamento",null)},Heading:true));equipmentHeading=true;}
   else if(effects.IsMatch(plain)&&!effectsHeading&&!equipmentHeading){lines.Add(new(new[]{new DescriptionPart("Efeitos",null)},Heading:true));effectsHeading=true;}
   effectsHeading|=explicitEffects;equipmentHeading|=explicitEquipment;
   lines.Add(new(parts,Heading:explicitEffects||explicitEquipment));
  }
  while(lines.Count>0&&(lines[^1].Divider||lines[^1].Text.Length==0))lines.RemoveAt(lines.Count-1);return lines;
 }
 public static string PlainText(string? raw)=>string.Join("\n",Format(raw).Where(l=>!l.Divider).Select(l=>l.Text));
}
