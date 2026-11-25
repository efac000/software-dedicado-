using System.Text.RegularExpressions;

namespace Dedicado;

public enum LuaOptionKind { Boolean, Number, Text }
public sealed record LuaOption(string Name,LuaOptionKind Kind,string Value);

public static partial class Lua {
    static readonly Regex OptionNumber=new("""\A-?(?:0[xX][0-9a-fA-F]+|(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?)\z""",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    static readonly Regex SimpleString=new("""\A(?:"(?:\\["'\\]|[^"\\\p{Cc}])*"|'(?:\\["'\\]|[^'\\\p{Cc}])*')\z""",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));

    public static IReadOnlyList<LuaOption> GetOptions(string text,string id)=>
        FindOptions(text,id).Select(field=>field.Option).ToArray();

    public static string SetOptions(string text,string id,IReadOnlyDictionary<string,string> values){
        var fields=FindOptions(text,id).ToDictionary(field=>field.Option.Name,StringComparer.Ordinal);
        var edits=new List<(int Start,int Length,string Replacement)>();
        foreach(var (name,value) in values){
            if(!fields.TryGetValue(name,out var field))throw new FormatException("Opção não editável: "+name);
            if(value==field.Option.Value)continue;
            var literal=field.Option.Kind switch{
                LuaOptionKind.Boolean when value is "true" or "false"=>value,
                LuaOptionKind.Number when value.Length<=128&&OptionNumber.IsMatch(value)=>value,
                LuaOptionKind.Text when value.Length<=4096&&!value.Any(char.IsControl)=>
                    "\""+value.Replace("\\","\\\\").Replace("\"","\\\"")+"\"",
                _=>throw new FormatException("Valor inválido para "+name+".")
            };
            edits.Add((field.Start,field.Length,literal));
        }
        foreach(var edit in edits.OrderByDescending(edit=>edit.Start))
            text=text.Remove(edit.Start,edit.Length).Insert(edit.Start,edit.Replacement);
        Parse(text,out _,out _);
        return text;
    }

    static IEnumerable<(LuaOption Option,int Start,int Length)> FindOptions(string text,string id){
        if(!WorkshopId.TryParse(id,out var parsed)||parsed!=id)throw new ArgumentException("ID Workshop inválido.");
        var parser=Parse(text,out var table,out var positions);
        if(!table.TryGetValue("workshop-"+id,out var raw)||raw is not Dictionary<string,object?> mod)
            throw new FormatException("Mod ausente no shard selecionado.");
        if(!mod.TryGetValue("configuration_options",out var configured)
            ||configured is not Dictionary<string,object?> options
            ||!parser.OptionTokens.TryGetValue(options,out var tokens))yield break;
        foreach(var (key,index) in tokens){
            var start=positions[index];
            var token=Lex.Match(text,start).Value;
            LuaOption? option=options[key] switch{
                bool=>new(key,LuaOptionKind.Boolean,token),
                string when OptionNumber.IsMatch(token)=>new(key,LuaOptionKind.Number,token),
                string when SimpleString.IsMatch(token)=>new(key,LuaOptionKind.Text,
                    Regex.Replace(token[1..^1],"""\\(["'\\])""","$1")),
                _=>null
            };
            if(option!=null)yield return (option,start,token.Length);
        }
    }
}
