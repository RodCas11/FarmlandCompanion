using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
namespace RagnarokCompanion.Services;
public static class AppearanceService
{
    public static readonly string[] Choices={"Tema Farmland","Tema Halloween","Automático"};
    static bool? applied;
    public static bool InSeason(DateTimeOffset now) { var date=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,SpotlightRotationService.ServerZone).DateTime);return date>=new DateOnly(2026,10,26)&&date<=new DateOnly(2026,11,2); }
    public static bool IsHalloween(string theme,DateTimeOffset now)=>theme=="Tema Halloween"||theme=="Automático"&&InSeason(now);
    public static void Apply(string theme,DateTimeOffset now)
    {
        if(Application.Current==null)return;bool halloween=IsHalloween(theme,now);if(applied==halloween)return;applied=halloween;
        var colors=new Dictionary<string,string>{{"0D111C","100D19"},{"141928","1C1329"},{"181D2C","241A32"},{"202739","30203E"},{"2F2B22","66351F"},{"30291B","3F233B"},{"756039","B65D2C"},{"E9C77A","FFB46B"},{"C29A4A","F09545"},{"665332","9A5FC7"},{"40351F","57304F"}};
        foreach(var pair in colors)Application.Current.Resources["Theme"+pair.Key]=new SolidColorBrush((Color)ColorConverter.ConvertFromString("#"+(halloween?pair.Value:pair.Key)));
        Application.Current.Resources["Panel"]=Application.Current.Resources["Theme181D2C"];
    }
}