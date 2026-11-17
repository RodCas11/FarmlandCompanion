using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;
public sealed partial class BuildViewModel
{
 public string FirebaseUid=>community is FirestoreBuildGateway f&&f.Session.SignedIn?f.Session.UserId:"";
 public string[] CommunityOrders=>new[]{"Padrão","Mais estreladas"};string communityOrder="Padrão";
 public string CommunityOrder {get=>communityOrder;set{if(!CommunityOrders.Contains(value))return;communityOrder=value;Changed();lastFetch=default;ReadCommunityCache();}}
 public ICommand ToggleStarCommand=>new RelayCommand(async value=>{if(value is CommunityBuild row)await ToggleStar(row);});
 void RefreshStarIdentity(){var votes=repository.StarVotes(FirebaseUid);communitySource=communitySource.Select(b=>b with{Starred=IsSignedIn&&votes.Contains(b.Id)}).ToList();FilterCommunity();}
 async Task ToggleStar(CommunityBuild row){if(Busy||community is not FirestoreBuildGateway f)return;if(!IsSignedIn){Tell("Entre com Google para dar uma estrela.");return;}if(row.OwnerUid==FirebaseUid){Tell("Você não pode dar estrela à própria build.");return;}if(!FirebaseOnline){Tell("Sem conexão. Sua estrela não foi alterada.");return;}Busy=true;try{var updated=await f.ToggleStar(row,cancellation.Token);repository.RememberStars(FirebaseUid,new[]{updated});repository.Cache(new[]{updated});communitySource=communitySource.Select(b=>b.Id==row.Id?updated:b).ToList();FilterCommunity();Tell(updated.Starred?"Estrela adicionada.":"Estrela removida.");}catch(Exception e){Tell(e.Message);}finally{Busy=false;}}
}
