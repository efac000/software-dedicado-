using Dedicado;

static class LuaOptionTests {
    public static void Run(){
        var declarations=Lua.DeclaredOptions("""
            configuration_options={
              {name="flag",label="Flag",default=true,options={{description="On",data=true},{description="Off",data=false}}},
              {name="rate",default=2,options={{description="Fast",data=2},{description="Slow",data=1}}}
            }
            """);
        if(declarations.Length!=2||declarations[0].Label!="Flag")throw new Exception("Declarações não importadas");
        var fresh=Lua.ImportDeclaredOptions("return {['workshop-42']={enabled=true}}","42",declarations);
        if(Lua.GetOptions(fresh,"42").Count!=2)throw new Exception("Opções de mod novo ausentes");
        var existing=Lua.ImportDeclaredOptions("return {['workshop-42']={enabled=true,configuration_options={flag=false}}}","42",declarations);
        if(Lua.GetOptions(existing,"42").Single(o=>o.Name=="flag").Value!="false"||Lua.GetOptions(existing,"42").Count!=2)throw new Exception("Opção anterior sobrescrita");
        if(Lua.DeclaredOptions("configuration_options=GetOptions()").Length!=0)throw new Exception("Lua dinâmico aceito");
        var metadata=Lua.GetModMetadata("""
            -- mod_dependencies={{workshop="workshop-100"}}
            name="Example"
            client_only_mod=true
            dst_compatible=false
            mod_dependencies={{workshop="workshop-42"},{workshop="workshop-43",local_mod="fallback"}}
            """);
        if(metadata==null||!metadata.ClientOnly||metadata.DstCompatible!=false||!metadata.Dependencies.SequenceEqual(new[]{"42","43"}))
            throw new Exception("Metadados declarativos não identificados");
        if(Lua.GetModMetadata("name = F()")!=null)
            throw new Exception("Metadados dinâmicos identificados sem confirmação");

        const string input = """
            return {
              ["workshop-42"]={enabled=true, configuration_options={flag=false, rate=2, title="Old", nested={flag=false}, -- keep
              }},
              ["workshop-43"]={enabled=false, configuration_options={flag=false}}
            }
            """;
        var fields = Lua.GetOptions(input,"42");
        if(fields.Select(x=>x.Name).SequenceEqual(new[]{"flag","rate","title"})==false)
            throw new Exception("Opções escalares incorretas");
        var edited=Lua.SetOptions(input,"42",new Dictionary<string,string>{{"flag","true"},{"rate","3.5"},{"title","New \"title\""}});
        var expected=input.Replace("flag=false, rate=2, title=\"Old\"","flag=true, rate=3.5, title=\"New \\\"title\\\"\"");
        if(edited!=expected)throw new Exception("Edição alterou conteúdo não selecionado");
        if(Lua.GetOptions(edited,"42").Single(x=>x.Name=="title").Value!="New \"title\"")throw new Exception("Texto não preservado");
        Reject(()=>Lua.SetOptions(input,"42",new Dictionary<string,string>{{"rate","2, flag=true"}}));
        Reject(()=>Lua.SetOptions(input,"42",new Dictionary<string,string>{{"nested","true"}}));
        if(Lua.GetOptions("return {['workshop-42']={enabled=true}}","42").Count!=0)throw new Exception("Opção inesperada");
    }
    static void Reject(Action edit){
        try{edit();}catch(FormatException){return;}
        throw new Exception("Edição inválida aceita");
    }
}
