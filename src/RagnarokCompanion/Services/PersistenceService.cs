using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RagnarokCompanion.Models;

namespace RagnarokCompanion.Services;

public sealed class PersistenceService
{
    public string Root { get; }
    public bool IsDefaultLocation { get; }
    public List<string> Warnings { get; } = new();
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public PersistenceService(string? root = null)
    {
        IsDefaultLocation = root == null;
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RagnarokCompanion");
        Directory.CreateDirectory(Root);
    }
    public T Load<T>(string name) where T : new()
    {
        string path = Path.Combine(Root, name);
        if (!File.Exists(path)) return new T();
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? throw new JsonException("JSON vazio"); }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Warnings.Add($"{name}: arquivo inválido; recuperação aplicada.");
            try { File.Copy(path, path + ".invalid-" + DateTime.UtcNow.Ticks, true); } catch (IOException) { }
            try
            {
                var recovered = JsonSerializer.Deserialize<T>(File.ReadAllText(path + ".bak")) ?? new T();
                File.Copy(path + ".bak", path, true);
                return recovered;
            }
            catch { return new T(); }
        }
    }
    public void Save<T>(string name, T value)
    {
        string path = Path.Combine(Root, name), temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        { JsonSerializer.Serialize(stream, value, Json); stream.Flush(true); }
        if (File.Exists(path)) File.Replace(temp, path, path + ".bak", true);
        else File.Move(temp, path);
    }
    public static void Validate(Settings s)
    {
        if (!double.IsFinite(s.MiningMinutes) || s.MiningMinutes <= 0 || s.MiningMinutes > 10080) s.MiningMinutes = 30;
        if (!double.IsFinite(s.CooldownMinutes) || s.CooldownMinutes <= 0 || s.CooldownMinutes > 10080) s.CooldownMinutes = 60;
        s.Opacity = double.IsFinite(s.Opacity) ? Math.Clamp(s.Opacity, .5, 1) : .95;
        s.FontSize = double.IsFinite(s.FontSize) ? Math.Clamp(s.FontSize, 11, 24) : 14;
        if (s.CompactCount is not (1 or 3 or 5)) s.CompactCount = 3;
        if (!Enum.IsDefined(s.CompactTab)) s.CompactTab = CompactTab.Mining;
        s.CompactFavoriteKeys = (s.CompactFavoriteKeys ?? new()).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().Take(5).ToList();
        if (s.CompactMode is not ("Ambos" or "Mineração" or "MVPs")) s.CompactMode = "Ambos";
        s.AdvanceMinutes ??= "10,5,1";
    }
    public static bool Valid(Mvp m) => m != null && !string.IsNullOrWhiteSpace(m.Name) && !string.IsNullOrWhiteSpace(m.Map) && double.IsFinite(m.MinMinutes) && double.IsFinite(m.MaxMinutes) && m.MinMinutes > 0 && m.MaxMinutes >= m.MinMinutes && m.MaxMinutes <= 10080 && m.Alerts != null && (m.DeathUtc == null || m.DeathUtc <= DateTimeOffset.UtcNow);
}
