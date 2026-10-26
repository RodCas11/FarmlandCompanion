using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FarmlandCompanion.Updates;
namespace RagnarokCompanion.Services;

public sealed record AvailableRelease(Version Version, Uri Zip, Uri Checksum);
public sealed class GitHubUpdateService
{
    const string Repository="https://github.com/RodCas11/FarmlandCompanion/releases/download/";
    public const string LatestEndpoint="https://api.github.com/repos/RodCas11/FarmlandCompanion/releases/latest";
    public static Version InstalledVersion => typeof(GitHubUpdateService).Assembly.GetName().Version ?? new(0,0,0);
    public static Version? StableVersion(string tag) => System.Text.RegularExpressions.Regex.IsMatch(tag,@"^v?\d+\.\d+\.\d+$")&&Version.TryParse(tag.TrimStart('v'),out var version)?new Version(version.Major,version.Minor,version.Build,0):null;
    public static AvailableRelease? Parse(string json)
    {
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
        if(root.GetProperty("draft").GetBoolean()||root.GetProperty("prerelease").GetBoolean())return null;
        string tag=root.GetProperty("tag_name").GetString()??"";var version=StableVersion(tag);if(version==null)return null;
        Uri Asset(string name)
        {
            var asset=root.GetProperty("assets").EnumerateArray().Single(a=>a.GetProperty("name").GetString()==name);
            string expected=Repository+Uri.EscapeDataString(tag)+"/"+name;
            var uri=new Uri(asset.GetProperty("browser_download_url").GetString()!);
            if(uri.AbsoluteUri!=expected)throw new InvalidDataException("Origem da release não autorizada.");return uri;
        }
        return new(version,Asset("FarmlandCompanion-win-x64.zip"),Asset("FarmlandCompanion-win-x64.sha256"));
    }
    static bool Allowed(Uri uri) => uri.Scheme=="https"&&uri.IsDefaultPort&&string.IsNullOrEmpty(uri.UserInfo)&&
        (uri.Host=="api.github.com"&&uri.AbsoluteUri==LatestEndpoint || uri.Host=="github.com"&&uri.AbsoluteUri.StartsWith(Repository,StringComparison.Ordinal) || uri.Host is "release-assets.githubusercontent.com" or "objects.githubusercontent.com");
    static async Task Download(Uri uri,string output,long limit,IProgress<double>? progress,CancellationToken token)
    {
        using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(15)};
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FarmlandCompanion/"+InstalledVersion);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        for(int redirects=0;redirects<6;redirects++)
        {
            if(!Allowed(uri))throw new InvalidDataException("Download fora das releases oficiais.");
            using var response=await client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);
            if((int)response.StatusCode is >=300 and <400){uri=response.Headers.Location is {} next?new Uri(uri,next):throw new IOException("Redirecionamento inválido.");continue;}
            response.EnsureSuccessStatusCode();long? length=response.Content.Headers.ContentLength;if(length>limit)throw new InvalidDataException("Download acima do limite.");
            using var input=await response.Content.ReadAsStreamAsync(token);using var file=File.Create(output);byte[] buffer=new byte[81920];long total=0;int read;
            while((read=await input.ReadAsync(buffer,token))>0){total+=read;if(total>limit)throw new InvalidDataException("Download acima do limite.");await file.WriteAsync(buffer.AsMemory(0,read),token);if(length>0)progress?.Report(total*100.0/length.Value);}
            return;
        }
        throw new IOException("Redirecionamentos excessivos.");
    }
    public async Task<AvailableRelease?> Check(CancellationToken token)
    {
        string file=Path.GetTempFileName();try{await Download(new(LatestEndpoint),file,1024*1024,null,token);return Parse(await File.ReadAllTextAsync(file,token));}finally{File.Delete(file);}
    }
    public async Task<string> Prepare(AvailableRelease release,IProgress<double> progress,CancellationToken token)
    {
        string work=Path.Combine(Path.GetTempPath(),"FarmlandCompanion-Update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
        await Download(release.Checksum,Path.Combine(work,"release.sha256"),4096,null,token);
        await Download(release.Zip,Path.Combine(work,"release.zip"),512L*1024*1024,progress,token);
        await Task.Run(()=>{UpdatePackage.VerifyHash(Path.Combine(work,"release.zip"),File.ReadAllText(Path.Combine(work,"release.sha256")));UpdatePackage.Extract(Path.Combine(work,"release.zip"),Path.Combine(work,"validated"));},token);
        // Run the installed, trusted helper; never launch code or scripts directly from the ZIP.
        File.Copy(Path.Combine(AppContext.BaseDirectory,"FarmlandUpdater.exe"),Path.Combine(work,"FarmlandUpdater.exe"));return work;
    }
}
