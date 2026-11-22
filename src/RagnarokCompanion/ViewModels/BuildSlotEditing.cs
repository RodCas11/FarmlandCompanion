using System;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.ViewModels;
public sealed partial class BuildViewModel
{
 bool transientEditor;string? editingDocument,editingSlot;BuildViewModel? slotDraft;
 public BuildViewModel? SlotDraft {get=>slotDraft;private set{slotDraft=value;Changed();Changed(nameof(IsSlotEditorOpen));}}
 public bool IsSlotEditorOpen=>SlotDraft!=null;
 public BuildSlotRow? PetSlot=>Slots.FirstOrDefault(s=>s.Definition.Id=="pet-egg");
 public bool IsEquipmentSlot=>SelectedSlot?.Definition.Group!="Pet"; public bool CanRefineSlot=>SelectedSlot?.Definition.Group is "Normal" or "Shadow"; public bool CanGradeSlot=>SelectedSlot?.Definition.Group=="Normal"; public bool CanComponentSlot=>CanGradeSlot;
 public void BeginSlotEdit(BuildSlotRow row)
 {
  if(readOnlyPreview||transientEditor||!HasActiveBuild||!row.Available)return;
  CancelSlotEdit();SelectedSlot=row;var draft=Preview(Document);draft.readOnlyPreview=false;draft.transientEditor=true;
  draft.Group=row.Definition.Group;draft.SelectedSlot=draft.Slots.Single(s=>s.Definition.Id==row.Definition.Id);
  draft.RefreshCompactSlot();draft.Query=row.SelectedItem?.Name??"";draft.Tell("");editingDocument=Document.Id;editingSlot=row.Definition.Id;SlotDraft=draft;
 }
 public ICommand SaveSlotDraftCommand=>new RelayCommand(_=>SaveSlotEdit());
 public ICommand CancelSlotDraftCommand=>new RelayCommand(_=>CancelSlotEdit());
 public void CancelSlotEdit(){var old=SlotDraft;SlotDraft=null;editingDocument=editingSlot=null;old?.Dispose();}
 void SaveSlotEdit()
 {
  var draft=SlotDraft;if(draft?.SelectedSlot==null||editingDocument!=Document.Id||editingSlot==null)return;
  var row=draft.SelectedSlot;if(row.InputError.Length>0||row.Refine is <0 or >20){draft.Tell("Confira os IDs e o refino antes de salvar.");return;}
  var item=row.Model;if(item.Enchants.Any(id=>draft.FindItem(id) is {} enchant&&!EnchantLevelAllowed(enchant.Name))){draft.Tell("Encantamentos permitidos somente até +6.");return;}var metadata=item.ItemId is {} id?Equipment.Find(id):null;
  if(item.ItemId!=null&&((row.Definition.Group=="Pet"&&metadata==null)||metadata!=null&&!Equipment.Compatible(metadata,SelectedClass,row.Definition))){draft.Tell("O item não é compatível com este slot/classe.");return;}
  var copy=JsonSerializer.Deserialize<BuildEquipment>(JsonSerializer.Serialize(item))!;
  if(row.Definition.Group is "Visual" or "Shadow"){copy.Grade="Sem";copy.Cards.Clear();copy.Enchants.Clear();if(row.Definition.Group=="Visual")copy.Refine=0;} int index=Document.Equipment.FindIndex(e=>e.Slot==editingSlot);Document.Equipment[index]=copy;
  if(editingSlot=="weapon"&&Services.BuildEquipmentCatalog.TwoHanded(metadata)){int shield=Document.Equipment.FindIndex(e=>e.Slot=="shield");Document.Equipment[shield]=new(){Slot="shield"};}
  string group=Group;string slotId=editingSlot;var document=Document;CancelSlotEdit();Load(document);Group=group;SelectedSlot=Slots.Single(s=>s.Definition.Id==slotId);Edited();Tell("Slot atualizado. Alterações aplicadas à build.");
 }
}
