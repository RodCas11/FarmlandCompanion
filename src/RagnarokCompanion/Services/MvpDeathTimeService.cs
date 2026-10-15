using System;
using System.Globalization;
namespace RagnarokCompanion.Services;
public static class MvpDeathTimeService
{
    public static bool TryResolve(string text, bool yesterday, DateTimeOffset now, TimeZoneInfo zone, out DateTimeOffset death, out string error)
    {
        death=default;error="Informe o horário em HH:mm.";
        if(!TimeOnly.TryParseExact(text.Trim(),"HH:mm",CultureInfo.InvariantCulture,DateTimeStyles.None,out var time))return false;
        var date=TimeZoneInfo.ConvertTime(now,zone).Date.AddDays(yesterday?-1:0);
        var wall=DateTime.SpecifyKind(date.Add(time.ToTimeSpan()),DateTimeKind.Unspecified);
        if(zone.IsInvalidTime(wall)||zone.IsAmbiguousTime(wall)){error="Esse horário não é válido ou é ambíguo no fuso local. Confira a data e o horário.";return false;}
        death=new DateTimeOffset(wall,zone.GetUtcOffset(wall)).ToUniversalTime();
        if(death>now){error="O horário está no futuro. Corrija-o ou marque Dia anterior se a morte ocorreu ontem.";return false;}
        error="";return true;
    }
}
