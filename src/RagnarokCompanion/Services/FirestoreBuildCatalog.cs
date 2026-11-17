using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public sealed partial class FirestoreBuildGateway
{
 static long Number(JsonElement fields,string key,long fallback=0)=>fields.TryGetProperty(key,out var v)&&v.TryGetProperty("integerValue",out var n)&&long.TryParse(n.GetString(),out long value)?value:fallback;
 static CommunityBuild ReadPublished(JsonElement doc,bool content)
 {
  var f=doc.GetProperty("fields");string date=Text(f,"updatedAt");var build=content?JsonSerializer.Deserialize<PlayerBuild>(Text(f,"payload")):new PlayerBuild{Id=Text(f,"buildId"),Title=Text(f,"title"),ClassId=Text(f,"classId"),Stage=Text(f,"stage"),Purpose=Text(f,"purpose"),Equipment=new(),Stats=new(),Notes="",UpdatedUtc=DateTimeOffset.TryParse(date,out var dt)?dt:DateTimeOffset.MinValue};
  return new(doc.GetProperty("name").GetString()!.Split('/')[^1],build??throw new InvalidOperationException("Build sem conteúdo."),"approved",Text(f,"authorName"),Text(f,"ownerUid"),(int)Number(f,"starCount"),false,Number(f,"version",1),date,content,Text(f,"adminNotes"));
 }
 public async Task<CatalogPage> FetchPage(string classId,string stage,string purpose,bool stars,string? cursor,CancellationToken ct)
 {
  var filters=new[]{("classId",classId),("stage",stage),("purpose",purpose)}.Where(f=>!string.IsNullOrEmpty(f.Item2)&&f.Item2!="Todas").Select(f=>new{fieldFilter=new{field=new{fieldPath=f.Item1},op="EQUAL",value=S(f.Item2)}}).ToArray();
  object? start=null;if(cursor!=null){using var d=JsonDocument.Parse(cursor);var values=new List<object>();if(stars)values.Add(new{integerValue=d.RootElement.GetProperty("Stars").GetInt32().ToString()});values.Add(new{referenceValue=d.RootElement.GetProperty("Name").GetString()});start=new{values,before=false};}
  var order=new List<object>();if(stars)order.Add(new{field=new{fieldPath="starCount"},direction="DESCENDING"});order.Add(new{field=new{fieldPath="__name__"},direction=stars?"DESCENDING":"ASCENDING"});
  string[] projection={"buildId","title","classId","stage","purpose","authorName","ownerUid","starCount","version","updatedAt","adminNotes"};
  using var query=await Send(HttpMethod.Post,":runQuery",new{structuredQuery=new{select=new{fields=projection.Select(s=>new{fieldPath=s})},from=new[]{new{collectionId="approvedBuilds"}},where=filters.Length==0?null:(object)new{compositeFilter=new{op="AND",filters}},orderBy=order,startAt=start,limit=20}},ct);
  var docs=query.RootElement.EnumerateArray().Where(r=>r.TryGetProperty("document",out _)).Select(r=>r.GetProperty("document")).ToArray();var rows=docs.Select(d=>ReadPublished(d,false)).ToList();rows=await WithAuthors(rows,ct);rows=await AttachStars(rows,ct);
  string? next=docs.Length==20?JsonSerializer.Serialize(new{Name=docs[^1].GetProperty("name").GetString(),Stars=rows[^1].StarCount}):null;return new(rows,next,next!=null);
 }
 public async Task<CommunityBuild> FetchContent(string id,CancellationToken ct){using var doc=await Send(HttpMethod.Get,"/approvedBuilds/"+Uri.EscapeDataString(id),null,ct);return ReadPublished(doc.RootElement,true);}
 public async Task<BuildCommunitySnapshot> FetchProgress(CancellationToken ct){using var doc=await TryDocument("communityProgress/global",ct);if(doc==null)return new(new(),new(),false,"empty");var f=doc.RootElement.GetProperty("fields");var keys=new List<string>();if(f.TryGetProperty("approvedKeys",out var a)&&a.TryGetProperty("arrayValue",out var v)&&v.TryGetProperty("values",out var values))foreach(var key in values.EnumerateArray())keys.Add(key.GetProperty("stringValue").GetString()!);bool reward=f.TryGetProperty("rewardUnlocked",out var r)&&r.GetProperty("booleanValue").GetBoolean();return new(new(),keys,reward,doc.RootElement.GetProperty("updateTime").GetString()??"server");}
}
