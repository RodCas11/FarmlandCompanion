using System;
namespace RagnarokCompanion.Services;
public static class SpotlightRotationService
{
    public static readonly string[] OfficialOrder = { "solar", "lunar", "estelar", "radiante", "celestial", "infernal", "astral" };
    public static TimeZoneInfo ServerZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
    public static DateTimeOffset Next(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, ServerZone);
        var next = DateTime.SpecifyKind(local.Date.AddHours(20), DateTimeKind.Unspecified);
        if (local.DateTime >= next) next = next.AddDays(1);
        return new DateTimeOffset(next, ServerZone.GetUtcOffset(next)).ToUniversalTime();
    }
    public static bool IsPending(DateTimeOffset? confirmed, DateTimeOffset now) => confirmed == null || confirmed > now || now >= Next(confirmed.Value);
    public static int Steps(DateTimeOffset reference, DateTimeOffset now)
    {
        DateTime Day(DateTimeOffset time) { var local=TimeZoneInfo.ConvertTime(time,ServerZone);return local.DateTime.AddHours(-20).Date; }
        return Math.Max(0,(Day(now)-Day(reference)).Days);
    }
    public static string Countdown(DateTimeOffset now) { var t = Next(now) - now; return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}"; }
}
