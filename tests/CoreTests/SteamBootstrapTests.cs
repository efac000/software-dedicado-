using System.IO.Compression;
using Dedicado;
static class SteamBootstrapTests {
    public static void Run(){
        var root=Path.Combine(Path.GetTempPath(),"GDD-bootstrap-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        byte[] pe=new byte[128];pe[0]=0x4d;pe[1]=0x5a;pe[0x3c]=64;pe[64]=0x50;pe[65]=0x45;
        MemoryStream Archive(params (string Name,byte[] Data)[] files){
            var s=new MemoryStream();
            using(var z=new ZipArchive(s,ZipArchiveMode.Create,true))foreach(var f in files){using var output=z.CreateEntry(f.Name).Open();output.Write(f.Data);}
            s.Position=0;return s;
        }
        void Reject(MemoryStream archive,string dest){using(archive){try{SteamBootstrap.InstallArchive(archive,dest);}catch(IOException){return;}catch(InvalidDataException){return;}throw new Exception("Pacote inválido aceito");}}
        try{
            var good=Path.Combine(root,"good");
            using(var zip=Archive(("steamcmd.exe",pe))){
                var exe=SteamBootstrap.InstallArchive(zip,good);
                if(!File.ReadAllBytes(exe).SequenceEqual(pe))throw new Exception("Bootstrap alterou bytes");
            }
            Reject(Archive(("steamcmd.exe",pe)),good);
            Reject(Archive(("../steamcmd.exe",pe)),Path.Combine(root,"traversal"));
            Reject(Archive(("steamcmd.exe",pe),("extra.dll",pe)),Path.Combine(root,"multiple"));
            Reject(Archive(("steamcmd.exe",new byte[128])),Path.Combine(root,"invalid"));
            if(Directory.GetDirectories(root).Any(x=>Path.GetFileName(x).StartsWith("steamcmd-stage-")))throw new Exception("Pasta temporária não removida");
            if(File.Exists(Path.Combine(root,"steamcmd.exe")))throw new Exception("Extração fora do destino");
        }finally{Directory.Delete(root,true);}
    }
}
