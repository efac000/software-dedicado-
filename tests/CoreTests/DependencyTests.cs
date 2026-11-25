using Dedicado;
static class DependencyTests{
    public static void Run(){
        var selected=new Dictionary<string,bool>{{"42",true},{"43",false}};
        var workshop=new Dictionary<string,string[]>{{"44",new[]{"42","45"}}};
        ModMetadata? Load(string id)=>id switch{"42"=>new(false,true,new[]{"43"}),"43"=>new(false,true,new[]{"44"}),_=>null};
        var warnings=DependencyAnalysis.Inspect("Caves",selected,Load,workshop);
        foreach(var expected in new[]{"43 desativada","44 ausente","45 ausente","ciclo","45: dependências desconhecidas","Workshop, dependência publicada","compatibilidade entre pares desconhecida"})
            if(!warnings.Any(w=>w.Contains(expected,StringComparison.Ordinal)))throw new Exception("Dependência não identificada: "+expected);
        if(warnings.Any(w=>!w.StartsWith("Caves:")))throw new Exception("Shard divergente");
    }
}