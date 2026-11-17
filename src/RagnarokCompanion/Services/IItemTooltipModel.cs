using System.ComponentModel;
using RagnarokCompanion.Models;
using System.Collections.Generic;
using System.Windows.Media;
namespace RagnarokCompanion.Services;
public interface IItemTooltipModel:INotifyPropertyChanged {FarmItem? SelectedItem {get;}string ItemName {get;}string Icon {get;}ItemDescriptionService Descriptions {get;}}
public sealed record BuildTooltipComponent(int Id,string Name,int Count,bool Known,ImageSource? Image);
public sealed record BuildTooltipData(int? ItemId,string Title,ImageSource? Image,IReadOnlyList<BuildTooltipComponent> Cards,IReadOnlyList<BuildTooltipComponent> Enchants,IReadOnlyList<string> RandomOptions);
public interface IBuildItemTooltipModel:IItemTooltipModel {BuildTooltipData TooltipData {get;}}
