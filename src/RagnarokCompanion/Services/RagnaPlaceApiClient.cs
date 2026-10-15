using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RagnarokCompanion.Models;
using RagnarokCompanion.Models.Api;

namespace RagnarokCompanion.Services;
public sealed class RagnaPlaceException : Exception
{
    public HttpStatusCode? Status { get; }
    public RagnaPlaceException(string safeMessage, HttpStatusCode? status = null) : base(safeMessage) => Status = status;
}
public interface IRagnaPlaceApiClient
{
    Task<List<Gateway>> GatewaysAsync(CancellationToken ct = default);
    Task<SearchResultDto> SearchAsync(string gateway, string name, int page = 1, bool refresh = false, CancellationToken ct = default);
    Task<MobDto> MonsterAsync(string gateway, int id, bool refresh = false, CancellationToken ct = default);
    void ClearCache();
}
public sealed class RagnaPlaceApiClient : IRagnaPlaceApiClient, IDisposable
{
    private readonly HttpClient http;
    private readonly ICredentialStorageService credentials;
    private readonly ILocalCacheService cache;
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset blockedUntil, nextRequest;
    public DateTimeOffset RetryAllowedUtc => blockedUntil;
    private long generation;
    private readonly TimeSpan spacing;
    public RagnaPlaceApiClient(HttpClient http, ICredentialStorageService credentials, ILocalCacheService cache, TimeSpan? spacing = null)
    { this.http = http; this.credentials = credentials; this.cache = cache; this.spacing = spacing ?? TimeSpan.FromSeconds(1); }
    public void ClearCache() { Interlocked.Increment(ref generation); cache.Clear(); }
    public async Task<List<Gateway>> GatewaysAsync(CancellationToken ct = default)
    {
        var result = await Get<GatewayResponse>("/v1/gateways", false, false, ct);
        if (result.Gateways == null || result.Gateways.Any(g => g == null || !ValidGateway(g.Slug) || g.Groups == null)) { ClearCache(); throw Invalid(); }
        return result.Gateways.Where(g => g.Groups.Contains("renewal") && !g.Groups.Contains("dead")).ToList();
    }
    public Task<SearchResultDto> SearchAsync(string gateway, string name, int page = 1, bool refresh = false, CancellationToken ct = default)
    {
        if (!ValidGateway(gateway) || string.IsNullOrWhiteSpace(name) || name.Length > 100 || page is < 1 or > 20) throw new RagnaPlaceException("Selecione um gateway Renewal, informe 1 a 100 caracteres e uma página de 1 a 20.");
        return SearchValidated(gateway, name, page, refresh, ct);
    }
    private async Task<SearchResultDto> SearchValidated(string gateway, string name, int page, bool refresh, CancellationToken ct)
    {
        var data = await Get<SearchResultDto>($"/v1/{gateway}/search?type=mob&q={Uri.EscapeDataString(name.Trim())}&page={page}&limit=10", true, refresh, ct);
        if (data.Type != "mob" || data.Items == null || data.Items.Count > 10 || data.Items.Any(m => m == null || m.Id is null or <= 0 || string.IsNullOrWhiteSpace(m.Name))) { ClearCache(); throw Invalid(); }
        return data;
    }
    public async Task<MobDto> MonsterAsync(string gateway, int id, bool refresh = false, CancellationToken ct = default)
    {
        if (!ValidGateway(gateway) || id <= 0) throw Invalid();
        var data = await Get<MobDto>($"/v1/{gateway}/mob/{id}", true, refresh, ct);
        if (data.Id != id || string.IsNullOrWhiteSpace(data.Name) || data.Spawns?.Any(s => s == null) == true) { ClearCache(); throw Invalid(); }
        return data;
    }
    public static bool ValidGateway(string? g) => g != null && Regex.IsMatch(g, "^[a-z0-9-]{1,32}$");
    private static RagnaPlaceException Invalid() => new("O RagnaPlace retornou dados em formato inesperado. Seus timers foram preservados.");
    private async Task<T> Get<T>(string path, bool auth, bool refresh, CancellationToken ct) where T : class
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(15));
        ct = timeout.Token;
        long version = Interlocked.Read(ref generation);
        string cacheKey = version + path;
        try
        {
            // Serialization and rechecking cache coalesce concurrent equal queries.
            await gate.WaitAsync(ct);
            try
            {
                if (!refresh && cache.TryGet<T>(cacheKey, out var hit)) return hit!;
                var now = DateTimeOffset.UtcNow;
                if (blockedUntil > now) throw new RagnaPlaceException($"Limite de consultas atingido. Tente novamente em {Math.Ceiling((blockedUntil - now).TotalSeconds)} segundos.", HttpStatusCode.TooManyRequests);
                if (nextRequest > now) await Task.Delay(nextRequest - now, ct);
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.ragnaplace.com" + path));
                    request.Headers.Accept.ParseAdd("application/json");
                    if (auth)
                    {
                        string? key;
                        try { key = credentials.Read(); } catch { throw new RagnaPlaceException("Não foi possível desbloquear a chave deste usuário do Windows. Salve-a novamente nas Configurações."); }
                        if (string.IsNullOrEmpty(key)) throw new RagnaPlaceException("Informe a API Key nas Configurações.", HttpStatusCode.Unauthorized);
                        request.Headers.Add("x-api-key", key);
                    }
                    nextRequest = DateTimeOffset.UtcNow + spacing;
                    using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                    UpdateRateLimit(response);
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    { if (blockedUntil <= DateTimeOffset.UtcNow) blockedUntil = DateTimeOffset.UtcNow.AddSeconds(60); throw new RagnaPlaceException("Limite da API atingido. Aguarde antes de consultar novamente.", response.StatusCode); }
                    if (response.StatusCode == HttpStatusCode.Unauthorized) throw new RagnaPlaceException("Chave inválida ou ausente (HTTP 401). Confira a chave nas Configurações.", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.Forbidden) throw new RagnaPlaceException("Acesso não autorizado (HTTP 403). A chave pode estar pendente de aprovação ou revogada.", response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.NotFound) throw new RagnaPlaceException("Monstro ou gateway não encontrado (HTTP 404). Confira o ID e o gateway nas Configurações.", response.StatusCode);
                    if (attempt == 0 && response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
                    { if (blockedUntil > DateTimeOffset.UtcNow) throw new RagnaPlaceException("O servidor pediu uma pausa. Aguarde antes de consultar novamente.", response.StatusCode); await Task.Delay(TimeSpan.FromSeconds(1.5), ct); continue; }
                    if (!response.IsSuccessStatusCode) throw new RagnaPlaceException($"RagnaPlace indisponível para esta consulta (HTTP {(int)response.StatusCode}). Os timers continuam locais.", response.StatusCode);
                    if (response.Content.Headers.ContentLength > 2_000_000) throw Invalid();
                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    using var buffer = new MemoryStream(); byte[] block = new byte[8192]; int read;
                    while ((read = await stream.ReadAsync(block, ct)) > 0)
                    { if (buffer.Length + read > 2_000_000) throw Invalid(); buffer.Write(block, 0, read); }
                    buffer.Position = 0;
                    var result = await JsonSerializer.DeserializeAsync<T>(buffer, cancellationToken: ct) ?? throw Invalid();
                    if (version != Interlocked.Read(ref generation)) throw new RagnaPlaceException("As credenciais ou o cache foram alterados durante a consulta. Consulte novamente.");
                    // Only successful deserialization is cached. DTO semantic validation occurs at the boundary above.
                    if (version == Interlocked.Read(ref generation)) cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
                    return result;
                }
                throw Invalid();
            }
            finally { gate.Release(); }
        }
        catch (OperationCanceledException) { throw new RagnaPlaceException("Consulta cancelada ou tempo de conexão excedido. Os timers continuam funcionando."); }
        catch (HttpRequestException) { throw new RagnaPlaceException("Sem conexão com o RagnaPlace. Seus dados locais e timers continuam disponíveis."); }
        catch (JsonException) { throw Invalid(); }
        catch (IOException) { throw new RagnaPlaceException("A conexão foi interrompida. Tente novamente quando estiver online."); }
    }
    private void UpdateRateLimit(HttpResponseMessage r)
    {
        var now = DateTimeOffset.UtcNow;
        TimeSpan? wait = r.Headers.RetryAfter?.Delta;
        if (r.Headers.RetryAfter?.Date is { } date) wait = date > now ? date - now : TimeSpan.Zero;
        if (r.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && int.TryParse(remaining.FirstOrDefault(), out int n) && n == 0 && r.Headers.TryGetValues("X-RateLimit-Reset", out var reset) && double.TryParse(reset.FirstOrDefault(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double seconds) && double.IsFinite(seconds) && seconds >= 0)
        {
            var resetDelay = TimeSpan.FromSeconds(Math.Min(seconds, (DateTimeOffset.MaxValue - now).TotalSeconds));
            if (wait == null || resetDelay > wait) wait = resetDelay;
        }
        if (wait is { } delay && delay > TimeSpan.Zero)
            blockedUntil = delay >= DateTimeOffset.MaxValue - now ? DateTimeOffset.MaxValue : now + delay;
    }
    public void Dispose() { http.Dispose(); gate.Dispose(); }
}
