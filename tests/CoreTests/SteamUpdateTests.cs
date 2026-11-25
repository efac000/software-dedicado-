using Dedicado;
static class SteamUpdateTests {
    public static void Run(){
        void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        var root=Path.Combine(Path.GetTempPath(),"GDD-steamcmd-"+Guid.NewGuid().ToString("N"));
        try{
            var install=Path.Combine(root,"dedicado com espaços");var cli=Path.Combine(root,"steamcmd","steamcmd.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(cli)!);File.WriteAllText(cli,"stub");
            Directory.CreateDirectory(Path.Combine(install,"bin64"));Directory.CreateDirectory(Path.Combine(install,"mods"));File.WriteAllText(Path.Combine(install,"bin64","dontstarve_dedicated_server_nullrenderer_x64.exe"),"stub");
            var conf=Path.Combine(root,"Klei","DST");var cluster=Path.Combine(conf,"Cluster_1");Directory.CreateDirectory(Path.Combine(cluster,"Master"));File.WriteAllText(Path.Combine(cluster,"cluster.ini"),"cluster_name = Teste");File.WriteAllText(Path.Combine(cluster,"Master","server.ini"),"[SHARD]");
            var target=Engine.Load(conf,"1");SteamUpdate.Check(cli,target,install);
            var args=SteamUpdate.Arguments(install,true);
            Check(args.SequenceEqual(new[]{"+force_install_dir",install,"+login","anonymous","+app_update","343050","validate","+quit"}),"Argumentos SteamCMD inválidos");
            Check(!SteamUpdate.Arguments(install,false).Contains("validate"),"Validação não solicitada");
            Check(SteamUpdate.Confirmed(new[]{"Success! App '343050' fully installed."}),"Sucesso não reconhecido");
            Check(!SteamUpdate.Confirmed(new[]{"Success! App '322330' fully installed."}),"App incorreto reconhecido");
            Check(!SteamUpdate.Confirmed(new[]{"Success! App '343050' fully installed.","ERROR! Failed to install app '343050'"}),"Falha ignorada");
            var setup=Path.Combine(install,"mods","dedicated_server_mods_setup.lua");
            var settings=Path.Combine(install,"mods","modsettings.lua");File.WriteAllText(setup,"ServerModSetup(\"42\")");
            var drive=new DriveInfo(Path.GetPathRoot(install)!);
            if(drive.AvailableFreeSpace>=5L*1024*1024*1024){
                var result=SteamUpdate.Run(cli,target,install,true,Path.Combine(root,"backup"),()=>{},(_,_,_,_)=>{File.WriteAllText(setup,"changed");File.WriteAllText(settings,"generated");},_=>{});
                Check(File.ReadAllText(setup)=="ServerModSetup(\"42\")"&&!File.Exists(settings),"Configuração não preservada");
                Check(File.Exists(Path.Combine(result,"cluster","cluster.ini")),"Backup incompleto");
                bool failed=false;
                try{SteamUpdate.Run(cli,target,install,false,Path.Combine(root,"backup"),()=>{},(_,_,_,_)=>{File.WriteAllText(setup,"corrompido");throw new IOException("falha simulada");},_=>{});}catch(IOException){failed=true;}
                Check(failed&&File.ReadAllText(setup)=="ServerModSetup(\"42\")","Falha não preservou configuração");
            }else Console.WriteLine("SteamCMD: teste de transação ignorado por espaço em disco do runner.");
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
