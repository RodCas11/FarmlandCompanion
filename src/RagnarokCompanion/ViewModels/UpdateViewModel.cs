using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;

public sealed class UpdateViewModel : Observable,IDisposable
{
    readonly GitHubUpdateService service=new();readonly CancellationTokenSource cancel=new();
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromHours(6)};
    AvailableRelease? release;DateTimeOffset dismissedUntil;bool busy;double progress;
    public Action? SaveBeforeUpdate {get;set;}
    public Action? ShowProgress {get;set;}
    public Action? ExitForUpdate {get;set;}
    public string InstalledVersion=>GitHubUpdateService.InstalledVersion.ToString(3);
    public string AvailableVersion=>release?.Version.ToString(3)??"Não verificada";
    public string Status {get;private set;}="Ainda não verificado.";
    public bool HasUpdate=>release!=null&&release.Version>GitHubUpdateService.InstalledVersion;
    public bool ShowNotice=>HasUpdate&&DateTimeOffset.UtcNow>=dismissedUntil;
    public bool Busy {get=>busy;private set{busy=value;Changed();}}
    public double Progress {get=>progress;private set{progress=value;Changed();}}
    public ICommand CheckCommand {get;}
    public ICommand InstallCommand {get;}
    public ICommand DismissCommand {get;}
    public UpdateViewModel()
    {
        CheckCommand=new RelayCommand(async _=>await Check());InstallCommand=new RelayCommand(async _=>await Install());DismissCommand=new RelayCommand(_=>{dismissedUntil=DateTimeOffset.UtcNow.AddHours(6);Changed(nameof(ShowNotice));});
        timer.Tick+=async(_,_)=>await Check();
    }
    public void Start(){timer.Start();_=Check();}
    void Tell(string text){Status=text;Changed(nameof(Status));Changed(nameof(AvailableVersion));Changed(nameof(HasUpdate));Changed(nameof(ShowNotice));}
    async Task Check()
    {
        if(Busy)return;Busy=true;Tell("Verificando releases estáveis…");using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel.Token);timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try{release=await service.Check(timeout.Token);Tell(HasUpdate?"Atualização disponível.":"Você está na versão mais recente.");}
        catch(Exception){Tell("GitHub indisponível. O Companion continua funcionando; tente novamente mais tarde.");}finally{Busy=false;}
    }
    async Task Install()
    {
        if(Busy||!HasUpdate||release==null||ExitForUpdate==null)return;
        if(MessageBox.Show($"Versão instalada: {InstalledVersion}\nNova versão: {AvailableVersion}\n\nBaixar, atualizar e reiniciar o Companion? Seus dados serão preservados.","Atualizar Farmland Companion",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        Busy=true;Progress=0;
        try
        {
            ShowProgress?.Invoke();
            string target=Path.GetDirectoryName(Environment.ProcessPath!)!;
            // Fail before closing if the installation is read-only. Never request elevation.
            string probe=Path.Combine(target,".update-write-"+Guid.NewGuid().ToString("N"));using(File.Create(probe)){}File.Delete(probe);
            Tell("Baixando atualização…");string work=await service.Prepare(release,new Progress<double>(p=>Progress=p),cancel.Token);
            SaveBeforeUpdate?.Invoke();
            var start=new ProcessStartInfo(Path.Combine(work,"FarmlandUpdater.exe")){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=work};
            foreach(string arg in new[]{Environment.ProcessId.ToString(),target,Path.Combine(work,"release.zip"),Path.Combine(work,"release.sha256")})start.ArgumentList.Add(arg);
            using var helper=Process.Start(start)??throw new IOException("Não foi possível iniciar o atualizador.");ExitForUpdate();
        }
        catch(Exception e){Tell("Atualização não instalada; versão atual preservada. "+e.Message);}finally{Busy=false;}
    }
    public void Dispose(){timer.Stop();cancel.Cancel();cancel.Dispose();}
}
