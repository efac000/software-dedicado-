using System.Text;
using System.Text.Json;
namespace Dedicado;
public record SavedShard(string Name,string? Hash);
public record SavedConfiguration(string Target,string Install,SavedShard[] Shards);
public static class Recovery {
    static readonly UTF8Encoding Utf8=new(false,true);
    public static string DefaultRoot=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Gerenciador de Dedicado","Backups");
    internal static void Record(string backup,Plan plan){
        var shards=plan.Target.Shards.Select(s=>{
            var file=Path.Combine(backup,"cluster",s,"modoverrides.lua");Engine.SafePath(file);
            return new SavedShard(s,File.Exists(file)?Engine.Hash(Read(file)):null);
        }).ToArray();
        var data=new SavedConfiguration(plan.Target.Path,plan.Install,shards);
        var file=Path.Combine(backup,"configuration-recovery.json");Engine.SafePath(file);
        using var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        JsonSerializer.Serialize(output,data);output.Flush(true);
    }
    static byte[] Read(string path){
        Engine.SafePath(path);
        if(new FileInfo(path).Length>4_000_000)throw new IOException("Arquivo de recuperação grande demais.");
        return File.ReadAllBytes(path);
    }
    public static string[] List(string root){
        if(!Directory.Exists(root))return [];
        Engine.SafePath(root);
        return Directory.GetDirectories(root).Where(d=>{try{Engine.SafePath(d);return File.Exists(Path.Combine(d,"configuration-recovery.json"));}catch{return false;}}).OrderDescending().ToArray();
    }
    public static Plan Prepare(string backup,Cluster target,string install){
        Engine.SafePath(backup);
        var record=JsonSerializer.Deserialize<SavedConfiguration>(Read(Path.Combine(backup,"configuration-recovery.json")))??throw new IOException("Manifesto inválido.");
        if(!string.Equals(record.Target,target.Path,StringComparison.OrdinalIgnoreCase)||!string.Equals(record.Install,Path.GetFullPath(install),StringComparison.OrdinalIgnoreCase))throw new IOException("Backup pertence a outro cluster ou instalação.");
        if(record.Shards==null||!record.Shards.Select(s=>s.Name).Order().SequenceEqual(target.Shards.Order()))throw new IOException("Shards do backup não correspondem ao destino.");
        var baseline=Engine.AnalyzeEditBaseline(target,install);
        var proposed=new Dictionary<string,byte[]>();
        foreach(var shard in record.Shards){
            var file=Path.Combine(backup,"cluster",shard.Name,"modoverrides.lua");Engine.SafePath(file);
            if(shard.Hash==null){
                if(File.Exists(file))throw new IOException("Backup alterado: arquivo inesperado.");
                // Restore the semantic empty configuration, never delete arbitrary files.
                proposed.Add(shard.Name,Utf8.GetBytes("return {}"));
            }else{
                var data=Read(file);
                if(!string.Equals(Engine.Hash(data),shard.Hash,StringComparison.Ordinal))throw new IOException("Backup alterado ou corrompido: "+shard.Name);
                proposed.Add(shard.Name,data);
            }
        }
        return Engine.AnalyzeRepair(target,install,proposed,baseline.Fingerprint);
    }
}
