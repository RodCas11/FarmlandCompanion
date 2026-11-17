using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace RagnarokCompanion.Services;
public sealed class FirebaseWebConfig
{
 public string ApiKey {get;set;}="";public string AuthDomain {get;set;}="";public string ProjectId {get;set;}="";public string AppId {get;set;}="";
 public bool Configured=>ProjectId=="farmland-companion"&&ApiKey.Length>20&&AuthDomain==ProjectId+".firebaseapp.com";
 public static FirebaseWebConfig Load(string root){foreach(var dir in new[]{root,AppContext.BaseDirectory,Environment.CurrentDirectory})foreach(var name in new[]{"firebase-config.json","firebase.config.json"}){var path=Path.Combine(dir,name);if(File.Exists(path)){try{var c=JsonSerializer.Deserialize<FirebaseWebConfig>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});if(c?.Configured==true)return c;}catch(JsonException){}}}using var stream=typeof(FirebaseWebConfig).Assembly.GetManifestResourceStream("RagnarokCompanion.FirebaseConfig.json");return stream==null?new():JsonSerializer.Deserialize<FirebaseWebConfig>(stream,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??new();}
}
public sealed class FirebaseSession:IDisposable
{
 readonly FirebaseWebConfig config;readonly string path;readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(20)};readonly SemaphoreSlim mutex=new(1,1);string? idToken,refreshToken;DateTimeOffset expires;public string Name {get;private set;}="";
 public string UserId {get;private set;}="";public event Action? StateChanged;public bool SignedIn=>refreshToken!=null;public FirebaseWebConfig Config=>config;
 static readonly byte[] entropy=Encoding.UTF8.GetBytes("Farmland/Firebase/session/v1");
 public FirebaseSession(FirebaseWebConfig config,string root){this.config=config;path=Path.Combine(root,"firebase-session.dpapi");try{if(File.Exists(path)){var clear=ProtectedData.Unprotect(File.ReadAllBytes(path),entropy,DataProtectionScope.CurrentUser);try{using var json=JsonDocument.Parse(clear);refreshToken=json.RootElement.GetProperty("refreshToken").GetString();Name=json.RootElement.GetProperty("name").GetString()??"Google";UserId=json.RootElement.GetProperty("uid").GetString()??"";}finally{CryptographicOperations.ZeroMemory(clear);}}}catch{SignOut();}}
 void Save(){byte[] clear=JsonSerializer.SerializeToUtf8Bytes(new{refreshToken,name=Name,uid=UserId});try{File.WriteAllBytes(path+".tmp",ProtectedData.Protect(clear,entropy,DataProtectionScope.CurrentUser));File.Move(path+".tmp",path,true);}finally{CryptographicOperations.ZeroMemory(clear);}}
 public void SignOut(){idToken=refreshToken=null;Name=UserId="";File.Delete(path);File.Delete(path+".tmp");StateChanged?.Invoke();}
 public async Task<string?> Token(CancellationToken ct){await mutex.WaitAsync(ct);try{if(refreshToken==null)return null;if(idToken!=null&&DateTimeOffset.UtcNow<expires)return idToken;using var response=await http.PostAsync("https://securetoken.googleapis.com/v1/token?key="+Uri.EscapeDataString(config.ApiKey),new FormUrlEncodedContent(new[]{new System.Collections.Generic.KeyValuePair<string,string>("grant_type","refresh_token"),new("refresh_token",refreshToken)}),ct);if(response.StatusCode==HttpStatusCode.BadRequest){SignOut();throw new InvalidOperationException("Sessão expirada. Entre novamente com Google.");}response.EnsureSuccessStatusCode();using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));UserId=json.RootElement.GetProperty("user_id").GetString()??"";idToken=json.RootElement.GetProperty("id_token").GetString();refreshToken=json.RootElement.GetProperty("refresh_token").GetString();expires=DateTimeOffset.UtcNow.AddMinutes(55);Save();return idToken;}finally{mutex.Release();}}
 public async Task Login(CancellationToken ct)
 {
  if(!config.Configured)throw new InvalidOperationException("Salve firebase-config.json com a configuração Web.");
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(3));ct=timeout.Token;
  var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();int port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();
  using var listener=new HttpListener();listener.Prefixes.Add($"http://127.0.0.1:{port}/");listener.Start();
  string state=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));string origin="https://"+config.ProjectId+".web.app";
  Process.Start(new ProcessStartInfo(origin+"/login.html#port="+port+"&state="+state){UseShellExecute=true});
  while(true){var context=await listener.GetContextAsync().WaitAsync(ct);var request=context.Request;var response=context.Response;
   bool valid=request.Headers["Origin"]==origin&&request.Url?.AbsolutePath=="/callback";
   if(!valid){response.StatusCode=403;response.Close();continue;}
   response.Headers["Access-Control-Allow-Origin"]=origin;response.Headers["Vary"]="Origin";
   if(request.HttpMethod=="OPTIONS"){response.Headers["Access-Control-Allow-Methods"]="POST";response.Headers["Access-Control-Allow-Headers"]="Content-Type";response.Headers["Access-Control-Allow-Private-Network"]="true";response.StatusCode=204;response.Close();continue;}
   if(request.HttpMethod!="POST"||request.ContentLength64 is <1 or >20000){response.StatusCode=400;response.Close();continue;}
   using var reader=new StreamReader(request.InputStream);string body=await reader.ReadToEndAsync(ct);if(body.Length>20000){response.StatusCode=400;response.Close();continue;}
   using var data=JsonDocument.Parse(body);var d=data.RootElement;if(d.GetProperty("state").GetString()!=state){response.StatusCode=403;response.Close();continue;}
   string candidate=d.GetProperty("idToken").GetString()??"";
   // Verify browser credential against Firebase; never trust callback identity fields.
   using var check=await http.PostAsJsonAsync("https://identitytoolkit.googleapis.com/v1/accounts:lookup?key="+Uri.EscapeDataString(config.ApiKey),new{idToken=candidate},ct);check.EnsureSuccessStatusCode();using var account=JsonDocument.Parse(await check.Content.ReadAsStringAsync(ct));var user=account.RootElement.GetProperty("users")[0];
   UserId=user.GetProperty("localId").GetString()??"";if(!user.GetProperty("providerUserInfo").EnumerateArray().Any(p=>p.GetProperty("providerId").GetString()=="google.com"))throw new InvalidOperationException("Use uma conta Google.");Name=user.TryGetProperty("displayName",out var name)?name.GetString()??"Google":"Google";idToken=candidate;refreshToken=d.GetProperty("refreshToken").GetString();expires=DateTimeOffset.UtcNow.AddMinutes(50);Save();StateChanged?.Invoke();response.StatusCode=204;response.Close();return;
  }
 }
 public void Dispose(){http.Dispose();mutex.Dispose();}
}
