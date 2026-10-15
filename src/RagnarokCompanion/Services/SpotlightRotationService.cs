using System;
namespace RagnarokCompanion.Services;
public static class SpotlightRotationService
{
    public static TimeZoneInfo ServerZone { get; } = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
    public static DateTimeOffset Next(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, ServerZone);
        var next = DateTime.SpecifyKind(local.Date.AddHours(20), DateTimeKind.Unspecified);
        if (local.DateTime >= next) next = next.AddDays(1);
        return new DateTimeOffset(next, ServerZone.GetUtcOffset(next)).ToUniversalTime();
    }
    public static bool IsPending(DateTimeOffset? confirmed, DateTimeOffset now) => confirmed == null || confirmed > now || now >= Next(confirmed.Value);
    public static string Countdown(DateTimeOffset now) { var t = Next(now) - now; return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}"; }
}
