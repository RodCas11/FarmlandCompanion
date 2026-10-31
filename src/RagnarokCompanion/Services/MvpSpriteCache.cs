using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace RagnarokCompanion.Services;
public static class MvpSpriteCache {
 static readonly HttpClient http=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(10)};
 static readonly SemaphoreSlim gate=new(2);static readonly ConcurrentDictionary<int,Task<ImageSource?>> requests=new();
 public static Task<ImageSource?> GetAsync(int id,bool online)=>requests.GetOrAdd(id,_=>LoadAsync(id,online));
 static async Task<ImageSource?> LoadAsync(int id,bool online){
  if(id<=0)return null;var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FarmlandCompanion","Cache","mvp-sprites",id+".png");
  try{if(File.Exists(path))return Decode(await File.ReadAllBytesAsync(path));}catch{}
  if(!online)return null;await gate.WaitAsync();
  try{using var response=await http.GetAsync("https://static.divine-pride.net/images/mobs/png/"+id+".png",HttpCompletionOption.ResponseHeadersRead);if(!response.IsSuccessStatusCode||response.Content.Headers.ContentLength>2_000_000)return null;
   using var source=await response.Content.ReadAsStreamAsync();using var buffer=new MemoryStream();var chunk=new byte[8192];int count;while((count=await source.ReadAsync(chunk))>0){if(buffer.Length+count>2_000_000)return null;buffer.Write(chunk,0,count);}var bytes=buffer.ToArray();var image=Decode(bytes);Directory.CreateDirectory(Path.GetDirectoryName(path)!);var temporary=path+".tmp";await File.WriteAllBytesAsync(temporary,bytes);File.Move(temporary,path,true);return image;
  }catch{return null;}finally{gate.Release();}
 }
 static ImageSource Decode(byte[] bytes){if(Convert.ToHexString(SHA256.HashData(bytes))=="90FD5DFC46354798FA8FC4CBCEC9DEE97D8071B75ABC58951AB1887B11CD916E")throw new InvalidDataException("Sprite não disponível na fonte pública.");using var stream=new MemoryStream(bytes);var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.DecodePixelWidth=160;image.EndInit();image.Freeze();return image;}
}
