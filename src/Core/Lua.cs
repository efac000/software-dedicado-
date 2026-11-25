using System.Text.RegularExpressions;
namespace Dedicado;
public static class Lua {
    // Accept literal tables only; never load or evaluate Lua.
    static readonly Regex Lex = new("""\G(?:\s+|--\[(?<eq>=*)\[.*?\]\k<eq>\]|--(?!\[=*\[)[^\r\n]*|\[(?<long>=*)\[.*?\]\k<long>\]|"(?:\\[\s\S]|[^"\\\r\n])*"|'(?:\\[\s\S]|[^'\\\r\n])*'|[A-Za-z_][A-Za-z_0-9]*|-?(?:0[xX][0-9a-fA-F]+|(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?)|[{}\[\]=,;])""",RegexOptions.Singleline|RegexOptions.CultureInvariant,TimeSpan.FromSeconds(3));
    static Parser Parse(string text,out Dictionary<string,object?> table,out List<int> positions) {
        if(text.Length>4_000_000) throw new FormatException("Arquivo Lua grande demais.");
        var tokens=new List<string>(); positions=new List<int>(); int p=0;
        while(p<text.Length) {
            var m=Lex.Match(text,p);
            if(!m.Success || m.Length==0) throw new FormatException("Lua não literal ou inválido na posição "+p);
            var t=m.Value; p+=m.Length;
            if(!char.IsWhiteSpace(t[0])&&!t.StartsWith("--")){tokens.Add(t);positions.Add(m.Index);}
        }
        var parser=new Parser(tokens); parser.Expect("return"); table=parser.Table(0);
        if(parser.Peek==";") parser.Take();
        if(parser.Peek!=null) throw new FormatException("Código Lua adicional não suportado.");
        return parser;
    }
    public static Dictionary<string,bool> Active(string text)=>Entries(text).Where(x=>x.Value).ToDictionary(x=>x.Key,x=>true,StringComparer.Ordinal);
    public static Dictionary<string,bool> Entries(string text){
        Parse(text,out var table,out _);
        var result=new Dictionary<string,bool>(StringComparer.Ordinal);
        foreach(var (key,v) in table) {
            if(v is not Dictionary<string,object?> entry || !entry.TryGetValue("enabled",out var enabled) || enabled is not bool b)
                throw new FormatException("Entrada sem enabled booleano: "+key);
            if(b&&!Regex.IsMatch(key,@"\Aworkshop-[1-9][0-9]{0,19}\z")) throw new FormatException("Mod local/privado não suportado: "+key);
            if(b&&!ulong.TryParse(key[9..],out _)) throw new FormatException("ID inválido: "+key);
            if(Regex.IsMatch(key,@"\Aworkshop-[1-9][0-9]{0,19}\z")&&ulong.TryParse(key[9..],out _))result.Add(key[9..],b);
        }
        return result;
    }
    // Modify just the boolean token, or append a new literal entry. Preserve unrelated bytes/options.
    public static string SetEnabled(string text,string id,bool enabled){
        if(!WorkshopId.TryParse(id,out var parsed)||parsed!=id)throw new ArgumentException("ID Workshop inválido.");
        var entries=Entries(text);
        var parser=Parse(text,out _,out var positions);
        if(entries.TryGetValue(id,out var old)){
            if(old==enabled)return text;
            var token=parser.EnabledTokens["workshop-"+id];
            var start=positions[token];var length=old?4:5;
            text=text.Remove(start,length).Insert(start,enabled?"true":"false");
        }else{
            if(!enabled)return text;
            var close=positions[parser.RootClose];var prefix=parser.RootHasEntries&&!parser.RootTrailingSeparator?",":"";
            text=text.Insert(close,prefix+"\n    [\"workshop-"+id+"\"] = { enabled = true, configuration_options = {} },\n");
        }
        if(!Entries(text).TryGetValue(id,out var actual)||actual!=enabled)throw new FormatException("Edição não conferida.");
        return text;
    }
    sealed class Parser(List<string> t) {
        public Dictionary<string,int> EnabledTokens {get;}=new(StringComparer.Ordinal);
        public int RootClose {get;private set;}
        public bool RootHasEntries {get;private set;}
        public bool RootTrailingSeparator {get;private set;}
        int lastEnabled=-1;
        int i;
        public string? Peek=>i<t.Count?t[i]:null;
        public string Take()=>Peek is string x?(i++,x).Item2:throw new FormatException("Fim inesperado.");
        public void Expect(string s){if(Take()!=s)throw new FormatException("Esperado: "+s);}
        static bool Quoted(string x)=>x.StartsWith('"')||x.StartsWith('\'')||x.StartsWith("[[")||x.StartsWith("[=");
        static string Key(string x){
            if(x.StartsWith('"')||x.StartsWith('\'')){
                var raw=x[1..^1];
                if(raw.Contains('\\')) throw new FormatException("Chave Lua com escape não suportada.");
                return raw;
            }
            if(x.StartsWith("[")||x.Contains('\\'))throw new FormatException("Chave complexa não suportada.");
            return x;
        }
        object? Value(int depth){
            if(depth>64)throw new FormatException("Lua muito aninhado.");
            var x=Peek??throw new FormatException("Valor ausente.");
            if(x=="{")return Table(depth+1);
            Take();
            if(x=="true")return true; if(x=="false")return false; if(x=="nil")return null;
            if(Quoted(x)||Regex.IsMatch(x,@"\A-?(?:[0-9]|\.[0-9])"))return x;
            throw new FormatException("Expressão Lua não literal: "+x);
        }
        public Dictionary<string,object?> Table(int depth){
            if(depth>64)throw new FormatException("Lua muito aninhado.");
            Expect("{"); var d=new Dictionary<string,object?>(StringComparer.Ordinal); int array=0;
            if(depth==1)lastEnabled=-1;
            while(Peek!="}"){
                string key;
                if(Peek=="["){Take();key=Key(Take());Expect("]");Expect("=");}
                else if(i+1<t.Count&&t[i+1]=="="){key=Key(Take());Expect("=");}
                else key=(++array).ToString();
                int valueIndex=i;
                var value=Value(depth);
                if(depth==1&&key=="enabled"&&value is bool)lastEnabled=valueIndex;
                if(depth==0&&value is Dictionary<string,object?>&&lastEnabled>=0)EnabledTokens[key]=lastEnabled;
                if(!d.TryAdd(key,value))throw new FormatException("Chave duplicada: "+key);
                bool separator=Peek is "," or ";";
                if(depth==0){RootHasEntries=true;RootTrailingSeparator=separator;}
                if(separator)Take();else if(Peek!="}")throw new FormatException("Separador ausente.");
            }
            if(depth==0)RootClose=i;
            Expect("}");return d;
        }
    }
}
