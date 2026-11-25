using Dedicado;
static void Check(bool b,string message){if(!b)throw new Exception(message);}
static void Reject(Action f){try{f();}catch(ArgumentException){return;}catch(FormatException){return;}catch(IOException){return;}throw new Exception("Entrada inválida aceita");}
foreach(var x in new[]{"","0","-1","1a2","1.5"," 1","1 ","01","١","9999999999","1\n"})Check(!Engine.ValidNumber(x),"Número inválido: "+x);
Check(Engine.ValidNumber("1")&&Engine.ValidNumber("12"),"Número válido rejeitado");
var mods=Lua.Active("""return { ["workshop-123"]={configuration_options={enabled=false,nested={x="}"},items={1,2}},enabled=true}, ["workshop-456"]={enabled=false}, }""");
Check(mods.Keys.SequenceEqual(new[]{"123"}),"Leitura incorreta");
Check(Lua.Active("return { -- comentario\n ['workshop-42'] = { enabled=true } }").ContainsKey("42"),"Comentários/aspas");
Check(Lua.Active("return { --[=[ comentario ]=]\n ['workshop-42'] = { enabled=true, configuration_options={ text=[=[literal }]=] } } }").ContainsKey("42"),"Comentário longo");
Reject(()=>Lua.Active("return os.execute('x')"));Reject(()=>Lua.Active("return { ['workshop-1']={enabled=foo()} }"));Reject(()=>Lua.Active("return { ['local']={enabled=true} }"));Reject(()=>Lua.Active("return { ['workshop-1']={enabled=true,enabled=false} }"));Reject(()=>Lua.Active("return {} print('x')"));Reject(()=>Lua.Active("return { ['workshop-99999999999999999999']={enabled=true} }"));
var root=Path.Combine(Path.GetTempPath(),"GDD-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try{
    var conf=Path.Combine(root,"Klei","DST");Directory.CreateDirectory(conf);
    void World(string n,string data){var p=Path.Combine(conf,"Cluster_"+n);Directory.CreateDirectory(Path.Combine(p,"Master"));File.WriteAllText(Path.Combine(p,"cluster.ini"),"cluster_name = Teste "+n);File.WriteAllText(Path.Combine(p,"Master","server.ini"),"[SHARD]\nis_master = true");File.WriteAllText(Path.Combine(p,"Master","modoverrides.lua"),data);}
    World("1","return { ['workshop-42']={enabled=true} }");World("2","return { ['workshop-99']={enabled=true} }");
    Reject(()=>Engine.Load(conf,"9"));Reject(()=>Engine.Load(conf,"1a"));
    var install=Path.Combine(root,"install");Directory.CreateDirectory(Path.Combine(install,"bin64"));Directory.CreateDirectory(Path.Combine(install,"mods"));File.WriteAllText(Path.Combine(install,"bin64","dontstarve_dedicated_server_nullrenderer_x64.exe"),"fake");
    var s=Engine.Load(conf,"1");var t=Engine.Load(conf,"2");
    Reject(()=>Engine.Analyze(s,s,install));var plan=Engine.Analyze(s,t,install);
    Check(plan.Ids.SequenceEqual(new[]{"42"}),"IDs");
    Check(plan.Shards[0].Old.SequenceEqual(new[]{"99"}),"Mods anteriores");
    Check(File.ReadAllText(Path.Combine(t.Path,"Master","modoverrides.lua")).Contains("99"),"Simulação modificou destino");
    World("1","return { ['workshop-43']={enabled=true} }");
    Check(plan.Fingerprint!=Engine.Analyze(Engine.Load(conf,"1"),t,install).Fingerprint,"Plano não invalidado");
}finally{Directory.Delete(root,true);}
Console.WriteLine("Testes de parser, entradas, clusters, simulação e fingerprint passaram.");
