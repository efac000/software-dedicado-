using System.Diagnostics;
using System.Text.RegularExpressions;
namespace Dedicado;
public static class SteamUpdate {
    public const string AppId="343050";
    public static string[] Arguments(string install,bool validate)=>new[]{"+force_install_dir",Path.GetFullPath(install),"+login","anonymous","+app_update",AppId}.Concat(validate?new[]{"validate"}:Array.Empty<string>()).Append("+quit").ToArray();
    static bool Under(string path,string parent){
        var a=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        var b=Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar);
        return a.Equals(b,StringComparison.OrdinalIgnoreCase)||a.StartsWith(b+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    }
    public static void Check(string steamcmd,Cluster target,string install){
        if(!Path.IsPathFullyQualified(steamcmd)||!Path.GetFileName(steamcmd).Equals("steamcmd.exe",StringComparison.OrdinalIgnoreCase)||!File.Exists(steamcmd))throw new IOException("Selecione o steamcmd.exe oficial já instalado.");
        Engine.SafePath(steamcmd);
        if(!Path.IsPathFullyQualified(install)||!Directory.Exists(install))throw new IOException("Instalação dedicada inválida.");
        Engine.FindExe(install);
        if(Under(steamcmd,install)||Under(target.Path,install)||Under(install,target.Path)||Under(steamcmd,target.Path))throw new IOException("SteamCMD, cluster e instalação devem estar separados.");
        var parts=Path.GetFullPath(install).Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        if(parts.Any(s=>s.Equals("steamapps",StringComparison.OrdinalIgnoreCase)))throw new IOException("Instalação gerenciada pela Steam: atualize pelo cliente Steam. Este modo atende instalações independentes SteamCMD.");
        var mods=Path.Combine(install,"mods");Engine.SafePath(mods);
        if(!Directory.Exists(mods))throw new IOException("Pasta mods ausente.");
        var ugc=Path.Combine(install,"ugc_mods");Engine.SafePath(ugc);
        if(Directory.Exists(ugc)&&Directory.GetDirectories(ugc).Any(d=>!Path.GetFileName(d).Equals(Path.GetFileName(target.Path),StringComparison.OrdinalIgnoreCase)))throw new IOException("Instalação possivelmente compartilhada: atualização bloqueada.");
    }
    static void Offline(){
        Engine.Offline();
        foreach(var proc in Process.GetProcessesByName("steamcmd"))using(proc)throw new IOException("Feche outras instâncias do SteamCMD antes de atualizar.");
    }
    public static string Apply(string steamcmd,Cluster target,string install,bool validate,Action<string> log){
        using var gate=new Mutex(false,"Local\\GerenciadorDeDedicadoReset");
        if(!gate.WaitOne(0))throw new IOException("Outra manutenção em andamento.");
        try{return Run(steamcmd,target,install,validate,Recovery.DefaultRoot,Offline,Execute,log);}
        finally{gate.ReleaseMutex();}
    }
    internal static string Run(string steamcmd,Cluster target,string install,bool validate,string backupRoot,Action offline,Action<string,string,bool,string> execute,Action<string> log){
        offline();Check(steamcmd,target,install);
        var fresh=Engine.Load(Path.GetDirectoryName(target.Path)!,target.Number);
        if(!fresh.Path.Equals(target.Path,StringComparison.OrdinalIgnoreCase))throw new IOException("Cluster alterado.");
        var backup=Path.Combine(backupRoot,"steamcmd-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]);
        Engine.SafePath(backup);
        if(Under(backup,install)||Under(backup,target.Path))throw new IOException("Backup sobreposto à instalação ou ao cluster.");
        var setup=Path.Combine(install,"mods","dedicated_server_mods_setup.lua");
        var settings=Path.Combine(install,"mods","modsettings.lua");
        var files=new Dictionary<string,byte[]?>();
        long bytes=Engine.TreeBytes(target.Path);
        foreach(var f in new[]{setup,settings}){
            Engine.SafePath(f);
            if(File.Exists(f)&&new FileInfo(f).Length>4_000_000)throw new IOException("Configuração excede limite de backup.");
            files[f]=File.Exists(f)?File.ReadAllBytes(f):null;
            bytes=checked(bytes+(files[f]?.LongLength??0));
        }
        var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(backup))!);
        if(!drive.IsReady||drive.AvailableFreeSpace<Engine.RequiredBackupBytes(bytes))throw new IOException("Espaço insuficiente para backup.");
        // SteamCMD can download several GiB; this is a conservative floor, not a guarantee.
        var installDrive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(install))!);
        if(!installDrive.IsReady||installDrive.AvailableFreeSpace<5L*1024*1024*1024)throw new IOException("Reserve ao menos 5 GiB na unidade do dedicado antes de atualizar.");
        Directory.CreateDirectory(backup);
        Engine.CopyTree(target.Path,Path.Combine(backup,"cluster"));
        foreach(var (f,data) in files)if(data!=null)Engine.Atomic(Path.Combine(backup,Path.GetFileName(f)),data);
        File.WriteAllLines(Path.Combine(backup,"steamcmd-recovery.txt"),new[]{"Cluster: "+target.Path,"Instalação: "+install,"O backup protege o cluster e configurações de mods, não os binários.","Estado: atualização ainda não confirmada."});
        offline();
        foreach(var (f,data) in files)if(data==null?File.Exists(f):!File.Exists(f)||!File.ReadAllBytes(f).AsSpan().SequenceEqual(data))throw new IOException("Configuração mudou durante o backup; atualização cancelada.");
        log("Backup SteamCMD: "+backup);
        Exception? failure=null;
        try{execute(steamcmd,install,validate,backup);}
        catch(Exception ex){failure=ex;}
        try{
            offline();
            foreach(var (f,data) in files){
                Engine.SafePath(f);
                if(data==null){
                    if(File.Exists(f)){
                        var generated=Path.Combine(backup,"gerado-"+Path.GetFileName(f));
                        File.Copy(f,generated);File.Delete(f);
                    }
                }else Engine.Atomic(f,data);
            }
            Engine.FindExe(install);
        }catch(Exception ex){throw new IOException("Não foi possível confirmar/restaurar configurações após SteamCMD. Não inicie o servidor. Backup: "+backup,ex);}
        if(failure!=null)throw new IOException("SteamCMD não confirmou atualização. Configurações protegidas; binários podem estar incompletos. Consulte "+backup,failure);
        File.AppendAllText(Path.Combine(backup,"steamcmd-recovery.txt"),"\nSteamCMD confirmou instalação; configurações preservadas. Teste servidor e mods antes de iniciar produção.");
        return backup;
    }
    static void Execute(string steamcmd,string install,bool validate,string backup){
        var start=new ProcessStartInfo(steamcmd){WorkingDirectory=Path.GetDirectoryName(steamcmd)!,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var arg in Arguments(install,validate))start.ArgumentList.Add(arg);
        using var proc=Process.Start(start)??throw new IOException("SteamCMD não iniciou.");
        var stdout=Path.Combine(backup,"steamcmd.log");var stderr=Path.Combine(backup,"steamcmd.stderr.log");
        using(var a=new FileStream(stdout,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
        using(var b=new FileStream(stderr,FileMode.CreateNew,FileAccess.Write,FileShare.Read)){
            var x=proc.StandardOutput.BaseStream.CopyToAsync(a);var y=proc.StandardError.BaseStream.CopyToAsync(b);
            if(!proc.WaitForExit(60*60*1000)){if(!proc.HasExited)proc.Kill(true);if(!proc.WaitForExit(10000))throw new IOException("SteamCMD não encerrou após tempo limite.");throw new IOException("Tempo limite excedido.");}
            if(!Task.WaitAll(new[]{x,y},TimeSpan.FromSeconds(15)))throw new IOException("Logs do SteamCMD incompletos.");
        }
        if(proc.ExitCode!=0||!Confirmed(File.ReadLines(stdout)))throw new IOException("SteamCMD não retornou confirmação de instalação do app "+AppId+".");
    }
    internal static bool Confirmed(IEnumerable<string> lines){
        bool success=false;
        foreach(var line in lines){
            if(Regex.IsMatch(line,@"(?i)\bERROR!\s+Failed to install app|No subscription|Disk write failure"))return false;
            if(line.Contains("Success! App '343050' fully installed.",StringComparison.OrdinalIgnoreCase)||line.Contains("Success! App '343050' already up to date.",StringComparison.OrdinalIgnoreCase))success=true;
        }
        return success;
    }
}
