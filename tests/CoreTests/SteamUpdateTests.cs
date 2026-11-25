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
            Check(args.SequenceEqual(new[]{"+force_install_dir",install,"+login","anonymous","+app_info_update","1","+app_update","343050","validate","+quit"}),"Argumentos SteamCMD inválidos");
            Check(!SteamUpdate.Arguments(install,false).Contains("validate"),"Validação não solicitada");
            Check(SteamUpdate.Confirmed(new[]{"Success! App '343050' fully installed."}),"Sucesso não reconhecido");
            Check(!SteamUpdate.Confirmed(new[]{"Success! App '322330' fully installed."}),"App incorreto reconhecido");
            Check(!SteamUpdate.Confirmed(new[]{"Success! App '343050' fully installed.","ERROR! Failed to install app '343050'"}),"Falha ignorada");
            Check(!SteamUpdate.Confirmed(new[]{"Timed out waiting for update to start, bailing.","Success! App '343050' fully installed."}),"Timeout ignorado pela confirmação");
            var manifest="""
                "AppState"
                {
                  "appid" "343050"
                  "StateFlags" "4"
                  "UpdateResult" "0"
                  "buildid" "26797487"
                }
                """;
            Check(SteamUpdate.InstalledManifest(manifest),"Manifesto válido não reconhecido");
            Check(!SteamUpdate.InstalledManifest(manifest.Replace("\"4\"","\"6\"")),"Manifesto incompleto aceito");
            Check(!SteamUpdate.InstalledManifest(manifest.Replace("343050","322330")),"Manifesto de outro aplicativo aceito");
            int attempts=0;
            SteamUpdate.ConfirmUpdate(false,(verify,label)=>{
                attempts++;
                if(attempts==1){Check(!verify&&label=="steamcmd","Primeira tentativa inesperada");return (0,new[]{"Timed out waiting for update to start, bailing.","Success! App '343050' fully installed."},Array.Empty<string>());}
                Check(verify&&label=="steamcmd-validate-retry","Nova validação não isolada");
                return (0,new[]{"Success! App '343050' fully installed."},Array.Empty<string>());
            });
            Check(attempts==2,"Timeout contraditório aceito sem nova validação");
            attempts=0;
            bool rejected=false;
            try{SteamUpdate.ConfirmUpdate(false,(_,_)=>{attempts++;return (0,new[]{"Success! App '343050' fully installed."},new[]{"Timed out waiting for update to start"});});}catch(IOException){rejected=true;}
            Check(rejected&&attempts==2,"Timeout persistente aceito ou repetição ilimitada");
            attempts=0;rejected=false;
            try{SteamUpdate.ConfirmUpdate(false,(_,_)=>{attempts++;return (0,new[]{"Success! App '343050' fully installed."},new[]{"ERROR! Failed to install app '343050'"});});}catch(IOException){rejected=true;}
            Check(rejected&&attempts==1,"Erro definitivo ignorado ou repetido");
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
                var backupRoot=Path.Combine(root,"backup");
                Check(SteamUpdate.Pending(backupRoot).Length==1,"Falha não registrou recuperação");
                File.AppendAllText(setup," -- mudança externa");
                bool invoked=false;
                try{SteamUpdate.Run(cli,target,install,false,backupRoot,()=>{},(_,_,_,_)=>invoked=true,_=>{});}
                catch(IOException){}
                Check(!invoked&&File.ReadAllText(setup).EndsWith("mudança externa"),"Retomada sobrescreveu alteração externa");
                File.WriteAllText(setup,"ServerModSetup(\"42\")");
                var clusterIni=Path.Combine(cluster,"cluster.ini");var clusterBefore=File.ReadAllBytes(clusterIni);
                File.AppendAllText(clusterIni,"\n-- divergente");invoked=false;
                try{SteamUpdate.Run(cli,target,install,false,backupRoot,()=>{},(_,_,_,_)=>invoked=true,_=>{});}catch(IOException){}
                Check(!invoked&&SteamUpdate.Pending(backupRoot).Length==1,"Retomada aceitou divergência no cluster");
                File.WriteAllBytes(clusterIni,clusterBefore);
                SteamUpdate.Run(cli,target,install,false,backupRoot,()=>{},(_,_,verify,_)=>{Check(verify,"Retomada sem validação");invoked=true;},_=>{});
                Check(invoked&&SteamUpdate.Pending(backupRoot).Length==0,"Retomada não encerrou recuperação");

            }else Console.WriteLine("SteamCMD: teste de transação ignorado por espaço em disco do runner.");
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
