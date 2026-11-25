using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
namespace Dedicado;
public static class SteamBootstrap {
    public const string OfficialUrl="https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
    const int Limit=32*1024*1024;
    public static string ManagedDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Gerenciador de Dedicado","Tools","SteamCMD");
    public static string? Find(){
        foreach(var p in new[]{Path.Combine(ManagedDirectory,"steamcmd.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"steamcmd","steamcmd.exe"),@"C:\steamcmd\steamcmd.exe"}){
            try{Engine.SafePath(p);if(File.Exists(p))return p;}catch{}
        }
        return null;
    }
    public static async Task<string> InstallAsync(Action<string> log,CancellationToken cancellation=default){
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        var destination=ManagedDirectory;Engine.SafePath(destination);
        if(Directory.Exists(destination)||File.Exists(destination))throw new IOException("A pasta gerenciada já existe. Selecione o SteamCMD existente ou revise essa pasta manualmente.");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});
        using var response=await http.GetAsync(OfficialUrl,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
        response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength is >Limit)throw new IOException("Pacote SteamCMD excede limite.");
        using var source=await response.Content.ReadAsStreamAsync(deadline.Token);
        using var archive=new MemoryStream();
        var buffer=new byte[8192];
        for(int n;(n=await source.ReadAsync(buffer.AsMemory(),deadline.Token))>0;){
            if(archive.Length+n>Limit)throw new IOException("Pacote SteamCMD excede limite.");
            archive.Write(buffer,0,n);
        }
        archive.Position=0;
        log("Pacote SteamCMD recebido por HTTPS. SHA-256 local: "+Convert.ToHexString(SHA256.HashData(archive)));
        archive.Position=0;deadline.Token.ThrowIfCancellationRequested();
        var path=InstallArchive(archive,destination);
        log("SteamCMD instalado na pasta gerenciada. Nenhum executável iniciado.");
        return path;
    }
    internal static string InstallArchive(Stream zip,string destination){
        if(!Path.IsPathFullyQualified(destination))throw new IOException("Destino inválido.");
        Engine.SafePath(destination);
        if(Directory.Exists(destination)||File.Exists(destination))throw new IOException("Destino existente; nenhuma sobrescrita.");
        var parent=Path.GetDirectoryName(destination)!;Directory.CreateDirectory(parent);Engine.SafePath(parent);
        // Valve's Windows bootstrap archive contains one executable. Fail closed if layout changes.
        using var package=new ZipArchive(zip,ZipArchiveMode.Read,true);
        if(package.Entries.Count!=1)throw new IOException("Estrutura inesperada no pacote SteamCMD.");
        var entry=package.Entries[0];
        if(!entry.FullName.Equals("steamcmd.exe",StringComparison.OrdinalIgnoreCase)||entry.Length<64||entry.Length>Limit)throw new IOException("Entrada SteamCMD inválida.");
        var stage=Path.Combine(parent,"steamcmd-stage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        try{
            var exe=Path.Combine(stage,"steamcmd.exe");
            using(var input=entry.Open())
            using(var output=new FileStream(exe,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                var buf=new byte[8192];long count=0;
                for(int n;(n=input.Read(buf))>0;){
                    count+=n;if(count>Limit||count>entry.Length)throw new IOException("Tamanho ZIP divergente.");
                    output.Write(buf,0,n);
                }
                if(count!=entry.Length)throw new IOException("Pacote incompleto.");
                output.Flush(true);
            }
            using(var check=File.OpenRead(exe)){
                using var reader=new BinaryReader(check);
                if(reader.ReadUInt16()!=0x5a4d)throw new IOException("SteamCMD não é um executável PE.");
                check.Position=0x3c;int offset=reader.ReadInt32();
                if(offset<64||offset>check.Length-4)throw new IOException("Cabeçalho PE inválido.");
                check.Position=offset;if(reader.ReadUInt32()!=0x00004550)throw new IOException("Assinatura PE inválida.");
            }
            Engine.SafePath(destination);Directory.Move(stage,destination);
            return Path.Combine(destination,"steamcmd.exe");
        }finally{if(Directory.Exists(stage)){Engine.SafePath(stage);Directory.Delete(stage,true);}}
    }
}
