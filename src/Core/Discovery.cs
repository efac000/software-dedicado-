using System.Text.RegularExpressions;
namespace Dedicado;
public sealed record DetectedInstall(string Path,string Source,string Executable){
    public override string ToString()=>Source+" | "+Path;
}
public sealed record DiscoveryResult(DetectedInstall[] Installs,Cluster[] Clusters,string[] SteamCmd);
public static class Discovery {
    const string DedicatedName="Don't Starve Together Dedicated Server";
    public static string[] LibraryPaths(string text){
        if(text.Length>2_000_000)return [];
        return Regex.Matches(text,"\"path\"\\s+\"((?:\\\\.|[^\"\\\\])+)\"",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1))
            .Select(m=>m.Groups[1].Value.Replace(@"\\",@"\")).Where(Path.IsPathFullyQualified)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(64).ToArray();
    }
    static IEnumerable<string> Paths(IEnumerable<string> roots){
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var root in roots.Take(256)){
            string path;
            try{
                if(string.IsNullOrWhiteSpace(root)||!Path.IsPathFullyQualified(root))continue;
                path=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
                if(OperatingSystem.IsWindows()&&path.StartsWith(@"\\",StringComparison.Ordinal))continue;
                Engine.SafePath(path);
                if(!Directory.Exists(path)||!seen.Add(path))continue;
            }catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException){continue;}
            yield return path;
        }
    }
    public static DiscoveryResult Scan(IEnumerable<string> steamRoots,IEnumerable<string> serverRoots,IEnumerable<string> clusterRoots,IEnumerable<string> steamCmdRoots){
        var libraries=new List<string>();
        foreach(var root in Paths(steamRoots)){
            libraries.Add(root);
            try{
                var file=Path.Combine(root,"steamapps","libraryfolders.vdf");
                Engine.SafePath(file);
                if(File.Exists(file)&&new FileInfo(file).Length<=2_000_000)libraries.AddRange(LibraryPaths(File.ReadAllText(file)));
            }catch(Exception e) when(e is IOException or UnauthorizedAccessException or RegexMatchTimeoutException){}
        }
        var installs=new Dictionary<string,DetectedInstall>(StringComparer.OrdinalIgnoreCase);
        void Add(string path,string source){
            try{
                if(!Directory.Exists(path)||Path.GetFileName(path).Equals("Don't Starve Together",StringComparison.OrdinalIgnoreCase))return;
                Engine.SafePath(path);
                var exe=Engine.FindExe(path);
                var mods=Path.Combine(path,"mods");
                Engine.SafePath(mods);
                if(!Directory.Exists(mods))return;
                installs.TryAdd(path,new(path,source,exe));
            }catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException){}
        }
        foreach(var library in Paths(libraries)){
            Add(Path.Combine(library,"steamapps","common",DedicatedName),"Biblioteca Steam");
        }
        foreach(var root in Paths(serverRoots.Concat(steamCmdRoots))){
            Add(root,"Instalação independente");
            foreach(var name in new[]{DedicatedName,"dst","server","dst-server","dstserver","dedicated","dedicado","DST","DSTServer","DST-Server"}){
                Add(Path.Combine(root,name),"Instalação independente");
            }
        }
        var clusters=new Dictionary<string,Cluster>(StringComparer.OrdinalIgnoreCase);
        foreach(var root in Paths(clusterRoots.Concat(installs.Keys.Select(p=>Path.Combine(p,"Klei","DoNotStarveTogether"))))){
            try{
                foreach(var cluster in Engine.Discover(root))clusters.TryAdd(cluster.Path,cluster);
            }catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException){}
        }
        var cmd=new List<string>();
        foreach(var root in Paths(steamCmdRoots)){
            try{
                var path=Path.Combine(root,"steamcmd.exe");Engine.SafePath(path);
                if(File.Exists(path))cmd.Add(path);
            }catch(Exception e) when(e is IOException or UnauthorizedAccessException){}
        }
        return new(installs.Values.OrderBy(x=>x.Path,StringComparer.OrdinalIgnoreCase).ToArray(),
            clusters.Values.OrderBy(x=>x.Path,StringComparer.OrdinalIgnoreCase).ToArray(),cmd.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}