using System;
using System.Net.Http;
using System.Threading.Tasks;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public static class DivinePrideDiagnostics {
 public static async Task<string> CheckAsync(){var store=new PersistenceService();var keys=new CredentialStorageService(store.Root);if(!keys.HasKey)return "SKIPPED: configure uma chave Divine Pride nas Configurações.";try{var settings=store.Load<IntegrationSettings>("divine-pride.json");using var api=new DivinePrideApiClient(new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}),keys,store,()=>settings.Language);await api.MonsterAsync(settings.Gateway,1832,true);return "PASS: consulta Divine Pride por ID confirmada, sem expor credenciais.";}catch{return "FAIL: consulta Divine Pride indisponível. Dados locais preservados.";}}
}