using System;
using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Views;
// Keeps typed text independent of the result list; refreshing suggestions cannot erase the selected name.
public sealed class BuildItemPicker:UserControl
{
 public static readonly DependencyProperty TextProperty=DependencyProperty.Register(nameof(Text),typeof(string),typeof(BuildItemPicker),new FrameworkPropertyMetadata("",FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,(d,e)=>((BuildItemPicker)d).UpdateText()));
 public static readonly DependencyProperty ItemsSourceProperty=DependencyProperty.Register(nameof(ItemsSource),typeof(IEnumerable),typeof(BuildItemPicker),new PropertyMetadata(null,(d,e)=>((BuildItemPicker)d).UpdateItems(e.OldValue,e.NewValue)));
 public string Text {get=>(string)GetValue(TextProperty);set=>SetValue(TextProperty,value);}
 public IEnumerable? ItemsSource {get=>(IEnumerable?)GetValue(ItemsSourceProperty);set=>SetValue(ItemsSourceProperty,value);}
 public FarmItem? SelectedItem {get;private set;} public event EventHandler? ItemChosen;
 readonly TextBox input=new(){Padding=new Thickness(8,5,8,5),Margin=new Thickness(0),Background=new SolidColorBrush(Color.FromRgb(12,15,22)),Foreground=Brushes.White,BorderThickness=new Thickness(0),FontSize=12};
 readonly ListBox results=new(){DisplayMemberPath="Name",MaxHeight=190,Background=new SolidColorBrush(Color.FromRgb(24,29,44)),Foreground=Brushes.White,BorderThickness=new Thickness(0),FontSize=12};
 readonly Popup popup=new(){AllowsTransparency=true,StaysOpen=true,Placement=PlacementMode.Bottom};bool updating,committing,typing;
 public BuildItemPicker(){var grid=new Grid();grid.Children.Add(new Border{CornerRadius=new CornerRadius(5),BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.FromRgb(53,64,87)),Background=input.Background,Child=input});popup.PlacementTarget=input;popup.Child=new Border{Background=results.Background,BorderBrush=new SolidColorBrush(Color.FromRgb(53,64,87)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Padding=new Thickness(3),Child=results};grid.Children.Add(popup);Content=grid;
  var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(7,5,7,5)));style.Setters.Add(new Setter(Control.ForegroundProperty,Brushes.White));style.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.Transparent));var selected=new Trigger{Property=ListBoxItem.IsSelectedProperty,Value=true};selected.Setters.Add(new Setter(Control.BackgroundProperty,new SolidColorBrush(Color.FromRgb(86,61,36))));style.Triggers.Add(selected);results.ItemContainerStyle=style;
  input.PreviewTextInput+=(_,_)=>typing=true;input.PreviewKeyDown+=Key;
  input.TextChanged+=(_,_)=>{if(updating||committing)return;typing=input.IsKeyboardFocusWithin;SetCurrentValue(TextProperty,input.Text);ShowResults();};
  results.PreviewMouseLeftButtonUp+=(_,e)=>{if(ItemsControl.ContainerFromElement(results,e.OriginalSource as DependencyObject) is ListBoxItem row){results.SelectedItem=row.DataContext;Choose();e.Handled=true;}};
  input.LostKeyboardFocus+=(_,_)=>Dispatcher.BeginInvoke(new Action(()=>{if(!results.IsKeyboardFocusWithin)popup.IsOpen=false;}));
  Unloaded+=(_,_)=>{popup.IsOpen=false;typing=false;};IsVisibleChanged+=(_,_)=>{if(!IsVisible)popup.IsOpen=false;};SizeChanged+=(_,_)=>{if(popup.Child is FrameworkElement child)child.Width=ActualWidth;};
 }
 void UpdateText(){if(input.Text==Text)return;updating=true;input.Text=Text??"";updating=false;}
 void UpdateItems(object oldValue,object newValue){if(oldValue is INotifyCollectionChanged old)old.CollectionChanged-=ItemsChanged;results.ItemsSource=newValue as IEnumerable;if(newValue is INotifyCollectionChanged current)current.CollectionChanged+=ItemsChanged;ShowResults();}
 void ItemsChanged(object? sender,NotifyCollectionChangedEventArgs e)=>ShowResults();
 void ShowResults(){popup.IsOpen=typing&&!committing&&IsVisible&&input.IsKeyboardFocusWithin&&!string.IsNullOrWhiteSpace(input.Text)&&results.Items.Count>0;}
 void Choose(){if(results.SelectedItem is not FarmItem item)return;committing=true;typing=false;SelectedItem=item;SetCurrentValue(TextProperty,item.Name);UpdateText();popup.IsOpen=false;ItemChosen?.Invoke(this,EventArgs.Empty);committing=false;input.CaretIndex=input.Text.Length;}
 void Key(object sender,KeyEventArgs e){if(e.Key==System.Windows.Input.Key.Escape&&popup.IsOpen){popup.IsOpen=false;typing=false;e.Handled=true;}else if(e.Key is System.Windows.Input.Key.Down or System.Windows.Input.Key.Up){typing=true;ShowResults();if(results.Items.Count>0){results.SelectedIndex=Math.Clamp(results.SelectedIndex+(e.Key==System.Windows.Input.Key.Down?1:-1),0,results.Items.Count-1);results.ScrollIntoView(results.SelectedItem);}e.Handled=true;}else if(e.Key==System.Windows.Input.Key.Enter&&popup.IsOpen){if(results.SelectedIndex<0&&results.Items.Count>0)results.SelectedIndex=0;Choose();e.Handled=true;}}
 public new bool Focus()=>input.Focus();
}
