using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
namespace FarmlandCompanion.Updates;

public static class UpdatePackage
{
    public static readonly string[] Files={"FarmlandCompanion.exe","FarmlandUpdater.exe","README.md","Data/mvp-seed.json","Data/farmland-items.sqlite","Data/item-monster-rules.json","docs/RAGNAPLACE_API.md","docs/SPOTLIGHT.md","licenses/rathena-GPL-3.0.txt","licenses/THIRD-PARTY-NOTICES.md"};
    public static void VerifyHash(string zip,string checksum)
    {
        var parts=checksum.Trim().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length!=2 || parts[1]!="FarmlandCompanion-win-x64.zip" || parts[0].Length!=64 || !parts[0].All(Uri.IsHexDigit))throw new InvalidDataException("SHA256 publicado inválido.");
        using var stream=File.OpenRead(zip);
        if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(parts[0],StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SHA256 divergente. Atualização cancelada.");
    }
    public static string SafePath(string root,string relative)
    {
        if(string.IsNullOrWhiteSpace(relative)||relative.Contains(':')||relative.Contains('\\')||relative.Split('/').Any(p=>p is ".." or "." or ""))throw new InvalidDataException("Caminho inválido no pacote.");
        var full=Path.GetFullPath(Path.Combine(root,relative));var scope=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!full.StartsWith(scope,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Arquivo fora da instalação.");
        for(string? cursor=full;cursor!=null;cursor=Path.GetDirectoryName(cursor))
        { if((Directory.Exists(cursor)||File.Exists(cursor))&&(File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0)throw new IOException("Links/junções não são permitidos na atualização."); }
        return full;
    }
    public static void Extract(string zip,string destination)
    {
        Directory.CreateDirectory(destination);using var archive=ZipFile.OpenRead(zip);
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long size=0;
        foreach(var entry in archive.Entries)
        {
            if(!Files.Contains(entry.FullName,StringComparer.Ordinal)||!seen.Add(entry.FullName)||entry.Length<=0||entry.Length>512L*1024*1024||(size+=entry.Length)>1024L*1024*1024||((entry.ExternalAttributes>>16)&0xF000)==0xA000)throw new InvalidDataException("Conteúdo inesperado no pacote.");
            var path=SafePath(destination,entry.FullName);Directory.CreateDirectory(Path.GetDirectoryName(path)!);entry.ExtractToFile(path,false);
        }
        if(!Files.All(seen.Contains))throw new InvalidDataException("Distribuição incompleta.");
    }
    // Only distribution files are replaced. APPDATA and unknown user files are never touched.
    public static void Install(string package,string target,string backup,Action? verify=null)
    {
        var changed=new List<(string Path,string? Backup)>();Directory.CreateDirectory(backup);
        try
        {
            foreach(var name in Files)
            {
                var source=SafePath(package,name);if(!File.Exists(source))throw new InvalidDataException("Distribuição incompleta.");
                var dest=SafePath(target,name);Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                string? copy=null;
                if(File.Exists(dest)){copy=SafePath(backup,name);Directory.CreateDirectory(Path.GetDirectoryName(copy)!);File.Copy(dest,copy,false);}
                changed.Add((dest,copy));File.Copy(source,dest,true);
            }
            verify?.Invoke();
        }
        catch(Exception failure)
        {
            var errors=new List<Exception>();
            foreach(var entry in changed.AsEnumerable().Reverse())try{if(entry.Backup!=null)File.Copy(entry.Backup,entry.Path,true);else if(File.Exists(entry.Path))File.Delete(entry.Path);}catch(Exception e){errors.Add(e);}
            if(errors.Count>0)throw new AggregateException("Restauração incompleta. Backup preservado em "+backup,errors.Prepend(failure));
            throw;
        }
    }
}
