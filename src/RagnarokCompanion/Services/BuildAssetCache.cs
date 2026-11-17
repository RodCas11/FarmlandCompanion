using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RagnarokCompanion.Models;
namespace RagnarokCompanion.Services;
public static class BuildAssetCache
{
    static readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(10)};static readonly SemaphoreSlim gate=new(2);static readonly ConcurrentDictionary<string,Task<ImageSource?>> requests=new();
    public static Task<ImageSource?> Item(int id,bool online)=>id<=0?Task.FromResult<ImageSource?>(null):Get("item-"+id,new Uri("https://static.divine-pride.net/images/items/item/"+id+".png"),online);
    public static Uri ClassUrl(BuildClass job){var hash=Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(job.Sprite))).ToLowerInvariant();return new("https://irowiki.org/w/images/"+hash[0]+"/"+hash[..2]+"/"+Uri.EscapeDataString(job.Sprite));}
    public static Task<ImageSource?> Class(BuildClass job,bool online)=>Get("class-"+job.Id,ClassUrl(job),online);
    static Task<ImageSource?> Get(string key,Uri uri,bool online)=>requests.GetOrAdd(key,_=>Load(key,uri,online));
    static ImageSource Decode(byte[] data,string key){using var input=new MemoryStream(data);var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=key.StartsWith("item-")?48:160;image.StreamSource=input;image.EndInit();image.Freeze();return image;}
    static async Task<ImageSource?> Load(string key,Uri uri,bool online)
    {
        string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FarmlandCompanion","Cache","build-assets",key+".png");
        try{if(File.Exists(path))return Decode(await File.ReadAllBytesAsync(path),key);}catch{}
        if(!online)return null;await gate.WaitAsync();try{using var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead);response.EnsureSuccessStatusCode();if(response.Content.Headers.ContentLength>1_000_000)return null;using var source=await response.Content.ReadAsStreamAsync();using var buffer=new MemoryStream();byte[] chunk=new byte[8192];int n;while((n=await source.ReadAsync(chunk))>0){if(buffer.Length+n>1_000_000)return null;buffer.Write(chunk,0,n);}var bytes=buffer.ToArray();var image=Decode(bytes,key);Directory.CreateDirectory(Path.GetDirectoryName(path)!);await File.WriteAllBytesAsync(path+".tmp",bytes);File.Move(path+".tmp",path,true);return image;}catch{return null;}finally{gate.Release();}
    }
}
