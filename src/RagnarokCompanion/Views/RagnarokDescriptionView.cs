using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.Views;
public sealed class RagnarokDescriptionView:ContentControl {
 public static readonly DependencyProperty RawTextProperty=DependencyProperty.Register(nameof(RawText),typeof(string),typeof(RagnarokDescriptionView),new PropertyMetadata("",(d,_)=>((RagnarokDescriptionView)d).Render()));
 public string RawText {get=>(string)GetValue(RawTextProperty);set=>SetValue(RawTextProperty,value);}
 void Render(){var panel=new StackPanel();foreach(var line in RagnarokDescriptionFormatter.Format(RawText)){
   if(line.Divider){panel.Children.Add(new Border{Height=1,Background=new SolidColorBrush(Color.FromRgb(48,56,75)),Margin=new Thickness(0,10,0,10)});continue;}
   if(line.Text.Length==0){panel.Children.Add(new Border{Height=6});continue;}
   var text=new TextBlock{TextWrapping=TextWrapping.Wrap,FontSize=13,LineHeight=21,Margin=new Thickness(0,line.Heading?10:2,0,line.Heading?5:2),FontWeight=line.Heading?FontWeights.SemiBold:FontWeights.Normal};
   foreach(var part in line.Parts){var run=new Run(part.Text);if(part.Color!=null){var color=(Color)ColorConverter.ConvertFromString(part.Color);double luminance=.2126*color.R+.7152*color.G+.0722*color.B;if(luminance<145){double mix=(145-luminance)/(255-luminance);color=Color.FromRgb((byte)(color.R+(255-color.R)*mix),(byte)(color.G+(255-color.G)*mix),(byte)(color.B+(255-color.B)*mix));}run.Foreground=new SolidColorBrush(color);}text.Inlines.Add(run);}
   panel.Children.Add(text);
  }Content=panel;}
}
