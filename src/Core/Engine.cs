using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("CoreTests")]
namespace Dedicado;
public record Cluster(string Path,string Name,string Number,string[] Shards) {
    public string FolderName=>System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(Path));
    public override string ToString()=> $"{FolderName} — {Name} ({System.IO.Path.GetDirectoryName(Path)})";
}
public record ShardPlan(string Name,byte[] Content,string[] Wanted,string[] Old);
public record Plan(Cluster Source,Cluster Target,string Install,string Exe,ShardPlan[] Shards,string Fingerprint,bool InPlace=false,bool Edited=false) {
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
    public static bool ValidClusterFolder(string name){
        if(name.StartsWith("Cluster_",StringComparison.OrdinalIgnoreCase))return ValidNumber(name[8..]);
        return Regex.IsMatch(name,@"\A[\p{L}\p{Nd}][\p{L}\p{Nd} _.-]{0,63}\z")
            &&!name.EndsWith('.')&&!name.EndsWith(' ');
    }
    public static Cluster Load(string root,string number){
        if(!ValidNumber(number))throw new ArgumentException("Digite somente um número inteiro positivo existente.");
        return LoadClusterDirectory(System.IO.Path.GetFullPath(System.IO.Path.Combine(root,"Cluster_"+number)));
    }
    public static Cluster LoadClusterDirectory(string path){
        if(!Path.IsPathFullyQualified(path))throw new IOException("Caminho do cluster inválido.");
        var p=Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var folder=Path.GetFileName(p);
        if(!ValidClusterFolder(folder))throw new IOException("Nome da pasta do cluster não suportado.");
        SafePath(p);
        var number=folder.StartsWith("Cluster_",StringComparison.OrdinalIgnoreCase)?folder[8..]:folder;
        var ini=Path.Combine(p,"cluster.ini");
        if(!File.Exists(ini))throw new DirectoryNotFoundException(folder+" não encontrado ou sem cluster.ini.");
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
        var rootName=Path.GetFileName(Path.TrimEndingDirectorySeparator(root));
        if(File.Exists(Path.Combine(root,"cluster.ini"))||(rootName.StartsWith("Cluster_",StringComparison.OrdinalIgnoreCase)&&ValidNumber(rootName[8..])))
            return new[]{LoadClusterDirectory(Path.GetFullPath(root))};
        var roots=new List<string>{root};
        foreach(var child in Directory.GetDirectories(root)){
            if(Regex.IsMatch(Path.GetFileName(child),@"\A[0-9]{1,20}\z"))try{SafePath(child);roots.Add(child);}catch(IOException){}
        }
        var clusters=new List<Cluster>();
        foreach(var folder in roots.SelectMany(Directory.EnumerateDirectories)){
            if(!File.Exists(Path.Combine(folder,"cluster.ini")))continue;
            try{clusters.Add(LoadClusterDirectory(Path.GetFullPath(folder)));}
            catch(IOException){}
        }
        return clusters.OrderBy(c=>int.TryParse(c.Number,out var number)?number:int.MaxValue)
            .ThenBy(c=>c.Path,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static string[] Numbers(string root)=>Directory.Exists(root)?Directory.GetDirectories(root,"Cluster_*").Select(x=>System.IO.Path.GetFileName(x)![8..]).Where(ValidNumber).Where(n=>{try{Load(root,n);return true;}catch{return false;}}).OrderBy(x=>int.Parse(x)).ToArray():[];
    public static void Offline(){
        foreach(var p in Process.GetProcesses()){
            using(p){if(p.ProcessName.StartsWith("dontstarve",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Feche DST e todos os dedicados antes da manutenção.");}
        }
    }
    internal static (string Dir,string Name)[] ExecutableCandidates(System.Runtime.InteropServices.Architecture os){
        var x86=new[]{("bin","dontstarve_dedicated_server_nullrenderer.exe"),("bin64","dontstarve_dedicated_server_nullrenderer.exe")};
        var x64=new[]{("bin64","dontstarve_dedicated_server_nullrenderer_x64.exe"),("bin","dontstarve_dedicated_server_nullrenderer_x64.exe")};
        return os==System.Runtime.InteropServices.Architecture.X86?x86:x64.Concat(x86).ToArray();
    }
    public static string FindExe(string install){
        SafePath(install);
        foreach(var (dir,name) in ExecutableCandidates(System.Runtime.InteropServices.RuntimeInformation.OSArchitecture)){
            var p=System.IO.Path.Combine(install,dir,name); if(File.Exists(p)){SafePath(p);return p;}
        }
        throw new IOException("Executável do dedicado não encontrado.");
    }
    static string Override(Cluster c,string shard)=>System.IO.Path.Combine(c.Path,shard,"modoverrides.lua");
    static byte[]? Read(string path,bool required){
        SafePath(path);
        if(!File.Exists(path)){if(required)throw new IOException("Arquivo ausente: "+path);return null;}
        if(new FileInfo(path).Length>4_000_000)throw new IOException("Arquivo Lua grande demais.");
        return File.ReadAllBytes(path);
    }
    static string[] Ids(byte[] content)=>Lua.Active(Utf8.GetString(content).TrimStart('\uFEFF')).Keys.Order().ToArray();
    // Only literal, individual Workshop registrations are supported; never execute setup Lua.
    static readonly Regex SetupToken=new("""\G(?:\s+|--\[(?<eq>=*)\[.*?\]\k<eq>\]|--(?!\[=*\[)[^\r\n]*|ServerModSetup\s*\(\s*(?<q>["'])(?<id>[1-9][0-9]{0,19})\k<q>\s*\)\s*;?)""",RegexOptions.Singleline|RegexOptions.CultureInvariant,TimeSpan.FromSeconds(2));
    internal static HashSet<string> SetupIds(byte[]? bytes){
        var ids=new HashSet<string>(StringComparer.Ordinal);
        if(bytes is null)return ids;
        var text=Utf8.GetString(bytes).TrimStart('\uFEFF');
        for(int p=0;p<text.Length;){
            var m=SetupToken.Match(text,p);
            if(!m.Success||m.Length==0)throw new IOException("Setup global contém Lua não suportado ou coleção Workshop. Revise manualmente antes de continuar.");
            if(m.Groups["id"].Success){
                var id=m.Groups["id"].Value;
                if(!ulong.TryParse(id,out _))throw new IOException("ID inválido no setup global.");
                ids.Add(id);
            }
            p+=m.Length;
        }
        return ids;
    }
    static void CheckSetup(byte[]? bytes,HashSet<string> affected){
        foreach(var id in SetupIds(bytes))if(!affected.Contains(id))throw new IOException("Setup global contém mod não relacionado ao plano: "+id+". Confirme que a instalação é exclusiva e revise o arquivo manualmente.");
    }
    public static Plan Analyze(Cluster source,Cluster target,string install)=>AnalyzeInternal(source,target,install,false,null,null);
    // In-place repair is explicit; copying a world onto itself remains prohibited.
    public static Plan AnalyzeRepair(Cluster target,string install,IReadOnlyDictionary<string,byte[]>? proposed=null,string? expectedBaseline=null)=>AnalyzeInternal(target,target,install,true,proposed,expectedBaseline);
    public static Plan AnalyzeEditBaseline(Cluster target,string install)=>AnalyzeInternal(target,target,install,true,null,null,true);
    static Plan AnalyzeInternal(Cluster source,Cluster target,string install,bool inPlace,IReadOnlyDictionary<string,byte[]>? proposed,string? expectedBaseline,bool allowEmpty=false){
        if(string.IsNullOrWhiteSpace(install)||!Directory.Exists(install))throw new IOException("Selecione uma instalação válida do servidor dedicado.");
        install=System.IO.Path.GetFullPath(install);
        if(string.Equals(source.Path,target.Path,StringComparison.OrdinalIgnoreCase)!=inPlace)throw new ArgumentException("Origem igual ao destino no modo de cópia.");
        if(proposed!=null&&(!inPlace||proposed.Count!=target.Shards.Length||target.Shards.Any(s=>!proposed.ContainsKey(s))))throw new ArgumentException("A edição precisa corresponder aos shards do destino.");
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
        var setupBytes=Read(setup,false);
        // Capture each override once; missing and empty-table files are different states.
        var fingerprintParts=new List<string>{inPlace?"reparo":"copia",source.Path,target.Path,install,exe,FileHash(Path.Combine(source.Path,"cluster.ini")),FileHash(Path.Combine(target.Path,"cluster.ini")),setupBytes is null?"missing":Hash(setupBytes),File.Exists(settings)?FileHash(settings):"missing"};
        var shards=source.Shards.Select(s=>{
            var original=Read(Override(source,s),!inPlace);
            var data=original??Utf8.GetBytes("return {}");
            var previous=inPlace?original:Read(Override(target,s),false);
            var sourceIni=Path.Combine(source.Path,s,"server.ini");
            var targetIni=Path.Combine(target.Path,s,"server.ini");
            SafePath(sourceIni);SafePath(targetIni);
            fingerprintParts.AddRange(new[]{s,FileHash(sourceIni),FileHash(targetIni),original is null?"missing":Hash(data),previous is null?"missing":Hash(previous)});
            var wanted=proposed is null?data:proposed[s].ToArray();
            if(wanted.Length>4_000_000)throw new IOException("Arquivo Lua editado grande demais.");
            return new ShardPlan(s,wanted,Ids(wanted),previous is null?[]:Ids(previous));
        }).ToArray();

        if(!allowEmpty&&proposed==null&&!shards.Any(x=>x.Wanted.Length>0))throw new ArgumentException("Nenhum mod ativo encontrado; use o catálogo para editar a lista.");
        CheckSetup(setupBytes,shards.SelectMany(s=>s.Old.Concat(s.Wanted)).ToHashSet(StringComparer.Ordinal));
        var baseline=Hash(Utf8.GetBytes(System.Text.Json.JsonSerializer.Serialize(fingerprintParts)));
        if(expectedBaseline!=null&&baseline!=expectedBaseline)throw new IOException("Arquivos mudaram durante a edição; analise novamente.");
        if(proposed!=null){fingerprintParts.Add("edicao");fingerprintParts.AddRange(shards.Select(s=>Hash(s.Content)));}
        var fingerprint=Hash(Utf8.GetBytes(System.Text.Json.JsonSerializer.Serialize(fingerprintParts)));
        return new(source,target,install,exe,shards,fingerprint,inPlace,proposed!=null);
    }
    static bool IsWithin(string path,string parent){var a=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);var b=Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar);return a.Equals(b,StringComparison.OrdinalIgnoreCase)||a.StartsWith(b+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
    // Calculate required backup space before copying, without following reparse points.
    internal static long TreeBytes(string source){
        SafePath(source);
        long size=0;
        foreach(var f in Directory.GetFiles(source)){SafePath(f);size=checked(size+new FileInfo(f).Length);}
        foreach(var d in Directory.GetDirectories(source))size=checked(size+TreeBytes(d));
        return size;
    }
    internal static long RequiredBackupBytes(long data){
        if(data<0)throw new ArgumentOutOfRangeException(nameof(data));
        return checked(data+Math.Max(64L*1024*1024,data/20));
    }
    static void CheckBackupSpace(string target,string setup,string backup){
        long bytes=TreeBytes(target);
        SafePath(setup);
        if(File.Exists(setup))bytes=checked(bytes+new FileInfo(setup).Length);
        var driveRoot=Path.GetPathRoot(Path.GetFullPath(backup))??throw new IOException("Unidade de backup não identificada.");
        var drive=new DriveInfo(driveRoot);
        if(!drive.IsReady||drive.AvailableFreeSpace<RequiredBackupBytes(bytes))throw new IOException("Espaço insuficiente ou indisponível para o backup do cluster. Libere espaço antes de continuar.");
    }
    internal static void CopyTree(string source,string dest){
        SafePath(source);Directory.CreateDirectory(dest);
        foreach(var f in Directory.GetFiles(source)){SafePath(f);var d=System.IO.Path.Combine(dest,System.IO.Path.GetFileName(f));File.Copy(f,d);if(FileHash(f)!=FileHash(d))throw new IOException("Backup divergente.");}
        foreach(var dir in Directory.GetDirectories(source)){SafePath(dir);CopyTree(dir,System.IO.Path.Combine(dest,System.IO.Path.GetFileName(dir)));}
    }
    internal static void Atomic(string path,byte[] data){
        SafePath(path);var tmp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllBytes(tmp,data);File.Move(tmp,path,true);}finally{if(File.Exists(tmp))File.Delete(tmp);}
        if(FileHash(path)!=Hash(data))throw new IOException("Falha na conferência de "+path);
    }
    internal static void SafeTree(string path){SafePath(path);foreach(var f in Directory.GetFiles(path))SafePath(f);foreach(var d in Directory.GetDirectories(path))SafeTree(d);}
    public static Task<string> Apply(Plan plan,Action<string> log){
        using var gate=new Mutex(false,"Local\\GerenciadorDeDedicadoReset");
        if(!gate.WaitOne(0))throw new IOException("Outra manutenção em andamento.");
        // All work on this same thread: mutex ownership must not migrate across await.
        try{return Task.FromResult(Run(plan,log,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Gerenciador de Dedicado","Backups"),Offline,UpdateShard));}finally{gate.ReleaseMutex();}
    }
    internal static string Run(Plan plan,Action<string> log,string backupBase,Action offline,Action<Plan,ShardPlan,string,string,Action<string>> update){
        offline();InterruptedMaintenance.EnsureClear(backupBase);
        if(SteamUpdate.Pending(backupBase).Length>0)
            throw new IOException("Há uma atualização SteamCMD pendente. Retome-a antes de alterar os mods.");
        var source=LoadClusterDirectory(plan.Source.Path);
        var target=LoadClusterDirectory(plan.Target.Path);
        Plan Reanalyze(Plan p,Cluster a,Cluster b)=>p.InPlace?AnalyzeRepair(b,p.Install,p.Edited?p.Shards.ToDictionary(s=>s.Name,s=>s.Content):null):Analyze(a,b,p.Install);
        var fresh=Reanalyze(plan,source,target);
        if(fresh.Fingerprint!=plan.Fingerprint)throw new IOException("Arquivos mudaram; analise e confirme novamente.");
        var id=DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8];
        plan=fresh;
        var backup=Path.Combine(backupBase,id);
        var quarantine=System.IO.Path.Combine(plan.Install,"GDD-quarentena",id);
        SafePath(backup);SafePath(quarantine);
        if(IsWithin(backup,target.Path)||IsWithin(backup,source.Path)||IsWithin(backup,plan.Install))throw new IOException("Backup dentro do cluster ou da instalação.");
        var mods=System.IO.Path.Combine(plan.Install,"mods");var setup=System.IO.Path.Combine(mods,"dedicated_server_mods_setup.lua");
        var ugc=System.IO.Path.Combine(plan.Install,"ugc_mods",target.FolderName);
        SafePath(ugc);
        var affected=plan.Shards.SelectMany(s=>s.Old.Concat(s.Wanted)).Distinct().ToHashSet(StringComparer.Ordinal);
        var ugcRoot=Path.GetDirectoryName(ugc)!;SafePath(ugcRoot);
        if(Directory.Exists(ugcRoot)&&Directory.GetDirectories(ugcRoot).Any(d=>!string.Equals(Path.GetFileName(d),target.FolderName,StringComparison.OrdinalIgnoreCase)))throw new IOException("Há caches UGC de outros clusters nesta instalação; reset bloqueado.");
        if(Directory.Exists(ugc)&&Directory.GetDirectories(ugc).Any(d=>!target.Shards.Contains(Path.GetFileName(d),StringComparer.OrdinalIgnoreCase)))throw new IOException("Há outros shards UGC; reset bloqueado.");
        var caches=Directory.GetDirectories(mods,"workshop-*").Where(x=>affected.Contains(Path.GetFileName(x)[9..])).Concat(target.Shards.Select(s=>Path.Combine(ugc,s)).Where(Directory.Exists)).ToArray();
        foreach(var p in caches)SafeTree(p);
        // Snapshot every changed file and full destination before any mutation.
        CheckBackupSpace(target.Path,setup,backup);
        Directory.CreateDirectory(backup);log("Backup: "+backup);CopyTree(target.Path,System.IO.Path.Combine(backup,"cluster"));
        Recovery.Record(backup,plan);
        var originals=new Dictionary<string,byte[]?>();foreach(var p in plan.Shards.Select(s=>Override(target,s.Name)).Append(setup)){SafePath(p);originals[p]=File.Exists(p)?File.ReadAllBytes(p):null;}
        if(originals[setup] is byte[] old)Atomic(System.IO.Path.Combine(backup,"dedicated_server_mods_setup.lua"),old);
        Directory.CreateDirectory(quarantine);
        var moved=new List<(string Old,string New)>();
        var touched=new List<string>();
        var journal=System.IO.Path.Combine(backup,"recovery.txt");
        File.WriteAllLines(journal,new[]{"Origem: "+source.Path,"Destino: "+target.Path,"Instalação: "+plan.Install,"Quarentena: "+quarantine,"Setup anterior: "+(originals[setup]!=null?"presente":"ausente")}.Concat(caches.Select((x,i)=>x+" -> "+System.IO.Path.Combine(quarantine,i.ToString()))));
        offline();
        if(Reanalyze(plan,LoadClusterDirectory(source.Path),LoadClusterDirectory(target.Path)).Fingerprint!=plan.Fingerprint)throw new IOException("Arquivos mudaram durante o backup; nenhuma manutenção executada.");
        var newPaths=affected.Select(x=>Path.Combine(mods,"workshop-"+x)).Concat(target.Shards.Select(s=>Path.Combine(ugc,s))).Where(p=>!Directory.Exists(p)).ToArray();
        var nextSetup=Utf8.GetBytes("-- Gerenciador de Dedicado 1.0.0\n"+string.Join("\n",plan.Ids.Select(x=>"ServerModSetup(\""+x+"\")"))+"\n");
        InterruptedMaintenance.Begin(backup,plan,originals,nextSetup,caches,newPaths);
        try{
            foreach(var p in caches){var q=System.IO.Path.Combine(quarantine,moved.Count.ToString());Directory.Move(p,q);moved.Add((p,q));}
            foreach(var s in plan.Shards){var p=Override(target,s.Name);if(originals[p] is byte[] oldBytes&&oldBytes.AsSpan().SequenceEqual(s.Content))continue;touched.Add(p);Atomic(p,s.Content);}
            touched.Add(setup);Atomic(setup,nextSetup);
            foreach(var s in plan.Shards.OrderBy(s=>s.Name=="Master"?0:1)){
                offline();var ugcShard=Path.Combine(ugc,s.Name);SafePath(ugcShard);
                if(s.Wanted.Length==0&&plan.Ids.Length==0){log("Nenhum mod ativo: download dispensado em "+s.Name+".");continue;}
                update(plan,s,ugcShard,backup,log);
                foreach(var mod in s.Wanted){
                    var v1=Path.Combine(mods,"workshop-"+mod);
                    var v2=Path.Combine(ugcShard,"content","322330",mod);
                    var v2Direct=Path.Combine(ugcShard,"workshop-"+mod);
                    if(!HasMod(v1)&&!HasMod(v2)&&!HasMod(v2Direct))throw new IOException("Arquivos do mod "+mod+" não confirmados em "+s.Name+".");
                }
            }
            foreach(var warning in ModWarnings(plan))log(warning);
            File.AppendAllText(journal,"\nAtualizadores concluídos; confirme logs e teste mundo manualmente.");
            InterruptedMaintenance.Complete(backup,"completed");
            return backup;
        }catch(Exception error){
            log("Falha: "+error.Message+"; tentando restaurar.");
            var rollbackErrors=new List<string>();
            try{offline();}catch{throw new IOException("Processos ainda em execução. Não foi feita restauração automática. Backup: "+backup,error);}
            foreach(var p in touched.AsEnumerable().Reverse())try{if(originals[p] is byte[] b)Atomic(p,b);else if(File.Exists(p))File.Delete(p);}catch(Exception ex){rollbackErrors.Add(p+": "+ex.Message);}
            foreach(var p in newPaths)try{if(Directory.Exists(p)){SafeTree(p);Directory.Move(p,Path.Combine(quarantine,"novo-"+Guid.NewGuid().ToString("N")));}}catch(Exception ex){rollbackErrors.Add(p+": "+ex.Message);}
            foreach(var (p,q) in moved.AsEnumerable().Reverse())try{if(Directory.Exists(p)){SafeTree(p);Directory.Move(p,System.IO.Path.Combine(quarantine,"novo-"+Guid.NewGuid().ToString("N")));}Directory.Move(q,p);}catch(Exception ex){rollbackErrors.Add(p+": "+ex.Message);}
            File.AppendAllText(journal,"\nFalha: "+error.Message+"\n"+string.Join("\n",rollbackErrors));
            if(rollbackErrors.Count==0)InterruptedMaintenance.Complete(backup,"rolledback");
            throw new IOException("Manutenção interrompida. "+(rollbackErrors.Count==0?"Configurações e caches afetados foram restaurados; verifique o relatório e o mundo antes de iniciar.":"Restauração incompleta; consulte recovery.txt.")+" Backup: "+backup,error);
        }
    }
    internal static bool HasMod(string dir){
        SafePath(dir);
        if(!Directory.Exists(dir))return false;
        var info=Path.Combine(dir,"modinfo.lua");SafePath(info);
        return File.Exists(info)&&new FileInfo(info).Length>0;
    }
    public static IEnumerable<string> ModInfoPaths(Plan plan,string shard,string id){
        if(!plan.Target.Shards.Contains(shard)||!WorkshopId.TryParse(id,out var parsed)||parsed!=id)
            throw new ArgumentException("Mod ou shard inválido.");
        var ugc=Path.Combine(plan.Install,"ugc_mods",plan.Target.FolderName,shard);
        yield return Path.Combine(plan.Install,"mods","workshop-"+id,"modinfo.lua");
        yield return Path.Combine(ugc,"content","322330",id,"modinfo.lua");
        yield return Path.Combine(ugc,"workshop-"+id,"modinfo.lua");
    }
    public static IEnumerable<string> ModWarnings(Plan plan,IReadOnlyDictionary<string,string[]>? workshop=null){
        foreach(var shard in plan.Shards){
            var entries=Lua.Entries(Utf8.GetString(shard.Content).TrimStart('\uFEFF'));
            ModMetadata? Load(string id){
                foreach(var path in ModInfoPaths(plan,shard.Name,id)){
                    try{
                        SafePath(path);
                        if(File.Exists(path)&&new FileInfo(path).Length<=1_000_000){
                            var metadata=Lua.GetModMetadata(File.ReadAllText(path));
                            if(metadata!=null)return metadata;
                        }
                    }catch(IOException){}catch(UnauthorizedAccessException){}
                }
                return null;
            }
            foreach(var warning in DependencyAnalysis.Inspect(shard.Name,entries,Load,workshop))yield return warning;
        }
    }
    static void UpdateShard(Plan plan,ShardPlan s,string ugcShard,string backup,Action<string> log){
        // Some dedicated builds can enter world startup after finishing the mod download.
        // Keep every persisted game file for this process inside the backup directory.
        var storage=Path.Combine(backup,"workshop-run-"+s.Name);
        var cluster=Path.Combine(storage,"config","Cluster_1");
        var shard=Path.Combine(cluster,s.Name);
        SafePath(shard);
        Directory.CreateDirectory(shard);
        File.WriteAllText(Path.Combine(cluster,"cluster.ini"),"[NETWORK]\noffline_cluster = true\nlan_only_cluster = true\n");
        File.WriteAllText(Path.Combine(shard,"server.ini"),"[SHARD]\nis_master = "+(s.Name=="Master"?"true":"false")+"\n");

        var start=new ProcessStartInfo(plan.Exe){
            WorkingDirectory=Path.GetDirectoryName(plan.Exe)!,UseShellExecute=false,
            RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true
        };
        foreach(var arg in new[]{"-persistent_storage_root",storage,"-conf_dir","config","-cluster","Cluster_1",
            "-shard",s.Name,"-ugc_directory",ugcShard,"-only_update_server_mods"})start.ArgumentList.Add(arg);

        var outputPath=Path.Combine(backup,s.Name+"-update.log");
        var errorPath=Path.Combine(backup,s.Name+"-update.stderr.log");
        log("Atualizando "+s.Name+" em diretório temporário...");
        bool accepted;
        {
            using var outputFile=new FileStream(outputPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
            using var errorsFile=new FileStream(errorPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read);
            using var writer=new StreamWriter(outputFile){AutoFlush=true};
            using var proc=Process.Start(start)??throw new IOException("Falha ao iniciar atualizador.");
            var downloaded=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var output=CaptureWorkshopOutput(proc.StandardOutput,writer,downloaded);
            var errors=proc.StandardError.BaseStream.CopyToAsync(errorsFile);
            using var deadline=new CancellationTokenSource();
            var exited=proc.WaitForExitAsync();
            var timeout=Task.Delay(TimeSpan.FromMinutes(30),deadline.Token);
            var first=Task.WhenAny(exited,downloaded.Task,timeout,output,errors).GetAwaiter().GetResult();
            if((first==output&&!output.IsFaulted)||(first==errors&&!errors.IsFaulted))
                first=Task.WhenAny(exited,downloaded.Task,timeout).GetAwaiter().GetResult();

            bool stoppedAfterDownload=false;
            if(!proc.HasExited){
                stoppedAfterDownload=first==downloaded.Task;
                try{proc.Kill(true);}
                catch(InvalidOperationException) when(proc.HasExited){stoppedAfterDownload=false;}
                if(!proc.WaitForExit(10000))throw new IOException("Atualizador não encerrou.");
            }
            deadline.Cancel();
            if(!Task.WaitAll([output,errors],TimeSpan.FromSeconds(15)))
                throw new IOException("Fluxos de log não encerraram após o atualizador.");
            accepted=first!=timeout&&(stoppedAfterDownload||proc.ExitCode==0);
        }
        var failure=new Regex(@"(?i)(#ERROR:|failed to (download|install|update)|error downloading|workshop.*timed out)",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(2));
        if(!accepted
            ||File.ReadLines(outputPath).Concat(File.ReadLines(errorPath)).Any(line=>failure.IsMatch(line)))
            throw new IOException("Atualizador informou falha ou excedeu o limite em "+s.Name+".");
    }

    static async Task CaptureWorkshopOutput(StreamReader reader,StreamWriter writer,TaskCompletionSource downloaded){
        while(await reader.ReadLineAsync().ConfigureAwait(false) is string line){
            await writer.WriteLineAsync(line).ConfigureAwait(false);
            if(line.Contains("FinishDownloadingServerMods Complete!",StringComparison.Ordinal))
                downloaded.TrySetResult();
        }
    }
}
