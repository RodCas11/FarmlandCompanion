using System.Diagnostics;
using System.Runtime.InteropServices;
using FarmlandCompanion.Updates;

internal static class Program
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int MessageBox(IntPtr window,string text,string title,uint flags);
    [STAThread]static int Main(string[] args)
    {
        string? target=null;bool parentExited=false;
        try
        {
            if(args.Length!=4||!int.TryParse(args[0],out int pid))throw new InvalidDataException("Parâmetros inválidos.");
            string work=Path.GetDirectoryName(Environment.ProcessPath!)!;
            target=Path.GetFullPath(args[1]);var zip=Path.GetFullPath(args[2]);var checksum=Path.GetFullPath(args[3]);
            if(!zip.Equals(Path.Combine(work,"release.zip"),StringComparison.OrdinalIgnoreCase)||!checksum.Equals(Path.Combine(work,"release.sha256"),StringComparison.OrdinalIgnoreCase)||target.Equals(work,StringComparison.OrdinalIgnoreCase)||!File.Exists(Path.Combine(target,"FarmlandCompanion.exe")))throw new InvalidDataException("Instalação inválida.");
            var userData=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if(target.StartsWith(userData,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("A instalação deve ficar fora da pasta de dados pessoais.");
            UpdatePackage.VerifyHash(zip,File.ReadAllText(checksum));
            var package=Path.Combine(work,"package");UpdatePackage.Extract(zip,package);
            try{using var parent=Process.GetProcessById(pid);if(!string.Equals(parent.MainModule?.FileName,Path.Combine(target,"FarmlandCompanion.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Processo de origem inválido.");if(!parent.WaitForExit(90000))throw new IOException("O aplicativo não encerrou.");}catch(ArgumentException){}
            parentExited=true;
            // Windows/antivirus can briefly retain a file after the parent exits.
            foreach(var name in UpdatePackage.Files)
            {
                string path=UpdatePackage.SafePath(target,name);if(!File.Exists(path))continue;
                for(int attempt=0;;attempt++)try{using var probe=File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);break;}catch(IOException)when(attempt<39){Thread.Sleep(250);}
            }
            UpdatePackage.Install(package,target,Path.Combine(work,"backup"),()=>
            {
                string ready=Path.Combine(work,"startup.ready");
                var info=new ProcessStartInfo(Path.Combine(target,"FarmlandCompanion.exe")){WorkingDirectory=target,UseShellExecute=false};info.ArgumentList.Add("--updated-ready="+ready);
                using var child=Process.Start(info)??throw new IOException("Falha ao reiniciar.");
                for(int i=0;i<120;i++){if(File.Exists(ready))return;if(child.WaitForExit(250))throw new IOException("A nova versão encerrou antes de iniciar.");}
                try{child.Kill();child.WaitForExit(10000);}catch{}
                throw new IOException("A nova versão não confirmou a inicialização.");
            });
            return 0;
        }
        catch(Exception e)
        {
            var log=Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!,"update-error.txt");try{File.WriteAllText(log,e.ToString());}catch{}
            if(parentExited&&target!=null)try{Process.Start(new ProcessStartInfo(Path.Combine(target,"FarmlandCompanion.exe")){UseShellExecute=true,WorkingDirectory=target});}catch{}
            MessageBox(IntPtr.Zero,"A atualização foi interrompida. Consulte os detalhes e o backup indicado antes de tentar novamente.\n"+e.Message+"\nDetalhes: "+log,"Farmland Companion — atualização",0x10);return 1;
        }
    }
}
