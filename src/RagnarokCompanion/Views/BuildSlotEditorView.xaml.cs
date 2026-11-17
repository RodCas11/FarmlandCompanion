using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RagnarokCompanion.ViewModels;
namespace RagnarokCompanion.Views;
public partial class BuildSlotEditorView:UserControl {
 public BuildSlotEditorView(){InitializeComponent();}
 void EditorVisibilityChanged(object sender,DependencyPropertyChangedEventArgs e){if(IsVisible)Dispatcher.BeginInvoke(new System.Action(()=>ItemQuery.Focus()));}
 void SelectItem(object sender,System.EventArgs e){if(DataContext is not BuildViewModel parent||parent.SlotDraft is not {} draft||sender is not BuildItemPicker {SelectedItem: {} item})return;draft.SelectedSuggestion=item;draft.EquipCommand.Execute(null);}
 void SelectComponent(object sender,System.EventArgs e){if(DataContext is not BuildViewModel parent||parent.SlotDraft is not {} draft||sender is not BuildItemPicker {SelectedItem: {} item})return;draft.SelectedComponent=item;draft.AddSlotComponentCommand.Execute(null);}
 void EditorLoaded(object sender,RoutedEventArgs e){ItemQuery.Focus();}
 static bool Invalid(DependencyObject root){if(Validation.GetHasError(root))return true;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)if(Invalid(VisualTreeHelper.GetChild(root,i)))return true;return false;}
 void SaveClick(object sender,RoutedEventArgs e){if(DataContext is BuildViewModel vm){if(Invalid(this)){vm.SlotDraft?.Tell("Confira os campos inválidos antes de salvar.");return;}vm.SaveSlotDraftCommand.Execute(null);}}
}