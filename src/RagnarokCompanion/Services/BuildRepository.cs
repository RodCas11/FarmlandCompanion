using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public sealed partial class BuildRepository
{
    readonly string path;readonly BuildLog log;public List<string> Warnings {get;}=new();public HashSet<string> RecoveredIds {get;}=new();
    public BuildRepository(string root){Directory.CreateDirectory(root);log=new(root);path=Path.Combine(root,"builds.sqlite");using var db=Open();using var c=db.CreateCommand();c.CommandText="PRAGMA journal_mode=WAL;CREATE TABLE IF NOT EXISTS build_drafts(id TEXT PRIMARY KEY,class_id TEXT NOT NULL,stage TEXT NOT NULL,purpose TEXT NOT NULL,title TEXT NOT NULL,updated TEXT NOT NULL,payload TEXT NOT NULL);CREATE TABLE IF NOT EXISTS community_build_cache(id TEXT PRIMARY KEY,class_id TEXT NOT NULL,stage TEXT NOT NULL,purpose TEXT NOT NULL,payload TEXT NOT NULL);";c.ExecuteNonQuery();}
    SqliteConnection Open(){var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Pooling=false,DefaultTimeout=5}.ToString());db.Open();return db;}
    public string DiscordNickname(string uid){using var db=Open();using var c=db.CreateCommand();c.CommandText="CREATE TABLE IF NOT EXISTS build_profiles(uid TEXT PRIMARY KEY,discord_nick TEXT NOT NULL)";c.ExecuteNonQuery();c.CommandText="SELECT discord_nick FROM build_profiles WHERE uid=$uid";c.Parameters.AddWithValue("$uid",uid);return c.ExecuteScalar() as string??"";}
    public void SaveDiscordNickname(string uid,string nickname){nickname=nickname.Trim();if(string.IsNullOrWhiteSpace(uid)||(nickname.Length==1||nickname.Length>64)||System.Linq.Enumerable.Any(nickname,char.IsControl))throw new ArgumentException("Informe um nick de 2 a 64 caracteres, ou deixe vazio para usar o nome Google.");_ = DiscordNickname(uid);using var db=Open();using var c=db.CreateCommand();c.CommandText="INSERT OR REPLACE INTO build_profiles VALUES($uid,$nick)";c.Parameters.AddWithValue("$uid",uid);c.Parameters.AddWithValue("$nick",nickname);c.ExecuteNonQuery();}
    public void Save(PlayerBuild build)
    {
        string json=JsonSerializer.Serialize(build);if(json.Length>150000||!Guid.TryParseExact(build.Id,"N",out _)||build.Title.Length>120||build.Notes.Length>8000)throw new InvalidDataException("Build inválida ou acima do limite.");
        using var db=Open();using var cmd=db.CreateCommand();cmd.CommandText="INSERT INTO build_drafts VALUES($id,$class,$stage,$purpose,$title,$updated,$payload) ON CONFLICT(id) DO UPDATE SET class_id=$class,stage=$stage,purpose=$purpose,title=$title,updated=$updated,payload=$payload";
        cmd.Parameters.AddWithValue("$id",build.Id);cmd.Parameters.AddWithValue("$class",build.ClassId);cmd.Parameters.AddWithValue("$stage",build.Stage);cmd.Parameters.AddWithValue("$purpose",build.Purpose);cmd.Parameters.AddWithValue("$title",build.Title);cmd.Parameters.AddWithValue("$updated",build.UpdatedUtc.ToString("O"));cmd.Parameters.AddWithValue("$payload",json);cmd.ExecuteNonQuery();
    }
    public List<PlayerBuild> Load()
    {
        Warnings.Clear();RecoveredIds.Clear();using var db=Open();using var c=db.CreateCommand();c.CommandText="SELECT id,title,class_id,stage,purpose,payload FROM build_drafts ORDER BY updated DESC";using var reader=c.ExecuteReader();var result=new List<PlayerBuild>();
        while(reader.Read()){
            string id=reader.GetString(0),payload=reader.GetString(5);PlayerBuild? build=null;
            try{build=JsonSerializer.Deserialize<PlayerBuild>(payload);}catch(JsonException e){log.Preserve(id,payload);log.Write("read-json",id,e);}
            if(build==null){build=new(){Id=id,Title=reader.GetString(1),ClassId=reader.GetString(2),Stage=reader.GetString(3),Purpose=reader.GetString(4)};RecoveredIds.Add(build.Id);Warnings.Add("Uma build tinha dados inválidos. O original foi preservado; confira os equipamentos ao abrir.");log.Preserve(id,payload);}
            var repairs=BuildRecovery.Normalize(build,id);if(repairs.Count>0){RecoveredIds.Add(build.Id);log.Preserve(id,payload);log.Write("read-repair",id,details:string.Join(" ",repairs));Warnings.Add(build.Title+": alguns dados estavam ausentes; carregamento parcial disponível.");}
            result.Add(build);
        }
        return result;
    }
    public HashSet<string> StarVotes(string uid){using var db=Open();using var c=db.CreateCommand();c.CommandText="CREATE TABLE IF NOT EXISTS build_star_cache(uid TEXT NOT NULL,build_id TEXT NOT NULL,starred INTEGER NOT NULL,PRIMARY KEY(uid,build_id))";c.ExecuteNonQuery();c.CommandText="SELECT build_id FROM build_star_cache WHERE uid=$uid AND starred=1";c.Parameters.AddWithValue("$uid",uid);using var r=c.ExecuteReader();var result=new HashSet<string>();while(r.Read())result.Add(r.GetString(0));return result;}
    public void RememberStars(string uid,IEnumerable<CommunityBuild> builds){if(uid.Length==0)return;_ = StarVotes(uid);using var db=Open();using var tx=db.BeginTransaction();foreach(var build in builds){using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="INSERT OR REPLACE INTO build_star_cache VALUES($uid,$id,$starred)";c.Parameters.AddWithValue("$uid",uid);c.Parameters.AddWithValue("$id",build.Id);c.Parameters.AddWithValue("$starred",build.Starred?1:0);c.ExecuteNonQuery();}tx.Commit();}
    public void Delete(string id){using var db=Open();using var c=db.CreateCommand();c.CommandText="DELETE FROM build_drafts WHERE id=$id";c.Parameters.AddWithValue("$id",id);c.ExecuteNonQuery();}
    public void CacheOwn(string uid,List<PlayerBuild> builds){using var db=Open();using var c=db.CreateCommand();c.CommandText="CREATE TABLE IF NOT EXISTS own_build_cache(uid TEXT PRIMARY KEY,payload TEXT NOT NULL);INSERT OR REPLACE INTO own_build_cache VALUES($uid,$payload)";c.Parameters.AddWithValue("$uid",uid);c.Parameters.AddWithValue("$payload",JsonSerializer.Serialize(builds));c.ExecuteNonQuery();}
    public List<PlayerBuild> CachedOwn(string uid){using var db=Open();using var c=db.CreateCommand();c.CommandText="CREATE TABLE IF NOT EXISTS own_build_cache(uid TEXT PRIMARY KEY,payload TEXT NOT NULL)";c.ExecuteNonQuery();c.CommandText="SELECT payload FROM own_build_cache WHERE uid=$uid";c.Parameters.AddWithValue("$uid",uid);return c.ExecuteScalar() is string json?JsonSerializer.Deserialize<List<PlayerBuild>>(json)??new():new();}
    public void ClearCachedQuery(string classId,string stage,string purpose){using var db=Open();using var c=db.CreateCommand();c.CommandText="DELETE FROM community_build_cache WHERE class_id=$class AND stage=$stage AND purpose=$purpose";c.Parameters.AddWithValue("$class",classId);c.Parameters.AddWithValue("$stage",stage);c.Parameters.AddWithValue("$purpose",purpose);c.ExecuteNonQuery();}
    public void Cache(IEnumerable<CommunityBuild> builds)=>StoreCommunity(builds);
    public List<CommunityBuild> Cached(string classId,string stage,string purpose)=>ReadCommunity(classId,stage,purpose);
}