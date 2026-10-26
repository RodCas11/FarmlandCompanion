using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
namespace RagnarokCompanion.Views;

// Small, cached vector ornaments: no bitmap downloads, timers or input interception.
public sealed class HalloweenOrnament : FrameworkElement
{
    public static readonly DependencyProperty MotifProperty=DependencyProperty.Register(nameof(Motif),typeof(string),typeof(HalloweenOrnament),new FrameworkPropertyMetadata("Pumpkin",FrameworkPropertyMetadataOptions.AffectsRender));
    public string Motif {get=>(string)GetValue(MotifProperty);set=>SetValue(MotifProperty,value);}
    static readonly Dictionary<string,Drawing> drawings=CreateDrawings();
    public HalloweenOrnament(){IsHitTestVisible=false;Focusable=false;SetResourceReference(VisibilityProperty,"HalloweenVisibility");}
    static Brush Brush(string color){var brush=(SolidColorBrush)new BrushConverter().ConvertFromString(color)!;brush.Freeze();return brush;}
    static Dictionary<string,Drawing> CreateDrawings()
    {
        var result=new Dictionary<string,Drawing>();
        var web=new DrawingGroup();using(var c=web.Open())
        {
            var pen=new Pen(Brush("#AC947E"),.85);
            c.DrawGeometry(null,pen,Geometry.Parse("M0,0 L100,0 L100,100 M100,0 L0,100 M100,0 L8,38 M100,0 L62,92 M18,0 Q25,34 42,58 Q66,76 100,82 M40,0 Q45,25 58,42 Q77,56 100,60 M62,0 Q65,15 73,27 Q86,36 100,38 M82,0 Q83,8 87,13 Q94,18 100,18"));
            c.DrawLine(pen,new Point(62,40),new Point(62,63));c.DrawEllipse(Brush("#AC947E"),null,new Point(62,66),2,3);
        }
        web.Freeze();result["Web"]=web;
        var pumpkin=new DrawingGroup();using(var c=pumpkin.Open())
        {
            c.DrawGeometry(Brush("#84934D"),null,Geometry.Parse("M47,31 Q39,16 54,10 L61,17 Q50,19 55,31 Z"));
            c.DrawEllipse(Brush("#D96720"),null,new Point(50,58),39,32);c.DrawEllipse(Brush("#F39432"),null,new Point(50,58),25,32);
            var rib=new Pen(Brush("#B45C22"),1.4);c.DrawGeometry(null,rib,Geometry.Parse("M43,28 C28,42 28,75 43,88 M57,28 C72,42 72,75 57,88"));
            c.DrawGeometry(Brush("#241719"),null,Geometry.Parse("M24,52 L40,43 L38,57 Z M60,43 L76,52 L62,57 Z M47,55 L53,55 L56,64 L44,64 Z M27,67 L39,72 L44,67 L50,75 L57,68 L62,72 L74,65 Q66,86 50,83 Q35,82 27,67 Z"));
        }
        pumpkin.Freeze();result["Pumpkin"]=pumpkin;
        var bat=new GeometryDrawing(Brush("#9877AF"),null,Geometry.Parse("M50,42 L44,32 L43,43 C32,37 19,26 3,25 C9,34 10,43 9,52 Q21,44 27,59 Q37,50 44,67 L50,74 L56,67 Q63,50 73,59 Q79,44 91,52 C90,43 91,34 97,25 C81,26 68,37 57,43 L56,32 Z"));bat.Freeze();result["Bat"]=bat;
        return result;
    }
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);if(!drawings.TryGetValue(Motif,out var drawing))return;
        double scale=Math.Min(ActualWidth,ActualHeight)/100;if(scale<=0)return;
        context.PushTransform(new TranslateTransform((ActualWidth-100*scale)/2,(ActualHeight-100*scale)/2));context.PushTransform(new ScaleTransform(scale,scale));context.DrawDrawing(drawing);context.Pop();context.Pop();
    }
}
