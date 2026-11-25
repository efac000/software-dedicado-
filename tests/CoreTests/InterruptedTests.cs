using Dedicado;
static class InterruptedTests{
    public static void Run(){
        var root=Path.Combine(Path.GetTempPath(),"GDD-interrupt-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        void Check(bool b,string message){if(!b)throw new Exception(message);}
        void Reject(Action action){try{action();}catch(IOException){return;}throw new Exception("Recuperação insegura aceita");}
        try{
            var conf=Path.Combine(root,"Klei");var cluster=Path.Combine(conf,"Cluster_1");var master=Path.Combine(cluster,"Master");Directory.CreateDirectory(master);File.WriteAllText(Path.Combine(cluster,"cluster.ini"),"cluster_name = teste");File.WriteAllText(Path.Combine(master,"server.ini"),"[SHARD]");
            var overrides=Path.Combine(master,"modoverrides.lua");var original=System.Text.Encoding.UTF8.GetBytes("return {['workshop-42']={enabled=true}}");File.WriteAllBytes(overrides,original);
            var install=Path.Combine(root,"dedicado");Directory.CreateDirectory(Path.Combine(install,"mods"));Directory.CreateDirectory(Path.Combine(install,"bin64"));File.WriteAllText(Path.Combine(install,"bin64","dontstarve_dedicated_server_nullrenderer_x64.exe"),"stub");
            var setup=Path.Combine(install,"mods","dedicated_server_mods_setup.lua");File.WriteAllText(setup,"ServerModSetup(\"42\")");
            var target=Engine.Load(conf,"1");var changed=System.Text.Encoding.UTF8.GetBytes("return {['workshop-43']={enabled=true}}");var plan=Engine.AnalyzeRepair(target,install,new Dictionary<string,byte[]>{{"Master",changed}});
            var backupRoot=Path.Combine(root,"backup");var backup=Path.Combine(backupRoot,"20261125-123456-abcdef12");
            var q=Path.Combine(install,"GDD-quarentena",Path.GetFileName(backup));Directory.CreateDirectory(q);Engine.CopyTree(cluster,Path.Combine(backup,"cluster"));File.Copy(setup,Path.Combine(backup,"dedicated_server_mods_setup.lua"));
            var oldCache=Path.Combine(install,"mods","workshop-42");var newCache=Path.Combine(install,"mods","workshop-43");Directory.CreateDirectory(oldCache);File.WriteAllText(Path.Combine(oldCache,"old"),"old");
            var next=System.Text.Encoding.UTF8.GetBytes("ServerModSetup(\"43\")");
            InterruptedMaintenance.Begin(backup,plan,new Dictionary<string,byte[]?>{{overrides,original},{setup,File.ReadAllBytes(setup)}},next,new[]{oldCache},new[]{newCache});
            Check(InterruptedMaintenance.Pending(backupRoot).Length==1,"Marcador não persistiu");Reject(()=>InterruptedMaintenance.EnsureClear(backupRoot));
            Directory.Move(oldCache,Path.Combine(q,"0"));Directory.CreateDirectory(newCache);File.WriteAllText(Path.Combine(newCache,"new"),"new");
            File.WriteAllBytes(overrides,changed);File.WriteAllBytes(setup,next);File.WriteAllText(Path.Combine(master,"save-test"),"save atual");
            File.WriteAllText(overrides,"return {}");Reject(()=>InterruptedMaintenance.RecoverCore(backup,backupRoot,target,install,()=>{},_=>{}));
            Check(Directory.Exists(newCache)&&Directory.Exists(Path.Combine(q,"0")),"Recuperação alterou arquivos diante de divergência");
            File.WriteAllBytes(overrides,changed);
            InterruptedMaintenance.RecoverCore(backup,backupRoot,target,install,()=>{},_=>{});
            Check(File.ReadAllBytes(overrides).SequenceEqual(original)&&File.ReadAllText(setup).Contains("42"),"Configurações não restauradas");
            Check(File.Exists(Path.Combine(oldCache,"old"))&&!Directory.Exists(newCache),"Caches não recuperados");
            Check(File.ReadAllText(Path.Combine(master,"save-test"))=="save atual","Save alterado");
            Check(InterruptedMaintenance.Pending(backupRoot).Length==0,"Marcador pendente após recuperação");
            Reject(()=>InterruptedMaintenance.RecoverCore(backup,backupRoot,target,install,()=>{},_=>{}));
        }finally{Directory.Delete(root,true);}
    }
}
