using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;

public sealed class BuildStatRow:Observable
{
    readonly BuildViewModel owner;public string Name {get;}
    public BuildStatRow(string name,BuildViewModel owner){Name=name;this.owner=owner;}
    public int Value {get=>owner.Document.Stats.GetValueOrDefault(Name,1);set{owner.Document.Stats[Name]=value;Changed();owner.Edited();}}
    public void Refresh()=>Changed(nameof(Value));
}
public sealed class BuildSlotRow:Observable,IBuildItemTooltipModel
{
    readonly BuildViewModel owner;ImageSource? image;
    readonly Dictionary<int,ImageSource?> componentImages=new();
    public BuildSlot Definition {get;}public BuildEquipment Model {get;}
    public BuildSlotRow(BuildSlot definition,BuildEquipment model,BuildViewModel owner){Definition=definition;Model=model;this.owner=owner;SelectedItem=model.ItemId is {} id?owner.FindItem(id):null;SelectCommand=new RelayCommand(_=>owner.BeginSlotEdit(this));ImageTask=LoadImage();ComponentImageTask=LoadComponentImages();}
    public FarmItem? SelectedItem {get;private set;}public ItemDescriptionService Descriptions=>owner.Descriptions;
    public bool OccupiedByWeapon=>Definition.Id=="shield"&&owner.TwoHandedWeapon;public bool Available=>!OccupiedByWeapon;
    public bool IsGear=>Definition.Group!="Pet";public string Name=>Definition.Name;public string ItemName=>OccupiedByWeapon?"Arma de duas mãos":SelectedItem?.Name??(Model.ItemId is {} id?"Item #"+id:"Vazio");public string Icon=>Model.ItemId is {} id?"https://static.divine-pride.net/images/items/item/"+id+".png":"pack://application:,,,/Assets/AppIcon.ico";
    public ImageSource? Image=>OccupiedByWeapon?owner.Slots.FirstOrDefault(s=>s.Definition.Id=="weapon")?.Image:image;public Task ImageTask {get;private set;}=Task.CompletedTask;
    public Task ComponentImageTask {get;private set;}=Task.CompletedTask;
    public BuildTooltipData TooltipData=>new(Model.ItemId,(Model.Refine>0?"+"+Model.Refine+" ":"")+ItemName+(!string.IsNullOrWhiteSpace(Model.Grade)&&Model.Grade!="Sem"?" ["+Model.Grade+"]":""),image,TooltipComponents(Model.Cards),TooltipComponents(Model.Enchants),Model.RandomOptions.Where(s=>!string.IsNullOrWhiteSpace(s)).Select(RagnarokCompanion.Services.BuildRandomOption.NameOnly).ToArray());
    List<BuildTooltipComponent> TooltipComponents(IEnumerable<int> ids)=>ids.GroupBy(id=>id).Select(g=>{var item=owner.FindItem(g.Key);return new BuildTooltipComponent(g.Key,item?.Name??"#"+g.Key,g.Count(),item!=null,componentImages.GetValueOrDefault(g.Key));}).ToList();
    public string Summary=>OccupiedByWeapon?"Ocupado pela arma":Model.ItemId is {} id?$"#{id}"+(Model.Refine>0?$" · +{Model.Refine}":""):"Escolher item";
    public bool Selected=>ReferenceEquals(owner.SelectedSlot,this);public ICommand SelectCommand {get;}
    public int BoardColumn=>owner.Group=="Extras"?Definition.Id switch{"costume-mid" or "costume-garment" or "shadow-shoes" or "shadow-shield" or "shadow-right"=>2,_=>0}:Definition.Id is "head-mid" or "armor" or "shield" or "shoes" or "accessory-right" or "costume-mid" or "shadow-armor" or "shadow-shield" or "shadow-shoes" or "shadow-right"?2:0;
    public int BoardRow=>owner.Group=="Extras"?Definition.Id switch{"costume-top" or "costume-mid"=>0,"costume-low" or "costume-garment"=>1,"shadow-armor" or "shadow-shoes"=>2,"shadow-weapon" or "shadow-shield"=>3,_=>4}:Definition.Id switch{"head-top" or "head-mid" or "costume-top" or "costume-mid"=>0,"head-low" or "armor" or "costume-low" or "shadow-armor"=>1,"weapon" or "shield" or "shadow-weapon" or "shadow-shield"=>2,"garment" or "shoes" or "costume-garment" or "shadow-shoes"=>3,_=>4};
    public int Refine {get=>Model.Refine;set{if(Model.Refine==value)return;Model.Refine=value;Changed();Changed(nameof(Summary));owner.Edited();}}
    public string Grade {get=>Model.Grade;set{if(Model.Grade==value)return;Model.Grade=value;Changed();owner.Edited();}}
    string? cardsText,enchantsText,optionsText;
    public string CardsText {get=>cardsText??string.Join(", ",Model.Cards);set{cardsText=value;SetIds(value,Model.Cards);Changed();}}
    public string EnchantsText {get=>enchantsText??string.Join(", ",Model.Enchants);set{enchantsText=value;SetIds(value,Model.Enchants);Changed();}}
    public string InputError {get;private set;}="";
    void SetIds(string text,List<int> target){var parts=text.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);if(parts.Length>4||parts.Any(s=>!int.TryParse(s,out int n)||n<=0)){InputError=Name+": use até quatro IDs positivos separados por vírgula.";owner.Tell(InputError);owner.Edited();return;}InputError="";target.Clear();target.AddRange(parts.Select(int.Parse));owner.Edited();}
    public string RandomOptionsText {get=>optionsText??string.Join(Environment.NewLine,Model.RandomOptions);set{optionsText=value;Model.RandomOptions=value.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(BuildRandomOption.NameOnly).ToList();Changed();owner.Edited();}}
    public string Components=>"Cartas: "+Names(Model.Cards)+Environment.NewLine+"Encantamentos: "+Names(Model.Enchants);
    string Names(IEnumerable<int> ids)=>string.Join(" · ",ids.Select(id=>owner.Items.Find(id)?.Label??"#"+id));
    public void Assign(FarmItem? item){if(item!=null&&OccupiedByWeapon){owner.Tell("A arma de duas mãos ocupa também o escudo.");return;}Model.ItemId=item?.Id;SelectedItem=item;InputError="";if(item==null){Model.Refine=0;Model.Grade="Sem";Model.Cards.Clear();Model.Enchants.Clear();Model.RandomOptions.Clear();cardsText=enchantsText=optionsText=null;}if(Definition.Id=="weapon"&&owner.TwoHandedWeapon)owner.Slots.FirstOrDefault(s=>s.Definition.Id=="shield")?.Assign(null);Refresh();ImageTask=LoadImage();owner.Edited();owner.RefreshOptionContext();}
    async Task LoadImage(){int? id=Model.ItemId;var loaded=await BuildAssetCache.Item(id??0,owner.Online);if(Model.ItemId==id){image=loaded;Changed(nameof(Image));Changed(nameof(TooltipData));if(Definition.Id=="weapon")owner.Slots.FirstOrDefault(s=>s.Definition.Id=="shield")?.Refresh();}}
    async Task LoadComponentImages(){foreach(int id in Model.Cards.Concat(Model.Enchants).Distinct().ToArray())if(!componentImages.ContainsKey(id)){componentImages[id]=null;componentImages[id]=owner.FindItem(id)!=null?await BuildAssetCache.Item(id,owner.Online):null;}Changed(nameof(TooltipData));}
    public void Refresh(){foreach(var name in new[]{nameof(OccupiedByWeapon),nameof(Available),nameof(Image),nameof(Selected),nameof(SelectedItem),nameof(ItemName),nameof(Icon),nameof(Summary),nameof(Refine),nameof(Grade),nameof(CardsText),nameof(EnchantsText),nameof(RandomOptionsText),nameof(Components),nameof(TooltipData)})Changed(name);ComponentImageTask=LoadComponentImages();}
    public void ComponentsChanged(){cardsText=enchantsText=null;Refresh();}
}
public sealed partial class BuildViewModel:Observable,IDisposable
{
    readonly BuildRepository repository;readonly IBuildCommunityGateway community;readonly PersistenceService store;readonly BuildValidator validator;
    readonly DispatcherTimer autosave=new(){Interval=TimeSpan.FromMilliseconds(600)},searchDelay=new(){Interval=TimeSpan.FromMilliseconds(220)};readonly CancellationTokenSource cancellation=new();
    public ItemFarmIndex Items {get;}public ItemDescriptionService Descriptions {get;}=new();public BuildEquipmentCatalog Equipment {get;}public BuildCriteria Criteria {get;}
    public PlayerBuild Document {get;private set;}=BuildCatalog.New();public bool Online=>store.IsDefaultLocation;
    public BuildClass[] Classes=>BuildCatalog.Classes;public string[] Stages=>BuildCatalog.Stages;public string[] Purposes=>BuildCatalog.Purposes;public string[] Grades=>BuildCatalog.Grades;public string[] Groups {get;}={"Normal","Extras"};public string[] SearchKinds {get;}={"Equipamento","Carta","Encantamento"};
    public ObservableCollection<BuildSlotRow> Slots {get;}=new();public ObservableCollection<BuildSlotRow> BoardSlots {get;}=new();public ObservableCollection<BuildStatRow> Stats {get;}=new();
    public ObservableCollection<FarmItem> Suggestions {get;}=new();public ObservableCollection<PlayerBuild> Saved {get;}=new();public ObservableCollection<CommunityBuild> Community {get;}=new();public ObservableCollection<string> Issues {get;}=new();
    public string Title {get=>Document.Title;set{Document.Title=value;Changed();Edited();}}
    public string Notes {get=>Document.Notes;set{Document.Notes=value;Changed();Edited();}}
    public BuildClass SelectedClass {get=>Classes.FirstOrDefault(c=>c.Id==Document.ClassId)??Classes[0];set{if(value==null||value.Id==Document.ClassId)return;Document.ClassId=value.Id;Changed();classSprite=null;ClassSpriteTask=LoadClass();Changed(nameof(ClassSprite));Edited();FiltersChanged();Search();RefreshOptionContext();}}
    public string Stage {get=>Document.Stage;set{if(!Stages.Contains(value)||Document.Stage==value)return;Document.Stage=value;Changed();Edited();FiltersChanged();}}
    public string Purpose {get=>Document.Purpose;set{if(!Purposes.Contains(value)||Document.Purpose==value)return;Document.Purpose=value;Changed();Edited();FiltersChanged();}}
    string group="Normal",query="",searchKind="Equipamento";bool compatibleOnly=true,busy;
    public bool IsNormalGroup=>Group=="Normal";public bool IsVisualGroup=>Group=="Extras";public bool IsShadowGroup=>Group=="Extras";
    public ICommand SwitchEquipmentGroupCommand=>new RelayCommand(value=>{if(value is string g)Group=g;});
    public string Group {get=>group;set{if(value is "Visual" or "Shadow")value="Extras";if(!Groups.Contains(value))return;group=value;Changed();Changed(nameof(IsNormalGroup));Changed(nameof(IsVisualGroup));Changed(nameof(IsShadowGroup));BoardSlots.Clear();foreach(var row in Slots.Where(s=>value=="Extras"?s.Definition.Group is "Visual" or "Shadow":s.Definition.Group==value))BoardSlots.Add(row);SelectedSlot=BoardSlots.FirstOrDefault();}}
    public string Query {get=>query;set{query=value??"";Changed();searchDelay.Stop();searchDelay.Start();}}
    public string SearchKind {get=>searchKind;set{searchKind=value;Changed();Search();}}
    public bool CompatibleOnly {get=>compatibleOnly;set{compatibleOnly=value;Changed();Search();}}
    BuildSlotRow? selectedSlot;public BuildSlotRow? SelectedSlot {get=>selectedSlot;set{selectedSlot=value;Changed();Changed(nameof(IsEquipmentSlot));Changed(nameof(CanRefineSlot));Changed(nameof(CanGradeSlot));Changed(nameof(CanComponentSlot));foreach(var row in Slots)row.Refresh();Search();RefreshOptionContext();}}
    public FarmItem? SelectedSuggestion {get;set;}public PlayerBuild? SelectedSaved {get;set;}public CommunityBuild? SelectedCommunity {get;set;}
    ImageSource? classSprite;public ImageSource? ClassSprite=>classSprite;public Task ClassSpriteTask {get;private set;}=Task.CompletedTask;
    public string Message {get;private set;}="Escolha equipamentos e salve seu rascunho. Aprovações são exclusivas do servidor/ADM.";
    public string LocalStatus=>Issues.Count==0?"Build completa nos critérios locais • aguardando aprovação real do ADM":"Rascunho incompleto • "+Issues.Count+" pendência(s)";
    public bool Busy {get=>busy;private set{busy=value;Changed();}}
    int approved;bool remoteReward;DateTimeOffset lastFetch;
    public int GlobalApproved=>approved;public double GlobalPercent=>approved*100.0/BuildCatalog.Total;public string GlobalLabel=>$"{approved}/{BuildCatalog.Total} — {GlobalPercent:0}%";
    public string RewardText=>remoteReward&&approved==BuildCatalog.Total?"Desafio concluído! Recompensa desbloqueada. Resgate exclusivamente pelo servidor/ADM.":"Recompensa oficial: aguardando aprovação das "+BuildCatalog.Total+" combinações pelo servidor/ADM.";
    public string CommunityStatus {get;private set;}="Firebase não configurado • modo offline. Nenhuma aprovação local conta para o desafio.";
    public ICommand NewCommand {get;}public ICommand SaveCommand {get;}public ICommand LoadCommand {get;}public ICommand DeleteCommand {get;}public ICommand EquipCommand {get;}public ICommand ClearSlotCommand {get;}public ICommand PublishCommand {get;}public ICommand SyncCommand {get;}public ICommand CopyCommunityCommand {get;}
    public BuildViewModel(PersistenceService store,IBuildCommunityGateway? gateway=null,BuildEquipmentCatalog? equipment=null,ItemFarmIndex? items=null)
    {
        this.store=store;log=new(store.Root);randomOptions=new(store.Root);repository=new(store.Root);Items=items??ItemFarmIndex.Shared;Equipment=equipment??new();validator=new(Equipment);Criteria=store.Load<BuildCriteria>("build-criteria.json");community=gateway??new FirestoreBuildGateway(new FirebaseSession(FirebaseWebConfig.Load(store.Root),store.Root));
        if(community.Configured)CommunityStatus="Comunidade configurada • atualize manualmente para consultar o servidor.";
        NewCommand=new RelayCommand(_=>{if(!Flush())return;Load(BuildCatalog.New());Area=0;Tell("Novo rascunho local.");});SaveCommand=new RelayCommand(_=>Flush());LoadCommand=new RelayCommand(OpenSaved);
        DeleteCommand=new RelayCommand(DeleteSaved);
        EquipCommand=new RelayCommand(_=>Assign());ClearSlotCommand=new RelayCommand(_=>{SelectedSlot?.Assign(null);if(transientEditor){Query="";SelectedSuggestion=null;}});PublishCommand=new RelayCommand(async _=>await Publish());SyncCommand=new RelayCommand(async _=>await Sync());CopyCommunityCommand=new RelayCommand(CopyCommunity);
        InitializeScreens();ReadOwnCache();ReadDiscordProfile();
        autosave.Tick+=(_,_)=>{autosave.Stop();Flush();};searchDelay.Tick+=(_,_)=>{searchDelay.Stop();Search();};foreach(var name in new[]{"FOR","AGI","VIT","INT","DES","SOR"})Stats.Add(new(name,this));Load(Document);HasActiveBuild=false;Area=1;ReloadSaved();InitializeAuthentication();InitializeCommunitySynchronization();
    }
    async Task LoadClass(){var job=SelectedClass;var image=await BuildAssetCache.Class(job,Online);if(SelectedClass.Id==job.Id){classSprite=image;Changed(nameof(ClassSprite));}}
    void Load(PlayerBuild document)
    {
        ArgumentNullException.ThrowIfNull(document);loading=true;
        try{var repairs=BuildRecovery.Normalize(document);LastLoadWarning=repairs.Count>0?"Alguns dados estavam ausentes ou inválidos. A build foi aberta parcialmente; confira os equipamentos.":"";if(repairs.Count>0)log.Write("load-repair",document.Id,details:string.Join(" ",repairs));
            HasActiveBuild=true;selectedSlot=null;BoardSlots.Clear();Slots.Clear();Document=document;
            foreach(var slot in BuildCatalog.Slots)Slots.Add(new(slot,document.Equipment.Single(e=>e.Slot==slot.Id),this));Group="Normal";foreach(var stat in Stats)stat.Refresh();
            foreach(var name in new[]{nameof(Title),nameof(SelectedClass),nameof(Stage),nameof(Purpose),nameof(Notes),nameof(Document),nameof(PetSlot),nameof(LastLoadWarning)})Changed(name);ClassSpriteTask=LoadClass();Validate();RefreshOptionContext();
        }finally{loading=false;}
    }
    public void Tell(string text){Message=text;Changed(nameof(Message));}
    void Validate(){Issues.Clear();foreach(var error in validator.Validate(Document,Criteria).Concat(Slots.Where(s=>s.InputError.Length>0).Select(s=>s.InputError)))Issues.Add(error);Changed(nameof(LocalStatus));SelectedSlot?.Refresh();}
    public bool TwoHandedWeapon=>BuildEquipmentCatalog.TwoHanded(Equipment.Find(Document.Equipment.FirstOrDefault(e=>e.Slot=="weapon")?.ItemId??0));
    public void Edited(){if(loading||readOnlyPreview||!HasActiveBuild)return;Slots.FirstOrDefault(s=>s.Definition.Id=="shield")?.Refresh();Document.UpdatedUtc=DateTimeOffset.UtcNow;Validate();RefreshCompactSlot();autosave.Stop();if(!transientEditor)autosave.Start();}
    public bool Flush(){autosave.Stop();if(readOnlyPreview||transientEditor||!HasActiveBuild)return true;try{repository.Save(Document);ReloadSaved();Tell("Build salva no computador • "+DateTime.Now.ToString("HH:mm:ss"));return true;}catch(Exception e){log.Write("save",Document.Id,e);Tell("Não foi possível salvar esta build. Os dados anteriores foram preservados. Confira o log local de Builds.");return false;}}
    void ReloadSaved(){Saved.Clear();SavedCards.Clear();foreach(var draft in repository.Load()){Saved.Add(draft);SavedCards.Add(new(draft,this));}Changed(nameof(NoSaved));if(repository.Warnings.Count>0)Tell(repository.Warnings[0]);}
    void FiltersChanged(){}
    void Search()
    {
        Suggestions.Clear();if(Query.Trim().Length==0)return;
        try{foreach(var item in Items.Search(Query)){var meta=Equipment.Find(item.Id);bool allowed=SearchKind=="Equipamento"?SelectedSlot!=null&&meta!=null&&Equipment.Compatible(meta,SelectedClass,SelectedSlot.Definition):meta?.Type=="Card";if(!CompatibleOnly||allowed)Suggestions.Add(item);}}catch(Exception e){Tell(e.Message);}
    }
    void Assign()
    {
        if(SelectedSlot==null||SelectedSuggestion==null)return;var item=SelectedSuggestion;var meta=Equipment.Find(item.Id);
        if(SearchKind=="Equipamento"){
            if((SelectedSlot.Definition.Group=="Pet"&&meta==null)||meta!=null&&!Equipment.Compatible(meta,SelectedClass,SelectedSlot.Definition)){Tell("Esse item não é compatível com a classe/slot na referência Renewal.");return;}
            SelectedSlot.Assign(item);if(meta==null)Tell("Item salvo como rascunho; compatibilidade Farmland ainda não confirmada.");
        }else{if(meta?.Type!="Card"){Tell("Selecione uma carta ou encantamento identificado no catálogo.");return;}var list=SearchKind=="Carta"?SelectedSlot.Model.Cards:SelectedSlot.Model.Enchants;if(list.Count>=4){Tell("Limite de quatro componentes por tipo.");return;}list.Add(item.Id);SelectedSlot.ComponentsChanged();Edited();}
    }
    async Task Publish()
    {
        if(Busy||!HasActiveBuild)return;if(community is FirestoreBuildGateway&&!FirebaseOnline){Tell("Sem conexão. Salve a build localmente e publique ao reconectar.");return;}if(!Flush())return;Validate();if(Issues.Count>0){Tell("Complete as pendências antes de publicar. Seu rascunho foi salvo.");return;}
        if(!community.Configured){Tell("Firebase não configurado. Build completa salva localmente; nenhuma aprovação foi criada.");return;}
        Busy=true;try{var copy=JsonSerializer.Deserialize<PlayerBuild>(JsonSerializer.Serialize(Document))!;await community.SubmitAsync(copy,cancellation.Token);Tell(community is FirestoreBuildGateway g&&g.LastSubmitWasUpdate?"Build aprovada atualizada. Estrelas e progresso preservados.":"Enviada para análise do ADM. Ainda não contribui para o progresso global.");}catch(Exception e){Tell("Publicação pendente; build local preservada. "+e.Message);}finally{Busy=false;}
    }
    async Task Sync()
    {
        if(communitySync!=null){await SyncCommunityPage(true,false);return;}if(Busy)return;ReadCommunityCache();
        if(!community.Configured){Tell(CommunityStatus);return;}if(DateTimeOffset.UtcNow-lastFetch<TimeSpan.FromMinutes(5)){Tell("Aguarde cinco minutos entre consultas à comunidade.");return;}
        Busy=true;try{
            if(community is FirestoreBuildGateway gateway)gateway.OrderByStars=CommunityOrder=="Mais estreladas";var snapshot=await community.FetchAsync(CommunityClass.Id,CommunityStage,CommunityPurpose,cancellation.Token);
            if(snapshot.Builds==null||snapshot.Builds.Count>30||snapshot.Builds.Any(b=>b.Build==null)||snapshot.ApprovedCombinations==null||string.IsNullOrWhiteSpace(snapshot.Revision))throw new InvalidDataException("Resposta do servidor inválida.");
            var valid=BuildCatalog.Classes.SelectMany(c=>BuildCatalog.Stages.Select(s=>c.Id+":"+s)).ToHashSet(StringComparer.Ordinal);
            approved=snapshot.ApprovedCombinations.Where(valid.Contains).Distinct(StringComparer.Ordinal).Count();remoteReward=snapshot.RewardUnlocked&&approved==BuildCatalog.Total;
            repository.ClearCachedQuery(CommunityClass.Id,CommunityStage,CommunityPurpose);repository.RememberStars(FirebaseUid,snapshot.Builds);repository.Cache(snapshot.Builds);communitySource=snapshot.Builds;FilterCommunity();lastFetch=DateTimeOffset.UtcNow;CommunityStatus="Servidor consultado em "+DateTime.Now.ToString("HH:mm")+" • somente combinações completas aprovadas pelo ADM.";
            foreach(var name in new[]{nameof(GlobalApproved),nameof(GlobalPercent),nameof(GlobalLabel),nameof(RewardText),nameof(CommunityStatus)})Changed(name);Tell("Comunidade atualizada. Finalidades não multiplicam as combinações.");
        }catch(Exception e){Tell("Sem sincronização; rascunhos e cache preservados. "+e.Message);}finally{Busy=false;}
    }
    public void Dispose(){syncTimer?.Stop();CancelSlotEdit();if(community is FirestoreBuildGateway f)f.Session.StateChanged-=SessionStateChanged;CommunityPreview=null;ReviewPreview=null;autosave.Stop();searchDelay.Stop();cancellation.Cancel();cancellation.Dispose();if(community is IDisposable d)d.Dispose();}
}
