using System.Text.Json;
using System.Text.RegularExpressions;
namespace Dedicado;
public record InterruptedFile(string Key,string? Before,string After);
public record InterruptedCache(string Key,int? Slot,string? OriginalHash=null);
public record InterruptedOperation(string Target,string Install,string Number,InterruptedFile[] Files,InterruptedCache[] Caches);
public static class InterruptedMaintenance {
    const string Marker="maintenance.pending.json";
    static readonly Regex OperationId=new(@"\A[0-9]{8}-[0-9]{6}-[a-f0-9]{8}\z",RegexOptions.CultureInvariant);
    static bool Same(string a,string b)=>a.Equals(b,StringComparison.OrdinalIgnoreCase);
    static bool Under(string p,string root){
        p=Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar);root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        return Same(p,root)||p.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
    }
    static byte[]? Read(string p){
        Engine.SafePath(p);
        if(Directory.Exists(p))throw new IOException("Diretório inesperado no lugar de arquivo.");
        if(!File.Exists(p))return null;
        if(new FileInfo(p).Length>4_000_000)throw new IOException("Arquivo excede limite de recuperação.");
        return File.ReadAllBytes(p);
    }
    static string? Hash(byte[]? b)=>b==null?null:Engine.Hash(b);
    // Fingerprint the entire original tree (including empty directories and relative names).
    // A directory at the old path is not proof that it is the original cache.
    internal static string CacheHash(string root){
        Engine.SafePath(root);
        if(!Directory.Exists(root))throw new IOException("Cache original indisponível.");
        using var hash=System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        void Add(string value){
            var bytes=System.Text.Encoding.UTF8.GetBytes(value);
            Span<byte> size=stackalloc byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(size,bytes.Length);
            hash.AppendData(size);hash.AppendData(bytes);
        }
        void Walk(string dir){
            Engine.SafePath(dir);
            Add("D");Add(Path.GetRelativePath(root,dir).Replace('\\','/'));
            foreach(var file in Directory.GetFiles(dir).Order(StringComparer.Ordinal)){
                Engine.SafePath(file);Add("F");Add(Path.GetRelativePath(root,file).Replace('\\','/'));
                using var stream=File.OpenRead(file);
                hash.AppendData(System.Security.Cryptography.SHA256.HashData(stream));
            }
            foreach(var sub in Directory.GetDirectories(dir).Order(StringComparer.Ordinal))Walk(sub);
        }
        Walk(root);return Convert.ToHexString(hash.GetHashAndReset());
    }
    public static string[] Pending(string root){
        if(!Directory.Exists(root))return [];
        Engine.SafePath(root);
        return Directory.GetDirectories(root).Where(d=>{
            Engine.SafePath(d);var marker=Path.Combine(d,Marker);Engine.SafePath(marker);
            return File.Exists(marker)||Directory.Exists(marker);
        }).Order().ToArray();
    }
    internal static void EnsureClear(string root){
        if(Pending(root).Length>0)throw new IOException("Existe manutenção interrompida. Abra a Central de backups e revise a recuperação antes de iniciar outra operação.");
    }
    static string Key(Plan p,string path){
        if(Same(path,Path.Combine(p.Install,"mods","dedicated_server_mods_setup.lua")))return "setup";
        foreach(var s in p.Shards)if(Same(path,Path.Combine(p.Target.Path,s.Name,"modoverrides.lua")))return s.Name;
        throw new IOException("Arquivo fora do escopo.");
    }
    static string CacheKey(Plan p,string path){
        var name=Path.GetFileName(path);
        if(name.StartsWith("workshop-",StringComparison.Ordinal)&&WorkshopId.TryParse(name[9..],out var id)&&id==name[9..]&&Same(path,Path.Combine(p.Install,"mods",name)))return "v1:"+id;
        if(p.Target.Shards.Contains(name)&&Same(path,Path.Combine(p.Install,"ugc_mods",p.Target.FolderName,name)))return "ugc:"+name;
        throw new IOException("Cache fora do escopo.");
    }
    internal static void Begin(string backup,Plan plan,Dictionary<string,byte[]?> originals,byte[] nextSetup,string[] caches,string[] newPaths){
        var files=originals.Select(k=>{
            var key=Key(plan,k.Key);var original=key=="setup"?Path.Combine(backup,"dedicated_server_mods_setup.lua"):Path.Combine(backup,"cluster",key,"modoverrides.lua");
            if(Hash(Read(original))!=Hash(k.Value))throw new IOException("Snapshot de recuperação divergente.");
            var after=key=="setup"?nextSetup:plan.Shards.Single(s=>s.Name==key).Content;
            return new InterruptedFile(key,Hash(k.Value),Engine.Hash(after));
        }).ToArray();
        var entries=caches.Select((path,i)=>new InterruptedCache(CacheKey(plan,path),i,CacheHash(path))).Concat(newPaths.Select(path=>new InterruptedCache(CacheKey(plan,path),null))).ToArray();
        var op=new InterruptedOperation(plan.Target.Path,plan.Install,plan.Target.Number,files,entries);
        var marker=Path.Combine(backup,Marker);
        using var f=new FileStream(marker,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        JsonSerializer.Serialize(f,op);f.Flush(true);
    }
    internal static void Complete(string backup,string state){
        var marker=Path.Combine(backup,Marker);
        Engine.SafePath(marker);File.Move(marker,Path.Combine(backup,"maintenance."+state+".json"));
    }
    public static void Recover(string backup,Cluster target,string install,Action<string> log){
        using var gate=new Mutex(false,"Local\\GerenciadorDeDedicadoReset");
        bool acquired=false;
        try{
            try{acquired=gate.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
            if(!acquired)throw new IOException("Outra manutenção em andamento.");
            RecoverCore(backup,Recovery.DefaultRoot,target,install,Engine.Offline,log);
        }finally{if(acquired)gate.ReleaseMutex();}
    }
    internal static void RecoverCore(string backup,string root,Cluster target,string install,Action offline,Action<string> log){
        offline();
        backup=Path.GetFullPath(backup);install=Path.GetFullPath(install);
        var id=Path.GetFileName(backup);
        if(!OperationId.IsMatch(id)||!Same(Path.GetDirectoryName(backup)!,Path.GetFullPath(root)))throw new IOException("Backup fora da pasta de recuperação.");
        Engine.SafePath(backup);Engine.SafePath(install);Engine.SafePath(target.Path);
        if(Under(backup,install)||Under(backup,target.Path)||Under(target.Path,install)||Under(install,target.Path))throw new IOException("Caminhos sobrepostos.");
        var fresh=Engine.LoadClusterDirectory(target.Path);
        Engine.FindExe(install);
        var bytes=Read(Path.Combine(backup,Marker))??throw new IOException("Nenhuma operação pendente neste backup.");
        var op=JsonSerializer.Deserialize<InterruptedOperation>(bytes)??throw new IOException("Manifesto inválido.");
        if(!Same(op.Target,fresh.Path)||!Same(op.Install,install)||op.Number!=fresh.Number)throw new IOException("Cluster ou instalação divergente.");
        if(op.Files==null||op.Caches==null||!op.Files.Select(x=>x.Key).Order().SequenceEqual(fresh.Shards.Append("setup").Order())||op.Caches.Length>20000)throw new IOException("Manifesto fora do escopo.");
        var quarantine=Path.Combine(install,"GDD-quarentena",id);Engine.SafePath(quarantine);
        if(!Directory.Exists(quarantine))throw new IOException("Quarentena não localizada.");
        var files=new List<(string Path,byte[]? Original,string? Current)>();
        foreach(var f in op.Files){
            var path=f.Key=="setup"?Path.Combine(install,"mods","dedicated_server_mods_setup.lua"):Path.Combine(fresh.Path,f.Key,"modoverrides.lua");
            var saved=f.Key=="setup"?Path.Combine(backup,"dedicated_server_mods_setup.lua"):Path.Combine(backup,"cluster",f.Key,"modoverrides.lua");
            var original=Read(saved);var current=Hash(Read(path));
            if(Hash(original)!=f.Before||(current!=f.Before&&current!=f.After))throw new IOException("Arquivo alterado fora do plano: "+f.Key+". Nenhuma recuperação automática iniciada.");
            files.Add((path,original,current));
        }
        var caches=new List<(string Path,string? Slot,string? OriginalHash)>();
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var slots=new HashSet<int>();
        foreach(var c in op.Caches){
            string path;
            if(c.Key.StartsWith("v1:",StringComparison.Ordinal)&&WorkshopId.TryParse(c.Key[3..],out var wid)&&wid==c.Key[3..])path=Path.Combine(install,"mods","workshop-"+wid);
            else if(c.Key.StartsWith("ugc:",StringComparison.Ordinal)&&fresh.Shards.Contains(c.Key[4..]))path=Path.Combine(install,"ugc_mods",fresh.FolderName,c.Key[4..]);
            else throw new IOException("Cache fora do escopo.");
            if(!seen.Add(path)||c.Slot is int n&&(n<0||n>=op.Caches.Length||!slots.Add(n)))throw new IOException("Registro de cache duplicado ou inválido.");
            var slot=c.Slot is int index?Path.Combine(quarantine,index.ToString()):null;
            Engine.SafePath(path);if(slot!=null)Engine.SafePath(slot);
            if(File.Exists(path)||slot!=null&&File.Exists(slot))throw new IOException("Arquivo inesperado no caminho de cache.");
            if(slot!=null){
                if(c.OriginalHash==null||!Regex.IsMatch(c.OriginalHash,@"\A[A-F0-9]{64}\z"))
                    throw new IOException("Backup anterior sem identidade verificável do cache; recuperação manual necessária.");
                var original=Directory.Exists(slot)?slot:path;
                if(!Directory.Exists(original)||CacheHash(original)!=c.OriginalHash)
                    throw new IOException("Cache original ausente ou divergente; recuperação manual necessária: "+c.Key);
            }else if(c.OriginalHash!=null)throw new IOException("Identidade de cache inesperada.");
            if(Directory.Exists(path))Engine.SafeTree(path);
            caches.Add((path,slot,c.OriginalHash));
        }
        offline();
        // All paths and hashes are verified before writes. A retry is safe if interrupted again:
        // an original cache already returned has no remaining slot and is left untouched.
        foreach(var f in files){
            offline();if(Hash(Read(f.Path))!=f.Current)throw new IOException("Arquivo mudou durante recuperação.");
            if(Hash(f.Original)==f.Current)continue;
            if(f.Original!=null)Engine.Atomic(f.Path,f.Original);
            else if(File.Exists(f.Path))File.Move(f.Path,Path.Combine(quarantine,"arquivo-novo-"+Guid.NewGuid().ToString("N")));
        }
        foreach(var c in caches){
            offline();Engine.SafePath(c.Path);if(c.Slot!=null)Engine.SafePath(c.Slot);
            if(c.Slot!=null){
                var original=Directory.Exists(c.Slot)?c.Slot:c.Path;
                if(!Directory.Exists(original)||CacheHash(original)!=c.OriginalHash)
                    throw new IOException("Cache original mudou durante a recuperação.");
                if(!Directory.Exists(c.Slot))continue;
            }
            if(Directory.Exists(c.Path)){Engine.SafeTree(c.Path);Directory.Move(c.Path,Path.Combine(quarantine,"novo-"+Guid.NewGuid().ToString("N")));}
            if(c.Slot!=null){Directory.CreateDirectory(Path.GetDirectoryName(c.Path)!);Directory.Move(c.Slot,c.Path);}
        }
        Complete(backup,"recovered");log("Recuperação concluída; saves não foram revertidos. Teste o servidor antes de iniciar produção.");
    }
}
