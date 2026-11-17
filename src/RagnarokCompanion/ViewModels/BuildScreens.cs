using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;
public sealed class SavedBuildCard:Observable
{
    public PlayerBuild Model {get;}readonly BuildViewModel owner;
    public SavedBuildCard(PlayerBuild model,BuildViewModel owner){Model=model;this.owner=owner;RenameText=model.Title;}
    public string Name=>Model.Title;public string Details=>(BuildCatalog.Classes.FirstOrDefault(c=>c.Id==Model.ClassId)?.Name??"Classe não informada")+" · "+Model.Stage+" Game · "+Model.Purpose;
    public string Updated=>"Atualizada em "+Model.UpdatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string Status=>owner.SavedBuildStatus(Model);
    public void RefreshStatus()=>Changed(nameof(Status));
    public string RenameText {get;set;}bool renaming;public bool Renaming {get=>renaming;set{renaming=value;Changed();}}
    public ICommand RenameCommand=>new RelayCommand(_=>{RenameText=Name;Changed(nameof(RenameText));Renaming=!Renaming;});
    public ICommand SaveNameCommand=>new RelayCommand(_=>owner.Rename(this));
}
public sealed class CommunityBuildCard
{
    readonly BuildViewModel owner;public CommunityBuild Model {get;}public CommunityBuildCard(CommunityBuild model,BuildViewModel owner){Model=model;this.owner=owner;}public string Author=>"Criado por: "+BuildPublicName.Clean(Model.AuthorName);public string StarText=>(Model.Starred?"★ ":"☆ ")+Model.StarCount;public string StarColor=>Model.Starred?"#E9C77A":"#9AA6BF";public bool IsOwner=>Model.OwnerUid.Length>0&&Model.OwnerUid==owner.FirebaseUid;public bool CanStar=>Model.OwnerUid.Length==0||Model.OwnerUid!=owner.FirebaseUid;
    public string Name=>Model.Build.Title;public string Details=>(BuildCatalog.Classes.FirstOrDefault(c=>c.Id==Model.Build.ClassId)?.Name??"Classe não informada")+" · "+Model.Build.Stage+" Game · "+Model.Build.Purpose;
}
public sealed partial class BuildViewModel
{
    readonly BuildLog log;readonly BuildRandomOptions randomOptions;bool loading;
    public string LastLoadWarning {get;private set;}="";
    public ObservableCollection<SavedBuildCard> SavedCards {get;}=new();public bool NoSaved=>SavedCards.Count==0;
    public ObservableCollection<CommunityBuildCard> CommunityCards {get;}=new();public bool NoCommunity=>CommunityCards.Count==0;
    public bool FirebaseConfigured=>community.Configured;
    public string CommunityAvailability=>FirebaseConfigured?"Firebase • consultas manuais com cache local":"Firebase não configurado • sincronização futura";
    int area;public int Area {get=>area;set{if(value is <0 or >3||value==3&&!IsAdmin||area==value)return;if(area==0&&!loading&&!Flush())return;area=value;Changed();Changed(nameof(IsEditor));Changed(nameof(IsEmptyEditor));Changed(nameof(IsMyBuilds));Changed(nameof(IsCommunity));Changed(nameof(IsModeration));}}
    bool hasActiveBuild;public bool HasActiveBuild {get=>hasActiveBuild;private set{hasActiveBuild=value;Changed();Changed(nameof(IsEditor));Changed(nameof(IsEmptyEditor));}}
    public bool IsEmptyEditor=>Area==0&&!HasActiveBuild;public bool IsEditor=>Area==0&&HasActiveBuild;public bool IsMyBuilds=>Area==1;public bool IsCommunity=>Area==2;
    public ICommand NavigateAreaCommand {get;private set;}=null!;public ICommand DuplicateCommand {get;private set;}=null!;
    public ObservableCollection<BuildRandomOption> OptionSuggestions {get;}=new();
    BuildRandomOption? selectedOption;public BuildRandomOption? SelectedOption {get=>selectedOption;set{selectedOption=value;Changed();if(value!=null)OptionValue=value.Negative?value.Maximum:value.Minimum;}}
    int optionValue=1;public int OptionValue {get=>optionValue;set{optionValue=value;Changed();}}
    public string[] OptionProfiles=>SelectedSlot==null?new[]{"Padrão"}:BuildRandomOptions.Profiles(SelectedSlot.Definition);
    public string OptionProfile {get=>SelectedSlot?.Model.RandomOptionProfile??"Padrão";set{if(SelectedSlot==null||SelectedSlot.Model.RandomOptionProfile==value||!OptionProfiles.Contains(value))return;SelectedSlot.Model.RandomOptionProfile=value;Changed();Edited();RefreshOptionContext();}}
    public string OptionsContext {get;private set;}="Escolha um item para ver as opções compatíveis.";
    public ICommand AddOptionCommand {get;private set;}=null!;
    static readonly BuildClass AllCommunityClasses=new("","Todas","","",""); public BuildClass[] CommunityClasses {get;}=new[]{AllCommunityClasses}.Concat(BuildCatalog.Classes).ToArray(); public string[] CommunityStages {get;}=new[]{"Todas"}.Concat(BuildCatalog.Stages).ToArray(); public string[] CommunityPurposes {get;}=new[]{"Todas"}.Concat(BuildCatalog.Purposes).ToArray(); List<CommunityBuild> communitySource=new();BuildClass communityClass=AllCommunityClasses;string communityStage="Todas",communityPurpose="Todas",communityQuery="";
    public BuildClass CommunityClass {get=>communityClass;set{if(value==null||communityClass==value)return;communityClass=value;Changed();ReadCommunityCache();}}
    public string CommunityStage {get=>communityStage;set{if(!CommunityStages.Contains(value)||communityStage==value)return;communityStage=value;Changed();ReadCommunityCache();}}
    public string CommunityPurpose {get=>communityPurpose;set{if(!CommunityPurposes.Contains(value)||communityPurpose==value)return;communityPurpose=value;Changed();ReadCommunityCache();}}
    public string CommunityQuery {get=>communityQuery;set{communityQuery=value??"";Changed();FilterCommunity();}}
    void InitializeScreens()
    {
        NavigateAreaCommand=new RelayCommand(p=>{if(int.TryParse(p?.ToString(),out int n))Area=n;});DuplicateCommand=new RelayCommand(DuplicateSaved);
        AddOptionCommand=new RelayCommand(_=>{
            var row=SelectedSlot;var option=SelectedOption;if(row==null||option==null||!OptionSuggestions.Contains(option))return;

            int limit=row.Definition.Group=="Visual"?3:5;if(row.Model.RandomOptions.Count>=limit){Tell("Este item já tem "+limit+" linhas. Edite ou remova uma linha antes de adicionar.");return;}
            row.RandomOptionsText=string.Join(Environment.NewLine,row.Model.RandomOptions.Append(option.Name));Tell("Opção adicionada. As linhas continuam editáveis.");
        });
    }
    public string SavedBuildStatus(PlayerBuild build)=>repository.HasApprovedPublication(FirebaseUid,build)?(IsComplete(build)?"Publicação aprovada":"Publicação aprovada · alterações locais pendentes"):(IsComplete(build)?"Completa localmente · aprovação não sincronizada":"Rascunho local");
    public bool IsComplete(PlayerBuild build){try{return validator.Validate(build,Criteria).Count==0;}catch{return false;}}
    public FarmItem? FindItem(int id){try{return Items.Find(id);}catch(Exception e){log.Write("item-lookup",Document.Id,e,details:"item="+id);return null;}}
    static PlayerBuild Clone(PlayerBuild model)=>JsonSerializer.Deserialize<PlayerBuild>(JsonSerializer.Serialize(model))??throw new InvalidOperationException("Dados da build ausentes.");
    void OpenSaved(object? value)
    {
        // Capture before Flush: refreshing the saved list clears WPF's selected item.
        var selected=value as PlayerBuild??SelectedSaved;if(selected==null){Tell("Selecione uma build para abrir.");return;}
        try{bool recovered=repository.RecoveredIds.Contains(selected.Id);var copy=Clone(selected.Id==Document.Id?Document:selected);if(!Flush())return;Load(copy);Area=0;Tell(LastLoadWarning.Length>0?LastLoadWarning:recovered?"Build aberta parcialmente. Alguns dados estavam ausentes ou inválidos; confira os equipamentos. O original foi preservado.":"Build aberta. Continue de onde parou.");}
        catch(Exception e){log.Write("open",selected.Id,e);Tell("Não foi possível abrir esta build. Alguns dados estão ausentes ou inválidos. Tente duplicar ou recriar a build; detalhes no log local.");}
    }
    void DuplicateSaved(object? value)
    {
        var selected=value as PlayerBuild??SelectedSaved;if(selected==null){Tell("Selecione uma build para duplicar.");return;}
        try{var copy=Clone(selected.Id==Document.Id?Document:selected);if(!Flush())return;copy.Id=Guid.NewGuid().ToString("N");copy.Title=("Cópia — "+copy.Title)[..Math.Min(120,("Cópia — "+copy.Title).Length)];copy.UpdatedUtc=DateTimeOffset.UtcNow;Load(copy);repository.Save(copy);ReloadSaved();Area=0;Tell("Build duplicada como novo rascunho local.");}
        catch(Exception e){log.Write("duplicate",selected.Id,e);Tell("Não foi possível duplicar esta build. O original foi preservado; confira o log local.");}
    }
    void DeleteSaved(object? value)
    {
        var selected=value as PlayerBuild??SelectedSaved;if(selected==null)return;string id=selected.Id;
        try{if(!Flush())return;autosave.Stop();repository.Delete(id);if(Document.Id==id){Load(BuildCatalog.New());HasActiveBuild=false;Area=1;}ReloadSaved();Tell("Build local excluída. Publicações da comunidade não foram alteradas.");}
        catch(Exception e){log.Write("delete",id,e);Tell("Não foi possível excluir esta build. Confira o log local.");}
    }
    public void Rename(SavedBuildCard card)
    {
        string title=card.RenameText?.Trim()??"";if(title.Length is <1 or >120){Tell("Informe um nome de 1 a 120 caracteres.");return;}
        try{if(Document.Id==card.Model.Id){Document.Title=title;Changed(nameof(Title));Edited();if(!Flush())return;}else{var copy=Clone(card.Model);copy.Title=title;copy.UpdatedUtc=DateTimeOffset.UtcNow;repository.Save(copy);ReloadSaved();}Tell("Nome da build atualizado.");}
        catch(Exception e){log.Write("rename",card.Model.Id,e);Tell("Não foi possível renomear. O nome anterior foi preservado.");}
    }
    void CopyCommunity(object? value)
    {
        var selected=value as CommunityBuild??SelectedCommunity;if(selected?.Build==null){Tell("Selecione uma build da comunidade para copiar.");return;}
        try{var copy=Clone(selected.Build);if(!Flush())return;copy.Id=Guid.NewGuid().ToString("N");copy.Title="Cópia — "+copy.Title;Load(copy);Edited();Area=0;Tell("Cópia local criada no editor; a publicação original e o progresso aprovado não foram alterados.");}
        catch(Exception e){log.Write("copy-community",selected.Id,e);Tell("Não foi possível abrir esta cópia. Alguns dados estão ausentes ou inválidos; confira o log local.");}
    }
    public void RefreshOptionContext()
    {
        OptionSuggestions.Clear();SelectedOption=null;var row=SelectedSlot;
        if(row==null||row.Model.ItemId==null)OptionsContext="Selecione um equipamento para ver as opções compatíveis.";
        else{
            var metadata=Equipment.Find(row.Model.ItemId.Value);
            if(metadata!=null&&!Equipment.Compatible(metadata,SelectedClass,row.Definition))OptionsContext="O item não é compatível com este slot/classe na referência conhecida. Revise o equipamento; a edição manual permanece disponível.";
            else{foreach(var option in randomOptions.For(row.Definition,row.Model.ItemId,metadata,row.Model.RandomOptionProfile))OptionSuggestions.Add(option);OptionsContext=OptionSuggestions.Count==0?"Não há Random Options compatíveis confirmadas para este item/perfil. Você pode manter linhas manuais para análise do ADM.":row.Name+" • "+BuildRandomOptions.Category(row.Definition)+" • "+row.Model.RandomOptionProfile+". Nomes das opções do Farmland, sem valores de bônus.";}
        }
        SelectedOption=OptionSuggestions.FirstOrDefault();Changed(nameof(OptionProfiles));Changed(nameof(OptionProfile));Changed(nameof(OptionsContext));
    }
    void ReadCommunityCache()
    {
        try{var page=communitySync?.CachedPage(CommunityClass.Id,CommunityStage,CommunityPurpose,CommunityOrder=="Mais estreladas");communitySource=page?.Builds??repository.Cached(CommunityClass.Id,CommunityStage,CommunityPurpose);catalogCursor=page?.Cursor;HasMoreCommunity=page?.HasMore??false;Changed(nameof(HasMoreCommunity));if(communitySync!=null){var stamp=repository.SyncState(BuildCommunitySyncService.CatalogKey(CommunityClass.Id,CommunityStage,CommunityPurpose,CommunityOrder=="Mais estreladas"));CommunityStatus="Catálogo em cache"+(stamp==null?" · ainda não sincronizado":" · "+stamp.Updated.ToLocalTime().ToString("dd/MM HH:mm"));Changed(nameof(CommunityStatus));}RefreshStarIdentity();}catch(Exception e){log.Write("community-cache",null,e);communitySource=new();FilterCommunity();Tell("Não foi possível ler o cache da comunidade. Seus rascunhos foram preservados.");}
    }
    void FilterCommunity()
    {
        string query=MvpSearchCoordinator.Normalize(CommunityQuery.Trim());Community.Clear();CommunityCards.Clear();SelectedCommunity=null;
        var localRows=query.Length>0?repository.SearchCommunity(CommunityClass.Id,CommunityStage,CommunityPurpose,CommunityQuery):communitySource;var votes=repository.StarVotes(FirebaseUid);foreach(var cachedRow in (CommunityOrder=="Mais estreladas"?localRows.OrderByDescending(b=>b.StarCount).ThenBy(b=>b.Id):localRows.OrderByDescending(b=>b.Build.UpdatedUtc)).Where(b=>b?.Build!=null&&(CommunityClass.Id.Length==0||b.Build.ClassId==CommunityClass.Id)&&(CommunityStage=="Todas"||b.Build.Stage==CommunityStage)&&(CommunityPurpose=="Todas"||b.Build.Purpose==CommunityPurpose)&&
            (query.Length==0||MvpSearchCoordinator.Normalize((b.Build.Title??"")+" "+(b.Build.Notes??"")+" "+b.AuthorName).Contains(query)))){var row=cachedRow with{Starred=IsSignedIn&&votes.Contains(cachedRow.Id)};Community.Add(row);CommunityCards.Add(new(row,this));}
        Changed(nameof(NoCommunity));foreach(var saved in SavedCards)saved.RefreshStatus();
    }
}
