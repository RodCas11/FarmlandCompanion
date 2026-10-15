using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public static class RagnaPlaceDiagnostics
{
    public static async Task<string> CheckAsync()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RagnarokCompanion");
        var credentials = new CredentialStorageService(root);
        if (!credentials.HasKey) return "SKIPPED: Nenhuma API Key armazenada. Configure e teste a conexão nas Configurações.";
        try
        {
            string path = Path.Combine(root, "ragnaplace.json");
            var settings = File.Exists(path) ? JsonSerializer.Deserialize<IntegrationSettings>(File.ReadAllText(path)) : null;
            if (settings == null || !RagnaPlaceApiClient.ValidGateway(settings.Gateway)) return "SKIPPED: Nenhum gateway selecionado nas Configurações.";
            using var api = new RagnaPlaceApiClient(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }), credentials, new LocalCacheService());
            var gateways = await api.GatewaysAsync();
            if (!gateways.Any(g => g.Slug == settings.Gateway)) return "FAIL: O gateway salvo não consta entre os Renewal ativos.";
            await new RagnaPlaceAuthService(api).TestAsync(settings.Gateway);
            var details = await new RagnaPlaceCatalogProvider(api).DetailsAsync(settings.Gateway, 1159);
            if (details.Count == 0) return "FAIL: A consulta de detalhes não retornou dados para verificação.";
            return "PASS: Gateway Renewal, autenticação, pesquisa e detalhes de MVP RagnaPlace confirmados. Três consultas limitadas; chave não exibida; timers e preferências não alterados.";
        }
        catch (RagnaPlaceException ex) { return "FAIL: " + ex.Message; }
        catch { return "FAIL: Não foi possível validar a integração. Nenhuma credencial foi exibida ou alterada."; }
    }
}
