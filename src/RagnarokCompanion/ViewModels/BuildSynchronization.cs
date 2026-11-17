using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;
public sealed partial class BuildViewModel
{
 BuildCommunitySyncService? communitySync;DispatcherTimer? syncTimer;string? catalogCursor;
 public bool HasMoreCommunity {get;private set;}public string ProgressCacheInfo {get;private set;}="Progresso ainda não sincronizado";
 public ICommand MoreCommunityCommand=>new RelayCommand(async _=>await SyncCommunityPage(true,true));
 void InitializeCommunitySynchronization(){if(community is not FirestoreBuildGateway remote)return;communitySync=new(repository,remote);ReadCommunityCache();if(communitySync.CachedProgress() is {} p)ApplyProgress(p,true,repository.SyncState("progress")?.Updated??DateTimeOffset.MinValue);if(!store.IsDefaultLocation)return;syncTimer=new(){Interval=TimeSpan.FromHours(6)+TimeSpan.FromSeconds(Random.Shared.Next(10,180))};syncTimer.Tick+=async(_,_)=>await AutomaticCommunitySync();syncTimer.Start();_ = InitialCommunitySync();}
 async Task InitialCommunitySync(){try{await Task.Delay(Random.Shared.Next(2000,8000),cancellation.Token);await AutomaticCommunitySync();}catch(OperationCanceledException){}}
 async Task AutomaticCommunitySync(){if(communitySync==null||Busy)return;await SyncCommunityPage(false,false);}
 void ApplyProgress(BuildCommunitySnapshot snapshot,bool cached,DateTimeOffset updated){var valid=BuildCatalog.Classes.SelectMany(c=>BuildCatalog.Stages.Select(s=>c.Id+":"+s)).ToHashSet();approved=snapshot.ApprovedCombinations.Where(valid.Contains).Distinct().Count();remoteReward=!cached&&snapshot.RewardUnlocked&&approved==BuildCatalog.Total;ProgressCacheInfo=(cached?"Progresso em cache":"Progresso verificado")+(updated==DateTimeOffset.MinValue?"":" · "+updated.ToLocalTime().ToString("dd/MM HH:mm"));foreach(var p in new[]{nameof(GlobalApproved),nameof(GlobalPercent),nameof(GlobalLabel),nameof(RewardText),nameof(ProgressCacheInfo)})Changed(p);}
 async Task SyncCommunityPage(bool manual,bool more)
 {
  if(Busy||communitySync==null||!community.Configured)return;string cls=CommunityClass.Id,stage=CommunityStage,purpose=CommunityPurpose;bool stars=CommunityOrder=="Mais estreladas";string? cursor=more?catalogCursor:null;if(more&&cursor==null)return;
  if(manual)Busy=true;try{if(authorizationDeferred&&FirebaseOnline)await RestoreAuthorization();if(profileDirty&&FirebaseOnline)await SyncCommunityProfile();var result=await communitySync.Catalog(cls,stage,purpose,stars,manual,cursor,cancellation.Token);if(cls==CommunityClass.Id&&stage==CommunityStage&&purpose==CommunityPurpose&&stars==(CommunityOrder=="Mais estreladas")){communitySource=more?communitySource.Concat(result.Value.Builds).GroupBy(b=>b.Id).Select(g=>g.Last()).ToList():result.Value.Builds;catalogCursor=result.Value.Cursor;HasMoreCommunity=result.Value.HasMore;Changed(nameof(HasMoreCommunity));RefreshStarIdentity();CommunityStatus=result.Message+(result.Updated==DateTimeOffset.MinValue?"":" · "+result.Updated.ToLocalTime().ToString("dd/MM HH:mm"));Changed(nameof(CommunityStatus));}if(!more){var progress=await communitySync.Progress(manual,cancellation.Token);ApplyProgress(progress.Value,progress.Cached,progress.Updated);}Tell(result.Message);}catch(OperationCanceledException){}catch(Exception e){Tell("Cache preservado. "+e.Message);}finally{if(manual)Busy=false;}
 }
 async Task<CommunityBuild> CommunityContent(CommunityBuild row){if(communitySync==null&&row.HasContent)return row;if(communitySync==null)throw new InvalidOperationException("Conteúdo não disponível no cache.");return await communitySync.Content(row,cancellation.Token);}
}
