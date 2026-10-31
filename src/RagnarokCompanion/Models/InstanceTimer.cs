using System;
namespace RagnarokCompanion.Models;
public sealed record InstanceDefinition(string Id,string Name,int CooldownHours,int NpcSpriteId,string NpcName,string NpcSource,bool FarmlandNpc=false,int? NpcReferenceId=null);
public sealed class InstanceTimer
{
    public string Id {get;set;}="";
    public DateTimeOffset? FinishedUtc {get;set;}
    public bool Notified {get;set;}
}
public sealed record InstanceReadyAlert(string Name,DateTimeOffset Utc);
