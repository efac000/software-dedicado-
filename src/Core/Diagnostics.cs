using System.Text;
using System.Text.RegularExpressions;
namespace Dedicado;
public enum Severity { Error, Warning, Info }
public record Finding(Severity Level,string Code,string Scope,string Problem,string Advice);
public enum DiagnosticAction { None, RepairMods, ManageMods }
public static class Diagnostics {
    public static DiagnosticAction ActionFor(Finding finding)=>finding.Code switch {
        "MOD_CACHE" or "SETUP_MISSING" or "LOG_WORKSHOP" => DiagnosticAction.RepairMods,
        "OVERRIDES_MISSING" or "LOG_LUA" => DiagnosticAction.ManageMods,
        _=>DiagnosticAction.None
    };
    // A shortcut can only prepare a plan, not perform the maintenance.
    public static bool CanPrepare(IEnumerable<Finding> findings)=>!findings.Any(f=>f.Level==Severity.Error||f.Code is "SETUP_EXTRA" or "FORCED_MODS" or "SHARED_UGC" or "RUNNING" or "LOW_DISK" or "CUSTOM_CLUSTER");

    static readonly UTF8Encoding Utf8=new(false,true);
    static readonly (string Code,Regex Pattern,string Problem,string Advice)[] Patterns=[
        ("LOG_WORKSHOP",R(@"failed to (download|install|update)|error downloading|workshop.{0,100}(failed|timed out|timeout)|mod download.{0,50}(failed|error)"),"Registro de falha de download/atualização.","Confira a disponibilidade na Workshop, a conexão e use reinstalação após desligar o servidor."),
        ("LOG_LUA",R(@"LUA ERROR|stack traceback|error loading (mod|module)|error running (mod|lua)|attempt to (index|call|perform arithmetic on)"),"Indício de falha de Lua ou mod.","Examine o server_log.txt localmente e teste os mods suspeitos em um mundo descartável."),
        ("LOG_PORT",R(@"address already in use|failed to bind|unable to bind|port.{0,50}already in use"),"Indício de conflito de porta.","Confira portas dos shards e processos em execução; não altere portas automaticamente."),
        ("LOG_AUTH",R(@"invalid.{0,30}(cluster token|server token)|(?:cluster|server) token.{0,50}(invalid|missing|expired)|authentication failed"),"Indício de falha de autenticação.","Verifique o token no portal da Klei e a conectividade. Nunca compartilhe o token."),
        ("LOG_SHARD",R(@"failed to connect to master|master.{0,50}(connection refused|timed out)|shard.{0,50}(connection failed|disconnected)"),"Indício de problema de comunicação entre shards.","Confira Master/Caves, shard_enabled, endereço, porta interna e configuração de rede."),
        ("LOG_VERSION",R(@"version mismatch|different version|out of date|incompatible version"),"Indício de divergência de versão.","Confira a versão do jogo/dedicado e dos mods; atualize pelo Steam ou SteamCMD quando necessário."),
        ("LOG_SAVE",R(@"failed to (load|read|write) (save|snapshot)|save.{0,50}(corrupt|failed)|no space left on device|disk full"),"Indício de erro de save ou armazenamento.","Preserve os backups, confira espaço e permissões antes de tentar recuperar o mundo.")
    ];
    static Regex R(string s)=>new(s,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(200));
    // Bounded reads; neither tokens nor arbitrary log lines appear in the report.
    static byte[] Read(string file,int max=4_000_000){
        Engine.SafePath(file);
        using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        if(stream.Length>max)throw new IOException("Arquivo excede limite de inspeção.");
        using var data=new MemoryStream();var buffer=new byte[8192];
        for(int n;(n=stream.Read(buffer))>0;){if(data.Length+n>max)throw new IOException("Arquivo excede limite de inspeção.");data.Write(buffer,0,n);}
        return data.ToArray();
    }
    static Dictionary<string,string> Ini(string path){
        var entries=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);string section="";
        foreach(var raw in Utf8.GetString(Read(path)).TrimStart('\uFEFF').Split('\n')){
            var line=raw.Trim();if(line.Length==0||line.StartsWith('#')||line.StartsWith(';'))continue;
            if(line.StartsWith('[')&&line.EndsWith(']')){section=line[1..^1].Trim();continue;}
            int equals=line.IndexOf('=');if(equals<1)continue;
            var key=section+"."+line[..equals].Trim();
            if(!entries.TryAdd(key,line[(equals+1)..].Trim()))throw new FormatException("Chave INI duplicada.");
        }return entries;
    }
    internal static IEnumerable<Finding> ScanLog(string path,string scope){
        Engine.SafePath(path);
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        long start=Math.Max(0,stream.Length-262144);stream.Seek(start,SeekOrigin.Begin);
        byte[] bytes=new byte[262144];int count=0;
        while(count<bytes.Length){int n=stream.Read(bytes,count,bytes.Length-count);if(n==0)break;count+=n;}
        string tail=Encoding.UTF8.GetString(bytes,0,count);
        // Count distinct categories only; a historical match is not a current diagnosis.
        foreach(var rule in Patterns)if(rule.Pattern.IsMatch(tail))
            yield return new Finding(Severity.Warning,rule.Code,scope,rule.Problem+" (log recente/histórico; confirme se persiste)",rule.Advice);
    }
    public static Finding[] Inspect(string clusterPath,string install,Action<string>? progress=null){
        var result=new List<Finding>();
        void Add(Severity level,string code,string scope,string problem,string advice)=>result.Add(new(level,code,scope,problem,advice));
        void Probe(string scope,Action action){try{action();}catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or FormatException or RegexMatchTimeoutException or System.Security.SecurityException){Add(Severity.Error,"READ_FAILED",scope,"Não foi possível concluir a inspeção deste item.","Confira caminho, sintaxe, acesso e links de diretório. Nenhum arquivo foi alterado.");}}
        bool clusterOk=false,installOk=false;string clusterName="";
        progress?.Invoke("Verificando instalação e estrutura do cluster...");
        Probe("Instalação",()=>{
            if(string.IsNullOrWhiteSpace(install)||!Path.IsPathFullyQualified(install)||!Directory.Exists(install)){Add(Severity.Error,"INSTALL_MISSING","Instalação","Instalação dedicada não localizada.","Selecione a pasta que contém bin64/bin e mods.");return;}
            Engine.SafePath(install);
            try{Engine.FindExe(install);installOk=true;}catch(IOException){Add(Severity.Error,"EXE_MISSING","Instalação","Executável dedicado ausente ou caminho não suportado.","Confira a instalação pelo Steam/SteamCMD.");}
            if(!Directory.Exists(Path.Combine(install,"mods")))Add(Severity.Error,"MODS_DIR","Instalação","Pasta mods ausente.","Verifique a integridade da instalação do dedicado.");
        });
        Probe("Cluster",()=>{
            if(string.IsNullOrWhiteSpace(clusterPath)||!Path.IsPathFullyQualified(clusterPath)||!Directory.Exists(clusterPath)){Add(Severity.Error,"CLUSTER_MISSING","Cluster","Pasta do cluster não encontrada.","Selecione um cluster existente.");return;}
            Engine.SafePath(clusterPath);clusterOk=true;clusterName=Path.GetFileName(Path.TrimEndingDirectorySeparator(clusterPath));
            if(!Engine.ValidClusterFolder(clusterName))Add(Severity.Warning,"CUSTOM_CLUSTER","Cluster","Nome de pasta não suportado.","Use uma pasta com nome válido antes da manutenção.");
        });
        if(!clusterOk)return result.ToArray();
        var clusterIni=Path.Combine(clusterPath,"cluster.ini");
        var config=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        Probe("cluster.ini",()=>{
            if(!File.Exists(clusterIni)){Add(Severity.Error,"CLUSTER_INI","Cluster","cluster.ini ausente.","Restaure ou configure o cluster antes de iniciar.");return;}
            config=Ini(clusterIni);
            if(!config.TryGetValue("NETWORK.offline_cluster",out var offline)||!offline.Equals("true",StringComparison.OrdinalIgnoreCase)){
                var tokens=new[]{Path.Combine(clusterPath,"cluster_token.txt"),Path.Combine(Path.GetDirectoryName(clusterPath)!,"cluster_token.txt")};
                bool present=false;
                foreach(var token in tokens){Engine.SafePath(token);if(File.Exists(token)&&new FileInfo(token).Length>0)present=true;}
                if(!present)Add(Severity.Warning,"TOKEN_MISSING","Cluster","Nenhum arquivo de token não vazio encontrado no cluster ou na pasta de configuração.","Se o servidor for online, obtenha um token pelo portal da Klei. Um arquivo presente não comprova validade.");
            }
        });
        var shards=new[]{"Master","Caves"}.Where(s=>Directory.Exists(Path.Combine(clusterPath,s))).ToArray();
        if(!shards.Contains("Master"))Add(Severity.Error,"MASTER_MISSING","Cluster","Pasta Master ausente.","Confira o diretório correto ou restaure a estrutura do servidor.");
        if(config.TryGetValue("SHARD.shard_enabled",out var enabled)&&enabled.Equals("true",StringComparison.OrdinalIgnoreCase)&&!shards.Contains("Caves"))Add(Severity.Warning,"CAVES_MISSING","Cluster","Shards habilitados, mas Caves não localizado.","Confira a topologia; outros shards personalizados não são inspecionados.");
        var ids=new HashSet<string>(StringComparer.Ordinal);var ports=new Dictionary<int,string>();
        bool completeMods=true;
        foreach(var shard in shards){
            progress?.Invoke("Inspecionando "+shard+"...");
            var dir=Path.Combine(clusterPath,shard);
            Probe(shard+"/server.ini",()=>{
                var file=Path.Combine(dir,"server.ini");
                if(!File.Exists(file)){Add(Severity.Error,"SERVER_INI",shard,"server.ini ausente.","Restaure a configuração deste shard.");return;}
                var ini=Ini(file);
                if(ini.TryGetValue("SHARD.is_master",out var master)&&bool.TryParse(master,out var isMaster)&&isMaster!=(shard=="Master"))Add(Severity.Warning,"SHARD_ROLE",shard,"Papel is_master não corresponde ao nome convencional do shard.","Confira a configuração e a topologia antes de iniciar.");
                foreach(var key in new[]{"NETWORK.server_port","STEAM.master_server_port","STEAM.authentication_port"}){
                    if(!ini.TryGetValue(key,out var value))continue;
                    if(!int.TryParse(value,out int port)||port<1||port>65535){Add(Severity.Error,"PORT_INVALID",shard,"Porta fora do intervalo válido: "+key+".","Configure uma porta entre 1 e 65535.");continue;}
                    if(key=="NETWORK.server_port"){
                        if(ports.TryGetValue(port,out var other))Add(Severity.Warning,"PORT_DUPLICATE",shard,"server_port igual ao de "+other+": "+port+".","Confirme portas distintas para shards no mesmo endereço; parâmetros de lançamento podem sobrescrever o INI.");
                        else ports[port]=shard;
                    }
                }
            });
            Probe(shard+"/modoverrides.lua",()=>{
                var file=Path.Combine(dir,"modoverrides.lua");
                if(!File.Exists(file)){Add(Severity.Info,"OVERRIDES_MISSING",shard,"modoverrides.lua ausente; nenhum mod declarado neste arquivo.","Use o gerenciador visual se desejar adicionar mods.");return;}
                Dictionary<string,bool> active;
                try{active=Lua.Active(Utf8.GetString(Read(file)).TrimStart('\uFEFF'));}catch{completeMods=false;throw;}
                foreach(var id in active.Keys){
                    ids.Add(id);if(!installOk)continue;
                    var ugc=Path.Combine(install,"ugc_mods",clusterName,shard);
                    if(!Engine.HasMod(Path.Combine(install,"mods","workshop-"+id))&&!Engine.HasMod(Path.Combine(ugc,"content","322330",id))&&!Engine.HasMod(Path.Combine(ugc,"workshop-"+id)))
                        Add(Severity.Warning,"MOD_CACHE",shard,"Mod "+id+" sem modinfo.lua válido nos caches padrão conhecidos.","Confira UGC personalizado; se usar caminhos padrão, reinstale o mod pelo aplicativo.");
                }
            });
            Probe(shard+"/server_log.txt",()=>{
                var file=Path.Combine(dir,"server_log.txt");if(!File.Exists(file)){Add(Severity.Info,"LOG_MISSING",shard,"Log atual não encontrado.","Inicie o dedicado somente após revisar os problemas; o log pode estar em outro caminho.");return;}
                result.AddRange(ScanLog(file,shard+"/server_log.txt"));
            });
        }
        if(installOk)Probe("Setup global",()=>{
            var file=Path.Combine(install,"mods","dedicated_server_mods_setup.lua");
            var registered=Engine.SetupIds(File.Exists(file)?Read(file):null);
            foreach(var id in ids.Except(registered))Add(Severity.Warning,"SETUP_MISSING","Setup global","Mod ativo "+id+" não registrado no setup global.","Reinstale os mods pelo aplicativo em instalação exclusiva.");
            if(completeMods)foreach(var id in registered.Except(ids))Add(Severity.Warning,"SETUP_EXTRA","Setup global","Registro "+id+" não consta nos mods ativos inspecionados.","Pode pertencer a outro mundo; revise antes de limpar ou alterar.");
            else Add(Severity.Info,"SETUP_INCOMPLETE","Setup global","A comparação dos registros extras é inconclusiva porque um arquivo Lua não pôde ser lido.","Corrija a configuração inválida antes de comparar ou limpar.");
            var settings=Path.Combine(install,"mods","modsettings.lua");
            if(File.Exists(settings)&&R(@"(?m)^\s*(ForceEnableMod|ForceDisableMod)\s*\(").IsMatch(Utf8.GetString(Read(settings))))Add(Severity.Warning,"FORCED_MODS","Instalação","Há regras de ativação/desativação forçada.","Revise modsettings.lua; o reset automático bloqueia esta situação.");
            var ugcRoot=Path.Combine(install,"ugc_mods");Engine.SafePath(ugcRoot);
            if(Directory.Exists(ugcRoot)&&Directory.GetDirectories(ugcRoot).Any(d=>!Path.GetFileName(d).Equals(clusterName,StringComparison.OrdinalIgnoreCase)))Add(Severity.Warning,"SHARED_UGC","Instalação","Outras pastas de cluster no cache UGC.","Não execute reset automático nesta instalação compartilhada.");
        });
        Probe("Processos",()=>{try{Engine.Offline();}catch(InvalidOperationException){Add(Severity.Warning,"RUNNING","Computador","DST ou dedicado em execução.","Diagnóstico pode ser feito em leitura, mas desligue o jogo, os shards e o reinício automático antes de manutenção.");}});
        Probe("Disco",()=>{
            var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            foreach(var (label,path) in new[]{("Backup local",local),("Instalação",install)}){
                if(string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path))continue;
                var root=Path.GetPathRoot(path);if(string.IsNullOrEmpty(root))continue;
                var drive=new DriveInfo(root);
                if(drive.IsReady&&drive.AvailableFreeSpace<1_073_741_824)Add(Severity.Warning,"LOW_DISK",label,"Menos de 1 GiB de espaço livre.","Libere espaço e confira o tamanho real de saves e downloads antes do backup.");
            }
        });
        if(result.All(x=>x.Level==Severity.Info))Add(Severity.Info,"LIMITED_OK","Resumo","Nenhum erro ou aviso identificado pelas regras aplicadas.","Não garante funcionamento: logs são limitados, dependências e rede exigem teste real.");
        return result.OrderBy(x=>x.Level).ThenBy(x=>x.Scope).ThenBy(x=>x.Code).ToArray();
    }
}
