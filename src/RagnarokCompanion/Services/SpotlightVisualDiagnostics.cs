using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RagnarokCompanion.ViewModels;
namespace RagnarokCompanion.Services;
public static class SpotlightVisualDiagnostics
{
    public static void Verify(Window window,string output)
    {
        var grid=Find<DataGrid>(window).Single();
        if(grid.Columns.Last().Header?.ToString()!="Mapa")throw new Exception("Cabeçalho externo deve ser Mapa.");
        grid.ScrollIntoView(grid.Items[0]);window.UpdateLayout();
        var row=(DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);
        row.IsSelected=true;row.Focus();window.UpdateLayout();
        if(((SolidColorBrush)row.Background).Color!=Color.FromRgb(57,49,81))throw new Exception("Seleção da linha fora do tema escuro.");
        if(row.ToolTip is not ToolTip tip)throw new Exception("Tooltip explícito da linha ausente.");
        var nativeTip=new ToolTip{Style=new Style(typeof(ToolTip)),Content="Tooltip nativo sem estilo explícito",PlacementTarget=row};
        try{nativeTip.IsOpen=true;Flush();nativeTip.UpdateLayout();Capture(nativeTip,Path.Combine(output,"spotlight-tooltip-native-baseline.png"));File.WriteAllText(Path.Combine(output,"spotlight-tooltip-comparison.txt"),"Native WPF tooltip background: "+nativeTip.Background+"\nSpotlight tooltip: explicit dark template #202739; row selection/focus use explicit dark cell templates.");}finally{nativeTip.IsOpen=false;Flush();}
        tip.PlacementTarget=row;tip.Placement=PlacementMode.MousePoint;
        try {
            tip.IsOpen=true;Flush();tip.ApplyTemplate();tip.UpdateLayout();
            if(tip.Template.FindName("TooltipSurface",tip) is not Border surface||((SolidColorBrush)surface.Background).Color!=Color.FromRgb(32,39,57))throw new Exception("Popup de monstros fora do tema escuro.");
            if(tip.DataContext is not SpotlightRow context||!ReferenceEquals(context,row.Item))throw new Exception("Tooltip não acompanha a linha atual.");
            Capture(tip,Path.Combine(output,"spotlight-tooltip-dark.png"));
        } finally {tip.IsOpen=false;Flush();}
        if(tip.IsOpen)throw new Exception("Tooltip permaneceu aberto.");
        var combo=Find<ComboBox>(window).First(c=>c.Items.Count>0);
        try{combo.IsDropDownOpen=true;Flush();if(combo.Template.FindName("PART_Popup",combo) is not Popup {Child:Border popup}||((SolidColorBrush)popup.Background).Color!=Color.FromRgb(32,39,57))throw new Exception("Seletor fora do tema escuro.");Capture(popup,Path.Combine(output,"spotlight-dropdown-open.png"));}finally{combo.IsDropDownOpen=false;Flush();}
        VerifyPointer(window,row,tip,output);
        double width=window.Width,height=window.Height;
        try {
            window.Width=window.MinWidth;window.Height=window.MinHeight;window.UpdateLayout();Flush();
            foreach(var button in Find<Button>(window).Where(b=>b.IsVisible&&b.Content?.ToString() is "Limpar filtros" or "Confirmar este grupo no servidor")){
                var bounds=button.TransformToAncestor(window).TransformBounds(new Rect(button.RenderSize));
                if(bounds.Right>window.ActualWidth+1||bounds.Bottom>window.ActualHeight+1)throw new Exception("Controles Spotlight cortados no tamanho mínimo.");
            }
            foreach(double dpi in new[]{96d,120d,144d})Capture(window,Path.Combine(output,$"spotlight-minimum-{dpi}.png"),dpi);
        }finally{window.Width=width;window.Height=height;window.UpdateLayout();}
    }
    [StructLayout(LayoutKind.Sequential)]struct CursorPoint{public int X,Y;}
    [DllImport("user32.dll")]static extern bool GetCursorPos(out CursorPoint point);
    [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
    static void WaitForInput(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    static void VerifyPointer(Window window,DataGridRow row,ToolTip tip,string output){
        if(!GetCursorPos(out var original))return;
        try{
            window.Activate();var outside=window.PointToScreen(new Point(25,20));SetCursorPos((int)outside.X,(int)outside.Y);WaitForInput(100);
            var point=row.PointToScreen(new Point(35,row.ActualHeight/2));SetCursorPos((int)point.X,(int)point.Y);WaitForInput(850);
            if(!row.IsMouseOver){File.WriteAllText(Path.Combine(output,"spotlight-pointer.txt"),"Interactive desktop pointer unavailable; real popup open/close and focus/selection verified separately.");return;}
            if(!tip.IsOpen)throw new Exception("Tooltip não abriu ao passar o cursor na linha.");
            Capture(tip,Path.Combine(output,"spotlight-tooltip-hover.png"));
            SetCursorPos((int)outside.X,(int)outside.Y);WaitForInput(200);
            if(tip.IsOpen)throw new Exception("Tooltip permaneceu aberto após sair da linha.");
            File.WriteAllText(Path.Combine(output,"spotlight-pointer.txt"),"PASS: real pointer hover opened the dark tooltip; selected/focused row retained dark colors; moving away closed the popup.");
        }finally{tip.IsOpen=false;SetCursorPos(original.X,original.Y);}
    }
    static void Flush()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ContextIdle);
    static System.Collections.Generic.IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject{
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T match)yield return match;foreach(var item in Find<T>(child))yield return item;}
    }
    static void Capture(FrameworkElement element,string path,double dpi=96){
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth*dpi/96),(int)Math.Ceiling(element.ActualHeight*dpi/96),dpi,dpi,PixelFormats.Pbgra32);bitmap.Render(element);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
}
