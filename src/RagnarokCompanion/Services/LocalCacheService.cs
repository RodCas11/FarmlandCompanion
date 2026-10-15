using System;
using System.Collections.Generic;

namespace RagnarokCompanion.Services;
public interface ILocalCacheService
{
    bool TryGet<T>(string key, out T? value) where T : class;
    void Set<T>(string key, T value, TimeSpan ttl) where T : class;
    void Clear();
}
// Session-only bounded cache: no raw API data is written to disk.
public sealed class LocalCacheService : ILocalCacheService
{
    private readonly Dictionary<string, (DateTimeOffset Until, object Value)> entries = new();
    public bool TryGet<T>(string key, out T? value) where T : class
    {
        lock (entries)
        {
            if (entries.TryGetValue(key, out var e) && e.Until > DateTimeOffset.UtcNow && e.Value is T found) { value = found; return true; }
            entries.Remove(key); value = null; return false;
        }
    }
    public void Set<T>(string key, T value, TimeSpan ttl) where T : class
    { lock (entries) { if (entries.Count >= 100) entries.Clear(); entries[key] = (DateTimeOffset.UtcNow + ttl, value); } }
    public void Clear() { lock (entries) entries.Clear(); }
}
