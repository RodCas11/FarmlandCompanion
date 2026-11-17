using System;
using System.Collections.Generic;
using System.Linq;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public sealed class BuildValidator
{
    readonly BuildEquipmentCatalog catalog;
    public BuildValidator(BuildEquipmentCatalog catalog)=>this.catalog=catalog;
    public List<string> Validate(PlayerBuild build,BuildCriteria rules)
    {
        var errors=new List<string>();var job=BuildCatalog.Classes.FirstOrDefault(c=>c.Id==build.ClassId);
        if(job==null){errors.Add("Classe não reconhecida.");return errors;}
        if(!BuildCatalog.Stages.Contains(build.Stage)||!BuildCatalog.Purposes.Contains(build.Purpose)){errors.Add("Estágio ou finalidade inválida.");return errors;}
        if(build.Equipment.GroupBy(e=>e.Slot).Any(g=>g.Count()>1)){errors.Add("Slots duplicados.");return errors;}
        if(build.Equipment.Any(e=>!BuildCatalog.Slots.Any(s=>s.Id==e.Slot)))errors.Add("Há um slot desconhecido na build.");
        if(build.Stats.Count!=6||build.Stats.Any(s=>!new[]{"FOR","AGI","VIT","INT","DES","SOR"}.Contains(s.Key)||s.Value is <1 or >999))errors.Add("Atributos devem conter FOR, AGI, VIT, INT, DES e SOR, entre 1 e 999; limites oficiais dependem do ADM.");
        bool twoHands=BuildEquipmentCatalog.TwoHanded(catalog.Find(build.Equipment.FirstOrDefault(e=>e.Slot=="weapon")?.ItemId??0));
        foreach(var slot in BuildCatalog.Slots)
        {
            var equipped=build.Equipment.FirstOrDefault(e=>e.Slot==slot.Id);bool required=slot.Group=="Normal"||slot.Group=="Visual"&&build.Stage!="Early"||slot.Group=="Shadow"&&build.Stage=="End";
            if(slot.Id=="shield"&&twoHands){if(equipped?.ItemId!=null)errors.Add("Arma de duas mãos ocupa também o slot do escudo.");continue;}
            var covering=build.Equipment.Where(e=>e.ItemId!=null).FirstOrDefault(e=>catalog.Find(e.ItemId!.Value) is {} meta&&BuildEquipmentCatalog.At(meta,slot.Location));
            if(equipped?.ItemId==null){if(required&&covering==null)errors.Add(slot.Name+": item obrigatório ausente.");continue;}
            var item=catalog.Find(equipped.ItemId.Value);
            if(item==null){errors.Add(slot.Name+": compatibilidade ainda não confirmada no catálogo. O rascunho pode ser salvo.");continue;}
            if(!catalog.Compatible(item,job,slot))errors.Add(slot.Name+": item incompatível com o slot/classe na referência Renewal.");
            if(slot.Group=="Pet")continue;
            if(equipped.Refine is <0 or >20||!BuildCatalog.Grades.Contains(equipped.Grade))errors.Add(slot.Name+": refino/grade inválido.");
            if(equipped.Cards.Count>item.Slots)errors.Add(slot.Name+": cartas excedem os sockets do item.");
            if(equipped.Cards.Concat(equipped.Enchants).Any(id=>catalog.Find(id)?.Type!="Card"))errors.Add(slot.Name+": carta/encantamento sem identificação válida no catálogo.");
            if(equipped.Cards.Any(id=>catalog.Find(id) is {} card&&card.Locations.Any(p=>p.Value)&&!BuildEquipmentCatalog.At(card,slot.Location)))errors.Add(slot.Name+": carta incompatível com a localização.");
            int options=equipped.RandomOptions.Count(s=>!string.IsNullOrWhiteSpace(s));if(options>(slot.Group=="Visual"?3:5))errors.Add(slot.Name+": limite de linhas de Random Options excedido (visuais: 3; demais: 5).");
            if(slot.Group=="Visual"&&build.Stage!="Early"&&options<(build.Stage=="End"?rules.EndCostumeOptions:rules.MidCostumeOptions))errors.Add(slot.Name+": faltam Random Options.");
            if(build.Stage=="End"){
                if(slot.Group=="Normal"&&(equipped.Refine<rules.NormalEndRefine||Array.IndexOf(BuildCatalog.Grades,equipped.Grade)<Array.IndexOf(BuildCatalog.Grades,rules.NormalEndGrade)))errors.Add(slot.Name+$": requer grade {rules.NormalEndGrade} ou superior e +{rules.NormalEndRefine}.");
                if(slot.Group=="Shadow"&&equipped.Refine<rules.ShadowEndRefine)errors.Add(slot.Name+$": requer +{rules.ShadowEndRefine}.");
                if(slot.Group!="Visual"&&options<rules.EndEquipmentOptions)errors.Add(slot.Name+": falta Random Option.");
                if(equipped.Cards.Count<rules.RequiredCards.GetValueOrDefault(slot.Id)||equipped.Enchants.Count<rules.RequiredEnchants.GetValueOrDefault(slot.Id))errors.Add(slot.Name+": cartas/encantamentos obrigatórios pendentes.");
            }
        }
        return errors.Distinct().ToList();
    }
}
