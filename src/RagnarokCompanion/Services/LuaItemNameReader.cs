using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
namespace RagnarokCompanion.Services;
// A data lexer only: no interpreter, calls, expressions or file includes are evaluated.
public static class LuaItemNameReader {
 public sealed record Names(string Identified,string Unidentified,string Description="");
 public static Dictionary<int,Names> Read(string file){
  var bytes=File.ReadAllBytes(file);string text;
  try{text=new UTF8Encoding(false,true).GetString(bytes);}catch(DecoderFallbackException){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);text=Encoding.GetEncoding(1252).GetString(bytes);}
  var result=new Dictionary<int,Names>();int id=0,depth=0;string identified="",unidentified="",description="";
  using var tokens=Tokens(text).GetEnumerator();string? Next()=>tokens.MoveNext()?tokens.Current:null;
  for(string? token=Next();token!=null;token=Next()){
   if(id==0&&token=="["){var number=Next();if(int.TryParse(number,out int candidate)&&candidate>0&&Next()=="]"&&Next()=="="&&Next()=="{"){id=candidate;depth=1;identified=unidentified=description="";}continue;}
   if(id==0)continue;if(token=="{"){depth++;continue;}if(token=="}"){if(--depth==0){if(!string.IsNullOrWhiteSpace(identified))result[id]=new(identified,unidentified,description);id=0;}continue;}
   if(depth==1&&token=="identifiedDescriptionName"){
    if(Next()!="="||Next()!="{")continue;var lines=new List<string>();int nested=1;
    for(var part=Next();part!=null;part=Next()){if(part=="{"){nested++;continue;}if(part=="}"){if(--nested==0)break;continue;}if(nested==1&&part.Length>=2&&(part[0]=='"'||part[0]=='\''))lines.Add(Unescape(part[1..^1]));}
    if(nested!=0)throw new InvalidDataException("Descrição Lua incompleta.");description=string.Join("\n",lines);continue;
   }
   if(depth==1&&token is "identifiedDisplayName" or "unidentifiedDisplayName"){
    string key=token;if(Next()!="=")continue;var value=Next();if(value==null||value.Length<2||value[0]!='"'&&value[0]!='\'')continue;
    string name=Unescape(value[1..^1]);if(name.Length>250)throw new InvalidDataException("Nome de item Lua excede o limite.");if(key=="identifiedDisplayName")identified=name;else unidentified=name;
   }
  }
  if(id!=0)throw new InvalidDataException("Tabela de itens Lua incompleta.");return result;
 }
 static IEnumerable<string> Tokens(string text){
  for(int i=0;i<text.Length;){char c=text[i];if(char.IsWhiteSpace(c)||c=='\uFEFF'){i++;continue;}
   if(c=='-'&&i+1<text.Length&&text[i+1]=='-'){i+=2;if(LongBracket(text,i,out int start,out string end)){int stop=text.IndexOf(end,start,StringComparison.Ordinal);i=stop<0?text.Length:stop+end.Length;}else{while(i<text.Length&&text[i]!='\n')i++;}continue;}
   if(LongBracket(text,i,out int content,out string closing)){int stop=text.IndexOf(closing,content,StringComparison.Ordinal);if(stop<0)throw new InvalidDataException("String Lua incompleta.");yield return "\""+text[content..stop].Replace("\\","\\\\").Replace("\"","\\\"")+"\"";i=stop+closing.Length;continue;}
   if(c=='"'||c=='\''){int start=i++;bool closed=false;while(i<text.Length){if(text[i]=='\\'){i+=Math.Min(2,text.Length-i);continue;}if(text[i++]==c){closed=true;break;}}if(!closed)throw new InvalidDataException("String Lua incompleta.");yield return text[start..i];continue;}
   if(char.IsLetterOrDigit(c)||c=='_'){int start=i++;while(i<text.Length&&(char.IsLetterOrDigit(text[i])||text[i]=='_'))i++;yield return text[start..i];continue;}
   yield return text[i++].ToString();
  }
 }
 static bool LongBracket(string text,int index,out int content,out string end){content=index;end="";if(index>=text.Length||text[index]!='[')return false;int i=index+1;while(i<text.Length&&text[i]=='=')i++;if(i>=text.Length||text[i]!='[')return false;content=i+1;end="]"+new string('=',i-index-1)+"]";return true;}
 static string Unescape(string value){var output=new StringBuilder();for(int i=0;i<value.Length;i++){char c=value[i];if(c!='\\'||i+1==value.Length){output.Append(c);continue;}c=value[++i];if(char.IsAsciiDigit(c)){int number=c-'0',count=1;while(count<3&&i+1<value.Length&&char.IsAsciiDigit(value[i+1])){number=number*10+value[++i]-'0';count++;}output.Append((char)number);}else if(c=='x'&&i+2<value.Length&&int.TryParse(value.AsSpan(i+1,2),System.Globalization.NumberStyles.HexNumber,null,out int hex)){output.Append((char)hex);i+=2;}else output.Append(c switch{'n'=>'\n','r'=>'\r','t'=>'\t',_=>c});}return output.ToString();}
}
