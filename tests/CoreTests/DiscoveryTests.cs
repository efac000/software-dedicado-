using Dedicado;
static class DiscoveryTests{
    public static void Run(){
        var root=Path.Combine(Path.GetTempPath(),"gdd-discovery-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try{
            string Install(string p){Directory.CreateDirectory(Path.Combine(p,"bin64"));Directory.CreateDirectory(Path.Combine(p,"mods"));File.WriteAllText(Path.Combine(p,"bin64","dontstarve_dedicated_server_nullrenderer_x64.exe"),"fixture");return p;}
            var steam=Path.Combine(root,"Steam");Directory.CreateDirectory(Path.Combine(steam,"steamapps"));
            var library=Path.Combine(root,"Library");Directory.CreateDirectory(library);
            File.WriteAllText(Path.Combine(steam,"steamapps","libraryfolders.vdf"),"\"libraryfolders\" { \"1\" { \"path\" \""+library.Replace("\\","\\\\")+"\" } }");
            var dedicated=Install(Path.Combine(library,"steamapps","common","Don't Starve Together Dedicated Server"));
            var client=Install(Path.Combine(library,"steamapps","common","Don't Starve Together"));
            var cmd=Path.Combine(root,"steamcmd");Directory.CreateDirectory(cmd);File.WriteAllText(Path.Combine(cmd,"steamcmd.exe"),"fixture");
            var independent=Install(Path.Combine(cmd,"dst"));
            var worlds=Path.Combine(root,"worlds");var world=Path.Combine(worlds,"Mundo QA");
            Directory.CreateDirectory(Path.Combine(world,"Master"));File.WriteAllText(Path.Combine(world,"cluster.ini"),"[NETWORK]\ncluster_name = QA");File.WriteAllText(Path.Combine(world,"Master","server.ini"),"[SHARD]");
            var before=Engine.Hash(File.ReadAllBytes(Path.Combine(world,"cluster.ini")));
            var result=Discovery.Scan([steam,library],[client,independent],[worlds,world],[cmd]);
            if(result.Installs.Length!=2||!result.Installs.Any(i=>i.Path==dedicated)||!result.Installs.Any(i=>i.Path==independent)||result.Installs.Any(i=>i.Path==client))throw new Exception("Dedicated detection");
            if(result.Clusters.Length!=1||result.Clusters[0].Path!=world||result.SteamCmd.Length!=1)throw new Exception("Cluster/SteamCMD detection");
            if(Engine.Hash(File.ReadAllBytes(Path.Combine(world,"cluster.ini")))!=before)throw new Exception("Discovery wrote configuration");
            if(Discovery.Scan([],[],[],[]).Installs.Length!=0)throw new Exception("Empty discovery");
        }finally{Directory.Delete(root,true);}
    }
}