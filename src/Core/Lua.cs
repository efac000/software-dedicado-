using System.Text.RegularExpressions;
namespace Dedicado;
public static class Lua {
    // Accept literal tables only; never load or evaluate Lua.
    static readonly Regex Lex = new("""\G(?:\s+|--\[(?<eq>=*)\[.*?\]\k<eq>\]|--[^\r\n]*|\[(?<long>=*)\[.*?\]\k<long>\]|"(?:\\[\s\S]|[^"\\\r\n])*"|'(?:\\[\s\S]|[^'\\\r\n])*'|[A-Za-z_][A-Za-z_0-9]*|-?(?:0[xX][0-9a-fA-F]+|(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?)|[{}\[\]=,;])""",RegexOptions.Singleline|RegexOptions.CultureInvariant,TimeSpan.FromSeconds(3));
    public static Dictionary<string,bool> Active(string text) {
        if(text.Length>4_000_000) throw new FormatException("Arquivo Lua grande demais.");
        var tokens=new List<string>(); int p=0;
        while(p<text.Length) {
            var m=Lex.Match(text,p);
            if(!m.Success || m.Length==0) throw new FormatException("Lua não literal ou inválido na posição "+p);
            var t=m.Value; p+=m.Length;
            if(!char.IsWhiteSpace(t[0])&&!t.StartsWith("--")) tokens.Add(t);
        }
        var parser=new Parser(tokens); parser.Expect("return"); var table=parser.Table(0);
        if(parser.Peek==";") parser.Take();
        if(parser.Peek!=null) throw new FormatException("Código Lua adicional não suportado.");
        var result=new Dictionary<string,bool>(StringComparer.Ordinal);
        foreach(var (key,v) in table) {
            if(v is not Dictionary<string,object?> entry || !entry.TryGetValue("enabled",out var enabled) || enabled is not bool b)
                throw new FormatException("Entrada sem enabled booleano: "+key);
            if(b&&!Regex.IsMatch(key,@"\Aworkshop-[1-9][0-9]{0,19}\z")) throw new FormatException("Mod local/privado não suportado: "+key);
            if(b&&!ulong.TryParse(key[9..],out _)) throw new FormatException("ID inválido: "+key);
            if(b) result.Add(key[9..],true);
        }
        return result;
    }
    sealed class Parser(List<string> t) {
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
            while(Peek!="}"){
                string key; object? value;
                if(Peek=="["){Take();key=Key(Take());Expect("]");Expect("=");value=Value(depth);}
                else if(i+1<t.Count&&t[i+1]=="="){key=Key(Take());Expect("=");value=Value(depth);}
                else {key=(++array).ToString();value=Value(depth);}
                if(!d.TryAdd(key,value))throw new FormatException("Chave duplicada: "+key);
                if(Peek is "," or ";")Take();else if(Peek!="}")throw new FormatException("Separador ausente.");
            }
            Expect("}");return d;
        }
    }
}
