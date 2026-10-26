using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using FarmlandCompanion.Updates;
using RagnarokCompanion.Services;
using RagnarokCompanion.ViewModels;
using Xunit;
namespace RagnarokCompanion.Tests;

public sealed class HalloweenUpdateTests
{
    [Fact] public void PermanentUpdateActionChecksFreshReleaseAndReportsWithoutOpeningUnnecessaryFlows()
    {
        int checks=0;var notices=new List<string>();var version=GitHubUpdateService.InstalledVersion;
        using var vm=new UpdateViewModel(_=>{checks++;return Task.FromResult<AvailableRelease?>(new(version,new Uri("https://github.com/RodCas11/FarmlandCompanion/releases/download/v4.18.0/FarmlandCompanion-win-x64.zip"),new Uri("https://github.com/RodCas11/FarmlandCompanion/releases/download/v4.18.0/FarmlandCompanion-win-x64.sha256")));});
        vm.Notice=notices.Add;vm.SmartCommand.Execute(null);Assert.Equal(1,checks);Assert.False(vm.HasUpdate);Assert.True(vm.IsIdle);Assert.Contains("versão mais recente",Assert.Single(notices));
        version=new Version(version.Major+1,0,0,0);vm.SmartCommand.Execute(null);Assert.Equal(2,checks);Assert.True(vm.HasUpdate);Assert.True(vm.ShowNotice);Assert.Contains("v"+version.ToString(3),vm.ButtonTooltip);Assert.Single(notices);
        vm.DismissCommand.Execute(null);Assert.False(vm.ShowNotice);Assert.True(vm.HasUpdate);
    }
    static string Temp(){string path=Path.Combine(Path.GetTempPath(),"Farmland-update-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
    static DateTimeOffset Server(int day,int hour=0,int minute=0,int month=10)=>new(2026,month,day,hour,minute,0,TimeSpan.FromHours(-3));
    [Fact] public void OfficialSeasonSchedulesAndAlertsExpireButPoringRemains()
    {
        var root=Temp();try{
            var repo=new EventRepository(root);var service=new EventScheduleService(repo);
            var rows=service.Occurrences(Server(26),true);Assert.Equal(28,rows.Count(r=>r.Event.Halloween));
            Assert.Equal(24,rows.Count(r=>r.Event.OfficialId=="pumpkins"));Assert.Equal(4,rows.Count(r=>r.Event.OfficialId=="poring"));
            Assert.DoesNotContain(service.Occurrences(Server(25),true),r=>r.Event.Halloween);
            Assert.Equal(28,service.Occurrences(Server(2,month:11),true).Count(r=>r.Event.Halloween));
            Assert.DoesNotContain(service.Upcoming(Server(3,month:11)),r=>r.Event.Halloween);
            Assert.Contains(service.Upcoming(Server(3,month:11)),r=>r.Event.OfficialId=="poring");
            var alerts=new List<RagnarokCompanion.Models.EventAlert>();service.Alert+=alerts.Add;
            service.Evaluate(Server(26,14,55));service.Evaluate(Server(26,14,55));Assert.Single(alerts,a=>a.EventId=="official-niflheim");
            service.Evaluate(Server(26,15));Assert.Contains(alerts,a=>a.EventId=="official-pumpkins");
            repo.Events.Single(e=>e.OfficialId=="zombies").AlertsEnabled=false;repo.Save();
            var restored=new EventRepository(root);Assert.False(restored.Events.Single(e=>e.OfficialId=="zombies").AlertsEnabled);
            var vm=new EventViewModel(restored,()=>Server(26,14));Assert.All(vm.Today,r=>Assert.False(r.Event.Halloween));vm.HalloweenTab=true;Assert.All(vm.Today,r=>Assert.True(r.Event.Halloween));Assert.Contains(vm.CompactUpcoming,r=>!r.Occurrence.Event.Halloween);Assert.Contains(vm.CompactUpcoming,r=>r.Occurrence.Event.Halloween);
        }finally{Directory.Delete(root,true);}
    }
    [Fact] public void AutomaticThemeUsesSaoPauloInclusiveDatesAndManualChoice()
    {
        Assert.False(AppearanceService.IsHalloween("Automático",Server(25,23,59)));Assert.True(AppearanceService.IsHalloween("Automático",Server(26)));
        Assert.True(AppearanceService.IsHalloween("Automático",Server(2,23,59,11)));Assert.False(AppearanceService.IsHalloween("Automático",Server(3,month:11)));
        Assert.True(AppearanceService.IsHalloween("Tema Halloween",Server(3,month:11)));Assert.False(AppearanceService.IsHalloween("Tema Farmland",Server(26)));
    }
    [Fact] public void ReleasesRequireStableVersionsAndExactOfficialAssetUrls()
    {
        string Json(string tag,bool prerelease=false,string owner="RodCas11")=>JsonSerializer.Serialize(new{tag_name=tag,draft=false,prerelease,assets=new[]{"FarmlandCompanion-win-x64.zip","FarmlandCompanion-win-x64.sha256"}.Select(name=>new{name,browser_download_url=$"https://github.com/{owner}/FarmlandCompanion/releases/download/{tag}/{name}"})});
        Assert.True(GitHubUpdateService.Parse(Json("v4.20.0"))!.Version>GitHubUpdateService.StableVersion("v4.9.0"));Assert.Null(GitHubUpdateService.Parse(Json("v4.20.0-beta")));Assert.Null(GitHubUpdateService.Parse(Json("v4.20.0",true)));Assert.Throws<InvalidDataException>(()=>GitHubUpdateService.Parse(Json("v4.20.0",owner:"other")));
    }
    [Fact] public void ChecksumTamperingAndZipTraversalAreRejected()
    {
        string root=Temp();try{
            string zip=Path.Combine(root,"test.zip");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create)){using var writer=new StreamWriter(archive.CreateEntry("../escape.exe").Open());writer.Write("test");}
            Assert.Throws<InvalidDataException>(()=>UpdatePackage.Extract(zip,Path.Combine(root,"unzip")));Assert.False(File.Exists(Path.Combine(root,"escape.exe")));
            string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip)));UpdatePackage.VerifyHash(zip,hash+"  FarmlandCompanion-win-x64.zip");Assert.Throws<InvalidDataException>(()=>UpdatePackage.VerifyHash(zip,new string('0',64)+"  FarmlandCompanion-win-x64.zip"));
        }finally{Directory.Delete(root,true);}
    }
    [Fact] public void FullDistributionInstallRollsBackOnStartupFailureAndPreservesUserFiles()
    {
        string root=Temp();try{
            string package=Path.Combine(root,"package"),target=Path.Combine(root,"installed");
            foreach(var name in UpdatePackage.Files){foreach(var dir in new[]{package,target}){string path=UpdatePackage.SafePath(dir,name);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,dir==package?"new":"old");}}
            File.WriteAllText(Path.Combine(target,"personal.json"),"keep");
            Assert.Throws<IOException>(()=>UpdatePackage.Install(package,target,Path.Combine(root,"backup"),()=>throw new IOException("startup failed")));
            Assert.All(UpdatePackage.Files,name=>Assert.Equal("old",File.ReadAllText(UpdatePackage.SafePath(target,name))));Assert.Equal("keep",File.ReadAllText(Path.Combine(target,"personal.json")));
            UpdatePackage.Install(package,target,Path.Combine(root,"backup2"));Assert.All(UpdatePackage.Files,name=>Assert.Equal("new",File.ReadAllText(UpdatePackage.SafePath(target,name))));
        }finally{Directory.Delete(root,true);}
    }
}
