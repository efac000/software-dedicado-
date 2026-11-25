using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("CoreTests")]
namespace Dedicado;
public record Cluster(string Path,string Name,string Number,string[] Shards) {
    public override string ToString()=> $"Cluster_{Number} — {Name} ({System.IO.Path.GetDirectoryName(Path)})";
}
public record ShardPlan(string Name,byte[] Content,string[] Wanted,string[] Old);
public record Plan(Cluster Source,Cluster Target,string Install,string Exe,ShardPlan[] Shards,string Fingerprint) {
    public string[] Ids=>Shards.SelectMany(x=>x.Wanted).Distinct().Order().ToArray();
}
public static class Engine {
    static readonly UTF8Encoding Utf8=new(false,true);
    public static bool ValidNumber(string s)=>Regex.IsMatch(s,@"\A[1-9][0-9]{0,8}\z");
    public static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b));
    static string FileHash(string p){using var f=File.OpenRead(p);return Convert.ToHexString(SHA256.HashData(f));}
    public static void SafePath(string p){
        for(string? x=System.IO.Path.GetFullPath(p);x!=null;x=System.IO.Path.GetDirectoryName(x)){
            if((File.Exists(x)||Directory.Exists(x)) && File.GetAttributes(x).HasFlag(FileAttributes.ReparsePoint))throw new IOException("Link/junção não suportado: "+x);
        }
    }
    public static Cluster Load(string root,string number){
        if(!ValidNumber(number))throw new ArgumentException("Digite somente um número inteiro positivo existente.");
        var p=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,"Cluster_"+number)); SafePath(p);
        var ini=System.IO.Path.Combine(p,"cluster.ini");
        if(!File.Exists(ini))throw new DirectoryNotFoundException("Cluster_"+number+" não encontrado ou sem cluster.ini.");
        SafePath(ini);
        var shards=Directory.GetDirectories(p).Where(x=>File.Exists(System.IO.Path.Combine(x,"server.ini"))).Select(x=>System.IO.Path.GetFileName(x)!).Order().ToArray();
        if(!shards.Contains("Master")||shards.Any(x=>x!="Master"&&x!="Caves"))throw new IOException("Apenas Master e Caves convencionais são suportados.");
        foreach(var s in shards)SafePath(System.IO.Path.Combine(p,s,"server.ini"));
        var name=Regex.Match(File.ReadAllText(ini),@"(?m)^\s*cluster_name\s*=\s*([^\r\n]*)").Groups[1].Value.Trim();
        return new Cluster(p,name.Length>0?name:"(sem nome)",number,shards);
    }
    public static Cluster[] Discover(string root){
        if(string.IsNullOrWhiteSpace(root)||!Directory.Exists(root))return [];
        SafePath(root);
        var roots=new List<string>{root};
        foreach(var child in Directory.GetDirectories(root)){
            if(Regex.IsMatch(Path.GetFileName(child),@"\A[0-9]{1,20}\z"))try{SafePath(child);roots.Add(child);}catch(IOException){}
        }
        return roots.SelectMany(r=>Numbers(r).Select(n=>Load(r,n))).OrderBy(c=>int.Parse(c.Number)).ThenBy(c=>c.Path,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static string[] Numbers(string root)=>Directory.Exists(root)?Directory.GetDirectories(root,"Cluster_*").Select(x=>System.IO.Path.GetFileName(x)![8..]).Where(ValidNumber).Where(n=>{try{Load(root,n);return true;}catch{return false;}}).OrderBy(x=>int.Parse(x)).ToArray():[];
    public static void Offline(){
        foreach(var p in Process.GetProcesses()){
            using(p){if(p.ProcessName.StartsWith("dontstarve",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Feche DST e todos os dedicados antes da manutenção.");}
        }
    }
    public static string FindExe(string install){
        SafePath(install);
        foreach(var dir in new[]{"bin64","bin"})foreach(var name in new[]{"dontstarve_dedicated_server_nullrenderer_x64.exe","dontstarve_dedicated_server_nullrenderer.exe"}){
            var p=System.IO.Path.Combine(install,dir,name); if(File.Exists(p)){SafePath(p);return p;}
        }
        throw new IOException("Executável do dedicado não encontrado.");
    }
    static string Override(Cluster c,string shard)=>System.IO.Path.Combine(c.Path,shard,"modoverrides.lua");
    static byte[] Read(string path,bool required){
        SafePath(path);
        if(!File.Exists(path)){if(required)throw new IOException("Arquivo ausente: "+path);return Utf8.GetBytes("return {}");}
        if(new FileInfo(path).Length>4_000_000)throw new IOException("Arquivo Lua grande demais.");
        return File.ReadAllBytes(path);
    }
    static string[] Ids(byte[] content)=>Lua.Active(Utf8.GetString(content).TrimStart('\uFEFF')).Keys.Order().ToArray();
    public static Plan Analyze(Cluster source,Cluster target,string install){
        if(string.IsNullOrWhiteSpace(install)||!Directory.Exists(install))throw new IOException("Selecione uma instalação válida do servidor dedicado.");
        install=System.IO.Path.GetFullPath(install);
        if(string.Equals(source.Path,target.Path,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Origem igual ao destino.");
        if(!source.Shards.SequenceEqual(target.Shards))throw new ArgumentException("Master/Caves precisam corresponder entre os mundos.");
        var exe=FindExe(install);
        if(IsWithin(source.Path,install)||IsWithin(target.Path,install)||IsWithin(install,source.Path)||IsWithin(install,target.Path))throw new IOException("A instalação e os clusters devem ficar em pastas separadas.");
        var mods=System.IO.Path.Combine(install,"mods");
        if(!Directory.Exists(mods))throw new IOException("Pasta mods não encontrada.");
        SafePath(mods);
        var setup=System.IO.Path.Combine(mods,"dedicated_server_mods_setup.lua");
        SafePath(setup);
        var settings=System.IO.Path.Combine(mods,"modsettings.lua"); SafePath(settings);
        if(File.Exists(settings)&&Regex.IsMatch(File.ReadAllText(settings),@"(?m)^\s*(ForceEnableMod|ForceDisableMod)\s*\("))throw new IOException("modsettings.lua contém regras forçadas; revise manualmente.");
        var shards=source.Shards.Select(s=>{var data=Read(Override(source,s),true);return new ShardPlan(s,data,Ids(data),Ids(Read(Override(target,s),false)));}).ToArray();
        if(!shards.Any(x=>x.Wanted.Length>0))throw new ArgumentException("Nenhum mod ativo encontrado.");
        var fingerprint=Hash(Utf8.GetBytes(string.Join("|",new[]{source.Path,target.Path,install,FileHash(System.IO.Path.Combine(source.Path,"cluster.ini")),FileHash(System.IO.Path.Combine(target.Path,"cluster.ini")),File.Exists(setup)?FileHash(setup):"missing",File.Exists(settings)?FileHash(settings):"missing"}.Concat(shards.Select(s=>Hash(s.Content)+":"+Hash(Read(Override(target,s.Name),false)))))));
        return new(source,target,install,exe,shards,fingerprint);
    }
    static bool IsWithin(string path,string parent){var a=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);var b=Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar);return a.Equals(b,StringComparison.OrdinalIgnoreCase)||a.StartsWith(b+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
    static void CopyTree(string source,string dest){
        SafePath(source);Directory.CreateDirectory(dest);
        foreach(var f in Directory.GetFiles(source)){SafePath(f);var d=System.IO.Path.Combine(dest,System.IO.Path.GetFileName(f));File.Copy(f,d);if(FileHash(f)!=FileHash(d))throw new IOException("Backup divergente.");}
        foreach(var dir in Directory.GetDirectories(source)){SafePath(dir);CopyTree(dir,System.IO.Path.Combine(dest,System.IO.Path.GetFileName(dir)));}
    }
    static void Atomic(string path,byte[] data){
        SafePath(path);var tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllBytes(tmp,data);File.Move(tmp,path,true);}finally{if(File.Exists(tmp))File.Delete(tmp);}
        if(FileHash(path)!=Hash(data))throw new IOException("Falha na conferência de "+path);
    }
    static void SafeTree(string path){SafePath(path);foreach(var f in Directory.GetFiles(path))SafePath(f);foreach(var d in Directory.GetDirectories(path))SafeTree(d);}
    public static Task<string> Apply(Plan plan,Action<string> log){
        using var gate=new Mutex(false,"Local\\GerenciadorDeDedicadoReset");
        if(!gate.WaitOne(0))throw new IOException("Outra manutenção em andamento.");
        // All work on this same thread: mutex ownership must not migrate across await.
        try{return Task.FromResult(Run(plan,log,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Gerenciador de Dedicado","Backups"),Offline,UpdateShard));}finally{gate.ReleaseMutex();}
    }
    internal static string Run(Plan plan,Action<string> log,string backupBase,Action offline,Action<Plan,ShardPlan,string,string,Action<string>> update){
        offline();
        var source=Load(System.IO.Path.GetDirectoryName(plan.Source.Path)!,plan.Source.Number);
        var target=Load(System.IO.Path.GetDirectoryName(plan.Target.Path)!,plan.Target.Number);
        var fresh=Analyze(source,target,plan.Install);
        if(fresh.Fingerprint!=plan.Fingerprint)throw new IOException("Arquivos mudaram; analise e confirme novamente.");
        var id=DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8];
        plan=fresh;
        var backup=Path.Combine(backupBase,id);
        var quarantine=System.IO.Path.Combine(plan.Install,"GDD-quarentena",id);
        SafePath(backup);SafePath(quarantine);
        if(IsWithin(backup,target.Path)||IsWithin(backup,source.Path)||IsWithin(backup,plan.Install))throw new IOException("Backup dentro do cluster ou da instalação.");
        var mods=System.IO.Path.Combine(plan.Install,"mods");var setup=System.IO.Path.Combine(mods,"dedicated_server_mods_setup.lua");
        var ugc=System.IO.Path.Combine(plan.Install,"ugc_mods","Cluster_"+target.Number);
        SafePath(ugc);
        var affected=plan.Shards.SelectMany(s=>s.Old.Concat(s.Wanted)).Distinct().ToHashSet(StringComparer.Ordinal);
        var ugcRoot=Path.GetDirectoryName(ugc)!;SafePath(ugcRoot);
        if(Directory.Exists(ugcRoot)&&Directory.GetDirectories(ugcRoot).Any(d=>!string.Equals(Path.GetFileName(d),"Cluster_"+target.Number,StringComparison.OrdinalIgnoreCase)))throw new IOException("Há caches UGC de outros clusters nesta instalação; reset bloqueado.");
        if(Directory.Exists(ugc)&&Directory.GetDirectories(ugc).Any(d=>!target.Shards.Contains(Path.GetFileName(d),StringComparer.OrdinalIgnoreCase)))throw new IOException("Há outros shards UGC; reset bloqueado.");
        var caches=Directory.GetDirectories(mods,"workshop-*").Where(x=>affected.Contains(Path.GetFileName(x)[9..])).Concat(target.Shards.Select(s=>Path.Combine(ugc,s)).Where(Directory.Exists)).ToArray();
        foreach(var p in caches)SafeTree(p);
        // Snapshot every changed file and full destination before any mutation.
        Directory.CreateDirectory(backup);log("Backup: "+backup);CopyTree(target.Path,System.IO.Path.Combine(backup,"cluster"));
        var originals=new Dictionary<string,byte[]?>();foreach(var p in plan.Shards.Select(s=>Override(target,s.Name)).Append(setup)){SafePath(p);originals[p]=File.Exists(p)?File.ReadAllBytes(p):null;}
        if(originals[setup] is byte[] old)Atomic(System.IO.Path.Combine(backup,"dedicated_server_mods_setup.lua"),old);
        Directory.CreateDirectory(quarantine);
        var moved=new List<(string Old,string New)>();
        var touched=new List<string>();
        var journal=System.IO.Path.Combine(backup,"recovery.txt");
        File.WriteAllLines(journal,new[]{"Origem: "+source.Path,"Destino: "+target.Path,"Instalação: "+plan.Install,"Quarentena: "+quarantine,"Setup anterior: "+(originals[setup]!=null?"presente":"ausente")}.Concat(caches.Select((x,i)=>x+" -> "+System.IO.Path.Combine(quarantine,i.ToString()))));
        offline();
        if(Analyze(Load(Path.GetDirectoryName(source.Path)!,source.Number),Load(Path.GetDirectoryName(target.Path)!,target.Number),plan.Install).Fingerprint!=plan.Fingerprint)throw new IOException("Arquivos mudaram durante o backup; nenhuma manutenção executada.");
        var newPaths=affected.Select(x=>Path.Combine(mods,"workshop-"+x)).Concat(target.Shards.Select(s=>Path.Combine(ugc,s))).Where(p=>!Directory.Exists(p)).ToArray();
        try{
            foreach(var p in caches){var q=System.IO.Path.Combine(quarantine,moved.Count.ToString());Directory.Move(p,q);moved.Add((p,q));}
            foreach(var s in plan.Shards){var p=Override(target,s.Name);touched.Add(p);Atomic(p,s.Content);}
            touched.Add(setup);Atomic(setup,Utf8.GetBytes("-- Gerenciador de Dedicado 1.0.0\n"+string.Join("\n",plan.Ids.Select(x=>"ServerModSetup(\""+x+"\")"))+"\n"));
            foreach(var s in plan.Shards.OrderBy(s=>s.Name=="Master"?0:1)){
                offline();var ugcShard=Path.Combine(ugc,s.Name);SafePath(ugcShard);
                update(plan,s,ugcShard,backup,log);
                foreach(var mod in s.Wanted){
                    var v1=Path.Combine(mods,"workshop-"+mod);
                    var v2=Path.Combine(ugcShard,"content","322330",mod);
                    var v2Direct=Path.Combine(ugcShard,"workshop-"+mod);
                    if(!HasMod(v1)&&!HasMod(v2)&&!HasMod(v2Direct))throw new IOException("Arquivos do mod "+mod+" não confirmados em "+s.Name+".");
                }
            }
            File.AppendAllText(journal,"\nAtualizadores concluídos; confirme logs e teste mundo manualmente.");
            return backup;
        }catch(Exception error){
            log("Falha: "+error.Message+"; tentando restaurar.");
            var rollbackErrors=new List<string>();
            try{offline();}catch{throw new IOException("Processos ainda em execução. Não foi feita restauração automática. Backup: "+backup,error);}
            foreach(var p in touched.AsEnumerable().Reverse())try{if(originals[p] is byte[] b)Atomic(p,b);else if(File.Exists(p))File.Delete(p);}catch(Exception ex){rollbackErrors.Add(p+": "+ex.Message);}
            foreach(var p in newPaths)try{if(Directory.Exists(p)){SafeTree(p);Directory.Move(p,Path.Combine(quarantine,"novo-"+Guid.NewGuid().ToString("N")));}}catch(Exception ex){rollbackErrors.Add(p+": "+ex.Message);}
            foreach(var (p,q) in moved.AsEnumerable().Reverse())try{if(Directory.Exists(p)){SafeTree(p);Directory.Move(p,System.IO.Path.Combine(quarantine,"novo-"+Guid.NewGuid().ToString("N")));}Directory.Move(q,p);}catch(Exception ex){rollbackErrors.Add(p+": "+ex.Message);}
            File.AppendAllText(journal,"\nFalha: "+error.Message+"\n"+string.Join("\n",rollbackErrors));
            throw new IOException("Manutenção interrompida. "+(rollbackErrors.Count==0?"Configurações e caches afetados foram restaurados; verifique o relatório e o mundo antes de iniciar.":"Restauração incompleta; consulte recovery.txt.")+" Backup: "+backup,error);
        }
    }
    internal static bool HasMod(string dir){
        SafePath(dir);
        if(!Directory.Exists(dir))return false;
        var info=Path.Combine(dir,"modinfo.lua");SafePath(info);
        return File.Exists(info)&&new FileInfo(info).Length>0;
    }
    static void UpdateShard(Plan plan,ShardPlan s,string ugcShard,string backup,Action<string> log){
        var start=new ProcessStartInfo(plan.Exe){WorkingDirectory=Path.GetDirectoryName(plan.Exe)!,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        var conf=Path.GetDirectoryName(plan.Target.Path)!;var storage=Path.GetDirectoryName(conf)??throw new IOException("Caminho de cluster inválido.");
        foreach(var arg in new[]{"-persistent_storage_root",storage,"-conf_dir",Path.GetFileName(conf)!,"-cluster","Cluster_"+plan.Target.Number,"-shard",s.Name,"-ugc_directory",ugcShard,"-only_update_server_mods"})start.ArgumentList.Add(arg);
        log("Atualizando "+s.Name+"...");using var proc=Process.Start(start)??throw new IOException("Falha ao iniciar atualizador.");
        var output=proc.StandardOutput.ReadToEndAsync();var errors=proc.StandardError.ReadToEndAsync();
        bool timeout=!proc.WaitForExit(30*60*1000);
        if(timeout){proc.Kill(true);if(!proc.WaitForExit(10000))throw new IOException("Atualizador não encerrou após o limite.");}
        var report=output.GetAwaiter().GetResult()+"\n"+errors.GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(backup,s.Name+"-update.log"),report);
        if(timeout||proc.ExitCode!=0||Regex.IsMatch(report,@"(?im)(#ERROR:|failed to (download|install|update)|error downloading|workshop.*timed out)"))throw new IOException("Atualizador informou falha ou excedeu o limite em "+s.Name+".");
    }
}
