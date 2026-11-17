using RagnarokCompanion.Models;
using RagnarokCompanion.Services;
using RagnarokCompanion.ViewModels;
using Xunit;
namespace RagnarokCompanion.Tests;
public sealed class BuildTests
{
    [Fact] public void SlotModalCommitsOnlyOnSaveAndPetEggIsOptional()
    {
        string root=Root();using var vm=new BuildViewModel(new(root));vm.NewCommand.Execute(null);var shield=vm.Slots.Single(s=>s.Definition.Id=="shield");shield.Assign(vm.Items.Find(2101));string original=System.Text.Json.JsonSerializer.Serialize(vm.Document);
        vm.BeginSlotEdit(vm.Slots.Single(s=>s.Definition.Id=="weapon"));var draft=vm.SlotDraft!;draft.SelectedSlot!.Assign(vm.Items.Find(21007));draft.SelectedSlot.Refine=9;draft.SelectedSlot.Grade="B";draft.SelectedSlot.CardsText="4004";draft.SelectedSlot.EnchantsText="4702";draft.SelectedSlot.RandomOptionsText="ATK +10";draft.Flush();Assert.Empty(new BuildRepository(root).Load());Assert.Equal(original,System.Text.Json.JsonSerializer.Serialize(vm.Document));vm.CancelSlotDraftCommand.Execute(null);Assert.Equal(original,System.Text.Json.JsonSerializer.Serialize(vm.Document));
        vm.BeginSlotEdit(vm.Slots.Single(s=>s.Definition.Id=="weapon"));vm.SlotDraft!.SelectedSlot!.Assign(vm.Items.Find(21007));vm.SlotDraft.SelectedSlot.Refine=9;vm.SaveSlotDraftCommand.Execute(null);Assert.False(vm.IsSlotEditorOpen);Assert.Equal(9,vm.Document.Equipment.Single(e=>e.Slot=="weapon").Refine);Assert.Null(vm.Document.Equipment.Single(e=>e.Slot=="shield").ItemId);
        foreach(string stage in BuildCatalog.Stages){vm.Stage=stage;Assert.DoesNotContain(vm.Issues,s=>s.Contains("Ovo"));}vm.BeginSlotEdit(vm.PetSlot!);vm.SlotDraft!.SelectedSlot!.Assign(vm.Items.Find(9001)??new FarmItem{Id=9001,Name="Ovo de fixture"});vm.CancelSlotDraftCommand.Execute(null);Assert.Null(vm.PetSlot!.Model.ItemId);vm.BeginSlotEdit(vm.PetSlot);vm.SlotDraft!.SelectedSlot!.Assign(vm.Items.Find(9001)??new FarmItem{Id=9001,Name="Ovo de fixture"});vm.SaveSlotDraftCommand.Execute(null);Assert.Equal(9001,vm.PetSlot!.Model.ItemId);Assert.DoesNotContain(vm.Issues,s=>s.Contains("Ovo"));vm.Flush();vm.LoadCommand.Execute(vm.Saved.Single());Assert.Equal(9001,vm.PetSlot!.Model.ItemId);
    }
    [Fact] public void EquipmentBoardMatchesRagnarokPositionsAndSwitchingPreservesBuild()
    {
        using var vm=new BuildViewModel(new(Root()));vm.NewCommand.Execute(null);var weapon=vm.Slots.Single(s=>s.Definition.Id=="weapon");weapon.Assign(vm.Items.Find(21007));weapon.Refine=9;weapon.Grade="B";weapon.CardsText="4004";weapon.EnchantsText="4702";weapon.RandomOptionsText="ATK +10";
        string before=System.Text.Json.JsonSerializer.Serialize(vm.Document);string[][] rows={new[]{"head-top","head-mid"},new[]{"head-low","armor"},new[]{"weapon","shield"},new[]{"garment","shoes"},new[]{"accessory-left","accessory-right"}};
        for(int r=0;r<rows.Length;r++)for(int c=0;c<2;c++){var slot=vm.Slots.Single(s=>s.Definition.Id==rows[r][c]);Assert.Equal(r,slot.BoardRow);Assert.Equal(c*2,slot.BoardColumn);}
        foreach(string group in new[]{"Extras","Normal"}){vm.SwitchEquipmentGroupCommand.Execute(group);Assert.All(vm.BoardSlots,s=>Assert.True(group=="Normal"?s.Definition.Group=="Normal":s.Definition.Group is "Visual" or "Shadow"));Assert.Equal(10,vm.BoardSlots.Count);Assert.All(vm.BoardSlots,s=>Assert.True(s.BoardColumn is 0 or 2));Assert.Equal(vm.BoardSlots.Count,vm.BoardSlots.Select(s=>(s.BoardRow,s.BoardColumn)).Distinct().Count());}
        Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(vm.Document));Assert.True(vm.Slots.Single(s=>s.Definition.Id=="shield").OccupiedByWeapon);
    }
    [Fact] public void PublicAuthorsAndStarsRemainSeparatedFromAccountIdentity()
    {
        string root=Root();var repo=new BuildRepository(root);var build=BuildCatalog.New();var row=new CommunityBuild("fixture",build,"approved","rodcas","owner",12,true,AdminNotes:"Revisada pelo ADM");repo.Cache(new[]{row});repo.RememberStars("alice",new[]{row});Assert.Contains("fixture",repo.StarVotes("alice"));Assert.Empty(repo.StarVotes("bob"));Assert.Equal("rodcas",repo.Cached(build.ClassId,build.Stage,build.Purpose).Single().AuthorName);Assert.Equal("Autor não informado",BuildPublicName.Clean(null));Assert.Equal("Autor não informado",BuildPublicName.Clean("private@example.com"));
        using var vm=new BuildViewModel(new(root));Assert.True(vm.IsSignedOut);Assert.False(vm.IsAdmin);vm.ViewBuildCommand.Execute(row);Assert.Equal("rodcas",vm.CommunityPreview!.PreviewAuthor);Assert.True(vm.CommunityPreview.HasAdminNotes);Assert.Equal("Revisada pelo ADM",vm.CommunityPreview.PreviewAdminNotes);var card=new CommunityBuildCard(row,vm);Assert.Equal("★ 12",card.StarText);Assert.Equal("Criado por: rodcas",card.Author);Assert.Equal("☆ 0",new CommunityBuildCard(new("old",build,"approved"),vm).StarText);
    }
    [Fact] public void CommunityAndModerationPreviewUseBuildEquipmentWithoutSavingDraft()
    {
        string root=Root();using var vm=new BuildViewModel(new(root));var build=BuildCatalog.New();var weapon=build.Equipment.Single(e=>e.Slot=="weapon");weapon.ItemId=21007;weapon.Refine=9;weapon.Grade="B";weapon.Cards.Add(4004);weapon.Enchants.Add(4702);weapon.RandomOptions.AddRange(new[]{"ATK +10","Precisão +3","Dano Crítico +2%"});
        vm.ViewBuildCommand.Execute(new CommunityBuild("fixture",build,"approved"));Assert.True(vm.HasCommunityPreview);var preview=vm.CommunityPreview!;var tip=preview.Slots.Single(s=>s.Definition.Id=="weapon").TooltipData;Assert.Contains("+9",tip.Title);Assert.Contains("[B]",tip.Title);Assert.Single(tip.Cards);Assert.Single(tip.Enchants);Assert.Equal(3,tip.RandomOptions.Count);Assert.Contains("Lorde",preview.BuildDetails);preview.Flush();Assert.Empty(vm.Saved);Assert.Empty(new BuildRepository(root).Load());
        vm.SelectedSubmission=new BuildSubmission("fixture","time","uid",build,new());Assert.NotNull(vm.ReviewPreview);Assert.Equal(tip.Title,vm.ReviewPreview!.Slots.Single(s=>s.Definition.Id=="weapon").TooltipData.Title);vm.CloseBuildPreviewCommand.Execute(null);Assert.False(vm.HasCommunityPreview);Assert.False(vm.HasActiveBuild);Assert.Empty(vm.Saved);
    }
    [Fact] public void NormalAccountsCannotOpenModerationOrModerateLocally()
    {
        using var vm=new BuildViewModel(new(Root()));Assert.False(vm.IsAdmin);vm.Area=3;Assert.NotEqual(3,vm.Area);Assert.False(vm.IsModeration);vm.ApproveBuildCommand.Execute(null);vm.RejectBuildCommand.Execute(null);Assert.Empty(vm.PendingBuilds);Assert.Equal(0,vm.GlobalApproved);Assert.Empty(vm.Saved);
    }
    [Fact] public void TwoHandedWeaponOccupiesShieldWithoutDuplicatingItem()
    {
        using var vm=new BuildViewModel(new(Root()));vm.NewCommand.Execute(null);var weapon=vm.Slots.Single(s=>s.Definition.Id=="weapon");var shield=vm.Slots.Single(s=>s.Definition.Id=="shield");shield.Assign(vm.Items.Find(2101));weapon.Assign(vm.Items.Find(21007));Assert.True(shield.OccupiedByWeapon);Assert.False(shield.Available);Assert.Null(shield.Model.ItemId);Assert.Contains("Ocupado",shield.Summary);shield.Assign(vm.Items.Find(2101));Assert.Null(shield.Model.ItemId);vm.Flush();vm.LoadCommand.Execute(vm.Saved.Single());Assert.True(vm.Slots.Single(s=>s.Definition.Id=="shield").OccupiedByWeapon);vm.Slots.Single(s=>s.Definition.Id=="weapon").Assign(null);Assert.True(vm.Slots.Single(s=>s.Definition.Id=="shield").Available);
    }
    [Fact] public void TenguMasksAndTwoHandedSwordUseCorrectSlots()
    {
        var catalog=new BuildEquipmentCatalog();var lord=BuildCatalog.Classes.Single(c=>c.Id=="lord");var mid=BuildCatalog.Slots.Single(s=>s.Id=="head-mid");var weapon=BuildCatalog.Slots.Single(s=>s.Id=="weapon");var shield=BuildCatalog.Slots.Single(s=>s.Id=="shield");
        foreach(int id in new[]{19456,19457}){Assert.True(catalog.Compatible(catalog.Find(id)!,lord,mid));Assert.False(catalog.Compatible(catalog.Find(id)!,lord,weapon));}
        var sword=catalog.Find(21007)!;Assert.True(catalog.Compatible(sword,lord,weapon));Assert.False(catalog.Compatible(sword,lord,shield));Assert.True(BuildEquipmentCatalog.TwoHanded(sword));Assert.False(catalog.Compatible(sword,BuildCatalog.Classes.Single(c=>c.Id=="high-wizard"),weapon));
    }
    [Fact] public void OpeningAndNavigatingDoNotCreateDrafts()
    {
        string root=Root();using(var vm=new BuildViewModel(new(root))){Assert.False(vm.HasActiveBuild);Assert.Equal(1,vm.Area);vm.Flush();vm.Area=0;vm.Area=2;vm.Area=1;vm.Flush();Assert.Empty(vm.Saved);vm.NewCommand.Execute(null);Assert.True(vm.HasActiveBuild);vm.Flush();Assert.Single(vm.Saved);}
        using var reopened=new BuildViewModel(new(root));reopened.Flush();Assert.Single(reopened.Saved);Assert.False(reopened.HasActiveBuild);reopened.LoadCommand.Execute(reopened.Saved.Single());Assert.True(reopened.HasActiveBuild);reopened.DeleteCommand.Execute(reopened.Document);reopened.Flush();Assert.Empty(reopened.Saved);Assert.False(reopened.HasActiveBuild);
    }
    [Fact] public void FirebaseOwnCacheIsSeparatedByUidAndNeverReplacesDrafts()
    {
        var repo=new BuildRepository(Root());var local=BuildCatalog.New();local.Title="Rascunho local";repo.Save(local);
        var cloud=BuildCatalog.New();cloud.Title="Publicação própria";repo.CacheOwn("alice",new(){cloud});
        Assert.Equal(cloud.Id,Assert.Single(repo.CachedOwn("alice")).Id);Assert.Empty(repo.CachedOwn("bob"));Assert.Equal(local.Id,Assert.Single(repo.Load()).Id);
        repo.CacheOwn("alice",new());Assert.Empty(repo.CachedOwn("alice"));Assert.Single(repo.Load());
    }
    [Fact] public void EquipmentTooltipUsesSavedBuildConfigurationAndSurvivesOpenDuplicateAndCommunityCopy()
    {
        using var vm=new BuildViewModel(new(Root()));vm.NewCommand.Execute(null);var row=vm.Slots.Single(s=>s.Definition.Id=="weapon");row.Assign(vm.Items.Find(1101));row.Refine=9;row.Grade="D";row.CardsText="4004, 4004, 999999999";
        int enchant=vm.Items.Search("Destreza 2").FirstOrDefault()?.Id??4702;row.EnchantsText=enchant.ToString();row.RandomOptionsText="ATK +10\nPrecisão +3\nDano Crítico +2%";
        var tip=row.TooltipData;Assert.StartsWith("+9 ",tip.Title);Assert.EndsWith(" [D]",tip.Title);Assert.Equal(vm.Items.Find(4004)!.Name,tip.Cards[0].Name);Assert.Equal(2,tip.Cards[0].Count);Assert.False(tip.Cards[1].Known);Assert.Equal("#999999999",tip.Cards[1].Name);Assert.Equal(3,tip.RandomOptions.Count);Assert.Equal(enchant,Assert.Single(tip.Enchants).Id);
        row.Refine=10;Assert.StartsWith("+10 ",row.TooltipData.Title);vm.Flush();var saved=vm.Saved.Single(b=>b.Id==vm.Document.Id);vm.NewCommand.Execute(null);vm.LoadCommand.Execute(saved);
        var restored=vm.Slots.Single(s=>s.Definition.Id=="weapon").TooltipData;Assert.Equal(tip.RandomOptions,restored.RandomOptions);Assert.Equal(tip.Enchants.Select(e=>e.Id),restored.Enchants.Select(e=>e.Id));Assert.StartsWith("+10 ",restored.Title);
        vm.DuplicateCommand.Execute(vm.Document);Assert.Equal(3,vm.Slots.Single(s=>s.Definition.Id=="weapon").TooltipData.RandomOptions.Count);vm.CopyCommunityCommand.Execute(new CommunityBuild("fixture-only",vm.Document,"approved"));Assert.Equal("D",vm.Slots.Single(s=>s.Definition.Id=="weapon").Model.Grade);Assert.Equal(0,vm.GlobalApproved);
        var empty=vm.Slots.Single(s=>s.Definition.Id=="shield").TooltipData;Assert.Empty(empty.Cards);Assert.Empty(empty.Enchants);Assert.Empty(empty.RandomOptions);
    }
    [Fact] public void OpeningSurvivesSelectionClearedBySaveAndSupportsDuplicateRename()
    {
        using var vm=new BuildViewModel(new(Root()));vm.NewCommand.Execute(null);vm.Title="Primeira";vm.Flush();var first=vm.Saved.Single(b=>b.Id==vm.Document.Id);vm.NewCommand.Execute(null);vm.Title="Segunda";vm.Flush();
        vm.SelectedSaved=first;vm.Saved.CollectionChanged+=(_,_)=>vm.SelectedSaved=null;vm.Area=1;vm.SelectedSaved=first;vm.LoadCommand.Execute(null);
        Assert.Equal(first.Id,vm.Document.Id);Assert.Equal("Primeira",vm.Title);Assert.Equal(0,vm.Area);Assert.NotNull(vm.SelectedSlot);Assert.Equal(21,vm.Slots.Count);
        vm.Title="Alteração ainda no editor";vm.SelectedSaved=first;vm.LoadCommand.Execute(null);Assert.Equal("Alteração ainda no editor",vm.Title);
        vm.DuplicateCommand.Execute(vm.Document);Assert.NotEqual(first.Id,vm.Document.Id);var card=vm.SavedCards.Single(c=>c.Model.Id==vm.Document.Id);card.RenameText="Minha cópia";card.SaveNameCommand.Execute(null);Assert.Equal("Minha cópia",vm.Title);Assert.Equal(0,vm.GlobalApproved);
        var editorClass=vm.Document.ClassId;vm.CommunityClass=BuildCatalog.Classes[1];vm.CommunityStage="End";Assert.Equal(editorClass,vm.Document.ClassId);Assert.Equal("Early",vm.Stage);
    }
    [Fact] public void MissingBuildDataLoadsPartiallyAndPreservesOriginalWithDiagnosticLog()
    {
        string root=Root();var repository=new BuildRepository(root);var build=BuildCatalog.New();build.Title="Recuperar";repository.Save(build);
        build.Stats=null!;build.Equipment=new(){new(){Slot="weapon",ItemId=1101,Cards=null!,Enchants=null!,RandomOptions=null!},null!};string raw=System.Text.Json.JsonSerializer.Serialize(build);
        using(var db=new Microsoft.Data.Sqlite.SqliteConnection("Data Source="+Path.Combine(root,"builds.sqlite"))){db.Open();using var c=db.CreateCommand();c.CommandText="UPDATE build_drafts SET payload=$data WHERE id=$id";c.Parameters.AddWithValue("$data",raw);c.Parameters.AddWithValue("$id",build.Id);c.ExecuteNonQuery();}
        using var vm=new BuildViewModel(new(root));var recovered=vm.Saved.Single(b=>b.Id==build.Id);vm.LoadCommand.Execute(recovered);Assert.Equal(1101,vm.Slots.Single(s=>s.Definition.Id=="weapon").Model.ItemId);Assert.Equal(6,vm.Document.Stats.Count);Assert.All(vm.Document.Equipment,e=>{Assert.NotNull(e);Assert.NotNull(e.Cards);Assert.NotNull(e.RandomOptions);});Assert.Contains("parcial",vm.Message);
        Assert.True(File.Exists(Path.Combine(root,"logs","builds.log")));Assert.Contains(Directory.GetFiles(Path.Combine(root,"build-recovery")),p=>File.ReadAllText(p)==raw);
    }
    [Fact] public void RandomOptionsAreContextualAndRespectKnownItemOverridesAndRanges()
    {
        var options=new BuildRandomOptions();var weapon=BuildCatalog.Slots.Single(s=>s.Id=="weapon");var top=BuildCatalog.Slots.Single(s=>s.Id=="head-top");var real=new BuildEquipmentCatalog();
        foreach(var shadow in BuildCatalog.Slots.Where(s=>s.Group=="Shadow")){var meta=new BuildItemMetadata{Id=999001,Type="ShadowGear",Locations=new(){{shadow.Location,true}}};var rows=options.For(shadow,meta.Id,meta,"Padrão");Assert.Equal(17,rows.Count);Assert.Contains(rows,o=>o.Name=="Redução de dano recebido de chefes");Assert.DoesNotContain(rows,o=>o.Name=="ATK");Assert.All(rows,o=>Assert.Equal("",o.Range));} var offensive=options.For(weapon,1101,real.Find(1101),"Padrão");Assert.Contains(offensive,o=>o.Name=="ATK");Assert.DoesNotContain(offensive,o=>o.Negative);foreach(var profile in BuildRandomOptions.Profiles(weapon))Assert.DoesNotContain(options.For(weapon,1101,real.Find(1101),profile),o=>o.Negative);Assert.DoesNotContain(offensive,o=>o.Name=="DEF");
        var head=new BuildItemMetadata{Id=900001,Type="Armor",Locations=new(){{"Head_Top",true}}};var defensive=options.For(top,head.Id,head,"Padrão");Assert.Contains(defensive,o=>o.Name=="DEF");Assert.DoesNotContain(defensive,o=>o.Name=="ATK");Assert.Empty(options.For(top,head.Id,head,"Forjado • rank 1–5"));Assert.Empty(options.For(top,999999,null,"Padrão"));
        string root=Root();Directory.CreateDirectory(root);File.WriteAllText(Path.Combine(root,"build-random-option-rules.json"),System.Text.Json.JsonSerializer.Serialize(new[]{new BuildOptionRule{ItemId=1101,AllowedSlots=new(){"weapon"},AllowedOptionIds=new(){offensive[0].Id}}}));Assert.Single(new BuildRandomOptions(root).For(weapon,1101,real.Find(1101),"Padrão"));
        using var vm=new BuildViewModel(new(Root()));vm.NewCommand.Execute(null);vm.SelectedSlot=vm.Slots.Single(s=>s.Definition.Id=="weapon");vm.SelectedSlot.Assign(vm.Items.Find(1101));vm.SelectedOption=vm.OptionSuggestions.First(o=>o.Name=="ATK"&&!o.Negative);vm.OptionValue=999;vm.AddOptionCommand.Execute(null);Assert.Equal("ATK",Assert.Single(vm.SelectedSlot.Model.RandomOptions));Assert.Equal("Resistência a Chefe",BuildRandomOption.NameOnly("Resistência a Chefe +2%"));
    }
    [Fact] public void TemporalBootsAreSearchableAndCompatibleInFarmland(){using var vm=new BuildViewModel(new(Root()));vm.NewCommand.Execute(null);vm.SelectedSlot=vm.Slots.Single(s=>s.Definition.Id=="shoes");vm.Query="Botas Temporais";vm.SearchKind="Equipamento";foreach(var name in new[]{"Botas Temporais FOR","Botas Temporais AGI","Botas Temporais DES"})Assert.Contains(vm.Suggestions,i=>i.Name==name);foreach(int id in new[]{22006,22007,22008,22009,22010,22011}){Assert.NotNull(vm.Items.Find(id));Assert.True(vm.Equipment.Compatible(vm.Equipment.Find(id)!,vm.SelectedClass,vm.SelectedSlot.Definition));}vm.CompatibleOnly=false;Assert.DoesNotContain(vm.Suggestions,i=>i.Id is >=22000 and <=22005);Assert.All(Enumerable.Range(22000,6),id=>Assert.False(BuildEquipmentCatalog.Selectable(id)));var index=new ItemFarmIndex(new FarmCatalog{Items=Enumerable.Range(1,30).Select(i=>new FarmItem{Id=i,Name="Bota "+i}).ToList()});Assert.Contains(index.Search("Bota",i=>i.Id==30),i=>i.Id==30);}
    static string Root()=>Path.Combine(Path.GetTempPath(),"Farmland-builds-"+Guid.NewGuid().ToString("N"));
    static BuildItemMetadata[] Metadata()=>BuildCatalog.Slots.Select((s,i)=>new BuildItemMetadata{Id=900000+i,Type=s.Group=="Shadow"?"ShadowGear":s.Id=="weapon"?"Weapon":"Armor",Jobs=new(){{"All",true}},Classes=new(){{"All",true}},Locations=new(){{s.Location,true}},Slots=1,Refineable=true}).ToArray();
    static PlayerBuild Complete(string stage,BuildItemMetadata[] metadata)
    {
        var b=BuildCatalog.New();b.Stage=stage;foreach(var e in b.Equipment){var slot=BuildCatalog.Slots.Single(s=>s.Id==e.Slot);if(slot.Group=="Normal"||slot.Group=="Visual"&&stage!="Early"||slot.Group=="Shadow"&&stage=="End"){
            e.ItemId=metadata[Array.IndexOf(BuildCatalog.Slots,slot)].Id;if(stage=="End"){e.Refine=slot.Group=="Shadow"?10:9;e.Grade="B";e.RandomOptions=Enumerable.Range(1,slot.Group=="Visual"?3:1).Select(i=>"Opção de fixture "+i).ToList();}else if(slot.Group=="Visual")e.RandomOptions.Add("Opção de fixture");
        }}return b;
    }
    [Fact] public void StageRulesUseRealRequirementsWithoutDemandingEarlyRefines()
    {
        var m=Metadata();var validator=new BuildValidator(new(m));var criteria=new BuildCriteria();Assert.Equal(18,BuildCatalog.Classes.Length);Assert.Equal(54,BuildCatalog.Total);Assert.DoesNotContain(BuildCatalog.Classes,c=>c.Name=="Taekwon");
        foreach(var stage in BuildCatalog.Stages)Assert.Empty(validator.Validate(Complete(stage,m),criteria));
        var end=Complete("End",m);end.Equipment.Single(e=>e.Slot=="shadow-weapon").Refine=9;Assert.Contains(validator.Validate(end,criteria),e=>e.Contains("+10"));end.Equipment.Single(e=>e.Slot=="shadow-weapon").Refine=10;
        end.Equipment.Single(e=>e.Slot=="armor").Grade="C";Assert.Contains(validator.Validate(end,criteria),e=>e.Contains("grade B"));end.Equipment.Single(e=>e.Slot=="armor").Grade="B";end.Equipment.Single(e=>e.Slot=="costume-top").RandomOptions.RemoveAt(0);Assert.Contains(validator.Validate(end,criteria),e=>e.Contains("Visual topo")&&e.Contains("Random Options"));
    }
    [Fact] public void CompatibilityAndTwoHandedWeaponsAreValidatedWithoutInventingUnknownItems()
    {
        var m=Metadata();var b=Complete("Early",m);var weapon=m[Array.FindIndex(BuildCatalog.Slots,s=>s.Id=="weapon")];weapon.SubType="2hSword";var validator=new BuildValidator(new(m));Assert.Contains(validator.Validate(b,new()),e=>e.Contains("duas mãos"));b.Equipment.Single(e=>e.Slot=="shield").ItemId=null;Assert.Empty(validator.Validate(b,new()));
        weapon.Jobs=new(){{"Wizard",true}};Assert.Contains(validator.Validate(b,new()),e=>e.Contains("incompatível"));weapon.Jobs=new(){{"Knight",true}};b.Equipment.Single(e=>e.Slot=="armor").ItemId=123456789;Assert.Contains(validator.Validate(b,new()),e=>e.Contains("não confirmada"));
        var real=new BuildEquipmentCatalog();Assert.NotNull(real.Find(1101));Assert.NotNull(real.Find(4001));
    }
    [Fact] public void SqliteDraftsPreserveEquipmentAndNeverContributeToGlobalProgress()
    {
        var root=Root();var repository=new BuildRepository(root);var m=Metadata();var b=Complete("End",m);b.Stats["FOR"]=99;b.Equipment[0].Enchants.Add(4001);repository.Save(b);var copy=System.Text.Json.JsonSerializer.Deserialize<PlayerBuild>(System.Text.Json.JsonSerializer.Serialize(b))!;copy.Id=Guid.NewGuid().ToString("N");copy.Purpose="PvP";repository.Save(copy);
        Assert.Equal(2,repository.Load().Count);Assert.All(repository.Load(),saved=>Assert.Equal(99,saved.Stats["FOR"]));
        using var vm=new BuildViewModel(new(root),equipment:new(m));Assert.Equal("Lorde",vm.SelectedClass.Name);Assert.Equal("Early",vm.Stage);Assert.Equal("PvM",vm.Purpose);Assert.Equal("0/54 — 0%",vm.GlobalLabel);Assert.DoesNotContain("Recompensa desbloqueada",vm.RewardText);Assert.Equal(2,vm.Saved.Count);
    }
    sealed class Gateway:IBuildCommunityGateway
    {
        public bool Configured=>true;public List<string> Keys {get;set;}=new();public bool Unlocked {get;set;}
        public Task<BuildCommunitySnapshot> FetchAsync(string c,string s,string p,CancellationToken ct)=>Task.FromResult(new BuildCommunitySnapshot(new(),Keys,Unlocked,"fixture-server-revision"));
        public Task SubmitAsync(PlayerBuild b,CancellationToken ct)=>Task.CompletedTask;
    }
    [Fact] public void GlobalProgressDeduplicatesClassStageAndRewardRequiresFreshServerAuthority()
    {
        var root=Root();var gateway=new Gateway{Keys=new(){"lord:Early","lord:Early","lord:Mid","invalid:End"},Unlocked=true};
        using(var vm=new BuildViewModel(new(root),gateway)){vm.SyncCommand.Execute(null);Assert.Equal(2,vm.GlobalApproved);Assert.DoesNotContain("Recompensa desbloqueada",vm.RewardText);}
        gateway.Keys=BuildCatalog.Classes.SelectMany(c=>BuildCatalog.Stages.Select(s=>c.Id+":"+s)).ToList();using(var vm=new BuildViewModel(new(root),gateway)){vm.SyncCommand.Execute(null);Assert.Equal(54,vm.GlobalApproved);Assert.Contains("Desafio concluído! Recompensa desbloqueada.",vm.RewardText);}
        using(var offline=new BuildViewModel(new(root))){Assert.Equal(0,offline.GlobalApproved);Assert.DoesNotContain("Recompensa desbloqueada",offline.RewardText);}
    }
}
