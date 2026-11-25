using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dedicado;

public static partial class SteamUpdate {
    const string PendingFile="steamcmd.pending.json";
    sealed record PendingUpdate(string Target,string Install,Dictionary<string,string?> Hashes,Dictionary<string,string?>? ClusterHashes=null);
    static Dictionary<string,string?> ClusterHashes(Cluster target,string root){
        var names=target.Shards.SelectMany(s=>new[]{Path.Combine(s,"server.ini"),Path.Combine(s,"modoverrides.lua")}).Prepend("cluster.ini");
        var result=new Dictionary<string,string?>(StringComparer.Ordinal);
        foreach(var relative in names){
            var file=Path.Combine(root,relative);Engine.SafePath(file);
            if(Directory.Exists(file)||File.Exists(file)&&new FileInfo(file).Length>4_000_000)throw new IOException("Arquivo de configuração inválido: "+relative);
            result[relative]=File.Exists(file)?Hash(File.ReadAllBytes(file)):null;
        }
        return result;
    }
    static bool SameHashes(Dictionary<string,string?> a,Dictionary<string,string?> b)=>a.Count==b.Count&&a.All(pair=>b.TryGetValue(pair.Key,out var value)&&value==pair.Value);

    public static string[] Pending(string root){
        if(!Directory.Exists(root))return [];
        Engine.SafePath(root);
        return Directory.GetDirectories(root,"steamcmd-*").Where(folder=>{
            Engine.SafePath(folder);
            var marker=Path.Combine(folder,PendingFile);
            Engine.SafePath(marker);
            return File.Exists(marker)||Directory.Exists(marker);
        }).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    static string? Hash(byte[]? data)=>data==null?null:Engine.Hash(data);
    static void WritePending(string backup,Cluster target,string install,Dictionary<string,byte[]?> files){
        var marker=Path.Combine(backup,PendingFile);
        var temporary=Path.Combine(backup,PendingFile+".tmp");
        var clusterHashes=ClusterHashes(target,target.Path);
        if(!SameHashes(clusterHashes,ClusterHashes(target,Path.Combine(backup,"cluster"))))throw new IOException("Configurações do cluster mudaram durante o backup.");
        Engine.SafePath(marker);
        Engine.SafePath(temporary);
        using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
            JsonSerializer.Serialize(output,new PendingUpdate(target.Path,Path.GetFullPath(install),
                files.ToDictionary(pair=>Path.GetFileName(pair.Key),pair=>Hash(pair.Value),StringComparer.Ordinal),clusterHashes));
            output.Flush(true);
        }
        File.Move(temporary,marker);
    }

    static void CheckPending(string backup,Cluster target,string install,Dictionary<string,byte[]?> files){
        var marker=Path.Combine(backup,PendingFile);
        Engine.SafePath(marker);
        if(!Regex.IsMatch(Path.GetFileName(backup),@"\Asteamcmd-[0-9]{8}-[0-9]{6}-[a-f0-9]{8}\z")
            ||!File.Exists(marker)||new FileInfo(marker).Length>32768)
            throw new IOException("Registro de atualização pendente inválido: "+backup);
        PendingUpdate? record;
        try{record=JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllBytes(marker));}
        catch(JsonException error){throw new IOException("Registro de atualização pendente ilegível: "+backup,error);}
        if(record==null||!string.Equals(record.Target,target.Path,StringComparison.OrdinalIgnoreCase)
            ||!string.Equals(record.Install,Path.GetFullPath(install),StringComparison.OrdinalIgnoreCase)
            ||record.Hashes==null||record.Hashes.Count!=files.Count)
            throw new IOException("Há uma atualização SteamCMD pendente para outro destino ou com registro incompleto: "+backup);
        // Legacy pending journals still contain the full cluster backup. Compare it to
        // the live configuration; new journals additionally authenticate the snapshot.
        var saved=ClusterHashes(target,Path.Combine(backup,"cluster"));
        if(record.ClusterHashes!=null&&!SameHashes(record.ClusterHashes,saved)||!SameHashes(saved,ClusterHashes(target,target.Path)))
            throw new IOException("Configurações do cluster divergem do backup SteamCMD. Revise antes de retomar: "+backup);
        foreach(var (path,data) in files){
            var name=Path.GetFileName(path);
            if(!record.Hashes.TryGetValue(name,out var expected)||expected!=Hash(data))
                throw new IOException("A configuração diverge do backup SteamCMD. Revise "+name+" antes de retomar: "+backup);
            var copy=Path.Combine(backup,name);
            Engine.SafePath(copy);
            if(Directory.Exists(copy)||(expected==null?File.Exists(copy):
                !File.Exists(copy)||new FileInfo(copy).Length>4_000_000||Hash(File.ReadAllBytes(copy))!=expected))
                throw new IOException("O backup SteamCMD foi alterado: "+backup);
        }
    }
}
