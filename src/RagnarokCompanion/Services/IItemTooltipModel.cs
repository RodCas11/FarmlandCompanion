using System.ComponentModel;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public interface IItemTooltipModel:INotifyPropertyChanged {FarmItem? SelectedItem {get;}string ItemName {get;}string Icon {get;}ItemDescriptionService Descriptions {get;}}
